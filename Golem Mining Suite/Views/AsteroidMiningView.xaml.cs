using System.Windows.Controls;
using System.Windows.Media;

namespace Golem_Mining_Suite.Views
{
    public partial class AsteroidMiningView : UserControl
    {
        public AsteroidMiningView()
        {
            InitializeComponent();
        }

        private void SearchBox_GotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (CheckAccess())
            {
                var box = sender as TextBox;
                if (box != null && box.Text == "Search mineral...")
                {
                    box.Text = "";
                    box.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
                }
            }
        }

        private void SearchBox_LostFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            var box = sender as TextBox;
            if (box != null && string.IsNullOrWhiteSpace(box.Text))
            {
                box.Text = "Search mineral...";
                box.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999999"));
            }
        }
    }
}