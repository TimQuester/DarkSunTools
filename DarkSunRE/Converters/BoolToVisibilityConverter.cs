using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace DarkSunRE.Converters
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BoolToVisibilityConverter : IValueConverter
    {
        // Параметры для инверсии и выбора между Collapsed/Hidden
        public bool Invert { get; set; }
        public bool UseHidden { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Обработка параметра из XAML
            if (parameter is string paramString)
            {
                if (paramString.Equals("invert", StringComparison.OrdinalIgnoreCase))
                    Invert = true;
                if (paramString.Equals("hidden", StringComparison.OrdinalIgnoreCase))
                    UseHidden = true;
            }

            if (value is bool boolValue)
            {
                bool result = Invert ? !boolValue : boolValue;
                return result ? Visibility.Visible :
                       UseHidden ? Visibility.Hidden : Visibility.Collapsed;
            }

            return UseHidden ? Visibility.Hidden : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                bool result = visibility == Visibility.Visible;
                return Invert ? !result : result;
            }
            return Invert;
        }
    }
}
