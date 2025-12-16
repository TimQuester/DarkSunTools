using DarkSunRE.Models;
using DarkSunRE.ViewModels;
using DarkSunRE.Views;
using Microsoft.Win32;
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
                    // Для шрифтов создаем FontResourceViewModel
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
                    
                    // Для шрифтов инициализируем вьюмодель с проектом и информацией о ресурсе
                    if (documentViewModel is FontResourceViewModel fontViewModel)
                    {
                        fontViewModel.Initialize(project, resource, resourceBytes);
                    }
                }
                else
                {
                    // Для других типов ресурсов (GPL и т.д.) используем старую логику
                    byte[] gffBytes = container.Data;
                    if (!string.IsNullOrWhiteSpace(project.ModifiedGamePath))
                    {
                        var modifiedPath = Path.Combine(project.ModifiedGamePath, container.Name);
                        if (File.Exists(modifiedPath))
                        {
                            gffBytes = File.ReadAllBytes(modifiedPath);
                        }
                    }

                    var gff = _resourceService.ParseGffFile(gffBytes);
                    var resourceBytes = _resourceService.GetResourceData(gff, resource.Tag, resource.Number);

                    string docName = $"{container.Name}:{resource.Tag}-{resource.Number}";
                    string resourceName = $"{resource.Tag}-{resource.Number}";
                    document = new GPLDocument(resourceBytes, docName, container.FullPath, container.Name, resourceName, project.TransliterationRules);
                    project.OpenedDocuments.Add(document);
                    documentViewModel = new GplDocumentViewModel(document);
                }

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
                else if (documentViewModel is FontResourceViewModel fontViewModel)
                {
                    view = new FontDisplayView { DataContext = fontViewModel };
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

                // Подписываемся на события изменения свойств вьюмодели
                documentViewModel.PropertyChanged += (o, args) =>
                {
                    if (documentViewModel is GplDocumentViewModel gplDocViewModel)
                    {
                        if (args.PropertyName == nameof(GplDocumentViewModel.SelectedString) && gplDocViewModel.SelectedString != null)
                        {
                            hvp.SetPosition(gplDocViewModel.SelectedString.StartPosition, gplDocViewModel.SelectedString.EndPosition);
                        }

                        if (args.PropertyName == nameof(GplDocumentViewModel.NewBytes))
                        {
                            hvp.LoadBytes(gplDocViewModel.OriginalBytes, gplDocViewModel.NewBytes);
                        }
                    }

                    if (args.PropertyName == nameof(DocumentViewModel.DisplayTitle))
                    {
                        layoutDocument.Title = documentViewModel.DisplayTitle;
                    }
                };

                // Загружаем данные в hex view для GPL документов
                if (documentViewModel is GplDocumentViewModel gplDocViewModel2)
                {
                    hvp.LoadBytes(gplDocViewModel2.OriginalBytes, gplDocViewModel2.NewBytes);
                }

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