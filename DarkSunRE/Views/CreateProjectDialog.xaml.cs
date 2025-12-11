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
using System.Windows.Shapes;
using DarkSunRE.ViewModels;

namespace DarkSunRE.Views
{
    /// <summary>
    /// Interaction logic for CreateProjectDialog.xaml
    /// </summary>
    public partial class CreateProjectDialog : Window
    {
        public CreateProjectDialog()
        {
            InitializeComponent();

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    DialogResult = false;
                    Close();
                }
            };
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //if (DataContext is BaseDialogViewModel viewModel)
            //{
            //    viewModel.RequestClose += (s, args) =>
            //    {
            //        DialogResult = args.Result;
            //        Close();
            //    };
            //}
        }
    }
}
