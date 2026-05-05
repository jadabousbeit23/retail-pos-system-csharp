using POSSystem.Database;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace POSSystem.Windows
{
    // ── View models ────────────────────────────────────────────
    public class ProfitRow
    {
        public string ProductName { get; set; }
        public int QtySold { get; set; }
        public double Revenue { get; set; }
        public double Cost { get; set; }
        public double Profit => Revenue - Cost;
        public double MarginPct => Revenue > 0
            ? Math.Round(Profit / Revenue * 100, 1) : 0;
        public string RevenueDisplay => $"LBP {Revenue:N0}";
        public string CostDisplay => $"LBP {Cost:N0}";
        public string ProfitDisplay => $"LBP {Profit:N0}";
        public string MarginDisplay => $"{MarginPct}%";
        public string MarginColor => MarginPct >= 30 ? "#107C10"
                                      : MarginPct >= 10 ? "#CA5010"
                                      : "#D13438";
    }

    public class DailyProfitRow
    {
        public string Date { get; set; }
        public double Revenue { get; set; }
        public double Cost { get; set; }
        public double Profit => Revenue - Cost;
        public string RevenueDisplay => $"LBP {Revenue:N0}";
        public string ProfitDisplay => $"LBP {Profit:N0}";
    }

    public class BestSellerRow
    {
        public int Rank { get; set; }
        public string ProductName { get; set; }
        public string Category { get; set; }
        public int QtySold { get; set; }
        public double Revenue { get; set; }
        public string RevenueDisplay => $"LBP {Revenue:N0}";
    }

    public class HourlyRow
    {
        public int Hour { get; set; }
        public string HourLabel { get; set; }
        public int SalesCount { get; set; }
        public double Revenue { get; set; }
        public string RevenueDisplay => $"LBP {Revenue:N0}";
    }

    public partial class AdvancedReportsWindow : Window
    {
        string _currentTab = "Profit";
        string _filterFrom = "";
        string _filterTo = "";

        public AdvancedReportsWindow()
        {
            InitializeComponent();
            // FIXED: Default to last month using ISO format
            txtDateFrom.Text = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd");
            txtDateTo.Text = DateTime.Now.ToString("yyyy-MM-dd");
            UpdateDbStatus();
            LoadAll();
        }

        // ══════════════════════════════════════════════════════
        // LOADING WITH MINIMUM DISPLAY TIME
        // ══════════════════════════════════════════════════════
        private async Task ShowLoadingWithMinimumTime(string message, Func<Task> operation)
        {
            // Show loading on UI thread
            await Dispatcher.InvokeAsync(() => ShowLoading(message));

            var stopwatch = Stopwatch.StartNew();

            try
            {
                await operation();
            }
            finally
            {
                // Ensure minimum 500ms display time
                var elapsed = stopwatch.ElapsedMilliseconds;
                if (elapsed < 500)
                {
                    await Task.Delay(500 - (int)elapsed);
                }

                // Hide loading on UI thread
                await Dispatcher.InvokeAsync(() => HideLoading());
            }
        }

        void ShowLoading(string message)
        {
            lblLoadingMessage.Text = message;
            loadingOverlay.Visibility = Visibility.Visible;
        }

        void HideLoading()
        {
            loadingOverlay.Visibility = Visibility.Collapsed;
        }

        // ══════════════════════════════════════════════════════
        // LOAD ALL TABS (WITH LOADING)
        // ══════════════════════════════════════════════════════
        async void LoadAll()
        {
            await ShowLoadingWithMinimumTime("Loading reports...", async () =>
            {
                await Task.Run(() =>
                {
                    Dispatcher.Invoke(() => lblStatus.Content = "Loading…");

                    LoadSummaryCards();
                    LoadProfitTab();
                    LoadBestSellersTab();
                    LoadHourlyTab();

                    Dispatcher.Invoke(() => lblStatus.Content = $"Last refreshed: {DateTime.Now:HH:mm:ss}");
                });
            });
        }

        // ══════════════════════════════════════════════════════
        // SUMMARY CARDS - FIXED WITH ISO DATES
        // ══════════════════════════════════════════════════════
        void LoadSummaryCards()
        {
            double revenue = 0, cost = 0;
            int count = 0;

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                string where = BuildDateWhere("s.Date");

                // Revenue + sale count
                var r = new SQLiteCommand($@"
                    SELECT COUNT(*) AS cnt, SUM(s.TotalAmount) AS rev
                    FROM Sales s {where}", con).ExecuteReader();
                if (r.Read())
                {
                    count = Convert.ToInt32(r["cnt"]);
                    revenue = r["rev"] == DBNull.Value ? 0 : Convert.ToDouble(r["rev"]);
                }
                r.Close();

                // Cost (sum of costPrice × qty for sold items)
                var cr = new SQLiteCommand($@"
                    SELECT SUM(si.Quantity * p.CostPrice) AS totalCost
                    FROM SaleItems si
                    JOIN Products p  ON si.ProductId = p.Id
                    JOIN Sales    s  ON si.SaleId    = s.Id
                    {where}", con).ExecuteReader();
                if (cr.Read() && cr["totalCost"] != DBNull.Value)
                    cost = Convert.ToDouble(cr["totalCost"]);
                cr.Close();
            }

            double profit = revenue - cost;
            double margin = revenue > 0 ? Math.Round(profit / revenue * 100, 1) : 0;
            double avg = count > 0 ? revenue / count : 0;

            Dispatcher.Invoke(() =>
            {
                lblTotalRevenue.Content = $"LBP {revenue:N0}";
                lblTotalCost.Content = $"LBP {cost:N0}";
                lblNetProfit.Content = $"LBP {profit:N0}";
                lblNetProfit.Foreground = profit >= 0
                    ? new SolidColorBrush(Color.FromRgb(16, 124, 16))
                    : new SolidColorBrush(Color.FromRgb(209, 52, 56));
                lblMarginPct.Content = $"{margin}% margin";
                lblTotalSales.Content = count.ToString();
                lblAvgSale.Content = $"LBP {avg:N0} avg";

                string range = string.IsNullOrEmpty(_filterFrom)
                    ? "All time" : $"{_filterFrom} → {_filterTo}";
                lblRevenueRange.Content = range;
            });
        }

        // ══════════════════════════════════════════════════════
        // TAB 1 — PROFIT - FIXED WITH ISO DATES
        // ══════════════════════════════════════════════════════
        void LoadProfitTab()
        {
            Dispatcher.Invoke(() =>
            {
                lvProfit.Items.Clear();
                lvDailyProfit.Items.Clear();
            });

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                string where = BuildDateWhere("s.Date");

                // Per-product profit
                var r = new SQLiteCommand($@"
                    SELECT p.Name,
                           SUM(si.Quantity)              AS QtySold,
                           SUM(si.Price)                 AS Revenue,
                           SUM(si.Quantity * p.CostPrice) AS Cost
                    FROM SaleItems si
                    JOIN Products p ON si.ProductId = p.Id
                    JOIN Sales    s ON si.SaleId    = s.Id
                    {where}
                    GROUP BY p.Id, p.Name
                    ORDER BY Revenue DESC", con).ExecuteReader();

                while (r.Read())
                {
                    var row = new ProfitRow
                    {
                        ProductName = r["Name"].ToString(),
                        QtySold = Convert.ToInt32(r["QtySold"]),
                        Revenue = Convert.ToDouble(r["Revenue"]),
                        Cost = r["Cost"] == DBNull.Value
                                      ? 0 : Convert.ToDouble(r["Cost"])
                    };
                    Dispatcher.Invoke(() => lvProfit.Items.Add(row));
                }
                r.Close();

                // Daily profit - FIXED: Extract date from ISO format
                var dr = new SQLiteCommand($@"
                    SELECT SUBSTR(s.Date,1,10) AS Day,
                           SUM(s.TotalAmount)  AS Revenue,
                           SUM(si.Quantity * p.CostPrice) AS Cost
                    FROM Sales s
                    JOIN SaleItems si ON si.SaleId    = s.Id
                    JOIN Products  p  ON si.ProductId = p.Id
                    {where}
                    GROUP BY Day
                    ORDER BY Day DESC", con).ExecuteReader();

                while (dr.Read())
                {
                    var row = new DailyProfitRow
                    {
                        Date = dr["Day"].ToString(),
                        Revenue = Convert.ToDouble(dr["Revenue"]),
                        Cost = dr["Cost"] == DBNull.Value
                                  ? 0 : Convert.ToDouble(dr["Cost"])
                    };
                    Dispatcher.Invoke(() => lvDailyProfit.Items.Add(row));
                }
                dr.Close();
            }
        }

        // ══════════════════════════════════════════════════════
        // TAB 2 — BEST SELLERS - FIXED WITH ISO DATES
        // ══════════════════════════════════════════════════════
        void LoadBestSellersTab()
        {
            Dispatcher.Invoke(() =>
            {
                lvBestQty.Items.Clear();
                lvBestRevenue.Items.Clear();
            });

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                string where = BuildDateWhere("s.Date");

                string baseSql = $@"
                    SELECT p.Name, p.Category,
                           SUM(si.Quantity) AS QtySold,
                           SUM(si.Price)    AS Revenue
                    FROM SaleItems si
                    JOIN Products p ON si.ProductId = p.Id
                    JOIN Sales    s ON si.SaleId    = s.Id
                    {where}
                    GROUP BY p.Id, p.Name, p.Category";

                // By quantity
                var r1 = new SQLiteCommand(
                    baseSql + " ORDER BY QtySold DESC LIMIT 10", con).ExecuteReader();
                int rank = 1;
                while (r1.Read())
                {
                    var row = new BestSellerRow
                    {
                        Rank = rank++,
                        ProductName = r1["Name"].ToString(),
                        Category = r1["Category"].ToString(),
                        QtySold = Convert.ToInt32(r1["QtySold"]),
                        Revenue = Convert.ToDouble(r1["Revenue"])
                    };
                    Dispatcher.Invoke(() => lvBestQty.Items.Add(row));
                }
                r1.Close();

                // By revenue
                var r2 = new SQLiteCommand(
                    baseSql + " ORDER BY Revenue DESC LIMIT 10", con).ExecuteReader();
                rank = 1;
                while (r2.Read())
                {
                    var row = new BestSellerRow
                    {
                        Rank = rank++,
                        ProductName = r2["Name"].ToString(),
                        Category = r2["Category"].ToString(),
                        QtySold = Convert.ToInt32(r2["QtySold"]),
                        Revenue = Convert.ToDouble(r2["Revenue"])
                    };
                    Dispatcher.Invoke(() => lvBestRevenue.Items.Add(row));
                }
                r2.Close();
            }
        }

        // ══════════════════════════════════════════════════════
        // TAB 3 — HOURLY - FIXED WITH ISO DATES
        // ══════════════════════════════════════════════════════
        void LoadHourlyTab()
        {
            Dispatcher.Invoke(() =>
            {
                lvHourly.Items.Clear();
                heatmapGrid.Children.Clear();
                heatmapGrid.RowDefinitions.Clear();
                heatmapGrid.ColumnDefinitions.Clear();
            });

            // Build hour data array 0–23
            var hours = new HourlyRow[24];
            for (int i = 0; i < 24; i++)
                hours[i] = new HourlyRow
                {
                    Hour = i,
                    HourLabel = i == 0 ? "12 AM"
                              : i < 12 ? $"{i} AM"
                              : i == 12 ? "12 PM"
                              : $"{i - 12} PM",
                    SalesCount = 0,
                    Revenue = 0
                };

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                string where = BuildDateWhere("Date");

                // Extract hour from ISO date string (yyyy-MM-dd HH:mm:ss)
                var r = new SQLiteCommand($@"
                    SELECT CAST(SUBSTR(Date, 12, 2) AS INTEGER) AS Hr,
                           COUNT(*)         AS cnt,
                           SUM(TotalAmount) AS rev
                    FROM Sales {where}
                    GROUP BY Hr", con).ExecuteReader();

                while (r.Read())
                {
                    int hr = Convert.ToInt32(r["Hr"]);
                    if (hr >= 0 && hr < 24)
                    {
                        hours[hr].SalesCount = Convert.ToInt32(r["cnt"]);
                        hours[hr].Revenue = Convert.ToDouble(r["rev"]);
                    }
                }
                r.Close();
            }

            // Find max for scaling
            int maxSales = 1;
            foreach (var h in hours)
                if (h.SalesCount > maxSales) maxSales = h.SalesCount;

            Dispatcher.Invoke(() =>
            {
                // Build heatmap — 6 columns × 4 rows (hours 0–23)
                for (int col = 0; col < 6; col++)
                    heatmapGrid.ColumnDefinitions.Add(
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                for (int row = 0; row < 4; row++)
                    heatmapGrid.RowDefinitions.Add(
                        new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                for (int i = 0; i < 24; i++)
                {
                    int col = i % 6;
                    int row = i / 6;
                    double intensity = (double)hours[i].SalesCount / maxSales;

                    // Interpolate colour: white → #0078D4
                    byte r2 = (byte)(229 - (int)(229 * intensity) + (int)(0 * intensity));
                    byte g2 = (byte)(243 - (int)(243 * intensity) + (int)(120 * intensity));
                    byte b2 = (byte)(255 - (int)(255 * intensity) + (int)(212 * intensity));

                    var cell = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(r2, g2, b2)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(208, 208, 208)),
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(2),
                        ToolTip = $"{hours[i].HourLabel}: {hours[i].SalesCount} sales"
                    };

                    var sp = new StackPanel
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    sp.Children.Add(new Label
                    {
                        Content = hours[i].HourLabel,
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = intensity > 0.5
                            ? Brushes.White : new SolidColorBrush(Color.FromRgb(26, 26, 26)),
                        Padding = new Thickness(0)
                    });
                    sp.Children.Add(new Label
                    {
                        Content = hours[i].SalesCount > 0
                            ? $"{hours[i].SalesCount} sales" : "—",
                        FontSize = 10,
                        Foreground = intensity > 0.5
                            ? Brushes.White : new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                        Padding = new Thickness(0)
                    });
                    cell.Child = sp;

                    Grid.SetColumn(cell, col);
                    Grid.SetRow(cell, row);
                    heatmapGrid.Children.Add(cell);

                    // Table list
                    lvHourly.Items.Add(hours[i]);
                }
            });
        }

        // ══════════════════════════════════════════════════════
        // DATE WHERE CLAUSE BUILDER - FIXED FOR ISO DATES
        // ══════════════════════════════════════════════════════
        string BuildDateWhere(string dateCol)
        {
            if (string.IsNullOrEmpty(_filterFrom)) return "WHERE 1=1";

            // ISO 8601 format: yyyy-MM-dd
            // Use LIKE for pattern matching since dates are stored as yyyy-MM-dd HH:mm:ss
            return $"WHERE {dateCol} LIKE '{_filterFrom}%'";
        }

        // ══════════════════════════════════════════════════════
        // TAB SWITCHING
        // ══════════════════════════════════════════════════════
        private void btnTab_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;

            tabProfit.Visibility = Visibility.Collapsed;
            tabBestSellers.Visibility = Visibility.Collapsed;
            tabHourly.Visibility = Visibility.Collapsed;

            var inactiveStyle = (Style)FindResource("TabBtn");
            var activeStyle = (Style)FindResource("TabBtnActive");
            btnTabProfit.Style = inactiveStyle;
            btnTabBestSellers.Style = inactiveStyle;
            btnTabHourly.Style = inactiveStyle;

            if (btn.Name == "btnTabProfit")
            {
                tabProfit.Visibility = Visibility.Visible; _currentTab = "Profit";
                btnTabProfit.Style = activeStyle;
            }
            else if (btn.Name == "btnTabBestSellers")
            {
                tabBestSellers.Visibility = Visibility.Visible; _currentTab = "BestSellers";
                btnTabBestSellers.Style = activeStyle;
            }
            else
            {
                tabHourly.Visibility = Visibility.Visible; _currentTab = "Hourly";
                btnTabHourly.Style = activeStyle;
            }
        }

        // ══════════════════════════════════════════════════════
        // DATE FILTER - FIXED FOR ISO DATES (WITH LOADING)
        // ══════════════════════════════════════════════════════
        private async void btnApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            _filterFrom = txtDateFrom.Text.Trim();
            _filterTo = txtDateTo.Text.Trim();
            await ShowLoadingWithMinimumTime("Applying filter...", async () =>
            {
                await Task.Run(() => LoadAll());
            });
        }

        private void btnClearFilter_Click(object sender, RoutedEventArgs e)
        {
            _filterFrom = "";
            _filterTo = "";
            txtDateFrom.Text = "";
            txtDateTo.Text = "";
            LoadAll();
        }

        // ══════════════════════════════════════════════════════
        // EXPORT EXCEL (WITH LOADING)
        // ══════════════════════════════════════════════════════
        private async void btnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            await ShowLoadingWithMinimumTime("Exporting to Excel...", async () =>
            {
                await Task.Run(() =>
                {
                    try
                    {
                        var rows = new List<SaleRow>();
                        double revenue = 0;
                        foreach (ProfitRow r in lvProfit.Items)
                        {
                            rows.Add(new SaleRow
                            {
                                Id = 0,
                                Date = r.ProductName,
                                TotalAmount = r.Revenue
                            });
                            revenue += r.Revenue;
                        }
                        if (rows.Count == 0)
                        {
                            Dispatcher.Invoke(() => MessageBox.Show("No data to export!"));
                            return;
                        }
                        Dispatcher.Invoke(() => ExcelExportHelper.ExportDaily(rows, revenue, rows.Count));
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => MessageBox.Show($"Export error: {ex.Message}"));
                    }
                });
            });
        }

        // ══════════════════════════════════════════════════════
        // DB STATUS
        // ══════════════════════════════════════════════════════
        void UpdateDbStatus()
        {
            bool isNetwork = DatabaseHelper.IsNetworkDatabase();
            lblDbStatus.Content = isNetwork ? "🟢 Network DB" : "🟡 Local DB";
            lblDbStatus.Foreground = isNetwork
                ? new SolidColorBrush(Color.FromRgb(16, 124, 16))
                : new SolidColorBrush(Color.FromRgb(202, 80, 16));
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}