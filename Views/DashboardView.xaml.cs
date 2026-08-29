using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace BhavaniSupportSuite.Views;

public partial class DashboardView : UserControl
{
    public DashboardView() { InitializeComponent(); }
}

public class BoolToProgressConverter : IValueConverter
{
    public static readonly BoolToProgressConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (bool)value ? 1.0 : 0.0;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class InverseBoolConverter : IValueConverter
{
    public static readonly InverseBoolConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => !(bool)value;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => !(bool)value;
}

public class BoolToStatusConverter : IValueConverter
{
    public static readonly BoolToStatusConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (bool)value ? "Enabled" : "Disabled";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class UsbToggleConverter : IValueConverter
{
    public static readonly UsbToggleConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (bool)value ? "Disable USB Storage" : "Enable USB Storage";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
