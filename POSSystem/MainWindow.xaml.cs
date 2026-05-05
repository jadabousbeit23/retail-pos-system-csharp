using POSSystem.Database;
using POSSystem.Windows;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace POSSystem
{
    public partial class MainWindow : Window
    {
        System.Windows.Threading.DispatcherTimer _clockTimer;
        System.Windows.Threading.DispatcherTimer _refreshTimer;
        List<DayData> _chartData = new List<DayData>();

        // CHANGED: ISO 8601 format to match database
        const string DATE_FMT = "yyyy-MM-dd";
        const string MONTH_FMT = "yyyy-MM";

        public MainWindow()
        {
            InitializeComponent();

            lblWelcome.Content = $"Welcome, {Session.Username}";

            if (!Session.IsAdmin)
            {
                btnProducts.Visibility = Visibility.Collapsed;
                btnUsers.Visibility = Visibility.Collapsed;
                btnVoidedSales.Visibility = Visibility.Collapsed;
                btnPurchaseOrders.Visibility = Visibility.Collapsed;
                btnBackup.Visibility = Visibility.Collapsed;
            }

            _clockTimer = new System.Windows.Threading.DispatcherTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (s, e) =>
                lblDateTime.Content = DateTime.Now.ToString("ddd, dd MMM yyyy   HH:mm:ss");
            _clockTimer.Start();
            lblDateTime.Content = DateTime.Now.ToString("ddd, dd MMM yyyy   HH:mm:ss");

            _refreshTimer = new System.Windows.Threading.DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(10);
            _refreshTimer.Tick += (s, e) => LoadDashboard();
            _refreshTimer.Start();

            this.PreviewKeyDown += MainWindow_PreviewKeyDown;

            this.ContentRendered += (s, e) =>
            {
                UpdateDbStatus();
                LoadDashboard();
                this.Focus();
            };
            _ = CheckForUpdateAsync();
        }

        private async Task CheckForUpdateAsync()
        {
            try
            {
                // Force run on thread pool to avoid UI blocking
                string latest = await Task.Run(() => UpdateChecker.GetLatestVersionAsync());

                System.Diagnostics.Debug.WriteLine($"[MAIN] latest result: '{latest ?? "null"}'");

                if (latest != null)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        lblUpdateBanner.Text = $"⬆  Update available: v{latest}  —  Visit GitHub to download.";
                        updateBanner.Visibility = Visibility.Visible;
                        System.Diagnostics.Debug.WriteLine("[MAIN] Banner VISIBLE");
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MAIN ERROR] {ex.Message}");
            }
        }
        void UpdateDbStatus()
        {
            bool isNetwork = DatabaseHelper.IsNetworkDatabase();
            bool reachable = System.IO.File.Exists(DatabaseHelper.GetCurrentDbPath());

            if (isNetwork && reachable)
            {
                lblDbStatus.Content = "🟢 Network DB";
                lblDbStatus.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 16));
            }
            else if (isNetwork && !reachable)
            {
                lblDbStatus.Content = "🔴 Network Unreachable";
                lblDbStatus.Foreground = new SolidColorBrush(Color.FromRgb(196, 43, 28));
                DatabaseHelper.ResetConnectionString();
            }
            else
            {
                lblDbStatus.Content = "🟡 Local DB";
                lblDbStatus.Foreground = new SolidColorBrush(Color.FromRgb(157, 93, 0));
            }
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e == null) return;
            switch (e.Key)
            {
                case Key.F5:
                    LoadDashboard();
                    UpdateDbStatus();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    btnLogout_Click(null, null);
                    e.Handled = true;
                    break;
            }
        }

        public void LoadDashboard()
        {
            try
            {
                using (SQLiteConnection con = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    con.Open();

                    int lowThreshold = SettingsHelper.GetInt("LowStockThreshold");
                    if (lowThreshold <= 0) lowThreshold = 5;

                    // CHANGED: Use ISO 8601 date format (yyyy-MM-dd)
                    string today = DateTime.Now.ToString(DATE_FMT);
                    // CHANGED: Use yyyy-MM format for month queries
                    string monthPrefix = DateTime.Now.ToString(MONTH_FMT);

                    // CHANGED: Use DatabaseHelper.GetTodayWhereClause for consistency
                    // Today's sales - using LIKE with ISO format date
                    var sc = new SQLiteCommand(
                        "SELECT COUNT(*), IFNULL(SUM(TotalAmount),0) " +
                        "FROM Sales WHERE Date LIKE @d", con);
                    sc.Parameters.AddWithValue("@d", $"{today}%");
                    using (var sr = sc.ExecuteReader())
                    {
                        if (sr.Read())
                        {
                            lblTodaySales.Content = Convert.ToInt32(sr[0]).ToString("N0");
                            lblTodayRevenue.Content = "LBP " + Convert.ToDouble(sr[1]).ToString("N0");
                        }
                    }

                    // Month's sales - using LIKE with yyyy-MM format
                    var mc = new SQLiteCommand(
                        "SELECT COUNT(*), IFNULL(SUM(TotalAmount),0) " +
                        "FROM Sales WHERE Date LIKE @m", con);
                    mc.Parameters.AddWithValue("@m", $"{monthPrefix}%");
                    using (var mr = mc.ExecuteReader())
                    {
                        if (mr.Read())
                        {
                            lblMonthRevenue.Content = "LBP " + Convert.ToDouble(mr[1]).ToString("N0");
                            lblMonthSales.Content = $"{Convert.ToInt32(mr[0])} sales this month";
                        }
                    }

                    int prodCount = Convert.ToInt32(
                        new SQLiteCommand("SELECT COUNT(*) FROM Products", con).ExecuteScalar());
                    lblTotalProducts.Content = prodCount.ToString("N0");

                    var lsc = new SQLiteCommand(
                        "SELECT Name, Stock FROM Products " +
                        "WHERE Stock <= @t ORDER BY Stock ASC", con);
                    lsc.Parameters.AddWithValue("@t", lowThreshold);
                    var lowItems = new List<LowStockItem>();
                    using (var lr = lsc.ExecuteReader())
                        while (lr.Read())
                            lowItems.Add(new LowStockItem
                            {
                                Name = lr["Name"].ToString(),
                                Stock = Convert.ToInt32(lr["Stock"]),
                                StockText = $"{lr["Stock"]} left"
                            });

                    if (lowItems.Count > 0)
                    {
                        borderLowStock.Visibility = Visibility.Visible;
                        icLowStock.ItemsSource = lowItems;
                        icLowStockPanel.ItemsSource = lowItems;
                        lblLowStockBadge.Content = $"⚠️ {lowItems.Count} low stock";
                        lblLowStockCount.Content = $"⚠️ {lowItems.Count} low stock";
                        lblLowStockCount.Foreground = new SolidColorBrush(Color.FromRgb(196, 43, 28));
                    }
                    else
                    {
                        borderLowStock.Visibility = Visibility.Collapsed;
                        lblLowStockBadge.Content = "";
                        lblLowStockCount.Content = "✅ All stocked";
                        lblLowStockCount.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 16));
                        icLowStockPanel.ItemsSource = null;
                        icLowStock.ItemsSource = null;
                    }

                    // Top products today - using ISO date format
                    var tpc = new SQLiteCommand(@"
                        SELECT Products.Name, SUM(SaleItems.Quantity) AS TotalQty
                        FROM SaleItems
                        JOIN Sales ON SaleItems.SaleId = Sales.Id
                        JOIN Products ON SaleItems.ProductId = Products.Id
                        WHERE Sales.Date LIKE @d
                        GROUP BY Products.Name
                        ORDER BY TotalQty DESC
                        LIMIT 5", con);
                    tpc.Parameters.AddWithValue("@d", $"{today}%");
                    var topItems = new List<TopProductItem>();
                    int rank = 1;
                    using (var tr2 = tpc.ExecuteReader())
                        while (tr2.Read())
                            topItems.Add(new TopProductItem
                            {
                                Rank = rank++.ToString(),
                                Name = tr2["Name"].ToString(),
                                QtyText = $"x{tr2["TotalQty"]}"
                            });

                    if (topItems.Count == 0)
                        topItems.Add(new TopProductItem
                        { Rank = "—", Name = "No sales today yet", QtyText = "" });
                    icTopProducts.ItemsSource = topItems;

                    Load7DayData(con);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Dashboard error: " + ex.Message);
            }
        }

        public void ClearDashboard()
        {
            lblTodaySales.Content = "0";
            lblTodayRevenue.Content = "LBP 0";
            lblMonthRevenue.Content = "LBP 0";
            lblMonthSales.Content = "0 sales this month";
            lblTotalProducts.Content = "0";
            lblLowStockCount.Content = "";
            lblLowStockBadge.Content = "";
            borderLowStock.Visibility = Visibility.Collapsed;
            icLowStock.ItemsSource = null;
            icLowStockPanel.ItemsSource = null;
            icTopProducts.ItemsSource = null;
            _chartData.Clear();
            canvasChart.Children.Clear();
            icDayLabels.ItemsSource = null;
        }

        void Load7DayData(SQLiteConnection con)
        {
            _chartData.Clear();
            var labels = new List<string>();
            var revMap = new Dictionary<string, double>();
            var cntMap = new Dictionary<string, int>();

            // CHANGED: Use ISO 8601 date format
            for (int i = 6; i >= 0; i--)
            {
                string key = DateTime.Now.AddDays(-i).ToString(DATE_FMT);
                revMap[key] = 0;
                cntMap[key] = 0;
            }

            var cmd = new SQLiteCommand("SELECT Date, TotalAmount FROM Sales", con);
            using (var dr = cmd.ExecuteReader())
                while (dr.Read())
                {
                    string raw = dr["Date"].ToString();
                    // CHANGED: Extract yyyy-MM-dd from ISO format (first 10 chars)
                    string datePart = raw.Length >= 10 ? raw.Substring(0, 10) : raw;
                    if (revMap.ContainsKey(datePart))
                    {
                        revMap[datePart] += Convert.ToDouble(dr["TotalAmount"]);
                        cntMap[datePart]++;
                    }
                }

            for (int i = 6; i >= 0; i--)
            {
                DateTime day = DateTime.Now.AddDays(-i);
                string key = day.ToString(DATE_FMT);
                _chartData.Add(new DayData
                {
                    // CHANGED: Display format remains dd/MM for readability
                    Label = day.ToString("ddd\ndd/MM"),
                    Revenue = revMap.ContainsKey(key) ? revMap[key] : 0,
                    Count = cntMap.ContainsKey(key) ? cntMap[key] : 0
                });
                labels.Add(day.ToString("ddd"));
            }

            icDayLabels.ItemsSource = labels;
            DrawBarChart();
        }

        void DrawBarChart()
        {
            canvasChart.Children.Clear();
            if (_chartData.Count == 0) return;

            double w = canvasChart.ActualWidth;
            double h = canvasChart.ActualHeight;
            if (w < 10 || h < 10) return;

            double maxRev = 1;
            foreach (var d in _chartData)
                if (d.Revenue > maxRev) maxRev = d.Revenue;

            for (int i = 0; i <= 4; i++)
            {
                double y = h - (h * i / 4.0);
                canvasChart.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = 58,
                    X2 = w,
                    Y1 = y,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                    StrokeThickness = 1
                });
                var tb = new TextBlock
                {
                    Text = FormatLbpShort(maxRev * i / 4.0),
                    FontSize = 8,
                    Foreground = new SolidColorBrush(Color.FromRgb(138, 138, 138))
                };
                System.Windows.Controls.Canvas.SetLeft(tb, 0);
                System.Windows.Controls.Canvas.SetTop(tb, y - 9);
                canvasChart.Children.Add(tb);
            }

            double barAreaW = w - 62;
            double barW = barAreaW / _chartData.Count;
            double gap = barW * 0.28;
            double actualW = barW - gap;

            for (int i = 0; i < _chartData.Count; i++)
            {
                var d = _chartData[i];
                double barH = d.Revenue / maxRev * (h - 32);
                if (barH < 2 && d.Revenue > 0) barH = 2;

                double x = 60 + i * barW + gap / 2.0;
                double y = h - barH;

                Color barColor = i == 6
                    ? Color.FromRgb(0, 120, 212)
                    : Color.FromRgb(189, 215, 238);

                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = actualW,
                    Height = Math.Max(barH, 0),
                    RadiusX = 4,
                    RadiusY = 4,
                    Fill = new SolidColorBrush(barColor)
                };
                System.Windows.Controls.Canvas.SetLeft(rect, x);
                System.Windows.Controls.Canvas.SetTop(rect, y);
                canvasChart.Children.Add(rect);

                if (d.Revenue > 0)
                {
                    var vl = new TextBlock
                    {
                        Text = FormatLbpShort(d.Revenue),
                        FontSize = 8,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(95, 95, 95))
                    };
                    System.Windows.Controls.Canvas.SetLeft(vl, x);
                    System.Windows.Controls.Canvas.SetTop(vl, Math.Max(0, y - 14));
                    canvasChart.Children.Add(vl);
                }
            }

            canvasChart.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = 58,
                X2 = w,
                Y1 = h,
                Y2 = h,
                Stroke = new SolidColorBrush(Color.FromRgb(208, 208, 208)),
                StrokeThickness = 1
            });
        }

        static string FormatLbpShort(double val)
        {
            if (val >= 1_000_000) return (val / 1_000_000).ToString("0.#") + "M";
            if (val >= 1_000) return (val / 1_000).ToString("0.#") + "K";
            return val.ToString("N0");
        }

        private void canvasChart_SizeChanged(object sender, SizeChangedEventArgs e)
            => DrawBarChart();

        private void btnFastSale_Click(object sender, RoutedEventArgs e)
        {
            var fastWin = new FastSalesWindow();
            fastWin.Owner = this;
            fastWin.ShowDialog();
            // Refresh dashboard after sale window closes
            LoadDashboard();
        }

        private void btnGridSale_Click(object sender, RoutedEventArgs e)
        {
            var gridWin = new GridSalesWindow();
            gridWin.Owner = this;
            gridWin.ShowDialog();
            // Refresh dashboard after sale window closes
            LoadDashboard();
            UpdateDbStatus();
        }

        private void btnProducts_Click(object sender, RoutedEventArgs e)
        {
            var prodWin = new ProductsWindow();
            prodWin.Owner = this;
            prodWin.ShowDialog();
            LoadDashboard();
        }

        private void btnCategories_Click(object sender, RoutedEventArgs e)
        {
            var catWin = new CategoriesWindow();
            catWin.Owner = this;
            catWin.ShowDialog();
        }

        private void btnPurchaseOrders_Click(object sender, RoutedEventArgs e)
        {
            var poWin = new PurchaseOrdersWindow();
            poWin.Owner = this;
            poWin.ShowDialog();
            LoadDashboard();
        }

        private void btnCustomers_Click(object sender, RoutedEventArgs e)
        {
            var custWin = new CustomersWindow();
            custWin.Owner = this;
            custWin.ShowDialog();
        }

        private void btnReturns_Click(object sender, RoutedEventArgs e)
        {
            var retWin = new ReturnsWindow();
            retWin.Owner = this;
            retWin.ShowDialog();
        }

        // REPORTS HUB - Single entry point for all reports
        private void btnReportsHub_Click(object sender, RoutedEventArgs e)
        {
            var reportsHub = new ReportsHubWindow();
            reportsHub.Owner = this;
            reportsHub.ShowDialog();
            LoadDashboard(); // Refresh dashboard after closing hub
        }

        private void btnUsers_Click(object sender, RoutedEventArgs e)
        {
            var userWin = new UsersWindow();
            userWin.Owner = this;
            userWin.ShowDialog();
            LoadDashboard();
        }

        private void btnVoidedSales_Click(object sender, RoutedEventArgs e)
        {
            var voidWin = new VoidedSalesWindow();
            voidWin.Owner = this;
            voidWin.ShowDialog();
            LoadDashboard();
        }

        private void btnBackup_Click(object sender, RoutedEventArgs e)
        {
            var backWin = new BackupWindow();
            backWin.Owner = this;
            backWin.ShowDialog();
        }

        private void DownloadUpdate_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/jadabousbeit23/POSSystem/blob/main/releases",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open browser: {ex.Message}", "Error");
            }
        }

        private void btnNetwork_Click(object sender, RoutedEventArgs e)
        {
            if (!Session.IsAdmin)
            {
                MessageBox.Show("⛔ Admins only!", "Access Denied");
                return;
            }
            var netWin = new NetworkSettingsWindow();
            netWin.Owner = this;
            netWin.ShowDialog();
            UpdateDbStatus();
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            var setWin = new SettingsWindow();
            setWin.Owner = this;
            setWin.ShowDialog();
        }

        private void btnChangePassword_Click(object sender, RoutedEventArgs e)
        {
            var passWin = new ChangePasswordWindow();
            passWin.Owner = this;
            passWin.ShowDialog();
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadDashboard();
            UpdateDbStatus();
        }

        private void DismissUpdate_Click(object sender, RoutedEventArgs e)
        {
            updateBanner.Visibility = Visibility.Collapsed;
        }


        private void btnShift_Click(object sender, RoutedEventArgs e)
        {
            var shiftWin = new POSSystem.Windows.ShiftWindow();
            shiftWin.Owner = this;
            shiftWin.ShowDialog();
        }

        private void btnAuditLog_Click(object sender, RoutedEventArgs e)
        {
            if (!Session.IsAdmin)
            {
                MessageBox.Show("⛔ Admins only!");
                return;
            }
            var auditWin = new POSSystem.Windows.AuditLogWindow();
            auditWin.Owner = this;
            auditWin.ShowDialog();
        }

        private void btnPromotions_Click(object sender, RoutedEventArgs e)
        {
            if (!Session.IsAdmin)
            {
                MessageBox.Show("⛔ Admins only!");
                return;
            }
            var promoWin = new POSSystem.Windows.PromotionsWindow();
            promoWin.Owner = this;
            promoWin.ShowDialog();
        }

        // FIXED: Close Day functionality - resets dashboard to zero
        private void btnCloseDay_Click(object sender, RoutedEventArgs e)
        {
            if (!Session.IsAdmin)
            {
                MessageBox.Show("⛔ Admins only!", "Access Denied");
                return;
            }

            // Confirm close day
            if (MessageBox.Show(
                "Close the day? This will archive today's sales and reset daily counters.\n\n" +
                "Make sure all sales are completed before closing.",
                "Close Day",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                using (SQLiteConnection con = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    con.Open();

                    string today = DateTime.Now.ToString(DATE_FMT);

                    // Get today's totals
                    var cmd = new SQLiteCommand(
                        "SELECT COUNT(*), IFNULL(SUM(TotalAmount),0) " +
                        "FROM Sales WHERE Date LIKE @d", con);
                    cmd.Parameters.AddWithValue("@d", $"{today}%");

                    int totalSales = 0;
                    double totalAmount = 0;

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            totalSales = Convert.ToInt32(reader[0]);
                            totalAmount = Convert.ToDouble(reader[1]);
                        }
                    }

                    if (totalSales == 0)
                    {
                        MessageBox.Show("No sales to close for today.", "Close Day",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        // Still clear dashboard to zero
                        ClearDashboard();
                        return;
                    }

                    // Check if already closed today
                    var checkCmd = new SQLiteCommand(
                        "SELECT COUNT(*) FROM DailyReports WHERE Date = @d", con);
                    checkCmd.Parameters.AddWithValue("@d", today);
                    int existing = Convert.ToInt32(checkCmd.ExecuteScalar());

                    if (existing > 0)
                    {
                        if (MessageBox.Show(
                            "Today's sales have already been closed. Do you want to update the closing?",
                            "Already Closed",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question) != MessageBoxResult.Yes)
                        {
                            return;
                        }

                        // Update existing record
                        var updateCmd = new SQLiteCommand(
                            "UPDATE DailyReports SET TotalSales = @ts, TotalAmount = @ta, ClosedAt = @ca " +
                            "WHERE Date = @d", con);
                        updateCmd.Parameters.AddWithValue("@ts", totalSales);
                        updateCmd.Parameters.AddWithValue("@ta", totalAmount);
                        updateCmd.Parameters.AddWithValue("@ca", DatabaseHelper.ToIsoDateTime(DateTime.Now));
                        updateCmd.Parameters.AddWithValue("@d", today);
                        updateCmd.ExecuteNonQuery();
                    }
                    else
                    {
                        // Insert new daily report
                        var insertCmd = new SQLiteCommand(
                            "INSERT INTO DailyReports (Date, TotalSales, TotalAmount, ClosedAt) " +
                            "VALUES (@d, @ts, @ta, @ca)", con);
                        insertCmd.Parameters.AddWithValue("@d", today);
                        insertCmd.Parameters.AddWithValue("@ts", totalSales);
                        insertCmd.Parameters.AddWithValue("@ta", totalAmount);
                        insertCmd.Parameters.AddWithValue("@ca", DatabaseHelper.ToIsoDateTime(DateTime.Now));
                        insertCmd.ExecuteNonQuery();
                    }

                    // Log the action
                    DatabaseHelper.Log("CloseDay",
                        $"Day closed: {totalSales} sales, LBP {totalAmount:N0}", "Admin");

                    MessageBox.Show(
                        $"✅ Day closed successfully!\n\n" +
                        $"Sales: {totalSales}\n" +
                        $"Revenue: LBP {totalAmount:N0}",
                        "Close Day",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Reset dashboard to zero (visual only - data is archived)
                    ClearDashboard();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error closing day: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Log out?", "Logout",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            _clockTimer.Stop();
            _refreshTimer.Stop();
            Session.Username = "";
            Session.Role = "";
            new LoginWindow().Show();
            this.Close();
        }

        public class LowStockItem
        {
            public string Name { get; set; }
            public int Stock { get; set; }
            public string StockText { get; set; }
        }

        public class TopProductItem
        {
            public string Rank { get; set; }
            public string Name { get; set; }
            public string QtyText { get; set; }
        }

        public class DayData
        {
            public string Label { get; set; }
            public double Revenue { get; set; }
            public int Count { get; set; }
        }
    }
}