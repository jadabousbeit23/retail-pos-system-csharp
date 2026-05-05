using POSSystem.Database;
using System;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;

namespace POSSystem.Windows
{
    public partial class UsersWindow : Window
    {
        public UsersWindow()
        {
            InitializeComponent();
            LoadUsers();
        }

        void LoadUsers()
        {
            lvUsers.Items.Clear();
            using (SQLiteConnection con = new SQLiteConnection(
                DatabaseHelper.ConnectionString))
            {
                con.Open();
                SQLiteCommand cmd = new SQLiteCommand(
                    "SELECT Id, Username, FullName, Role " +
                    "FROM Users ORDER BY Username", con);
                SQLiteDataReader r = cmd.ExecuteReader();
                int count = 0;
                while (r.Read())
                {
                    lvUsers.Items.Add(new User
                    {
                        Id       = Convert.ToInt32(r["Id"]),
                        Username = r["Username"].ToString(),
                        FullName = r["FullName"].ToString(),
                        Role     = r["Role"].ToString(),
                        Code     = "••••••"
                    });
                    count++;
                }
                r.Close();
                lblUserCount.Content = $"{count} user(s)";
            }
        }

        private void lvUsers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lvUsers.SelectedItem == null) return;
            User u = (User)lvUsers.SelectedItem;
            txtUsername.Text      = u.Username;
            txtFullName.Text      = u.FullName;
            txtCode.Text          = "";
            txtPassword.Password  = "";
            cmbRole.SelectedIndex = u.Role.ToLower() == "admin" ? 1 : 0;
            lblMessage.Content    = "";
        }

        // ── ADD ───────────────────────────────────────
        private void btnAddUser_Click(object sender, RoutedEventArgs e)
        {
            string name     = txtUsername.Text.Trim();
            string fullName = txtFullName.Text.Trim();
            string code     = txtCode.Text.Trim();
            string password = txtPassword.Password.Trim();
            string role     = (cmbRole.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "cashier";

            if (name == "" || code == "")
            { ShowError("⚠️ Username and Login Code are required!"); return; }
            if (code.Length < 4)
            { ShowError("⚠️ Login code must be at least 4 digits!"); return; }

            using (SQLiteConnection con = new SQLiteConnection(
                DatabaseHelper.ConnectionString))
            {
                con.Open();

                SQLiteCommand chk = new SQLiteCommand(
                    "SELECT COUNT(*) FROM Users WHERE Username = @u", con);
                chk.Parameters.AddWithValue("@u", name);
                if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
                { ShowError("⚠️ Username already exists!"); return; }

                SQLiteCommand cmd = new SQLiteCommand(@"
                    INSERT INTO Users (Username, FullName, Password, Role, Code)
                    VALUES (@u, @fn, @p, @r, @c)", con);
                cmd.Parameters.AddWithValue("@u",  name);
                cmd.Parameters.AddWithValue("@fn", fullName);
                cmd.Parameters.AddWithValue("@p",  password);
                cmd.Parameters.AddWithValue("@r",  role);
                cmd.Parameters.AddWithValue("@c",  HashHelper.Hash(code));
                cmd.ExecuteNonQuery();
            }

            // ── AUDIT LOG ──
            DatabaseHelper.Log("User Modified", $"Added user '{name}' ({role})", "User");

            ShowSuccess($"✅ User '{name}' added!");
            ClearForm();
            LoadUsers();
        }

        // ── UPDATE ────────────────────────────────────
        private void btnUpdateUser_Click(object sender, RoutedEventArgs e)
        {
            if (lvUsers.SelectedItem == null)
            { ShowError("⚠️ Select a user first!"); return; }

            User   selected = (User)lvUsers.SelectedItem;
            string name     = txtUsername.Text.Trim();
            string fullName = txtFullName.Text.Trim();
            string code     = txtCode.Text.Trim();
            string password = txtPassword.Password.Trim();
            string role     = (cmbRole.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "cashier";

            if (name == "") { ShowError("⚠️ Username is required!"); return; }

            using (SQLiteConnection con = new SQLiteConnection(
                DatabaseHelper.ConnectionString))
            {
                con.Open();

                if (code != "")
                {
                    if (code.Length < 4)
                    { ShowError("⚠️ Login code must be at least 4 digits!"); return; }

                    SQLiteCommand cmd = new SQLiteCommand(@"
                        UPDATE Users SET
                            Username=@u, FullName=@fn,
                            Password=@p, Role=@r, Code=@c
                        WHERE Id=@id", con);
                    cmd.Parameters.AddWithValue("@u",  name);
                    cmd.Parameters.AddWithValue("@fn", fullName);
                    cmd.Parameters.AddWithValue("@p",  password);
                    cmd.Parameters.AddWithValue("@r",  role);
                    cmd.Parameters.AddWithValue("@c",  HashHelper.Hash(code));
                    cmd.Parameters.AddWithValue("@id", selected.Id);
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    SQLiteCommand cmd = new SQLiteCommand(@"
                        UPDATE Users SET
                            Username=@u, FullName=@fn,
                            Password=@p, Role=@r
                        WHERE Id=@id", con);
                    cmd.Parameters.AddWithValue("@u",  name);
                    cmd.Parameters.AddWithValue("@fn", fullName);
                    cmd.Parameters.AddWithValue("@p",  password);
                    cmd.Parameters.AddWithValue("@r",  role);
                    cmd.Parameters.AddWithValue("@id", selected.Id);
                    cmd.ExecuteNonQuery();
                }
            }

            // ── AUDIT LOG ──
            DatabaseHelper.Log("User Modified", $"Updated user '{name}'", "User");

            ShowSuccess($"✅ User '{name}' updated!");
            ClearForm();
            LoadUsers();
        }

        // ── DELETE ────────────────────────────────────
        private void btnDeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (lvUsers.SelectedItem == null)
            { ShowError("⚠️ Select a user first!"); return; }

            User selected = (User)lvUsers.SelectedItem;

            if (selected.Username == Session.Username)
            { ShowError("⚠️ You cannot delete your own account!"); return; }

            if (MessageBox.Show($"Delete user '{selected.Username}'?", "Delete User",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            using (SQLiteConnection con = new SQLiteConnection(
                DatabaseHelper.ConnectionString))
            {
                con.Open();
                SQLiteCommand cmd = new SQLiteCommand(
                    "DELETE FROM Users WHERE Id=@id", con);
                cmd.Parameters.AddWithValue("@id", selected.Id);
                cmd.ExecuteNonQuery();
            }

            // ── AUDIT LOG ──
            DatabaseHelper.Log("User Modified", $"Deleted user '{selected.Username}'", "User");

            ShowSuccess("✅ User deleted!");
            ClearForm();
            LoadUsers();
        }

        void ClearForm()
        {
            txtUsername.Clear();
            txtFullName.Clear();
            txtCode.Clear();
            txtPassword.Clear();
            cmbRole.SelectedIndex   = 0;
            lvUsers.SelectedItem    = null;
            lblMessage.Content      = "";
        }

        void ShowError(string msg)
        {
            lblMessage.Content    = msg;
            lblMessage.Foreground = System.Windows.Media.Brushes.Tomato;
        }

        void ShowSuccess(string msg)
        {
            lblMessage.Content    = msg;
            lblMessage.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
    }
}
