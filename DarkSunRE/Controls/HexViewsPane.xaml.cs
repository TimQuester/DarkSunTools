using DarkSunRE.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
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

namespace DarkSunRE.Controls
{
    /// <summary>
    /// Interaction logic for HexViewsPane.xaml
    /// </summary>
    public partial class HexViewsPane : UserControl
    {
        public HexViewsPane()
        {
            InitializeComponent();
        }
        public void LoadBytes(byte[] bytesOld, byte[] bytesNew)
        {
            MemoryStream streamOld, streamNew;
            streamOld = new MemoryStream(bytesOld);
            streamNew = new MemoryStream(bytesNew);
            hexViewer1.Stream = streamOld;
            hexViewer2.Stream = streamNew;

            hexViewer1.RefreshView();
            hexViewer2.RefreshView();
        }

        public void SetPosition(long StartPosition, long EndPosition)
        {
            hexViewer1.SetPosition(StartPosition);
            hexViewer1.SelectionStart = StartPosition;
            hexViewer1.SelectionStop = EndPosition;

            hexViewer2.SetPosition(StartPosition);
            hexViewer2.SelectionStart = StartPosition;
            hexViewer2.SelectionStop = EndPosition;
        }
    }
}
