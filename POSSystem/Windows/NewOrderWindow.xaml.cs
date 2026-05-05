using POSSystem.Database;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;

namespace POSSystem.Windows
{
    public partial class NewOrderWindow : Window
    {
        List<PurchaseOrderItem> _items =
            new List<PurchaseOrderItem>();
        List<Product> _products =
            new List<Product>();

        public NewOrderWindow()
        {
            InitializeComponent();
            LoadSuppliers();
            LoadProducts();
        }

        void LoadSuppliers()
        {
            cmbSupplier.Items.Clear();
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
                    cmbSupplier.Items.Add(
                        new Supplier
                        {
                            Id = Convert.ToInt32(r["Id"]),
                            Name = r["Name"].ToString(),
                            Phone = r["Phone"].ToString()
                        });
                r.Close();
            }

            if (cmbSupplier.Items.Count > 0)
                cmbSupplier.SelectedIndex = 0;
        }

        void LoadProducts()
        {
            _products.Clear();
            cmbProduct.Items.Clear();
            using (SQLiteConnection con =
                new SQLiteConnection(
                    DatabaseHelper.ConnectionString))
            {
                con.Open();
                SQLiteDataReader r =
                    new SQLiteCommand(
                        "SELECT * FROM Products " +
                        "ORDER BY Name", con)
                    .ExecuteReader();
                while (r.Read())
                {
                    Product p = new Product
                    {
                        Id = Convert.ToInt32(r["Id"]),
                        Name = r["Name"].ToString(),
                        Price = Convert.ToDouble(r["Price"])
                    };
                    _products.Add(p);
                    cmbProduct.Items.Add(p);
                }
                r.Close();
            }

            if (cmbProduct.Items.Count > 0)
                cmbProduct.SelectedIndex = 0;
        }

        // ══════════════════════════════════════
        // ADD ITEM
        // ══════════════════════════════════════
        private void btnAddItem_Click(object sender,
            RoutedEventArgs e)
        {
            if (cmbProduct.SelectedItem == null)
            {
                ShowError("⚠️ Select a product!");
                return;
            }
            if (!int.TryParse(txtQty.Text,
                out int qty) || qty <= 0)
            {
                ShowError("⚠️ Enter a valid quantity!");
                return;
            }

            // Accept plain numbers — strip "LBP" if user left it in
            string costRaw = txtCost.Text
                .Replace("LBP", "")
                .Replace(",", "")
                .Trim();

            if (!double.TryParse(costRaw,
                out double cost) || cost < 0)
            {
                ShowError("⚠️ Enter a valid cost!");
                return;
            }

            Product p = (Product)cmbProduct.SelectedItem;

            // If product already in list, just increase qty
            foreach (PurchaseOrderItem existing in _items)
            {
                if (existing.ProductId == p.Id)
                {
                    existing.Quantity += qty;
                    RefreshList();
                    return;
                }
            }

            _items.Add(new PurchaseOrderItem
            {
                ProductId = p.Id,
                ProductName = p.Name,
                Quantity = qty,
                UnitCost = cost
            });

            RefreshList();
            txtQty.Text = "1";
            txtCost.Text = "0";          // plain 0 — no currency prefix in input
            lblMessage.Content = "";
        }

        // ══════════════════════════════════════
        // REMOVE ITEM
        // ══════════════════════════════════════
        private void btnRemoveItem_Click(
            object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            PurchaseOrderItem item =
                (PurchaseOrderItem)btn.Tag;
            _items.Remove(item);
            RefreshList();
        }

        // ══════════════════════════════════════
        // REFRESH LIST
        // ══════════════════════════════════════
        void RefreshList()
        {
            lvItems.Items.Clear();
            double total = 0;
            foreach (PurchaseOrderItem item in _items)
            {
                lvItems.Items.Add(item);
                total += item.TotalCost;
            }

            lblTotal.Content = "LBP " + total.ToString("N0");
        }

        // ══════════════════════════════════════
        // SAVE
        // ══════════════════════════════════════
        private void btnSave_Click(object sender,
            RoutedEventArgs e)
        {
            if (cmbSupplier.SelectedItem == null)
            {
                ShowError("⚠️ Select a supplier!");
                return;
            }
            if (_items.Count == 0)
            {
                ShowError("⚠️ Add at least one item!");
                return;
            }

            Supplier supplier =
                (Supplier)cmbSupplier.SelectedItem;

            using (SQLiteConnection con =
                new SQLiteConnection(
                    DatabaseHelper.ConnectionString))
            {
                con.Open();

                SQLiteCommand cmd =
                    new SQLiteCommand(@"
                    INSERT INTO PurchaseOrders
                    (SupplierId, OrderDate, Status, Notes)
                    VALUES
                    (@sid, @date, 'Pending', @notes)",
                    con);
                cmd.Parameters.AddWithValue(
                    "@sid", supplier.Id);
                cmd.Parameters.AddWithValue(
                    "@date",
                    DateTime.Now.ToString("dd/MM/yyyy"));
                cmd.Parameters.AddWithValue(
                    "@notes", txtNotes.Text.Trim());
                cmd.ExecuteNonQuery();

                int orderId = Convert.ToInt32(
                    new SQLiteCommand(
                        "SELECT last_insert_rowid()",
                        con).ExecuteScalar());

                foreach (PurchaseOrderItem item in _items)
                {
                    SQLiteCommand ic =
                        new SQLiteCommand(@"
                        INSERT INTO PurchaseOrderItems
                        (PurchaseOrderId, ProductId,
                         Quantity, UnitCost)
                        VALUES
                        (@oid, @pid, @qty, @cost)",
                        con);
                    ic.Parameters.AddWithValue(
                        "@oid", orderId);
                    ic.Parameters.AddWithValue(
                        "@pid", item.ProductId);
                    ic.Parameters.AddWithValue(
                        "@qty", item.Quantity);
                    ic.Parameters.AddWithValue(
                        "@cost", item.UnitCost);
                    ic.ExecuteNonQuery();
                }
            }

            MessageBox.Show(
                "✅ Purchase order saved!",
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            this.Close();
        }

        private void btnCancel_Click(object sender,
            RoutedEventArgs e) => this.Close();

        void ShowError(string msg)
        {
            lblMessage.Content = msg;
            lblMessage.Foreground =
                System.Windows.Media.Brushes.Tomato;
        }

        private void cmbSupplier_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        { }
    }
}
