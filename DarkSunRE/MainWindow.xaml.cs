using DarkSunRE.Models;
using Microsoft.Win32;
using System.Collections;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Text;
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

namespace DarkSunRE
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ProjectModel Project = new ProjectModel();   

        public MainWindow()
        {
            InitializeComponent();
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            var openFolderDialog = new OpenFolderDialog();
            if (openFolderDialog.ShowDialog() == true)
            {
                Project = new ProjectModel(openFolderDialog.FolderName);
                FileTree.Items.Clear();
                foreach (var file in Project.Files)
                {
                    FileTree.Items.Add(file.Name);
                }                
            }
        }

        private void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            MemoryStream streamOld, streamNew;
            string path = $"{Project.Path}\\{((TreeViewItem)sender).Header.ToString()}";
            string name = ((TreeViewItem)sender).Header.ToString();
            var Document = new GPLDocumentModel(path, name);
            LayoutDocument layoutDocument = new LayoutDocument();
            layoutDocument.Title = Document.Name;
            DataGrid dataGrid = new DataGrid();
            dataGrid.ItemsSource = Document.decodedStrings;
            dataGrid.SelectedCellsChanged += DataGrid_SelectedCellsChanged;

            streamOld = new MemoryStream(Document.originalBytes);
            if (Document.newBytes != null) streamNew = new MemoryStream(Document.newBytes);
            hvp.LoadBytes(Document.originalBytes, Document.newBytes);
            //hexViewer1.Stream = streamOld;
            //hexViewer2.Stream = streamNew;

            layoutDocument.Content = dataGrid;
            layoutDocumentPane.Children.Add(layoutDocument);
            layoutDocument.IsActive = true;
        }

        private void DataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            //long start = ((GPLString)((DataGrid)sender).SelectedItem).StartPosition;
            //hexViewer1.SetPosition(start);
            //hexViewer1.SelectionStart = ((GPLString)((DataGrid)sender).SelectedItem).StartPosition;
            //hexViewer1.SelectionStop = ((GPLString)((DataGrid)sender).SelectedItem).EndPosition;            
            hvp.SetPosition(((GPLString)((DataGrid)sender).SelectedItem).StartPosition, ((GPLString)((DataGrid)sender).SelectedItem).EndPosition);
        }
    }
}