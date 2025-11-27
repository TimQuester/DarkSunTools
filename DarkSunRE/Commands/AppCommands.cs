using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DarkSunRE.Commands
{
    public static class AppCommands
    {
        public static ICommand NewProject { get; }
        public static ICommand OpenProject { get; }
        public static ICommand SaveProject { get; }
        public static ICommand SaveAll { get; }
        public static ICommand ExportResource { get; }
        public static ICommand ImportResource { get; }
        public static ICommand Undo { get; }
        public static ICommand Redo { get; }
    }
}
