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
    public class CashierOverviewRow
    {
        public int Rank { get; set; }
        public string Cashier { get; set; }
        public int SalesCount { get; set; }
        public double Revenue { get; set; }
        public double CashRevenue { get; set; }
        public double CardRevenue { get; set; }
        public int Voids { get; set; }
        public string TopProduct { get; set; }
        public double SharePct { get; set; }   // % of total revenue

        public string RevenueDisplay => $"LBP {Revenue:N0}";
        public string AvgSaleDisplay => SalesCount > 0
            ? $"LBP {Revenue / SalesCount:N0}" : "—";
        public string CashDisplay => $"LBP {CashRevenue:N0}";
        public string CardDisplay => $"LBP {CardRevenue:N0}";
        public string ShareDisplay => $"{SharePct:F1}%";
        public double ShareBarWidth => Math.Max(4, SharePct * 0.7); // max ~70px at 100%

        public string RankColor => Rank == 1 ? "#9D5D00"     // gold
                                 : Rank == 2 ? "#5F5F5F"     // silver
                                 : Rank == 3 ? "#CA5010"     // bronze
                                 : "#8A8A8A";                // rest
    }

    public class CashierSaleRow
    {
        public int SaleId { get; set; }
        public string Date { get; set; }
        public double Amount { get; set; }
        public string Method { get; set; }
        public string ItemsSummary { get; set; }
        public string AmountDisplay => $"LBP {Amount:N0}";
    }

    public class ChartRow
    {
        public string Cashier { get; set; }
        public int SalesCount { get; set; }
        public double Revenue { get; set; }
        public double BarWidth { get; set; }  // pixel width, set by code
        public string RevenueDisplay => $"LBP {Revenue:N0}";
        public string BarColor { get; set; }
    }

    public partial class CashierReportWindow : Window
    {
        string _currentTab = "Overview";
        string _dateFrom = "";
        string _dateTo = "";

        // cached overview data (reused by drill-down picker + chart)
        List<CashierOverviewRow> _overviewData = new List<CashierOverviewRow>();

        // colour palette for chart bars
        static readonly string[] BarColors = {
            "#0078D4","#107C10","#9D5D00","#C42B1C",
            "#5F5F5F","#0063B1","#1A7C44","#CA5010"
        };

        public CashierReportWindow()
        {
            InitializeComponent();
            // Default: today - ISO format
            string todayIso = DatabaseHelper.GetTodayIso();
            txtFrom.Text = todayIso;
            txtTo.Text = todayIso;
            _dateFrom = todayIso;
            _dateTo = todayIso;
            LoadAll();
        }

        // ══════════════════════════════════════════════
        // LOAD ALL
        // ══════════════════════════════════════════════
        void LoadAll()
        {
            lblStatus.Content = "Loading…";
            LoadOverview();
            BuildChartData();
            RefreshDrillPicker();
            UpdateSummaryCards();

            string range = _dateFrom == _dateTo
                ? _dateFrom
                : $"{_dateFrom} → {_dateTo}";
            lblSubtitle.Content = $"Period: {range}";
            lblStatus.Content = $"Last refreshed: {DateTime.Now:HH:mm:ss}";
        }

        // ══════════════════════════════════════════════
        // OVERVIEW TAB - FIXED WITH ISO DATES
        // ══════════════════════════════════════════════
        void LoadOverview()
        {
            _overviewData.Clear();
            lvOverview.Items.Clear();

            string where = BuildWhere("s.Date");

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();

                // Get all cashier stats in one query
                var r = new SQLiteCommand($@"
                    SELECT s.CashierName,
                           COUNT(s.Id)              AS cnt,
                           SUM(s.TotalAmount)       AS rev,
                           SUM(s.CashPaid)          AS cash,
                           SUM(s.CardPaid)          AS card
                    FROM Sales s {where}
                    GROUP BY s.CashierName
                    ORDER BY rev DESC", con).ExecuteReader();

                while (r.Read())
                    _overviewData.Add(new CashierOverviewRow
                    {
                        Cashier = r["CashierName"].ToString(),
                        SalesCount = Convert.ToInt32(r["cnt"]),
                        Revenue = r["rev"] == DBNull.Value ? 0 : Convert.ToDouble(r["rev"]),
                        CashRevenue = r["cash"] == DBNull.Value ? 0 : Convert.ToDouble(r["cash"]),
                        CardRevenue = r["card"] == DBNull.Value ? 0 : Convert.ToDouble(r["card"])
                    });
                r.Close();

                // Get void count per cashier - ISO format
                var vr = new SQLiteCommand($@"
                    SELECT CashierName, COUNT(*) AS cnt
                    FROM VoidedSales {BuildWhere("Date")}
                    GROUP BY CashierName", con).ExecuteReader();
                var voids = new Dictionary<string, int>();
                while (vr.Read())
                    voids[vr["CashierName"].ToString()] = Convert.ToInt32(vr["cnt"]);
                vr.Close();

                // Get top product per cashier
                foreach (var row in _overviewData)
                {
                    row.Voids = voids.ContainsKey(row.Cashier) ? voids[row.Cashier] : 0;

                    var tp = new SQLiteCommand($@"
                        SELECT p.Name, SUM(si.Quantity) AS qty
                        FROM SaleItems si
                        JOIN Products p ON si.ProductId = p.Id
                        JOIN Sales s    ON si.SaleId    = s.Id
                        WHERE s.CashierName = @cashier {AppendAnd(where)}
                        GROUP BY p.Name
                        ORDER BY qty DESC
                        LIMIT 1", con);
                    tp.Parameters.AddWithValue("@cashier", row.Cashier);
                    object tpRes = tp.ExecuteScalar();
                    row.TopProduct = tpRes != null ? tpRes.ToString() : "—";
                }
            }

            // Calculate share %
            double totalRev = 0;
            foreach (var r in _overviewData) totalRev += r.Revenue;

            int rank = 1;
            foreach (var row in _overviewData)
            {
                row.Rank = rank++;
                row.SharePct = totalRev > 0
                    ? Math.Round(row.Revenue / totalRev * 100, 1) : 0;
                lvOverview.Items.Add(row);
            }
        }

        // ══════════════════════════════════════════════
        // SUMMARY CARDS
        // ══════════════════════════════════════════════
        void UpdateSummaryCards()
        {
            int totalSales = 0;
            double totalRev = 0;
            foreach (var r in _overviewData)
            { totalSales += r.SalesCount; totalRev += r.Revenue; }

            lblTotalCashiers.Content = _overviewData.Count.ToString();
            lblTotalSales.Content = totalSales.ToString("N0");
            lblTotalRevenue.Content = $"LBP {totalRev:N0}";

            if (_overviewData.Count > 0)
            {
                lblTopCashier.Content = _overviewData[0].Cashier;
                lblTopCashierRev.Content = _overviewData[0].RevenueDisplay +
                                           $" · {_overviewData[0].SalesCount} sales";
            }
            else
            {
                lblTopCashier.Content = "—";
                lblTopCashierRev.Content = "";
            }
        }

        // ══════════════════════════════════════════════
        // DRILL DOWN TAB
        // ══════════════════════════════════════════════
        void RefreshDrillPicker()
        {
            lvCashierPicker.Items.Clear();
            foreach (var r in _overviewData)
                lvCashierPicker.Items.Add(r);
        }

        private void lvCashierPicker_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (lvCashierPicker.SelectedItem == null) return;
            var row = (CashierOverviewRow)lvCashierPicker.SelectedItem;
            LoadDrillDown(row.Cashier, row.SalesCount, row.Revenue);
        }

        void LoadDrillDown(string cashier, int salesCount, double revenue)
        {
            lblDrillName.Content = cashier;
            lblDrillSales.Content = salesCount.ToString("N0");
            lblDrillRevenue.Content = $"LBP {revenue:N0}";
            lblDrillAvg.Content = salesCount > 0
                ? $"LBP {revenue / salesCount:N0}" : "—";

            lvDrillSales.Items.Clear();
            string where = BuildWhere("s.Date");

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();

                var r = new SQLiteCommand($@"
                    SELECT s.Id, s.Date, s.TotalAmount, s.PaymentMethod
                    FROM Sales s
                    WHERE s.CashierName = @cashier {AppendAnd(where)}
                    ORDER BY s.Id DESC", con);
                r.Parameters.AddWithValue("@cashier", cashier);
                var dr = r.ExecuteReader();

                var sales = new List<(int Id, string Date, double Amt, string Method)>();
                while (dr.Read())
                    sales.Add((
                        Convert.ToInt32(dr["Id"]),
                        dr["Date"].ToString(),
                        Convert.ToDouble(dr["TotalAmount"]),
                        dr["PaymentMethod"].ToString()));
                dr.Close();

                foreach (var (id, date, amt, method) in sales)
                {
                    // Get items summary
                    var ir = new SQLiteCommand(@"
                        SELECT p.Name, si.Quantity
                        FROM SaleItems si JOIN Products p ON si.ProductId=p.Id
                        WHERE si.SaleId=@sid", con);
                    ir.Parameters.AddWithValue("@sid", id);
                    var irr = ir.ExecuteReader();
                    string items = "";
                    while (irr.Read())
                        items += $"{irr["Name"]} ×{irr["Quantity"]}  ";
                    irr.Close();

                    lvDrillSales.Items.Add(new CashierSaleRow
                    {
                        SaleId = id,
                        Date = date,
                        Amount = amt,
                        Method = method,
                        ItemsSummary = items.TrimEnd()
                    });
                }
            }
        }

        // ══════════════════════════════════════════════
        // CHART TAB
        // ══════════════════════════════════════════════
        void BuildChartData()
        {
            icChart.Items.Clear();
            if (_overviewData.Count == 0) return;

            double maxRev = _overviewData[0].Revenue;
            double maxBarPx = 600; // max bar width in pixels

            int colorIdx = 0;
            foreach (var row in _overviewData)
            {
                double barW = maxRev > 0
                    ? (row.Revenue / maxRev) * maxBarPx : 0;

                icChart.Items.Add(new ChartRow
                {
                    Cashier = row.Cashier,
                    SalesCount = row.SalesCount,
                    Revenue = row.Revenue,
                    BarWidth = Math.Max(4, barW),
                    BarColor = BarColors[colorIdx % BarColors.Length]
                });
                colorIdx++;
            }

            lblChartNote.Content =
                $"Bar width scaled relative to top cashier " +
                $"({_overviewData[0].Cashier}: LBP {_overviewData[0].Revenue:N0})";
        }

        // ══════════════════════════════════════════════
        // TAB SWITCHING
        // ══════════════════════════════════════════════
        private void btnTab_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;

            tabOverview.Visibility = Visibility.Collapsed;
            tabDrillDown.Visibility = Visibility.Collapsed;
            tabChart.Visibility = Visibility.Collapsed;

            var inactive = (Style)FindResource("FilterBtn");
            var active = (Style)FindResource("FilterBtnActive");
            btnTabOverview.Style = inactive;
            btnTabDrillDown.Style = inactive;
            btnTabChart.Style = inactive;

            switch (btn.Name)
            {
                case "btnTabOverview":
                    tabOverview.Visibility = Visibility.Visible;
                    btnTabOverview.Style = active;
                    _currentTab = "Overview";
                    break;
                case "btnTabDrillDown":
                    tabDrillDown.Visibility = Visibility.Visible;
                    btnTabDrillDown.Style = active;
                    _currentTab = "DrillDown";
                    break;
                default:
                    tabChart.Visibility = Visibility.Visible;
                    btnTabChart.Style = active;
                    _currentTab = "Chart";
                    break;
            }
        }

        // ══════════════════════════════════════════════
        // DATE FILTERS - FIXED WITH ISO DATES
        // ══════════════════════════════════════════════
        private void btnQuickFilter_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;

            var inactive = (Style)FindResource("FilterBtn");
            var active = (Style)FindResource("FilterBtnActive");
            btnToday.Style = inactive;
            btnThisWeek.Style = inactive;
            btnThisMonth.Style = inactive;
            btnAllTime.Style = inactive;
            btn.Style = active;

            // ISO format
            string todayIso = DatabaseHelper.GetTodayIso();

            switch (btn.Name)
            {
                case "btnToday":
                    _dateFrom = todayIso;
                    _dateTo = todayIso;
                    break;
                case "btnThisWeek":
                    int offset = (int)DateTime.Now.DayOfWeek;
                    if (offset == 0) offset = 7;
                    _dateFrom = DateTime.Now.AddDays(-(offset - 1)).ToString("yyyy-MM-dd");
                    _dateTo = todayIso;
                    break;
                case "btnThisMonth":
                    _dateFrom = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1)
                                .ToString("yyyy-MM-dd");
                    _dateTo = todayIso;
                    break;
                default: // All Time
                    _dateFrom = "";
                    _dateTo = "";
                    break;
            }

            txtFrom.Text = _dateFrom;
            txtTo.Text = _dateTo;
            LoadAll();
        }

        private void btnApply_Click(object sender, RoutedEventArgs e)
        {
            _dateFrom = txtFrom.Text.Trim();
            _dateTo = txtTo.Text.Trim();

            // Clear quick filter highlights
            var inactive = (Style)FindResource("FilterBtn");
            btnToday.Style = inactive;
            btnThisWeek.Style = inactive;
            btnThisMonth.Style = inactive;
            btnAllTime.Style = inactive;

            LoadAll();
        }

        // ══════════════════════════════════════════════
        // EXPORT TO EXCEL
        // ══════════════════════════════════════════════
        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Build SaleRow list from overview data
                var rows = new List<SaleRow>();
                foreach (var r in _overviewData)
                    rows.Add(new SaleRow
                    {
                        Id = r.Rank,
                        Date = r.Cashier,
                        TotalAmount = r.Revenue
                    });

                if (rows.Count == 0)
                { MessageBox.Show("No data to export!"); return; }

                double total = 0;
                foreach (var r in _overviewData) total += r.Revenue;
                ExcelExportHelper.ExportDaily(rows, total, rows.Count);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export error: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════
        // SQL HELPERS - FIXED FOR ISO DATES
        // ══════════════════════════════════════════════
        string BuildWhere(string dateCol)
        {
            if (string.IsNullOrEmpty(_dateFrom)) return "WHERE 1=1";

            // ISO 8601 format: yyyy-MM-dd - use LIKE for pattern matching
            return $"WHERE {dateCol} LIKE '{_dateFrom}%'";
        }

        // Appends AND if WHERE already exists, for sub-queries
        string AppendAnd(string existingWhere)
        {
            if (existingWhere.StartsWith("WHERE 1=1") &&
                existingWhere.Length > "WHERE 1=1".Length)
                return existingWhere.Replace("WHERE 1=1", "AND");
            if (existingWhere == "WHERE 1=1") return "";
            return existingWhere.Replace("WHERE ", "AND ");
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}