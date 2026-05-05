using POSSystem.Database;
using System;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace POSSystem.Windows
{
    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string LoyaltyCode { get; set; }
        public int Points { get; set; }
        public int Stamps { get; set; }
        public double TotalSpent { get; set; }
        public string Notes { get; set; }
        public string CreatedAt { get; set; }
        public string TotalSpentDisplay => $"LBP {TotalSpent:N0}";
        public string Display => $"{Name}  |  📞 {Phone}  |  ⭐ {Points} pts";
    }

    public partial class CustomersWindow : Window
    {
        public CustomersWindow()
        {
            InitializeComponent();
            LoadCustomers();
        }

        void LoadCustomers(string search = "")
        {
            lvCustomers.Items.Clear();
            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var cmd = new SQLiteCommand(@"
                    SELECT * FROM Customers
                    WHERE Name  LIKE @s
                       OR Phone LIKE @s
                       OR Email LIKE @s
                       OR LoyaltyCode LIKE @s
                    ORDER BY Name", con);
                cmd.Parameters.AddWithValue("@s", $"%{search}%");
                var r = cmd.ExecuteReader();
                int count = 0;
                while (r.Read())
                {
                    lvCustomers.Items.Add(new Customer
                    {
                        Id         = Convert.ToInt32(r["Id"]),
                        Name       = r["Name"].ToString(),
                        Phone      = r["Phone"].ToString(),
                        Email      = r["Email"].ToString(),
                        LoyaltyCode = r["LoyaltyCode"].ToString(),
                        Points     = Convert.ToInt32(r["Points"]),
                        Stamps     = Convert.ToInt32(r["Stamps"]),
                        TotalSpent = Convert.ToDouble(r["TotalSpent"]),
                        Notes      = r["Notes"].ToString(),
                        CreatedAt  = r["CreatedAt"].ToString()
                    });
                    count++;
                }
                r.Close();
                lblCustomerCount.Content = $"{count} customers";
            }
        }

        private void lvCustomers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lvCustomers.SelectedItem == null) return;
            var c = (Customer)lvCustomers.SelectedItem;
            txtCustName.Text  = c.Name;
            txtCustPhone.Text = c.Phone;
            txtCustEmail.Text = c.Email;
            txtCustNotes.Text = c.Notes;
            lblMessage.Content = "";
        }

        private void btnAddCustomer_Click(object sender, RoutedEventArgs e)
        {
            string name = txtCustName.Text.Trim();
            if (name == "") { ShowError("⚠️ Customer name is required!"); return; }

            string code = $"CUS-{name.Substring(0, Math.Min(3, name.Length)).ToUpper()}-{new Random().Next(1000, 9999)}";

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var cmd = new SQLiteCommand(@"
                    INSERT INTO Customers
                    (Name, Phone, Email, LoyaltyCode, Points, Stamps, TotalSpent, CreatedAt, Notes)
                    VALUES (@n, @ph, @em, @code, 0, 0, 0, @at, @notes)", con);
                cmd.Parameters.AddWithValue("@n",     name);
                cmd.Parameters.AddWithValue("@ph",    txtCustPhone.Text.Trim());
                cmd.Parameters.AddWithValue("@em",    txtCustEmail.Text.Trim());
                cmd.Parameters.AddWithValue("@code",  code);
                cmd.Parameters.AddWithValue("@at",    DateTime.Now.ToString("dd/MM/yyyy"));
                cmd.Parameters.AddWithValue("@notes", txtCustNotes.Text.Trim());
                cmd.ExecuteNonQuery();
            }
            ShowSuccess($"✅ '{name}' added! Card: {code}");
            ClearForm();
            LoadCustomers();
        }

        private void btnUpdateCustomer_Click(object sender, RoutedEventArgs e)
        {
            if (lvCustomers.SelectedItem == null) { ShowError("⚠️ Select a customer!"); return; }
            var c = (Customer)lvCustomers.SelectedItem;
            string name = txtCustName.Text.Trim();
            if (name == "") { ShowError("⚠️ Name is required!"); return; }

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var cmd = new SQLiteCommand(@"
                    UPDATE Customers SET
                        Name=@n, Phone=@ph, Email=@em, Notes=@notes
                    WHERE Id=@id", con);
                cmd.Parameters.AddWithValue("@n",     name);
                cmd.Parameters.AddWithValue("@ph",    txtCustPhone.Text.Trim());
                cmd.Parameters.AddWithValue("@em",    txtCustEmail.Text.Trim());
                cmd.Parameters.AddWithValue("@notes", txtCustNotes.Text.Trim());
                cmd.Parameters.AddWithValue("@id",    c.Id);
                cmd.ExecuteNonQuery();
            }
            ShowSuccess($"✅ '{name}' updated!");
            ClearForm();
            LoadCustomers();
        }

        private void btnAddPoints_Click(object sender, RoutedEventArgs e)
        {
            if (lvCustomers.SelectedItem == null) { ShowError("⚠️ Select a customer!"); return; }
            var c = (Customer)lvCustomers.SelectedItem;

            string input = ShowInputDialog(
                $"Add points to {c.Name}\nCurrent: {c.Points} pts\n\nEnter points to add:",
                "Add Points", "0");

            if (!int.TryParse(input, out int pts) || pts <= 0) return;

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                new SQLiteCommand(
                    $"UPDATE Customers SET Points=Points+{pts} WHERE Id={c.Id}", con)
                    .ExecuteNonQuery();
            }
            ShowSuccess($"✅ +{pts} points added to {c.Name}");
            LoadCustomers();
        }

        private void btnDeleteCustomer_Click(object sender, RoutedEventArgs e)
        {
            if (lvCustomers.SelectedItem == null) { ShowError("⚠️ Select a customer!"); return; }
            var c = (Customer)lvCustomers.SelectedItem;
            if (MessageBox.Show($"Delete '{c.Name}'?", "Delete Customer",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                new SQLiteCommand($"DELETE FROM LoyaltyTransactions WHERE CustomerId={c.Id}", con).ExecuteNonQuery();
                new SQLiteCommand($"DELETE FROM Customers WHERE Id={c.Id}", con).ExecuteNonQuery();
            }
            ShowSuccess("✅ Customer deleted.");
            ClearForm();
            LoadCustomers();
        }

        // ── PRINT CARD — opens branded loyalty card with barcode ──
        private void btnPrintCard_Click(object sender, RoutedEventArgs e)
        {
            if (lvCustomers.SelectedItem == null) { ShowError("⚠️ Select a customer!"); return; }
            var c = (Customer)lvCustomers.SelectedItem;
            var win = new LoyaltyCardWindow(c);
            win.Owner = this;
            win.ShowDialog();
        }

        private void txtScanCard_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            string code = txtScanCard.Text.Trim();
            txtScanCard.Clear();
            if (string.IsNullOrEmpty(code)) return;

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var cmd = new SQLiteCommand("SELECT * FROM Customers WHERE LoyaltyCode=@code", con);
                cmd.Parameters.AddWithValue("@code", code);
                var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    string name = r["Name"].ToString();
                    r.Close();
                    txtSearch.Text = name;
                    LoadCustomers(name);
                    ShowSuccess($"✅ Found: {name}");
                }
                else { r.Close(); ShowError($"❌ No customer for card: {code}"); }
            }
            e.Handled = true;
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
            => LoadCustomers(txtSearch.Text.Trim());

        void ClearForm()
        {
            txtCustName.Clear(); txtCustPhone.Clear();
            txtCustEmail.Clear(); txtCustNotes.Clear();
            lvCustomers.SelectedItem = null;
            lblMessage.Content = "";
        }

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

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();

        private string ShowInputDialog(string message, string title, string defaultValue = "")
        {
            var dlg = new Window
            {
                Title = title,
                Width = 360, Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, ResizeMode = ResizeMode.NoResize,
                Background = System.Windows.Media.Brushes.White
            };
            var stack = new StackPanel { Margin = new Thickness(16) };
            stack.Children.Add(new TextBlock
            {
                Text = message, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });
            var txt = new TextBox
            {
                Text = defaultValue, Padding = new Thickness(6),
                FontSize = 14, Margin = new Thickness(0, 0, 0, 12)
            };
            stack.Children.Add(txt);
            var btnPanel = new StackPanel
            { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOk     = new Button { Content = "OK",     Width = 80, Height = 30, Margin = new Thickness(0,0,8,0), IsDefault = true };
            var btnCancel = new Button { Content = "Cancel", Width = 80, Height = 30, IsCancel = true };
            string result = "";
            btnOk.Click     += (s, ev) => { result = txt.Text; dlg.DialogResult = true; };
            btnCancel.Click += (s, ev) => { dlg.DialogResult = false; };
            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            stack.Children.Add(btnPanel);
            dlg.Content = stack;
            dlg.Loaded += (s, ev) => { txt.Focus(); txt.SelectAll(); };
            dlg.ShowDialog();
            return result;
        }

        public static void LogLoyalty(SQLiteConnection con, int customerId,
            int saleId, int points, int stamps, string type, string desc)
        {
            var cmd = new SQLiteCommand(@"
                INSERT INTO LoyaltyTransactions
                (CustomerId, SaleId, Points, Stamps, Type, Description, CreatedAt)
                VALUES (@cid, @sid, @pts, @st, @type, @desc, @at)", con);
            cmd.Parameters.AddWithValue("@cid",  customerId);
            cmd.Parameters.AddWithValue("@sid",  saleId);
            cmd.Parameters.AddWithValue("@pts",  points);
            cmd.Parameters.AddWithValue("@st",   stamps);
            cmd.Parameters.AddWithValue("@type", type);
            cmd.Parameters.AddWithValue("@desc", desc);
            cmd.Parameters.AddWithValue("@at",   DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
            cmd.ExecuteNonQuery();
        }
    }
}
