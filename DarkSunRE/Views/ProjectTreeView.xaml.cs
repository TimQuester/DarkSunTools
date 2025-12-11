using DarkSunRE.Models;
using DarkSunRE.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DarkSunRE.Views
{
    /// <summary>
    /// Interaction logic for ProjectTreeView.xaml
    /// </summary>
    public partial class ProjectTreeView : UserControl
    {
        public ProjectTreeView()
        {
            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is ProjectNode node)
            {
                // Обработка выбора элемента
                node.IsSelected = true;
            }
        }

        private void TreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement source &&
                source.DataContext is ProjectNode node &&
                node.ItemType == ProjectItemType.Resource)
            {
                // Открываем ресурс при двойном клике
                if (DataContext is ProjectTreeViewModel vm)
                {
                    vm.OpenResource(node.Tag, node.ResourceNumber);
                }
            }
        }

        private void GenerateNewFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element &&
                element.DataContext is ProjectNode node &&
                DataContext is ProjectTreeViewModel vm)
            {
                vm.GenerateNewFile(node);
            }
        }

        private void SaveAllJsons_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element &&
                element.DataContext is ProjectNode node &&
                DataContext is ProjectTreeViewModel vm)
            {
                vm.SaveAllJsons(node);
            }
        }

        private void LoadAllJsons_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element &&
                element.DataContext is ProjectNode node &&
                DataContext is ProjectTreeViewModel vm)
            {
                vm.LoadAllJsons(node);
            }
        }
    }
}
