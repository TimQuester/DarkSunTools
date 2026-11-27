using DarkSunRE.Models;
using DarkSunRE.ViewModels;
using DarkSunRE.Views;
using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Xceed.Wpf.AvalonDock.Layout;
using DarkSunRE.Services;

namespace DarkSunRE
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IResourceService _resourceService;

        public MainWindow(IResourceService resourceService)
        {
            _resourceService = resourceService;
            InitializeComponent();
            SetupInputBindings();

            Loaded += MainWindow_Loaded;

            var viewModel = (MainViewModel)DataContext;

            // Подписываемся на событие изменения активного документа
            dockManager.ActiveContentChanged += OnActiveContentChanged;

            // Инициализируем активный документ
            UpdateActiveGplDocument();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.ProjectTree != null)
            {
                vm.ProjectTree.ResourceOpened += ProjectTree_ResourceOpened;
            }
            UpdateActiveGplDocument();
        }

        private void FilesViewMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (filesAnchorable == null)
                return;

            if (filesAnchorable.IsVisible)
            {
                filesAnchorable.Hide();
                FilesViewMenuItem.IsChecked = false;
            }
            else
            {
                filesAnchorable.Show();
                filesAnchorable.IsActive = true;
                FilesViewMenuItem.IsChecked = true;
            }
        }

        private void FilesAnchorable_Closing(object sender, CancelEventArgs e)
        {
            // The X button hides the panel instead of removing it from the
            // layout, so View -> Files panel can always bring it back.
            e.Cancel = true;
            filesAnchorable.Hide();
            FilesViewMenuItem.IsChecked = false;
        }

        private byte[] GetOriginalResourceBytes(ResourceFile container, ResourceInfo resource)
        {
            var originalGff = _resourceService.ParseGffFile(container.Data);
            return _resourceService.GetResourceData(originalGff, resource.Tag, resource.Number);
        }

        private byte[] GetCurrentResourceBytes(
            Project project,
            ResourceFile container,
            ResourceInfo resource,
            byte[] originalBytes)
        {
            if (resource.ModifiedData != null)
                return resource.ModifiedData;

            if (!string.IsNullOrWhiteSpace(project.ModifiedGamePath))
            {
                string modifiedPath = System.IO.Path.Combine(project.ModifiedGamePath, container.Name);
                if (File.Exists(modifiedPath))
                {
                    var modifiedGff = _resourceService.ParseGffFile(File.ReadAllBytes(modifiedPath));
                    return _resourceService.GetResourceData(modifiedGff, resource.Tag, resource.Number);
                }
            }

            return originalBytes;
        }

        private void ProjectTree_ResourceOpened(object? sender, DarkSunRE.ViewModels.ResourceEventArgs e)
        {
            // Only handle GPL-like resources (you can extend detection by resource type)
            if (e?.Resource == null || e.Project == null) return;

            // Find resource container file
            var project = e.Project;
            var resource = e.Resource;
            var container = project.Files.FirstOrDefault(f => f.Resources?.Any(r => r.Tag == resource.Tag && r.Number == resource.Number) == true);
            if (container == null)
            {
                MessageBox.Show("Containing resource file not found.");
                return;
            }

            try
            {
                // reuse existing opened document if present
                var existingDoc = project.OpenedDocuments.FirstOrDefault(d =>
                    string.Equals(d.FileName, container.Name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(d.ResourceName, $"{resource.Tag}-{resource.Number}", StringComparison.OrdinalIgnoreCase));

                GPLDocument document = null;
                DocumentViewModel documentViewModel = null;
                
                // Проверяем тип ресурса и создаем соответствующую вьюмодель
                if (resource.Type == ResourceType.Image)
                {
                    // Для изображений создаем ImageResourceViewModel
                    byte[] resourceBytes = null;
                    
                    // prefer on-disk modified file if it exists
                    if (!string.IsNullOrWhiteSpace(project.ModifiedGamePath))
                    {
                        var modifiedPath = Path.Combine(project.ModifiedGamePath, container.Name);
                        if (File.Exists(modifiedPath))
                        {
                            var gffBytes = File.ReadAllBytes(modifiedPath);
                            var gff = _resourceService.ParseGffFile(gffBytes);
                            resourceBytes = _resourceService.GetResourceData(gff, resource.Tag, resource.Number);
                        }
                    }
                    
                    if (resourceBytes == null)
                    {
                        var gff = _resourceService.ParseGffFile(container.Data);
                        resourceBytes = _resourceService.GetResourceData(gff, resource.Tag, resource.Number);
                    }
                    
                    documentViewModel = _resourceService.CreateEditorViewModel(resource, resourceBytes);
                    
                    // Для изображений инициализируем вьюмодель с проектом и информацией о ресурсе
                    if (documentViewModel is ImageResourceViewModel imageViewModel)
                    {
                        imageViewModel.Initialize(project, resource, resourceBytes);
                    }
                }
                else if (resource.Type == ResourceType.Font)
                {
                    // Always use the original game resource as the base font; project glyph overrides are layered by the editor.
                    var originalGff = _resourceService.ParseGffFile(container.Data);
                    byte[] resourceBytes = _resourceService.GetResourceData(originalGff, resource.Tag, resource.Number);
                    documentViewModel = _resourceService.CreateEditorViewModel(resource, resourceBytes);
                    
                    // Для шрифтов инициализируем вьюмодель с проектом и информацией о ресурсе
                    if (documentViewModel is FontResourceViewModel fontViewModel)
                    {
                        fontViewModel.Initialize(project, resource, resourceBytes);
                    }
                }
                else if (resource.Type == ResourceType.Text)
                {
                    byte[] originalBytes = GetOriginalResourceBytes(container, resource);
                    byte[] currentBytes = GetCurrentResourceBytes(project, container, resource, originalBytes);
                    var textViewModel = (TextResourceViewModel)_resourceService.CreateEditorViewModel(resource, currentBytes);
                    textViewModel.Initialize(resource, originalBytes, currentBytes, project.TransliterationRules);
                    documentViewModel = textViewModel;
                }
                else if (resource.Type == ResourceType.Animation)
                {
                    // BMA: cinematic image stream. ACF scripts are deliberately
                    // not interpreted; only the frames are shown.
                    byte[] resourceBytes = resource.ModifiedData;

                    if (resourceBytes == null && !string.IsNullOrWhiteSpace(project.ModifiedGamePath))
                    {
                        var modifiedPath = Path.Combine(project.ModifiedGamePath, container.Name);
                        if (File.Exists(modifiedPath))
                        {
                            var gffBytes = File.ReadAllBytes(modifiedPath);
                            var gff = _resourceService.ParseGffFile(gffBytes);
                            resourceBytes = _resourceService.GetResourceData(gff, resource.Tag, resource.Number);
                        }
                    }

                    if (resourceBytes == null)
                    {
                        var gff = _resourceService.ParseGffFile(container.Data);
                        resourceBytes = _resourceService.GetResourceData(gff, resource.Tag, resource.Number);
                    }

                    var animationViewModel = (AnimationResourceViewModel)_resourceService.CreateEditorViewModel(resource, resourceBytes);
                    animationViewModel.Initialize(project, resource, resourceBytes);
                    documentViewModel = animationViewModel;
                }
                else if (resource.Type == ResourceType.Name)
                {
                    byte[] originalBytes = GetOriginalResourceBytes(container, resource);
                    byte[] currentBytes = GetCurrentResourceBytes(project, container, resource, originalBytes);
                    var originalName = _resourceService.DecodeResource(resource, originalBytes) as NameResource;
                    var currentName = _resourceService.DecodeResource(resource, currentBytes) as NameResource;

                    documentViewModel = _resourceService.CreateEditorViewModel(resource, currentBytes);
                    if (documentViewModel is NameResourceViewModel nameResourceViewModel)
                    {
                        nameResourceViewModel.Initialize(
                            currentName,
                            $"{resource.Tag}-{resource.Number}",
                            resource,
                            project.TransliterationRules,
                            originalName);
                    }
                }
                else if (resource.Type == ResourceType.AnimationControl)
                {
                    // ACF drives the cinematic player; this tool only decodes
                    // the image stream (BMA) and intentionally ignores scripts.
                    MessageBox.Show("Animation control scripts (ACF) are not interpreted. Open the matching BMA resource to view the frames.");
                    return;
                }
                else if (resource.Type == ResourceType.RawData ||
                         resource.Type == ResourceType.Unknown ||
                         resource.Type == ResourceType.Item ||
                         resource.Type == ResourceType.Character ||
                         resource.Type == ResourceType.Map ||
                         resource.Type == ResourceType.Dialog)
                {
                    byte[] resourceBytes = resource.ModifiedData;

                    if (resourceBytes == null && !string.IsNullOrWhiteSpace(project.ModifiedGamePath))
                    {
                        var modifiedPath = Path.Combine(project.ModifiedGamePath, container.Name);
                        if (File.Exists(modifiedPath))
                        {
                            var gffBytes = File.ReadAllBytes(modifiedPath);
                            var gff = _resourceService.ParseGffFile(gffBytes);
                            resourceBytes = _resourceService.GetResourceData(gff, resource.Tag, resource.Number);
                        }
                    }

                    if (resourceBytes == null)
                    {
                        var gff = _resourceService.ParseGffFile(container.Data);
                        resourceBytes = _resourceService.GetResourceData(gff, resource.Tag, resource.Number);
                    }

                    var rawViewModel = new RawResourceViewModel();
                    rawViewModel.Initialize(resource, resourceBytes);
                    documentViewModel = rawViewModel;
                }
                else
                {
                    string resourceName = $"{resource.Tag}-{resource.Number}";
                    document = existingDoc;
                    if (document == null)
                    {
                        var originalGff = _resourceService.ParseGffFile(container.Data);
                        var resourceData = _resourceService.GetResourceData(originalGff, resource.Tag, resource.Number);
                        string docName = $"{container.Name}:{resourceName}";
                        document = new GPLDocument(resourceData, docName, container.FullPath, container.Name, resourceName, project.TransliterationRules);
                        project.OpenedDocuments.Add(document);
                    }

                    documentViewModel = new GplDocumentViewModel(document);
                }

                // Keep document titles consistent for every resource type:
                // file name, then the resource tag/number.
                documentViewModel.Title = $"{container.Name}:{resource.Tag}-{resource.Number}";
                documentViewModel.File = container.FullPath;

                // Создаем представление в зависимости от типа вьюмодели
                UserControl view = null;
                if (documentViewModel is GplDocumentViewModel gplViewModel)
                {
                    view = new GplDocumentView { DataContext = gplViewModel };
                }
                else if (documentViewModel is ImageResourceViewModel imageViewModel)
                {
                    view = new ImageResourceView { DataContext = imageViewModel };
                }
                else if (documentViewModel is AnimationResourceViewModel animationViewModel)
                {
                    view = new AnimationResourceView { DataContext = animationViewModel };
                }
                else if (documentViewModel is FontResourceViewModel fontViewModel)
                {
                    view = new FontDisplayView { DataContext = fontViewModel };
                }
                else if (documentViewModel is NameResourceViewModel nameResourceViewModel)
                {
                    view = new NameResourceView { DataContext = nameResourceViewModel };
                }
                else if (documentViewModel is TextResourceViewModel textResourceViewModel)
                {
                    view = new TextResourceView { DataContext = textResourceViewModel };
                }
                else if (documentViewModel is RawResourceViewModel rawResourceViewModel)
                {
                    view = new RawResourceView { DataContext = rawResourceViewModel };
                }
                else
                {
                    // Для других типов ресурсов можно добавить соответствующие представления
                    MessageBox.Show($"Unsupported resource type: {resource.Type}");
                    return;
                }

                LayoutDocument layoutDocument = new LayoutDocument
                {
                    Title = documentViewModel.DisplayTitle,
                    Content = view
                };

                // Keep the document tab title in sync with the view model.
                documentViewModel.PropertyChanged += (o, args) =>
                {
                    if (args.PropertyName == nameof(DocumentViewModel.DisplayTitle))
                    {
                        layoutDocument.Title = documentViewModel.DisplayTitle;
                    }
                };

                layoutDocumentPane.Children.Add(layoutDocument);
                layoutDocument.IsActive = true;

                UpdateActiveGplDocument();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open resource: {ex.Message}");
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            var openFolderDialog = new OpenFolderDialog();
            if (openFolderDialog.ShowDialog() == true)
            {
                ;
            }
        }

        public GplDocumentViewModel GetActiveGplDocumentViewModel()
        {
            if (layoutDocumentPane?.SelectedContent is LayoutDocument layoutDocument &&
                layoutDocument.Content is FrameworkElement element)
            {
                return element.DataContext as GplDocumentViewModel;
            }

            return null;
        }

        // existing input bindings and command handlers unchanged...
        private void SetupInputBindings()
        {
            // Глобальные горячие клавиши
            InputBindings.Add(new KeyBinding(
                ApplicationCommands.New,
                new KeyGesture(Key.N, ModifierKeys.Control)));

            InputBindings.Add(new KeyBinding(
                ApplicationCommands.Open,
                new KeyGesture(Key.O, ModifierKeys.Control)));
        }

        private void NewCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.NewProjectCommand.CanExecute(null))
            {
                vm.NewProjectCommand.Execute(null);
            }
        }

        private void OpenCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.OpenProjectCommand.CanExecute(null))
            {
                vm.OpenProjectCommand.Execute(null);
            }
        }

        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.SaveProjectCommand?.CanExecute(null) == true)
            {
                vm.SaveProjectCommand.Execute(null);
            }
        }
        private void SettingsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.SettingsCommand.CanExecute(null))
            {
                vm.SettingsCommand.Execute(null);
            }
        }
        private void OnActiveContentChanged(object sender, EventArgs e)
        {
            UpdateActiveGplDocument();            
            CommandManager.InvalidateRequerySuggested();
        }

        private void UpdateActiveGplDocument()
        {
            try
            {
                var activeDoc = GetActiveGplDocumentViewModel();
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.ActiveGplDocument = activeDoc;
                }
            }
            catch (Exception ex)
            {                
                System.Diagnostics.Debug.WriteLine($"Error updating active document: {ex.Message}");
            }
        }
    }
}