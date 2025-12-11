using DarkSunRE.Services;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DarkSunRE.Converters
{
    public class StatusMessageTypeToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush InfoBrush = CreateFrozenBrush("#263238");      // dark slate (default)
        private static readonly SolidColorBrush WarningBrush = CreateFrozenBrush("#FFB74D");   // muted orange
        private static readonly SolidColorBrush ErrorBrush = CreateFrozenBrush("#E57373");     // soft red
        private static readonly SolidColorBrush SuccessBrush = CreateFrozenBrush("#6DB36D");   // muted green (less bright)
        private static readonly SolidColorBrush ProgressBrush = CreateFrozenBrush("#90A4AE");  // gray-blue

        private static SolidColorBrush CreateFrozenBrush(string hex)
        {
            var brush = (SolidColorBrush)(new BrushConverter().ConvertFromString(hex));
            if (brush.CanFreeze) brush.Freeze();
            return brush;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is StatusMessageType type)
            {
                return type switch
                {
                    StatusMessageType.Info => InfoBrush,
                    StatusMessageType.Warning => WarningBrush,
                    StatusMessageType.Error => ErrorBrush,
                    StatusMessageType.Success => SuccessBrush,
                    StatusMessageType.Progress => ProgressBrush,
                    _ => InfoBrush
                };
            }
            // fallback: use InfoBrush so empty/statusbar background is consistent
            return InfoBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
