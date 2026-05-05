using POSSystem.Database;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace POSSystem.Windows
{
    // ── View models ────────────────────────────────────────────
    public class AdjustmentViewModel
    {
        public string AdjustedAt { get; set; }
        public int ChangeAmount { get; set; }
        public int NewStock { get; set; }
        public string Reason { get; set; }
        public string AdjustedBy { get; set; }
        public string ChangeDisplay => ChangeAmount >= 0
            ? $"+{ChangeAmount}" : $"{ChangeAmount}";
        public string ChangeColor => ChangeAmount >= 0 ? "#1E8449" : "#922B21";
    }

    public partial class StockAdjustmentWindow : Window
    {
        int _productId;
        string _productName;
        int _currentStock;

        public StockAdjustmentWindow(int productId, string productName, int currentStock)
        {
            InitializeComponent();
            _productId = productId;
            _productName = productName;
            _currentStock = currentStock;

            lblProductName.Content = $"Product:  {productName}";
            lblCurrentStock.Content = currentStock.ToString();
            LoadHistory();
        }

        // ══════════════════════════════════════════════
        // LOAD HISTORY
        // ══════════════════════════════════════════════
        void LoadHistory()
        {
            lvHistory.Items.Clear();
            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var r = new SQLiteCommand(@"
                    SELECT * FROM StockAdjustments
                    WHERE ProductId = @pid
                    ORDER BY Id DESC
                    LIMIT 50", con);
                r.Parameters.AddWithValue("@pid", _productId);
                var reader = r.ExecuteReader();
                while (reader.Read())
                    lvHistory.Items.Add(new AdjustmentViewModel
                    {
                        AdjustedAt = reader["AdjustedAt"].ToString(),
                        ChangeAmount = Convert.ToInt32(reader["ChangeAmount"]),
                        NewStock = Convert.ToInt32(reader["NewStock"]),
                        Reason = reader["Reason"].ToString(),
                        AdjustedBy = reader["AdjustedBy"].ToString()
                    });
                reader.Close();
            }
        }

        // ══════════════════════════════════════════════
        // APPLY ADJUSTMENT - FIXED WITH ISO DATE
        // ══════════════════════════════════════════════
        private void btnApply_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(txtAdjQty.Text, out int qty) || qty <= 0)
            { ShowError("⚠️ Enter a valid quantity!"); return; }

            bool isAdd = rbAdd.IsChecked == true;
            int change = isAdd ? qty : -qty;

            string reason = ((ComboBoxItem)cmbReason.SelectedItem)?.Content?.ToString()
                            ?? "Other";
            if (!string.IsNullOrEmpty(txtNotes.Text.Trim()))
                reason += $" — {txtNotes.Text.Trim()}";

            // Validate removal doesn't go below 0
            if (!isAdd && _currentStock + change < 0)
            {
                ShowError($"⚠️ Cannot remove {qty} — only {_currentStock} in stock!");
                return;
            }

            if (MessageBox.Show(
                $"{(isAdd ? "Add" : "Remove")} {qty} units?\n" +
                $"Reason: {reason}\n\n" +
                $"Stock: {_currentStock} → {_currentStock + change}",
                "Confirm Adjustment",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();

                // Update stock
                new SQLiteCommand(
                    $"UPDATE Products SET Stock=Stock+({change}) WHERE Id={_productId}", con)
                    .ExecuteNonQuery();

                // Get new stock
                int newStock = Convert.ToInt32(new SQLiteCommand(
                    $"SELECT Stock FROM Products WHERE Id={_productId}", con)
                    .ExecuteScalar());

                // Log adjustment with ISO format
                var cmd = new SQLiteCommand(@"
                    INSERT INTO StockAdjustments
                    (ProductId, ChangeAmount, Reason, AdjustedBy, AdjustedAt, NewStock)
                    VALUES (@pid, @change, @reason, @user, @at, @stock)", con);
                cmd.Parameters.AddWithValue("@pid", _productId);
                cmd.Parameters.AddWithValue("@change", change);
                cmd.Parameters.AddWithValue("@reason", reason);
                cmd.Parameters.AddWithValue("@user", Session.Username);
                cmd.Parameters.AddWithValue("@at", DatabaseHelper.ToIsoDateTime(DateTime.Now));
                cmd.Parameters.AddWithValue("@stock", newStock);
                cmd.ExecuteNonQuery();

                _currentStock = newStock;
            }

            lblCurrentStock.Content = _currentStock.ToString();
            txtAdjQty.Clear();
            txtNotes.Clear();
            ShowSuccess($"✅ Stock adjusted by {(change >= 0 ? "+" : "")}{change} → {_currentStock} units");
            LoadHistory();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
            => this.Close();

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