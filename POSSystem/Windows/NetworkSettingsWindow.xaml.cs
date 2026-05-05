using POSSystem.Database;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace POSSystem.Windows
{
    public partial class NetworkSettingsWindow : Window
    {
        public NetworkSettingsWindow()
        {
            InitializeComponent();
            LoadCurrentStatus();
        }

        void LoadCurrentStatus()
        {
            string path = DatabaseHelper.GetCurrentDbPath();
            lblCurrentPath.Content = path;

            bool isNetwork = DatabaseHelper.IsNetworkDatabase();
            bool reachable = File.Exists(path);

            if (isNetwork && reachable)
            {
                lblStatus.Content      = "🟢 Network — Connected";
                statusBadge.Background = new SolidColorBrush(Color.FromRgb(16, 124, 16));
            }
            else if (isNetwork && !reachable)
            {
                lblStatus.Content      = "🔴 Network — Unreachable";
                statusBadge.Background = new SolidColorBrush(Color.FromRgb(209, 52, 56));
            }
            else
            {
                lblStatus.Content      = "🟡 Local DB";
                statusBadge.Background = new SolidColorBrush(Color.FromRgb(202, 80, 16));
            }

            // Pre-fill with saved path
            string saved = DatabaseHelper.ReadSavedDbPath();
            if (!string.IsNullOrEmpty(saved))
                txtNetworkPath.Text = saved;
        }

        // ── BROWSE ───────────────────────────────────────────
        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Select POS Database File",
                Filter = "SQLite Database (*.db)|*.db|All Files (*.*)|*.*",
                FileName = "pos.db"
            };
            if (dlg.ShowDialog() == true)
                txtNetworkPath.Text = dlg.FileName;
        }

        // ── TEST CONNECTION ──────────────────────────────────
        private void btnTest_Click(object sender, RoutedEventArgs e)
        {
            string path = txtNetworkPath.Text.Trim();
            if (string.IsNullOrEmpty(path))
            { ShowError("⚠️ Enter a path first!"); return; }

            ShowInfo("Testing connection…");

            bool ok = DatabaseHelper.TestConnection(path);
            if (ok)
                ShowSuccess($"✅ Connection successful! Database found at:\n{path}");
            else
                ShowError($"❌ Cannot reach: {path}\n" +
                          "Check that the file exists and the folder is shared.");
        }

        // ── SAVE ─────────────────────────────────────────────
        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            string path = txtNetworkPath.Text.Trim();
            if (string.IsNullOrEmpty(path))
            { ShowError("⚠️ Enter a path first!"); return; }

            if (!DatabaseHelper.TestConnection(path))
            {
                var result = MessageBox.Show(
                    $"Cannot reach:\n{path}\n\n" +
                    "Save anyway? (App will use local DB until network is available)",
                    "Connection Failed",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
            }

            DatabaseHelper.SaveDbPath(path);
            LoadCurrentStatus();
            ShowSuccess("✅ Network database saved! Restart the app to apply changes.");
        }

        // ── COPY LOCAL → NETWORK ─────────────────────────────
        private void btnCopyToNetwork_Click(object sender, RoutedEventArgs e)
        {
            string path = txtNetworkPath.Text.Trim();
            if (string.IsNullOrEmpty(path))
            { ShowError("⚠️ Enter the network path first!"); return; }

            if (MessageBox.Show(
                $"Copy local database to:\n{path}\n\n" +
                "This will OVERWRITE any existing database at that location.\n" +
                "Are you sure?",
                "Copy Database",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            // Make sure directory exists
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                { ShowError($"❌ Directory not found:\n{dir}"); return; }
            }
            catch { }

            bool ok = DatabaseHelper.CopyLocalToNetwork(path);
            if (ok)
                ShowSuccess($"✅ Database copied to:\n{path}\n\n" +
                            "Now click 'Save & Use Network DB' to connect.");
        }

        // ── USE LOCAL ────────────────────────────────────────
        private void btnUseLocal_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Switch back to local database?\n" +
                "The app will use the local pos.db file.",
                "Use Local DB",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            DatabaseHelper.ClearDbPath();
            txtNetworkPath.Clear();
            LoadCurrentStatus();
            ShowSuccess("✅ Switched to local database. Restart to apply.");
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();

        void ShowSuccess(string msg)
        {
            lblMessage.Content    = msg;
            lblMessage.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 16));
        }

        void ShowError(string msg)
        {
            lblMessage.Content    = msg;
            lblMessage.Foreground = new SolidColorBrush(Color.FromRgb(209, 52, 56));
        }

        void ShowInfo(string msg)
        {
            lblMessage.Content    = msg;
            lblMessage.Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212));
        }
    }
}
