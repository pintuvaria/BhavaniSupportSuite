using System.Windows.Controls;

namespace BhavaniSupportSuite.Views;

public partial class UtilitiesView : UserControl
{
    public UtilitiesView()
    {
        InitializeComponent();
        PasswordInput.PasswordChanged += PasswordInput_PasswordChanged;
    }

    private void PasswordInput_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.UtilitiesViewModel vm)
        {
            vm.NewPassword = PasswordInput.Password;
        }
    }
}
