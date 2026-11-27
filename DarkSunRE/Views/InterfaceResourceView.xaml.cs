using DarkSunRE.ViewModels;
using System;
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

        private void WindGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit ||
                e.Row.Item is not InterfaceItemViewModel item ||
                e.EditingElement is not TextBox editor)
            {
                return;
            }

            if (!short.TryParse(editor.Text, out short value))
                return;

            if (string.Equals(e.Column.Header?.ToString(), "New X", StringComparison.Ordinal))
                item.X = value;
            else if (string.Equals(e.Column.Header?.ToString(), "New Y", StringComparison.Ordinal))
                item.Y = value;
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
