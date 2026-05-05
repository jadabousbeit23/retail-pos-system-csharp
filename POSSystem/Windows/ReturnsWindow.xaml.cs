using POSSystem.Database;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;

namespace POSSystem.Windows
{
    public class ReturnItemViewModel : INotifyPropertyChanged
    {
        public int    SaleItemId  { get; set; }
        public int    ProductId   { get; set; }
        public string ProductName { get; set; }
        public int    Quantity    { get; set; }
        public double UnitPrice   { get; set; }
        public string UnitPriceDisplay => $"LBP {UnitPrice:N0}";
        public string TotalDisplay     => $"LBP {Quantity * UnitPrice:N0}";

        bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged("IsSelected");
                if (value && ReturnQty == 0) ReturnQty = Quantity;
                else if (!value) ReturnQty = 0;
            }
        }

        int _returnQty;
        public int ReturnQty
        {
            get => _returnQty;
            set { _returnQty = Math.Min(value, Quantity); OnPropertyChanged("ReturnQty"); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string p) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    public partial class ReturnsWindow : Window
    {
        int    _currentSaleId    = 0;
        double _currentSaleTotal = 0;

        public ReturnsWindow()
        {
            InitializeComponent();
        }

        private void btnSearchSale_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(txtInvoiceSearch.Text.Trim(), out int saleId))
            { ShowError("⚠️ Enter a valid invoice number!"); return; }

            lvSaleItems.Items.Clear();
            _currentSaleId    = 0;
            _currentSaleTotal = 0;

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var sc = new SQLiteCommand("SELECT * FROM Sales WHERE Id=@id", con);
                sc.Parameters.AddWithValue("@id", saleId);
                var sr = sc.ExecuteReader();
                if (!sr.Read())
                {
                    sr.Close();
                    ShowError($"❌ Invoice #{saleId} not found!");
                    lblSaleInfo.Content = "— Sale not found —";
                    return;
                }
                _currentSaleId    = saleId;
                _currentSaleTotal = Convert.ToDouble(sr["TotalAmount"]);
                string date       = sr["Date"].ToString();
                sr.Close();

                lblSaleInfo.Content =
                    $"✅ Invoice #{saleId}  —  {date}  —  LBP {_currentSaleTotal:N0}";

                var r = new SQLiteCommand(@"
                    SELECT si.Id AS SaleItemId, si.ProductId,
                           p.Name AS ProductName,
                           si.Quantity, si.Price
                    FROM SaleItems si
                    JOIN Products p ON si.ProductId = p.Id
                    WHERE si.SaleId = @sid", con);
                r.Parameters.AddWithValue("@sid", saleId);
                var reader = r.ExecuteReader();
                while (reader.Read())
                {
                    int    qty   = Convert.ToInt32(reader["Quantity"]);
                    double price = Convert.ToDouble(reader["Price"]);
                    lvSaleItems.Items.Add(new ReturnItemViewModel
                    {
                        SaleItemId  = Convert.ToInt32(reader["SaleItemId"]),
                        ProductId   = Convert.ToInt32(reader["ProductId"]),
                        ProductName = reader["ProductName"].ToString(),
                        Quantity    = qty,
                        UnitPrice   = qty > 0 ? price / qty : price,
                        ReturnQty   = 0,
                        IsSelected  = false
                    });
                }
                reader.Close();
            }
            UpdateSummary();
        }

        void UpdateSummary()
        {
            int count = 0; double total = 0;
            foreach (ReturnItemViewModel item in lvSaleItems.Items)
                if (item.IsSelected && item.ReturnQty > 0)
                { count++; total += item.ReturnQty * item.UnitPrice; }

            lblReturnItems.Content  = count.ToString();
            lblRefundAmount.Content = $"LBP {total:N0}";
        }

        private void chkItem_Changed(object sender, RoutedEventArgs e)     => UpdateSummary();
        private void returnQty_Changed(object sender, TextChangedEventArgs e) => UpdateSummary();

        private void btnProcessReturn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSaleId == 0) { ShowError("⚠️ Search for a sale first!"); return; }

            var toReturn = new List<ReturnItemViewModel>();
            double refundAmount = 0;

            foreach (ReturnItemViewModel item in lvSaleItems.Items)
                if (item.IsSelected && item.ReturnQty > 0)
                { toReturn.Add(item); refundAmount += item.ReturnQty * item.UnitPrice; }

            if (toReturn.Count == 0)
            { ShowError("⚠️ Select at least one item to return!"); return; }

            string reason     = ((ComboBoxItem)cmbReturnReason.SelectedItem)?.Content?.ToString() ?? "Other";
            bool   restock    = chkRestock.IsChecked == true;
            string returnType = rbRefund.IsChecked   == true ? "Refund"
                              : rbExchange.IsChecked  == true ? "Exchange"
                              : "StoreCredit";

            if (MessageBox.Show(
                $"Process return for Invoice #{_currentSaleId}?\n\n" +
                $"Items: {toReturn.Count}  —  Refund: LBP {refundAmount:N0}\n" +
                $"Type: {returnType}  |  Restock: {(restock ? "Yes" : "No")}\n" +
                $"Reason: {reason}",
                "Confirm Return", MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                string summary = "";
                foreach (var item in toReturn)
                    summary += $"{item.ProductName} x{item.ReturnQty}, ";

                var rc = new SQLiteCommand(@"
                    INSERT INTO Returns
                    (OriginalSaleId, ReturnDate, CashierName, Reason, RefundAmount, ReturnType, Items)
                    VALUES (@sid, @date, @cashier, @reason, @amount, @type, @items)", con);
                rc.Parameters.AddWithValue("@sid",     _currentSaleId);
                rc.Parameters.AddWithValue("@date",    DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                rc.Parameters.AddWithValue("@cashier", Session.Username);
                rc.Parameters.AddWithValue("@reason",  reason);
                rc.Parameters.AddWithValue("@amount",  refundAmount);
                rc.Parameters.AddWithValue("@type",    returnType);
                rc.Parameters.AddWithValue("@items",   summary);
                rc.ExecuteNonQuery();

                int returnId = Convert.ToInt32(
                    new SQLiteCommand("SELECT last_insert_rowid()", con).ExecuteScalar());

                foreach (var item in toReturn)
                {
                    var ri = new SQLiteCommand(@"
                        INSERT INTO ReturnItems
                        (ReturnId, ProductId, Quantity, UnitPrice, Restocked)
                        VALUES (@rid, @pid, @qty, @price, @restock)", con);
                    ri.Parameters.AddWithValue("@rid",     returnId);
                    ri.Parameters.AddWithValue("@pid",     item.ProductId);
                    ri.Parameters.AddWithValue("@qty",     item.ReturnQty);
                    ri.Parameters.AddWithValue("@price",   item.UnitPrice);
                    ri.Parameters.AddWithValue("@restock", restock ? 1 : 0);
                    ri.ExecuteNonQuery();

                    if (restock)
                        new SQLiteCommand(
                            $"UPDATE Products SET Stock=Stock+{item.ReturnQty} WHERE Id={item.ProductId}",
                            con).ExecuteNonQuery();
                }
            }

            // ── AUDIT LOG ──
            DatabaseHelper.Log("Return",
                $"Invoice #{_currentSaleId} — {returnType} — LBP {refundAmount:N0}", "Return");

            if (returnType == "Exchange")
            {
                MessageBox.Show($"✅ Return done! LBP {refundAmount:N0}\n\nOpening sales window...",
                    "Exchange", MessageBoxButton.OK, MessageBoxImage.Information);
                new FastSalesWindow().Show();
            }
            else
                MessageBox.Show($"✅ Return processed!\nType: {returnType}\nRefund: LBP {refundAmount:N0}",
                    "Return Complete", MessageBoxButton.OK, MessageBoxImage.Information);

            lvSaleItems.Items.Clear();
            txtInvoiceSearch.Clear();
            lblSaleInfo.Content     = "— Search for a sale to process return —";
            lblReturnItems.Content  = "0";
            lblRefundAmount.Content = "LBP 0";
            _currentSaleId          = 0;
            lblMessage.Content      = "";
        }

        private void btnViewHistory_Click(object sender, RoutedEventArgs e)
        {
            var win = new ReturnHistoryWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();

        void ShowError(string msg)
        {
            lblMessage.Content    = msg;
            lblMessage.Foreground = System.Windows.Media.Brushes.Tomato;
        }
    }
}
