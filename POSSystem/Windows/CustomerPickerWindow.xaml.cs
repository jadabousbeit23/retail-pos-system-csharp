using POSSystem.Database;
using System;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace POSSystem.Windows
{
    public partial class CustomerPickerWindow : Window
    {
        public Customer SelectedCustomer { get; private set; }

        public CustomerPickerWindow()
        {
            InitializeComponent();
            LoadCustomers();
            txtSearch.Focus();
        }

        void LoadCustomers(string search = "")
        {
            lvCustomers.Items.Clear();
            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var cmd = new SQLiteCommand(@"
                    SELECT * FROM Customers
                    WHERE Name LIKE @s OR Phone LIKE @s OR LoyaltyCode LIKE @s
                    ORDER BY Name", con);
                cmd.Parameters.AddWithValue("@s", $"%{search}%");
                var r = cmd.ExecuteReader();
                while (r.Read())
                    lvCustomers.Items.Add(new Customer
                    {
                        Id = Convert.ToInt32(r["Id"]),
                        Name = r["Name"].ToString(),
                        Phone = r["Phone"].ToString(),
                        Points = Convert.ToInt32(r["Points"]),
                        Stamps = Convert.ToInt32(r["Stamps"]),
                        LoyaltyCode = r["LoyaltyCode"].ToString(),
                        TotalSpent = Convert.ToDouble(r["TotalSpent"])
                    });
                r.Close();
            }
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
            => LoadCustomers(txtSearch.Text.Trim());

        private void txtScanCard_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            string code = txtScanCard.Text.Trim();
            txtScanCard.Clear();
            if (string.IsNullOrEmpty(code)) return;

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var cmd = new SQLiteCommand(
                    "SELECT * FROM Customers WHERE LoyaltyCode=@code", con);
                cmd.Parameters.AddWithValue("@code", code);
                var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    SelectedCustomer = new Customer
                    {
                        Id = Convert.ToInt32(r["Id"]),
                        Name = r["Name"].ToString(),
                        Phone = r["Phone"].ToString(),
                        Points = Convert.ToInt32(r["Points"]),
                        Stamps = Convert.ToInt32(r["Stamps"]),
                        LoyaltyCode = r["LoyaltyCode"].ToString()
                    };
                    r.Close();
                    DialogResult = true;
                    Close();
                }
                else { r.Close(); lblMsg.Content = $"❌ Card not found: {code}"; }
            }
            e.Handled = true;
        }

        private void lvCustomers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lvCustomers.SelectedItem == null) return;
            SelectedCustomer = (Customer)lvCustomers.SelectedItem;
            DialogResult = true;
            Close();
        }

        private void btnSelect_Click(object sender, RoutedEventArgs e)
        {
            if (lvCustomers.SelectedItem == null)
            { lblMsg.Content = "⚠️ Select a customer first!"; return; }
            SelectedCustomer = (Customer)lvCustomers.SelectedItem;
            DialogResult = true;
            Close();
        }

        private void btnNoCustomer_Click(object sender, RoutedEventArgs e)
        {
            SelectedCustomer = null;
            DialogResult = false;
            Close();
        }
    }
}