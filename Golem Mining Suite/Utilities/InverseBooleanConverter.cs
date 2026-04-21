using System;
using System.Globalization;
using System.Windows.Data;

namespace Golem_Mining_Suite.Utilities
{
    /// <summary>
    /// Inverts a boolean. Handy for <c>IsEnabled</c> bindings that want the opposite of an
    /// <c>IsBusy</c>-style flag without needing a whole viewmodel property for the negation.
    /// </summary>
    public sealed class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b ? !b : true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b ? !b : false;
        }
    }
}
