using POSSystem.Database;
using System;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;

namespace POSSystem.Windows
{
    public partial class SuppliersWindow : Window
    {
        public SuppliersWindow()
        {
            InitializeComponent();
            LoadSuppliers();
        }

        void LoadSuppliers()
        {
            lvSuppliers.Items.Clear();
            using (SQLiteConnection con =
                new SQLiteConnection(
                    DatabaseHelper.ConnectionString))
            {
                con.Open();
                SQLiteDataReader r =
                    new SQLiteCommand(
                        "SELECT * FROM Suppliers " +
                        "ORDER BY Name", con)
                    .ExecuteReader();
                while (r.Read())
                    lvSuppliers.Items.Add(
                        new Supplier
                        {
                            Id = Convert.ToInt32(
                                r["Id"]),
                            Name = r["Name"].ToString(),
                            Phone = r["Phone"].ToString(),
                            Email = r["Email"].ToString()
                        });
                r.Close();
            }
        }

        private void lvSuppliers_SelectionChanged(
            object sender, SelectionChangedEventArgs e)
        {
            if (lvSuppliers.SelectedItem == null)
                return;
            Supplier s =
                (Supplier)lvSuppliers.SelectedItem;
            txtName.Text = s.Name;
            txtPhone.Text = s.Phone;
            txtEmail.Text = s.Email;
            lblMessage.Content = "";
        }

        private void btnAdd_Click(object sender,
            RoutedEventArgs e)
        {
            string name = txtName.Text.Trim();
            if (name == "")
            {
                ShowError(
                    "⚠️ Supplier name is required!");
                return;
            }

            using (SQLiteConnection con =
                new SQLiteConnection(
                    DatabaseHelper.ConnectionString))
            {
                con.Open();
                SQLiteCommand cmd =
                    new SQLiteCommand(@"
                    INSERT INTO Suppliers
                    (Name, Phone, Email)
                    VALUES (@n, @p, @e)", con);
                cmd.Parameters.AddWithValue(
                    "@n", name);
                cmd.Parameters.AddWithValue(
                    "@p", txtPhone.Text.Trim());
                cmd.Parameters.AddWithValue(
                    "@e", txtEmail.Text.Trim());
                cmd.ExecuteNonQuery();
            }

            ShowSuccess($"✅ '{name}' added!");
            ClearForm();
            LoadSuppliers();
        }

        private void btnUpdate_Click(object sender,
            RoutedEventArgs e)
        {
            if (lvSuppliers.SelectedItem == null)
            {
                ShowError(
                    "⚠️ Select a supplier first!");
                return;
            }

            Supplier s =
                (Supplier)lvSuppliers.SelectedItem;
            string name = txtName.Text.Trim();
            if (name == "")
            {
                ShowError("⚠️ Name is required!");
                return;
            }

            using (SQLiteConnection con =
                new SQLiteConnection(
                    DatabaseHelper.ConnectionString))
            {
                con.Open();
                SQLiteCommand cmd =
                    new SQLiteCommand(@"
                    UPDATE Suppliers SET
                    Name=@n, Phone=@p, Email=@e
                    WHERE Id=@id", con);
                cmd.Parameters.AddWithValue(
                    "@n", name);
                cmd.Parameters.AddWithValue(
                    "@p", txtPhone.Text.Trim());
                cmd.Parameters.AddWithValue(
                    "@e", txtEmail.Text.Trim());
                cmd.Parameters.AddWithValue(
                    "@id", s.Id);
                cmd.ExecuteNonQuery();
            }

            ShowSuccess($"✅ '{name}' updated!");
            ClearForm();
            LoadSuppliers();
        }

        private void btnDelete_Click(object sender,
            RoutedEventArgs e)
        {
            if (lvSuppliers.SelectedItem == null)
            {
                ShowError(
                    "⚠️ Select a supplier first!");
                return;
            }

            Supplier s =
                (Supplier)lvSuppliers.SelectedItem;

            MessageBoxResult confirm = MessageBox.Show(
                $"Delete supplier '{s.Name}'?",
                "Delete Supplier",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            using (SQLiteConnection con =
                new SQLiteConnection(
                    DatabaseHelper.ConnectionString))
            {
                con.Open();
                new SQLiteCommand(
                    "DELETE FROM Suppliers " +
                    "WHERE Id=" + s.Id, con)
                    .ExecuteNonQuery();
            }

            ShowSuccess("✅ Supplier deleted!");
            ClearForm();
            LoadSuppliers();
        }

        private void btnClose_Click(object sender,
            RoutedEventArgs e)
        {
            this.Close();
        }

        void ClearForm()
        {
            txtName.Clear();
            txtPhone.Clear();
            txtEmail.Clear();
            lvSuppliers.SelectedItem = null;
        }

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