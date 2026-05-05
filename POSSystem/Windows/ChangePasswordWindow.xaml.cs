using POSSystem.Database;
using System;
using System.Data.SQLite;
using System.Windows;

namespace POSSystem.Windows
{
    public partial class ChangePasswordWindow : Window
    {
        public ChangePasswordWindow()
        {
            InitializeComponent();
        }

        // ══════════════════════════════════════
        // SAVE
        // ══════════════════════════════════════
        private void btnSave_Click(object sender,
            RoutedEventArgs e)
        {
            string currentCode = txtCurrent.Password.Trim();
            string newCode = txtNew.Password.Trim();
            string confirmCode = txtConfirm.Password.Trim();

            if (currentCode == "" || newCode == "" ||
                confirmCode == "")
            {
                ShowError("⚠️ All fields are required!");
                return;
            }
            if (newCode.Length < 4)
            {
                ShowError(
                    "⚠️ New code must be at least 4 digits!");
                return;
            }
            if (newCode != confirmCode)
            {
                ShowError("⚠️ New codes do not match!");
                return;
            }

            using (SQLiteConnection con = new SQLiteConnection(
                DatabaseHelper.ConnectionString))
            {
                con.Open();

                // Get stored BCrypt hash for current user
                SQLiteCommand getCmd = new SQLiteCommand(
                    "SELECT Code FROM Users " +
                    "WHERE Username = @u", con);
                getCmd.Parameters.AddWithValue("@u",
                    Session.Username);
                string storedHash =
                    getCmd.ExecuteScalar()?.ToString();

                if (storedHash == null)
                {
                    ShowError("❌ User not found!");
                    return;
                }

                // ★ BCrypt verify current code
                if (!HashHelper.Verify(currentCode, storedHash))
                {
                    ShowError("❌ Current code is incorrect!");
                    return;
                }

                // ★ Save new BCrypt hash
                SQLiteCommand upd = new SQLiteCommand(
                    "UPDATE Users SET Code = @c " +
                    "WHERE Username = @u", con);
                upd.Parameters.AddWithValue("@c",
                    HashHelper.Hash(newCode));
                upd.Parameters.AddWithValue("@u",
                    Session.Username);
                upd.ExecuteNonQuery();
            }

            ShowSuccess("✅ Code changed successfully!");
            txtCurrent.Clear();
            txtNew.Clear();
            txtConfirm.Clear();
        }

        // ══════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════
        void ShowError(string msg)
        {
            lblMessage.Content = msg;
            lblMessage.Foreground =
                System.Windows.Media.Brushes.Tomato;
        }

        void ShowSuccess(string msg)
        {
            lblMessage.Content = msg;
            lblMessage.Foreground =
                System.Windows.Media.Brushes.LightGreen;
        }
    }
}