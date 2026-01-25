using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace ImmenseWall.Converters
{
    public class BoolToSelectionModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? SelectionMode.Multiple : SelectionMode.Single;
            }
            return SelectionMode.Single;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}