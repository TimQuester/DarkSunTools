using DarkSunRE.Models;
using DarkSunRE.ViewModels;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
                // Parse GFF and extract resource bytes
                var gff = GffFile.Create(container.Data);
                var resourceBytes = gff.GetResourceData(resource.Tag, resource.Number);

                string docName = $"{container.Name}:{resource.Tag}-{resource.Number}";
                var Document = new GPLDocument(resourceBytes, docName, container.FullPath);

                LayoutDocument layoutDocument = new LayoutDocument();
                layoutDocument.Title = Document.Name;
                DataGrid dataGrid = new DataGrid();

                dataGrid.AutoGenerateColumns = false;
                dataGrid.CanUserAddRows = false;
                dataGrid.CanUserDeleteRows = false;
                dataGrid.CanUserReorderColumns = false;
                dataGrid.CanUserSortColumns = false;

                if (Resources["GrayedOutRowStyle"] is Style grayStyle)
                {
                    dataGrid.RowStyle = grayStyle;
                }

                dataGrid.ItemsSource = Document.decodedStrings;
                dataGrid.Columns.Add(new DataGridTextColumn() { Header = "#", Binding = new Binding(nameof(GPLString.StringNumber)) });
                dataGrid.Columns.Add(new DataGridTextColumn() { Header = "Start", Binding = new Binding(nameof(GPLString.StartPosition)) });
                dataGrid.Columns.Add(new DataGridTextColumn() { Header = "End", Binding = new Binding(nameof(GPLString.EndPosition)) });
                var templateColumn = new DataGridTemplateColumn
                {
                    Header = "Strings",
                    CellTemplate = (DataTemplate)FindResource("VerticalStringsTemplate")
                };
                dataGrid.Columns.Add(templateColumn);
                dataGrid.Columns.Add(new DataGridCheckBoxColumn() { Header = "Real", Binding = new Binding(nameof(GPLString.RealString)) });

                dataGrid.SelectedCellsChanged += DataGrid_SelectedCellsChanged;

                // load into hex view
                hvp.LoadBytes(Document.originalBytes, Document.newBytes);

                layoutDocument.Content = dataGrid;
                layoutDocumentPane.Children.Add(layoutDocument);
                layoutDocument.IsActive = true;

                // Add opened document to project list for later save/export operations
                project.OpenedDocuments.Add(Document);
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

        private void DataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            var str = (GPLString)((DataGrid)sender).SelectedItem;
            if (str?.StartPosition == null)
                return;
            hvp.SetPosition(str.StartPosition, str.EndPosition);
            ((DataGrid)sender).Focus();
        }

        private void UpdateGPL_Click(object sender, RoutedEventArgs e)
        {
            if (layoutDocumentPane.SelectedContent != null)
            {
                var doc = FindOpenedDocumentByLayoutTitle(((LayoutDocument)layoutDocumentPane.SelectedContent).Title);
                if (doc != null)
                {
                    doc.ApplyStringChanges();
                    hvp.LoadBytes(doc.originalBytes, doc.newBytes);
                }
            }
        }

        private void SaveNewGPL_Click(object sender, RoutedEventArgs e)
        {
            if (layoutDocumentPane.SelectedContent != null)
            {
                var doc = FindOpenedDocumentByLayoutTitle(((LayoutDocument)layoutDocumentPane.SelectedContent).Title);
                if (doc != null)
                {
                    string newFilePath = $"{doc.Path}_modified";
                    File.WriteAllBytes(newFilePath, doc.newBytes);
                }
            }
        }

        private void SaveJSONGPL_Click(object sender, RoutedEventArgs e)
        {
            var doc = FindOpenedDocumentByLayoutTitle(((LayoutDocument)layoutDocumentPane.SelectedContent).Title);
            if (doc == null) return;

            if (DataContext is MainViewModel vm && vm.CurrentProject.JsonFolderPath != null)
            {
                using (FileStream fs = new FileStream($"{vm.CurrentProject.JsonFolderPath}\\{doc.Name.Replace(':', '-')}.json", FileMode.Create))
                {
                    JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions()
                    { WriteIndented = true };
                    JsonSerializer.Serialize<List<GPLString>>(fs, doc.decodedStrings, jsonSerializerOptions);
                }
            }
        }

        private void LoadJSONGPL_Click(object sender, RoutedEventArgs e)
        {
            var doc = FindOpenedDocumentByLayoutTitle(((LayoutDocument)layoutDocumentPane.SelectedContent).Title);
            if (doc == null) return;

            var filename = $"{doc.Path}.json";
            if (!File.Exists(filename))
            {
                MessageBox.Show($"Файл {filename} отсутствует");
                return;
            }
            using (FileStream fs = new FileStream(filename, FileMode.Open))
            {
                var decodedStrings = JsonSerializer.Deserialize<List<GPLString>>(fs);
                if (decodedStrings != null)
                {
                    doc.decodedStrings = decodedStrings;
                    var layoutDocument = (LayoutDocument)layoutDocumentPane.SelectedContent;
                    ((DataGrid)layoutDocument.Content).ItemsSource = doc.decodedStrings;
                    doc.RefreshNewBytesAfterLoadJson();
                }
                else
                {
                    MessageBox.Show("Загрузка данных не удалась.");
                    return;
                }
            }
        }

        private GPLDocument FindOpenedDocumentByLayoutTitle(string title)
        {
            // assume main VM is DataContext and has CurrentProject
            if (DataContext is MainViewModel vm && vm.CurrentProject != null)
            {
                return vm.CurrentProject.OpenedDocuments.FirstOrDefault(d => d.Name == title);
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