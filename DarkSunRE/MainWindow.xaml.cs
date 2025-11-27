using DarkSunRE.Models;
using DarkSunRE.ViewModels;
using Microsoft.Win32;
using System;
using System.Collections;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Xceed.Wpf.AvalonDock.Layout;
using DarkSunRE.ViewModels;

namespace DarkSunRE
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Project Project = new Project();

        public MainWindow()
        {
            InitializeComponent();            
            SetupInputBindings();
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            var openFolderDialog = new OpenFolderDialog();
            if (openFolderDialog.ShowDialog() == true)
            {
                Project = new Project(openFolderDialog.FolderName);
                //FileTree.Items.Clear();
                //foreach (var file in Project.Files)
                //{
                //    FileTree.Items.Add(file.Name);
                //}                
            }
        }

        private void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {            
            string? name = ((TreeViewItem)sender).Header.ToString();
            if (name == null)
            {
                MessageBox.Show("Ошибка определения имени документа");
                return;
            }
            string path = $"{Project.Path}\\{name}";            

            if (!Project.OpenedDocuments.Any(d => d.Name == name))
            {                
                var Document = new GPLDocument(path, name);
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
                else
                {
                    MessageBox.Show("Стиль не найден в ресурсах");
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
                
                hvp.LoadBytes(Document.originalBytes, Document.newBytes);
                layoutDocument.Content = dataGrid;
                layoutDocumentPane.Children.Add(layoutDocument);
                layoutDocument.IsActive = true;

                Project.OpenedDocuments.Add(Document);
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
                var doc = Project.OpenedDocuments.Single(d => d.Name == ((LayoutDocument)layoutDocumentPane.SelectedContent).Title);
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
                var doc = Project.OpenedDocuments.Single(d => d.Name == ((LayoutDocument)layoutDocumentPane.SelectedContent).Title);
                if (doc != null)
                {
                    string newFilePath = $"{doc.Path}_modified";

                    File.WriteAllBytes(newFilePath, doc.newBytes);
                }
            }
        }

        private void SaveJSONGPL_Click(object sender, RoutedEventArgs e)
        {
            var doc = Project.OpenedDocuments.Single(d => d.Name == ((LayoutDocument)layoutDocumentPane.SelectedContent).Title);
            using (FileStream fs = new FileStream($"{doc.Path}.json", FileMode.Create))
            {
                JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions()
                { WriteIndented = true };
                JsonSerializer.Serialize<List<GPLString>>(fs, doc.decodedStrings, jsonSerializerOptions);
            }
        }
        private void LoadJSONGPL_Click(object sender, RoutedEventArgs e)
        {
            var doc = Project.OpenedDocuments.Single(d => d.Name == ((LayoutDocument)layoutDocumentPane.SelectedContent).Title);
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
    }
}