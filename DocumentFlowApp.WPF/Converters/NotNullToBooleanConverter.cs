using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.Windows.Data;

namespace DocumentFlowApp.WPF.Converters
{
    public class NotNullToBooleanConverter : IValueConverter
    {
        public static NotNullToBooleanConverter Instance { get; } = new NotNullToBooleanConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
