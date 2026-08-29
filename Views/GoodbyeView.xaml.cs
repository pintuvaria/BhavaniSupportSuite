using System.Windows.Controls;

namespace BhavaniSupportSuite.Views;

public partial class GoodbyeView : UserControl
{
    public GoodbyeView()
    {
        InitializeComponent();
        Loaded += GoodbyeView_Loaded;
    }

    private void GoodbyeView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        SessionTimeText.Text = DateTime.Now.ToString("hh:mm:ss tt");
    }
}
