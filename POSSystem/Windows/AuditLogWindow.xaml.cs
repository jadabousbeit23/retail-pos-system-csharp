using POSSystem.Database;
using System;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;

namespace POSSystem.Windows
{
    public class AuditRow
    {
        public int Id { get; set; }
        public string LoggedAt { get; set; }
        public string Username { get; set; }
        public string Module { get; set; }
        public string Action { get; set; }
        public string Details { get; set; }
        public string ModuleColor
        {
            get
            {
                switch (Module?.ToLower())
                {
                    case "sale": return "#107C10";
                    case "void": return "#C42B1C";
                    case "product": return "#0078D4";
                    case "user": return "#9D5D00";
                    case "shift": return "#5F5F5F";
                    case "return": return "#CA5010";
                    case "login": return "#0063B1";
                    case "customer": return "#107C10";
                    default: return "#8A8A8A";
                }
            }
        }
    }

    public partial class AuditLogWindow : Window
    {
        public AuditLogWindow()
        {
            InitializeComponent();
            LoadFilters();
            LoadLog();
        }

        void LoadFilters()
        {
            // FIX: Store current selections before clearing
            string selectedUser = cmbUser.SelectedItem?.ToString();
            string selectedModule = cmbModule.SelectedItem?.ToString();

            cmbUser.Items.Clear();
            cmbUser.Items.Add("All Users");
            cmbModule.Items.Clear();
            cmbModule.Items.Add("All Modules");

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();

                var ru = new SQLiteCommand(
                    "SELECT DISTINCT Username FROM AuditLog ORDER BY Username", con)
                    .ExecuteReader();
                while (ru.Read()) cmbUser.Items.Add(ru["Username"].ToString());
                ru.Close();

                var rm = new SQLiteCommand(
                    "SELECT DISTINCT Module FROM AuditLog WHERE Module != '' ORDER BY Module", con)
                    .ExecuteReader();
                while (rm.Read()) cmbModule.Items.Add(rm["Module"].ToString());
                rm.Close();
            }

            // FIX: Restore selections if they still exist, otherwise default to 0
            cmbUser.SelectedIndex = 0;
            cmbModule.SelectedIndex = 0;

            // Try to restore previous selection
            if (!string.IsNullOrEmpty(selectedUser))
            {
                int userIndex = cmbUser.Items.IndexOf(selectedUser);
                if (userIndex >= 0) cmbUser.SelectedIndex = userIndex;
            }

            if (!string.IsNullOrEmpty(selectedModule))
            {
                int moduleIndex = cmbModule.Items.IndexOf(selectedModule);
                if (moduleIndex >= 0) cmbModule.SelectedIndex = moduleIndex;
            }
        }

        void LoadLog(string search = "", string user = "", string module = "",
                     string dateFrom = "", string dateTo = "")
        {
            lvLog.Items.Clear();
            int count = 0;

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                string where = "WHERE 1=1 ";
                if (!string.IsNullOrEmpty(search))
                    where += $"AND (Action LIKE '%{search}%' OR Details LIKE '%{search}%') ";
                if (!string.IsNullOrEmpty(user) && user != "All Users")
                    where += $"AND Username='{user}' ";
                if (!string.IsNullOrEmpty(module) && module != "All Modules")
                    where += $"AND Module='{module}' ";
                if (!string.IsNullOrEmpty(dateFrom))
                    where += $"AND LoggedAt >= '{dateFrom}' ";
                if (!string.IsNullOrEmpty(dateTo))
                    where += $"AND LoggedAt <= '{dateTo} 23:59:59' ";

                var r = new SQLiteCommand(
                    $"SELECT * FROM AuditLog {where} ORDER BY Id DESC LIMIT 500", con)
                    .ExecuteReader();

                while (r.Read())
                {
                    lvLog.Items.Add(new AuditRow
                    {
                        Id = Convert.ToInt32(r["Id"]),
                        LoggedAt = r["LoggedAt"].ToString(),
                        Username = r["Username"].ToString(),
                        Module = r["Module"].ToString(),
                        Action = r["Action"].ToString(),
                        Details = r["Details"].ToString()
                    });
                    count++;
                }
                r.Close();
            }
            lblCount.Content = $"{count} records";
        }

        private void txtSearch_Changed(object sender, TextChangedEventArgs e)
        {
            if (txtSearch.Text.Length == 0) LoadLog();
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
            => ApplyFilters();

        private void btnSearch_Click(object sender, RoutedEventArgs e)
            => ApplyFilters();

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            // FIX: Clear text fields
            txtSearch.Clear();
            txtDateFrom.Clear();
            txtDateTo.Clear();

            // FIX: Reset dropdowns to "All" selection (index 0)
            cmbUser.SelectedIndex = 0;
            cmbModule.SelectedIndex = 0;

            // FIX: Reload the log with empty filters
            LoadLog();
        }

        void ApplyFilters()
        {
            LoadLog(
                txtSearch.Text.Trim(),
                cmbUser.SelectedItem?.ToString() ?? "",
                cmbModule.SelectedItem?.ToString() ?? "",
                txtDateFrom.Text.Trim(),
                txtDateTo.Text.Trim());
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}