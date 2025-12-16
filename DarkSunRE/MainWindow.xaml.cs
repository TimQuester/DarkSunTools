using DarkSunRE.Models;
using DarkSunRE.ViewModels;
using DarkSunRE.Views;
using Microsoft.Win32;
using System.IO;
using System.Windows;
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

                GPLDocument document;
                if (existingDoc != null)
                {
                    existingDoc.TransliterationRules = project.TransliterationRules ?? new List<TransliterationRule>();
                    document = existingDoc;
                }
                else
                {
                    // prefer on-disk modified file if it exists
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
                }

                var documentViewModel = new GplDocumentViewModel(document);
                var view = new GplDocumentView { DataContext = documentViewModel };

                LayoutDocument layoutDocument = new LayoutDocument
                {
                    Title = documentViewModel.DisplayTitle,
                    Content = view
                };

                documentViewModel.PropertyChanged += (o, args) =>
                {
                    if (args.PropertyName == nameof(GplDocumentViewModel.SelectedString) && documentViewModel.SelectedString != null)
                    {
                        hvp.SetPosition(documentViewModel.SelectedString.StartPosition, documentViewModel.SelectedString.EndPosition);
                    }

                    if (args.PropertyName == nameof(GplDocumentViewModel.NewBytes))
                    {
                        hvp.LoadBytes(documentViewModel.OriginalBytes, documentViewModel.NewBytes);
                    }

                    if (args.PropertyName == nameof(DocumentViewModel.DisplayTitle))
                    {
                        layoutDocument.Title = documentViewModel.DisplayTitle;
                    }
                };

                // load into hex view
                hvp.LoadBytes(documentViewModel.OriginalBytes, documentViewModel.NewBytes);

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