using DarkSunRE.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace DarkSunRE.Converters
{
    public class StatusMessageTypeToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is StatusMessageType type)
            {
                return type switch
                {
                    StatusMessageType.Info => Brushes.LightBlue,
                    StatusMessageType.Warning => Brushes.Orange,
                    StatusMessageType.Error => Brushes.LightCoral,
                    StatusMessageType.Success => Brushes.LightGreen,
                    StatusMessageType.Progress => Brushes.LightGray,
                    _ => Brushes.Transparent
                };
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
