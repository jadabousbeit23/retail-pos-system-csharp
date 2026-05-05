using Microsoft.Win32;
using POSSystem.Database;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;

namespace POSSystem.Windows
{
    public partial class BackupWindow : Window
    {
        // Track selected items for multi-select
        private List<BackupFile> _selectedBackups = new List<BackupFile>();
        private bool _isCtrlPressed = false;

        public BackupWindow()
        {
            InitializeComponent();
            txtBackupFolder.Text = BackupManager.BackupFolder;
            LoadBackups();

            // Hook up keyboard events for Ctrl tracking
            this.KeyDown += Window_KeyDown;
            this.KeyUp += Window_KeyUp;

            // Hook up right mouse click on ListView
            lvBackups.MouseRightButtonDown += LvBackups_MouseRightButtonDown;
        }

        // ═══════════════════════════════════════════════════════════
        // KEYBOARD EVENTS - Track Ctrl key state
        // ═══════════════════════════════════════════════════════════
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.LeftCtrl || e.Key == System.Windows.Input.Key.RightCtrl)
            {
                _isCtrlPressed = true;
            }
        }

        private void Window_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.LeftCtrl || e.Key == System.Windows.Input.Key.RightCtrl)
            {
                _isCtrlPressed = false;
            }
        }

        // ═══════════════════════════════════════════════════════════
        // RIGHT MOUSE CLICK MULTI-SELECT
        // ═══════════════════════════════════════════════════════════
        private void LvBackups_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Get the item under mouse
            var hitTestResult = VisualTreeHelper.HitTest(lvBackups, e.GetPosition(lvBackups));

            if (hitTestResult == null) return;

            // Find the ListViewItem in the visual tree
            var listViewItem = FindParent<System.Windows.Controls.ListViewItem>(hitTestResult.VisualHit);
            if (listViewItem == null) return;

            // Get the data item
            var clickedBackup = listViewItem.DataContext as BackupFile;
            if (clickedBackup == null) return;

            // Handle selection logic
            if (_isCtrlPressed)
            {
                // Ctrl+RightClick = Toggle selection
                if (_selectedBackups.Contains(clickedBackup))
                {
                    _selectedBackups.Remove(clickedBackup);
                    listViewItem.IsSelected = false;
                }
                else
                {
                    _selectedBackups.Add(clickedBackup);
                    listViewItem.IsSelected = true;
                }
            }
            else
            {
                // Regular RightClick = Single select (clear others)
                _selectedBackups.Clear();
                _selectedBackups.Add(clickedBackup);

                // Clear all selections first
                foreach (var item in lvBackups.Items)
                {
                    var container = lvBackups.ItemContainerGenerator.ContainerFromItem(item) as System.Windows.Controls.ListViewItem;
                    if (container != null)
                        container.IsSelected = false;
                }

                listViewItem.IsSelected = true;
            }

            // Update UI feedback
            UpdateSelectionDisplay();

            // Mark event as handled
            e.Handled = true;
        }

        // ═══════════════════════════════════════════════════════════
        // HELPER: Find parent of type in visual tree
        // ═══════════════════════════════════════════════════════════
        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);

            while (parent != null && !(parent is T))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }

            return parent as T;
        }

        // ═══════════════════════════════════════════════════════════
        // UPDATE SELECTION DISPLAY
        // ═══════════════════════════════════════════════════════════
        void UpdateSelectionDisplay()
        {
            int count = _selectedBackups.Count;

            if (count == 0)
            {
                lblMessage.Content = "";
            }
            else if (count == 1)
            {
                lblMessage.Content = $"📄 Selected: {_selectedBackups[0].FileName}";
                lblMessage.Foreground = System.Windows.Media.Brushes.DodgerBlue;
            }
            else
            {
                lblMessage.Content = $"📦 {count} backups selected";
                lblMessage.Foreground = System.Windows.Media.Brushes.DodgerBlue;
            }

            // Update button states
            btnRestore.IsEnabled = (count == 1); // Can only restore one at a time
            btnDelete.IsEnabled = (count > 0);   // Can delete multiple
        }

        // ═══════════════════════════════════════════════════════════
        // LOAD BACKUPS
        // ═══════════════════════════════════════════════════════════
        void LoadBackups()
        {
            lvBackups.Items.Clear();
            _selectedBackups.Clear();
            var backups = BackupManager.GetAllBackups();
            foreach (var b in backups)
                lvBackups.Items.Add(b);
            lblBackupCount.Content = $"{backups.Count} backup(s)";
            lblMessage.Content = "";
            UpdateSelectionDisplay();
        }

        // ═══════════════════════════════════════════════════════════
        // BROWSE FOLDER
        // ═══════════════════════════════════════════════════════════
        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Select backup folder";
                dlg.SelectedPath = BackupManager.BackupFolder;

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    txtBackupFolder.Text = dlg.SelectedPath;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        // LOADING WITH MINIMUM DISPLAY TIME (Recommended)
        // ═══════════════════════════════════════════════════════════
        private async Task ShowLoadingWithMinimumTime(string message, Func<Task> operation)
        {
            ShowLoading(message);
            SetButtonsEnabled(false);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                await operation();
            }
            finally
            {
                // Ensure minimum 500ms display time
                var elapsed = stopwatch.ElapsedMilliseconds;
                if (elapsed < 500)
                {
                    await Task.Delay(500 - (int)elapsed);
                }

                HideLoading();
                SetButtonsEnabled(true);
            }
        }

        // ═══════════════════════════════════════════════════════════
        // CREATE BACKUP (WITH MINIMUM LOADING TIME)
        // ═══════════════════════════════════════════════════════════
        private async void btnCreateBackup_Click(object sender, RoutedEventArgs e)
        {
            string folder = txtBackupFolder.Text.Trim();
            if (folder == "")
            {
                ShowError("⚠️ Please select a backup folder!");
                return;
            }

            await ShowLoadingWithMinimumTime("Creating backup...", async () =>
            {
                var result = await Task.Run(() => BackupManager.CreateBackup(folder));

                if (result.Success)
                {
                    ShowSuccess(result.Message);
                    LoadBackups();
                }
                else
                {
                    ShowError(result.Message);
                }
            });
        }

        // ═══════════════════════════════════════════════════════════
        // RESTORE SINGLE BACKUP (WITH MINIMUM LOADING TIME)
        // ═══════════════════════════════════════════════════════════
        private async void btnRestore_Click(object sender, RoutedEventArgs e)
        {
            // Must have exactly one selected
            if (_selectedBackups.Count == 0)
            {
                ShowError("⚠️ Select a backup to restore!");
                return;
            }

            if (_selectedBackups.Count > 1)
            {
                ShowError("⚠️ Can only restore one backup at a time!");
                return;
            }

            BackupFile selected = _selectedBackups[0];

            MessageBoxResult confirm = System.Windows.MessageBox.Show(
                $"Restore from:\n{selected.FileName}\n\n" +
                "⚠️ This will REPLACE your current database.\n" +
                "A safety backup will be created first.\n\n" +
                "Continue?",
                "⚠️ Confirm Restore",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            await ShowLoadingWithMinimumTime("Restoring backup...", async () =>
            {
                var result = await Task.Run(() => BackupManager.RestoreBackup(selected.FilePath));

                if (result.Success)
                {
                    System.Windows.MessageBox.Show(
                        result.Message,
                        "✅ Restore Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    System.Diagnostics.Process.Start(
                        System.Windows.Application.ResourceAssembly.Location);
                    System.Windows.Application.Current.Shutdown();
                }
                else
                {
                    ShowError(result.Message);
                }
            });
        }

        // ═══════════════════════════════════════════════════════════
        // DELETE MULTIPLE BACKUPS (WITH MINIMUM LOADING TIME)
        // ═══════════════════════════════════════════════════════════
        private async void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBackups.Count == 0)
            {
                ShowError("⚠️ Select at least one backup to delete!");
                return;
            }

            string confirmMessage;
            if (_selectedBackups.Count == 1)
            {
                confirmMessage = $"Delete backup:\n{_selectedBackups[0].FileName}?";
            }
            else
            {
                confirmMessage = $"Delete {_selectedBackups.Count} selected backups?\n\n" +
                    string.Join("\n", _selectedBackups.Select(b => $"• {b.FileName}"));
            }

            MessageBoxResult confirm = System.Windows.MessageBox.Show(
                confirmMessage,
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            await ShowLoadingWithMinimumTime($"Deleting {_selectedBackups.Count} backup(s)...", async () =>
            {
                int successCount = 0;
                int failCount = 0;

                // Delete each selected backup
                foreach (var backup in _selectedBackups.ToList())
                {
                    var result = await Task.Run(() =>
                    {
                        return BackupManager.DeleteBackup(backup.FilePath);
                    });

                    if (result.Success)
                    {
                        successCount++;
                    }
                    else
                    {
                        failCount++;
                    }
                }

                // Show results
                if (failCount == 0)
                {
                    ShowSuccess($"✅ Deleted {successCount} backup(s) successfully!");
                }
                else if (successCount == 0)
                {
                    ShowError($"❌ Failed to delete {failCount} backup(s)");
                }
                else
                {
                    ShowSuccess($"✅ Deleted {successCount}, ❌ Failed {failCount}");
                }

                LoadBackups();
            });
        }

        // ═══════════════════════════════════════════════════════════
        // SELECTION CHANGED (for regular left-click)
        // ═══════════════════════════════════════════════════════════
        private void lvBackups_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Only process if it's a left-click selection (not our right-click handling)
            if (System.Windows.Input.Mouse.RightButton != System.Windows.Input.MouseButtonState.Pressed)
            {
                _selectedBackups.Clear();

                foreach (var item in lvBackups.SelectedItems)
                {
                    if (item is BackupFile backup)
                        _selectedBackups.Add(backup);
                }

                UpdateSelectionDisplay();
            }
        }

        // ═══════════════════════════════════════════════════════════
        // CLOSE
        // ═══════════════════════════════════════════════════════════
        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // ═══════════════════════════════════════════════════════════
        // LOADING HELPERS
        // ═══════════════════════════════════════════════════════════
        void ShowLoading(string message)
        {
            lblLoadingMessage.Text = message;
            loadingOverlay.Visibility = Visibility.Visible;
        }

        void HideLoading()
        {
            loadingOverlay.Visibility = Visibility.Collapsed;
        }

        void SetButtonsEnabled(bool enabled)
        {
            btnCreate.IsEnabled = enabled;
            btnRestore.IsEnabled = enabled && _selectedBackups.Count > 0;
            btnDelete.IsEnabled = enabled && _selectedBackups.Count > 0;
            btnBrowse.IsEnabled = enabled;
            btnClose.IsEnabled = enabled;
        }

        // ═══════════════════════════════════════════════════════════
        // MESSAGE HELPERS
        // ═══════════════════════════════════════════════════════════
        void ShowError(string msg)
        {
            lblMessage.Content = msg;
            lblMessage.Foreground = System.Windows.Media.Brushes.Tomato;
        }

        void ShowSuccess(string msg)
        {
            lblMessage.Content = msg;
            lblMessage.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
    }
}