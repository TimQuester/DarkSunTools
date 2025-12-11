using DarkSunRE.Models;
using DarkSunRE.ViewModels;
using DarkSunRE.Views;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Xceed.Wpf.AvalonDock.Layout;
using DSunTools.Common.Gff;

namespace DarkSunRE
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            SetupInputBindings();

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.ProjectTree != null)
            {
                vm.ProjectTree.ResourceOpened += ProjectTree_ResourceOpened;
            }
        }

        private void ProjectTree_ResourceOpened(object? sender, ResourceEventArgs e)
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

                    var gff = GffFile.Create(gffBytes);
                    var resourceBytes = gff.GetResourceData(resource.Tag, resource.Number);

                    string docName = $"{container.Name}:{resource.Tag}-{resource.Number}";
                    string resourceName = $"{resource.Tag}-{resource.Number}";
                    document = new GPLDocument(resourceBytes, docName, container.FullPath, container.Name, resourceName);
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

                // Add opened document to project list for later save/export operations
                project.OpenedDocuments.Add(document);
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
                // legacy behaviour - not used for project model
                // Project = new Project(openFolderDialog.FolderName);
            }
        }

        private void UpdateGPL_Click(object sender, RoutedEventArgs e)
        {
            var vm = GetActiveGplDocumentViewModel();
            if (vm == null) return;

            vm.ApplyStringChanges();
            hvp.LoadBytes(vm.OriginalBytes, vm.NewBytes);
        }

        private void SaveNewGPL_Click(object sender, RoutedEventArgs e)
        {
            var vm = GetActiveGplDocumentViewModel();
            if (vm == null) return;

            vm.ApplyStringChanges();
            vm.SaveModifiedFile();
            hvp.LoadBytes(vm.OriginalBytes, vm.NewBytes);
        }

        private void SaveJSONGPL_Click(object sender, RoutedEventArgs e)
        {
            var docVm = GetActiveGplDocumentViewModel();
            if (docVm == null) return;

            if (DataContext is MainViewModel vm && !string.IsNullOrWhiteSpace(vm.CurrentProject?.JsonFolderPath))
            {
                docVm.SaveJson(vm.CurrentProject.JsonFolderPath);
            }
            else
            {
                MessageBox.Show("JSON folder path is not configured for the current project.");
            }
        }

        private void LoadJSONGPL_Click(object sender, RoutedEventArgs e)
        {
            var docVm = GetActiveGplDocumentViewModel();
            if (docVm == null) return;

            var jsonFolder = (DataContext as MainViewModel)?.CurrentProject?.JsonFolderPath;
            if (string.IsNullOrWhiteSpace(jsonFolder))
            {
                MessageBox.Show("JSON folder path is not configured for the current project.");
                return;
            }

            var filename = Path.Combine(jsonFolder, $"{docVm.FileName}_{docVm.ResourceName}.json");
            if (!File.Exists(filename))
            {
                MessageBox.Show($"Файл {filename} отсутствует");
                return;
            }

            try
            {
                docVm.LoadJson(filename);
                hvp.LoadBytes(docVm.OriginalBytes, docVm.NewBytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Загрузка данных не удалась: {ex.Message}");
            }
        }

        private GplDocumentViewModel GetActiveGplDocumentViewModel()
        {
            if (layoutDocumentPane.SelectedContent is LayoutDocument layoutDocument &&
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
    }
}