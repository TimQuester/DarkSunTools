using DarkSunRE.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace DarkSunRE.Views
{
    public partial class InterfaceResourceView : UserControl
    {
        public InterfaceResourceView()
        {
            InitializeComponent();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Commit the active DataGrid cell/row first; otherwise a Save
            // button click can run while the edited X/Y value is still only
            // in the editor and not in the bound item.
            WindGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            WindGrid.CommitEdit(DataGridEditingUnit.Row, true);
            ButtonGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            ButtonGrid.CommitEdit(DataGridEditingUnit.Row, true);

            if (DataContext is InterfaceResourceViewModel viewModel)
                viewModel.Save();
        }
    }
}
