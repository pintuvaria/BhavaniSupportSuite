using System.Globalization;
using System.Windows.Data;

namespace BhavaniSupportSuite.Converters;

public class InverseBoolConverter : IValueConverter
{
    public static readonly InverseBoolConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is bool b ? !b : value;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is bool b ? !b : value;
}

public class BoolToProgressConverter : IValueConverter
{
    public static readonly BoolToProgressConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is bool b && b ? 1.0 : 0.0;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BoolToStatusConverter : IValueConverter
{
    public static readonly BoolToStatusConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is bool b && b ? "Enabled" : "Disabled";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class UsbToggleConverter : IValueConverter
{
    public static readonly UsbToggleConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is bool b && b ? "Disable USB Storage" : "Enable USB Storage";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
