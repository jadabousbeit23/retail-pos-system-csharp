using POSSystem.Database;
using System;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace POSSystem.Windows
{
    public class LowStockViewModel
    {
        public int    Id          { get; set; }
        public string Name        { get; set; }
        public string Category    { get; set; }
        public int    Stock       { get; set; }
        public int    MinStock    { get; set; }
        public string StatusText  { get; set; }
        public string StatusColor { get; set; }
        public string PriceDisplay{ get; set; }
    }

    public class ExpiryViewModel
    {
        public string ProductName { get; set; }
        public string BatchNumber { get; set; }
        public string ExpiryDate  { get; set; }
        public int    Quantity    { get; set; }
        public string StatusText  { get; set; }
        public string StatusColor { get; set; }
        public string DaysLeft    { get; set; }
    }

    public partial class InventoryAlertsWindow : Window
    {
        string _currentTab = "Low";

        public InventoryAlertsWindow()
        {
            InitializeComponent();
            LoadAll();
            ShowTab("Low");
        }

        // ══════════════════════════════════════════════
        // LOAD ALL DATA
        // ══════════════════════════════════════════════
        void LoadAll()
        {
            LoadLowStock();
            LoadExpiry();
        }

        void LoadLowStock()
        {
            lvLowStock.Items.Clear();
            int outCount = 0, lowCount = 0;

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var r = new SQLiteCommand(
                    "SELECT * FROM Products WHERE Stock <= MinStock ORDER BY Stock ASC", con)
                    .ExecuteReader();

                while (r.Read())
                {
                    int stock    = Convert.ToInt32(r["Stock"]);
                    int minStock = Convert.ToInt32(r["MinStock"]);
                    double price = Convert.ToDouble(r["Price"]);

                    string st, sc;
                    if (stock == 0)
                    { st = "🔴 OUT OF STOCK"; sc = "#922B21"; outCount++; }
                    else
                    { st = "⚠️ LOW STOCK"; sc = "#D35400"; lowCount++; }

                    lvLowStock.Items.Add(new LowStockViewModel
                    {
                        Id           = Convert.ToInt32(r["Id"]),
                        Name         = r["Name"].ToString(),
                        Category     = r["Category"].ToString(),
                        Stock        = stock,
                        MinStock     = minStock,
                        StatusText   = st,
                        StatusColor  = sc,
                        PriceDisplay = $"LBP {price:N0}"
                    });
                }
                r.Close();
            }

            lblOutCount.Content = $"🔴 {outCount} Out of Stock";
            lblLowCount.Content = $"⚠️ {lowCount} Low Stock";
        }

        void LoadExpiry()
        {
            lvExpiry.Items.Clear();
            int expiredCount = 0;

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var r = new SQLiteCommand(@"
                    SELECT pb.*, p.Name AS ProductName
                    FROM ProductBatches pb
                    JOIN Products p ON pb.ProductId = p.Id
                    WHERE pb.ExpiryDate IS NOT NULL
                      AND pb.ExpiryDate != ''
                      AND pb.Quantity > 0
                    ORDER BY pb.ExpiryDate ASC", con)
                    .ExecuteReader();

                while (r.Read())
                {
                    string expStr = r["ExpiryDate"].ToString();
                    if (!DateTime.TryParse(expStr, out DateTime expiry)) continue;

                    int days = (expiry - DateTime.Today).Days;
                    if (days > 30) continue; // Only show ≤30 days or expired

                    string st, sc, dl;
                    if (days < 0)
                    { st = "🔴 Expired"; sc = "#922B21"; dl = $"{Math.Abs(days)}d ago"; expiredCount++; }
                    else if (days == 0)
                    { st = "🔴 Today!"; sc = "#922B21"; dl = "Today"; expiredCount++; }
                    else if (days <= 7)
                    { st = "⚠️ This week"; sc = "#D35400"; dl = $"{days} days"; }
                    else
                    { st = "🟡 Soon"; sc = "#B7950B"; dl = $"{days} days"; }

                    lvExpiry.Items.Add(new ExpiryViewModel
                    {
                        ProductName = r["ProductName"].ToString(),
                        BatchNumber = r["BatchNumber"].ToString(),
                        ExpiryDate  = expStr,
                        Quantity    = Convert.ToInt32(r["Quantity"]),
                        StatusText  = st,
                        StatusColor = sc,
                        DaysLeft    = dl
                    });
                }
                r.Close();
            }
            lblExpiredCount.Content = $"🔴 {expiredCount} Expired";
        }

        // ══════════════════════════════════════════════
        // TAB SWITCHING
        // ══════════════════════════════════════════════
        void ShowTab(string tab)
        {
            _currentTab = tab;

            lvLowStock.Visibility = tab == "Low"     ? Visibility.Visible : Visibility.Collapsed;
            lvExpiry.Visibility   = tab != "Low"     ? Visibility.Visible : Visibility.Collapsed;

            // If showing expired only, filter the expiry list
            if (tab == "Expired")
            {
                lvExpiry.Items.Clear();
                foreach (ExpiryViewModel item in GetAllExpiryItems())
                    if (item.StatusText.Contains("Expired") || item.StatusText.Contains("Today"))
                        lvExpiry.Items.Add(item);
            }
            else if (tab == "Expiring")
            {
                lvExpiry.Items.Clear();
                foreach (ExpiryViewModel item in GetAllExpiryItems())
                    if (!item.StatusText.Contains("Expired"))
                        lvExpiry.Items.Add(item);
            }

            // Highlight active tab
            btnTabLow.Background      = new SolidColorBrush(tab == "Low"      ? ColorFromHex("#D35400") : ColorFromHex("#7F8C8D"));
            btnTabExpiring.Background = new SolidColorBrush(tab == "Expiring" ? ColorFromHex("#B7950B") : ColorFromHex("#7F8C8D"));
            btnTabExpired.Background  = new SolidColorBrush(tab == "Expired"  ? ColorFromHex("#922B21") : ColorFromHex("#7F8C8D"));
        }

        System.Collections.Generic.List<ExpiryViewModel> GetAllExpiryItems()
        {
            var list = new System.Collections.Generic.List<ExpiryViewModel>();
            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var r = new SQLiteCommand(@"
                    SELECT pb.*, p.Name AS ProductName
                    FROM ProductBatches pb
                    JOIN Products p ON pb.ProductId = p.Id
                    WHERE pb.ExpiryDate IS NOT NULL
                      AND pb.ExpiryDate != ''
                      AND pb.Quantity > 0
                    ORDER BY pb.ExpiryDate ASC", con).ExecuteReader();

                while (r.Read())
                {
                    string expStr = r["ExpiryDate"].ToString();
                    if (!DateTime.TryParse(expStr, out DateTime expiry)) continue;
                    int days = (expiry - DateTime.Today).Days;
                    if (days > 30) continue;

                    string st, sc, dl;
                    if      (days < 0)  { st = "🔴 Expired";    sc = "#922B21"; dl = $"{Math.Abs(days)}d ago"; }
                    else if (days == 0) { st = "🔴 Today!";     sc = "#922B21"; dl = "Today"; }
                    else if (days <= 7) { st = "⚠️ This week";  sc = "#D35400"; dl = $"{days} days"; }
                    else                { st = "🟡 Soon";        sc = "#B7950B"; dl = $"{days} days"; }

                    list.Add(new ExpiryViewModel
                    {
                        ProductName = r["ProductName"].ToString(),
                        BatchNumber = r["BatchNumber"].ToString(),
                        ExpiryDate  = expStr,
                        Quantity    = Convert.ToInt32(r["Quantity"]),
                        StatusText  = st,
                        StatusColor = sc,
                        DaysLeft    = dl
                    });
                }
                r.Close();
            }
            return list;
        }

        static Color ColorFromHex(string hex)
            => (Color)ColorConverter.ConvertFromString(hex);

        // ══════════════════════════════════════════════
        // BUTTON HANDLERS
        // ══════════════════════════════════════════════
        private void btnTab_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;
            string tab = btn.Name == "btnTabLow"      ? "Low"
                       : btn.Name == "btnTabExpiring" ? "Expiring"
                       : "Expired";
            ShowTab(tab);
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadAll();
            ShowTab(_currentTab);
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
            => this.Close();
    }
}
