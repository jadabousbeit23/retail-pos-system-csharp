using POSSystem.Database;
using System;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;

namespace POSSystem.Windows
{
    public class BatchViewModel
    {
        public int    Id           { get; set; }
        public string BatchNumber  { get; set; }
        public string ExpiryDate   { get; set; }
        public int    Quantity     { get; set; }
        public string ReceivedDate { get; set; }
        public string StatusText   { get; set; }
        public string StatusColor  { get; set; }
        public int    DaysUntilExpiry { get; set; }  // for sorting priority
    }

    public partial class BatchManagementWindow : Window
    {
        int    _productId;
        string _productName;

        public BatchManagementWindow(int productId, string productName)
        {
            InitializeComponent();
            _productId   = productId;
            _productName = productName;
            lblProductName.Content = $"Product:  {productName}";
            LoadBatches();
        }

        // ══════════════════════════════════════════════
        // FLEXIBLE DATE PARSING  (dd/MM/yyyy  or  dd-MM-yyyy)
        // ══════════════════════════════════════════════
        static bool TryParseFlexibleDate(string input, out DateTime result)
        {
            result = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(input)) return false;

            // Normalise separator: replace '-' with '/' so we only need one set of formats
            string normalised = input.Trim().Replace('-', '/');

            string[] formats = {
                "dd/MM/yyyy",
                "d/M/yyyy",
                "d/MM/yyyy",
                "dd/M/yyyy",
                "MM/dd/yyyy",   // fallback American
                "yyyy/MM/dd"    // fallback ISO
            };
            return DateTime.TryParseExact(normalised, formats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out result);
        }

        // ══════════════════════════════════════════════
        // LOAD  (sorted: expired first, then soonest expiry, then no-expiry)
        // ══════════════════════════════════════════════
        void LoadBatches()
        {
            lvBatches.Items.Clear();
            int totalStock    = 0;
            int expiringCount = 0;   // ≤ 30 days
            int expiredCount  = 0;

            var batches = new System.Collections.Generic.List<BatchViewModel>();

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var r = new SQLiteCommand(
                    "SELECT * FROM ProductBatches WHERE ProductId=@pid",
                    con);
                r.Parameters.AddWithValue("@pid", _productId);
                var reader = r.ExecuteReader();

                while (reader.Read())
                {
                    int    qty    = Convert.ToInt32(reader["Quantity"]);
                    string expStr = reader["ExpiryDate"].ToString();
                    totalStock   += qty;

                    string statusText, statusColor;
                    int daysLeft;
                    GetBatchStatus(expStr, out statusText, out statusColor, out daysLeft);

                    if (daysLeft < 0)               expiredCount++;
                    else if (daysLeft <= 30)         expiringCount++;

                    batches.Add(new BatchViewModel
                    {
                        Id             = Convert.ToInt32(reader["Id"]),
                        BatchNumber    = reader["BatchNumber"].ToString(),
                        ExpiryDate     = expStr,
                        Quantity       = qty,
                        ReceivedDate   = reader["ReceivedDate"].ToString(),
                        StatusText     = statusText,
                        StatusColor    = statusColor,
                        DaysUntilExpiry = daysLeft
                    });
                }
                reader.Close();
            }

            // Sort: expired (most negative first) → soonest expiry → no-expiry last
            batches.Sort((a, b) =>
            {
                bool aNoExp = a.DaysUntilExpiry == int.MaxValue;
                bool bNoExp = b.DaysUntilExpiry == int.MaxValue;
                if (aNoExp && bNoExp) return 0;
                if (aNoExp)  return 1;
                if (bNoExp)  return -1;
                return a.DaysUntilExpiry.CompareTo(b.DaysUntilExpiry);
            });

            foreach (var b in batches)
                lvBatches.Items.Add(b);

            lblTotalStock.Content = $"{totalStock} units";

            // ── Alert banner ──────────────────────────────
            if (expiredCount > 0 || expiringCount > 0)
            {
                string msg = "";
                if (expiredCount  > 0) msg += $"🔴 {expiredCount} EXPIRED batch(es) — remove from stock!  ";
                if (expiringCount > 0) msg += $"⚠️ {expiringCount} batch(es) expiring soon — sell first!";
                ShowWarning(msg);
            }
            else
            {
                lblMessage.Content = "";
            }
        }

        // daysLeft = int.MaxValue means "No Expiry"
        void GetBatchStatus(string expiryStr, out string text, out string color, out int daysLeft)
        {
            daysLeft = int.MaxValue;

            if (string.IsNullOrEmpty(expiryStr))
            { text = "No Expiry"; color = "#5D6D7B"; return; }

            if (!TryParseFlexibleDate(expiryStr, out DateTime expiry))
            { text = "Invalid Date"; color = "#922B21"; daysLeft = int.MinValue; return; }

            daysLeft = (expiry - DateTime.Today).Days;

            if      (daysLeft < 0)    { text = "🔴 Expired";             color = "#7B241C"; }
            else if (daysLeft == 0)   { text = "🔴 Expires TODAY";       color = "#922B21"; }
            else if (daysLeft <= 3)   { text = $"🚨 {daysLeft}d — URGENT"; color = "#C0392B"; }
            else if (daysLeft <= 7)   { text = $"⚠️ {daysLeft}d left";   color = "#D35400"; }
            else if (daysLeft <= 30)  { text = $"🟡 {daysLeft}d left";   color = "#B7950B"; }
            else                      { text = $"✅ {daysLeft}d left";   color = "#1E8449"; }
        }

        // ══════════════════════════════════════════════
        // ADD BATCH
        // ══════════════════════════════════════════════
        private void btnAddBatch_Click(object sender, RoutedEventArgs e)
        {
            string batch  = txtBatchNumber.Text.Trim();
            string expiry = txtExpiryDate.Text.Trim();

            if (batch == "") { ShowError("⚠️ Enter a batch number!"); return; }
            if (!int.TryParse(txtBatchQty.Text, out int qty) || qty <= 0)
            { ShowError("⚠️ Enter a valid quantity!"); return; }

            // Validate & normalise expiry date
            string storedExpiry = "";
            if (!string.IsNullOrEmpty(expiry))
            {
                if (!TryParseFlexibleDate(expiry, out DateTime parsedExpiry))
                { ShowError("⚠️ Invalid date! Use dd/MM/yyyy or dd-MM-yyyy"); return; }

                // Always store in a consistent ISO-compatible format
                storedExpiry = parsedExpiry.ToString("dd/MM/yyyy");
            }

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();

                var cmd = new SQLiteCommand(@"
                    INSERT INTO ProductBatches
                    (ProductId, BatchNumber, ExpiryDate, Quantity, ReceivedDate)
                    VALUES (@pid, @batch, @exp, @qty, @recv)", con);
                cmd.Parameters.AddWithValue("@pid",   _productId);
                cmd.Parameters.AddWithValue("@batch", batch);
                cmd.Parameters.AddWithValue("@exp",   storedExpiry);
                cmd.Parameters.AddWithValue("@qty",   qty);
                cmd.Parameters.AddWithValue("@recv",  DateTime.Today.ToString("dd/MM/yyyy"));
                cmd.ExecuteNonQuery();

                new SQLiteCommand(
                    $"UPDATE Products SET Stock=Stock+{qty} WHERE Id={_productId}", con)
                    .ExecuteNonQuery();

                LogAdjustment(con, _productId, qty, $"Batch received: {batch}", Session.Username);
            }

            ShowSuccess($"✅ Batch '{batch}' added — {qty} units!");
            txtBatchNumber.Clear();
            txtExpiryDate.Clear();
            txtBatchQty.Clear();
            LoadBatches();
        }

        // ══════════════════════════════════════════════
        // DELETE BATCH
        // ══════════════════════════════════════════════
        private void btnDeleteBatch_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;
            int batchId = Convert.ToInt32(btn.Tag);

            int qty = 0;
            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                object val = new SQLiteCommand(
                    $"SELECT Quantity FROM ProductBatches WHERE Id={batchId}", con)
                    .ExecuteScalar();
                if (val != null) qty = Convert.ToInt32(val);

                if (MessageBox.Show(
                    $"Delete this batch? This will remove {qty} units from stock.",
                    "Delete Batch", MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

                new SQLiteCommand($"DELETE FROM ProductBatches WHERE Id={batchId}", con)
                    .ExecuteNonQuery();
                new SQLiteCommand(
                    $"UPDATE Products SET Stock=MAX(0,Stock-{qty}) WHERE Id={_productId}", con)
                    .ExecuteNonQuery();

                LogAdjustment(con, _productId, -qty, "Batch deleted", Session.Username);
            }
            LoadBatches();
        }

        void LogAdjustment(SQLiteConnection con, int productId,
            int change, string reason, string user)
        {
            object val = new SQLiteCommand(
                $"SELECT Stock FROM Products WHERE Id={productId}", con).ExecuteScalar();
            int newStock = val != null ? Convert.ToInt32(val) : 0;

            var cmd = new SQLiteCommand(@"
                INSERT INTO StockAdjustments
                (ProductId, ChangeAmount, Reason, AdjustedBy, AdjustedAt, NewStock)
                VALUES (@pid, @change, @reason, @user, @at, @stock)", con);
            cmd.Parameters.AddWithValue("@pid",    productId);
            cmd.Parameters.AddWithValue("@change", change);
            cmd.Parameters.AddWithValue("@reason", reason);
            cmd.Parameters.AddWithValue("@user",   user);
            cmd.Parameters.AddWithValue("@at",     DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
            cmd.Parameters.AddWithValue("@stock",  newStock);
            cmd.ExecuteNonQuery();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
            => this.Close();

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

        void ShowWarning(string msg)
        {
            lblMessage.Content    = msg;
            lblMessage.Foreground = System.Windows.Media.Brushes.Orange;
        }
    }
}
