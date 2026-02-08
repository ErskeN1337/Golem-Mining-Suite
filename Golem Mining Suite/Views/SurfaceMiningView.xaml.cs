using System.Windows.Controls;

namespace Golem_Mining_Suite.Views
{
    public partial class SurfaceMiningView : UserControl
    {
        public SurfaceMiningView()
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
                    box.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#CCCCCC"));
                }
            }
        }

        private void SearchBox_LostFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            var box = sender as TextBox;
            if (box != null && string.IsNullOrWhiteSpace(box.Text))
            {
                box.Text = "Search mineral...";
                box.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#999999"));
            }
        }
    }
}