using System.Windows.Controls;

namespace Golem_Mining_Suite.Views
{
    /// <summary>
    /// Wave 5B crew-session management view. DataContext is assigned from DI by
    /// <see cref="Golem_Mining_Suite.ViewModels.MainViewModel"/> — no wiring here.
    /// </summary>
    public partial class CrewSessionView : UserControl
    {
        public CrewSessionView()
        {
            InitializeComponent();
        }
    }
}
