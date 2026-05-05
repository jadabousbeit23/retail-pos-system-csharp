using System.Windows;
using System.Windows.Controls;

namespace POSSystem.Controls
{
    public partial class LoadingOverlay : UserControl
    {
        public LoadingOverlay()
        {
            InitializeComponent();
        }

        public void Show(string message = "Processing...")
        {
            lblMessage.Text = message;
            this.Visibility = Visibility.Visible;
            this.IsHitTestVisible = true;
        }

        public void Hide()
        {
            this.Visibility = Visibility.Collapsed;
            this.IsHitTestVisible = false;
        }

        public bool IsShowing => this.Visibility == Visibility.Visible;
    }
}