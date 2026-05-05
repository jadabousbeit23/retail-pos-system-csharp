using POSSystem.Database;
using System;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;

namespace POSSystem.Windows
{
    public class HeldCart
    {
        public int Id { get; set; }
        public string HoldName { get; set; }
        public string CreatedAt { get; set; }
        public string CashierName { get; set; }
        public string CartData { get; set; }
    }

    public partial class HeldCartsWindow : Window
    {
        private FastSalesWindow _fastParent;
        private GridSalesWindow _gridParent;

        // ─────────────────────────────────────────────
        // Constructor (FastSalesWindow)
        // ─────────────────────────────────────────────
        public HeldCartsWindow(FastSalesWindow parent)
        {
            InitializeComponent();
            _fastParent = parent;
            LoadHolds();
        }

        // ─────────────────────────────────────────────
        // Constructor (GridSalesWindow)
        // ─────────────────────────────────────────────
        public HeldCartsWindow(GridSalesWindow parent)
        {
            InitializeComponent(); // 🔥 FIXED (was missing)
            _gridParent = parent;
            LoadHolds();
        }

        // ─────────────────────────────────────────────
        // Load Held Carts
        // ─────────────────────────────────────────────
        void LoadHolds()
        {
            lvHolds.Items.Clear();

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();

                var cmd = new SQLiteCommand(
                    "SELECT * FROM HeldCarts ORDER BY Id DESC", con);

                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    lvHolds.Items.Add(new HeldCart
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        HoldName = reader["HoldName"].ToString(),
                        CreatedAt = reader["CreatedAt"].ToString(),
                        CashierName = reader["CashierName"].ToString(),
                        CartData = reader["CartData"].ToString()
                    });
                }

                reader.Close();
            }
        }

        // ─────────────────────────────────────────────
        // Resume Cart
        // ─────────────────────────────────────────────
        private void btnResume_Click(object sender, RoutedEventArgs e)
        {
            if (lvHolds.SelectedItem == null)
            {
                MessageBox.Show("⚠️ Select a hold to resume!");
                return;
            }

            var hold = (HeldCart)lvHolds.SelectedItem;

            if (_fastParent != null)
                _fastParent.ResumeCart(hold.Id, hold.CartData);
            else if (_gridParent != null)
                _gridParent.ResumeCart(hold.Id, hold.CartData);

            Close();
        }

        // ─────────────────────────────────────────────
        // Delete Hold
        // ─────────────────────────────────────────────
        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (lvHolds.SelectedItem == null)
            {
                MessageBox.Show("⚠️ Select a hold to delete!");
                return;
            }

            var hold = (HeldCart)lvHolds.SelectedItem;

            if (MessageBox.Show($"Delete '{hold.HoldName}'?",
                "Delete Hold",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();

                var cmd = new SQLiteCommand(
                    "DELETE FROM HeldCarts WHERE Id=@id", con);

                cmd.Parameters.AddWithValue("@id", hold.Id);
                cmd.ExecuteNonQuery();
            }

            LoadHolds();

            // Update count in parent
            if (_fastParent != null)
                _fastParent.UpdateHeldCount();
            else if (_gridParent != null)
                _gridParent.UpdateHeldCount();
        }

        // ─────────────────────────────────────────────
        // Close Window
        // ─────────────────────────────────────────────
        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
