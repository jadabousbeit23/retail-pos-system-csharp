using POSSystem.Database;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace POSSystem.Windows
{
    // ═════════════════════════════════════════════════════════════════
    // DATA CLASSES
    // ═════════════════════════════════════════════════════════════════
    public class XReportData
    {
        public int ShiftId { get; set; }
        public string GeneratedAt { get; set; }
        public string Cashier { get; set; }
        public string OpenedAt { get; set; }
        public double StartingCash { get; set; }
        public int TransactionCount { get; set; }
        public double TotalRevenue { get; set; }
        public double CashSales { get; set; }
        public double CardSales { get; set; }
        public double ChangeGiven { get; set; }
        public int PointsAwarded { get; set; }
        public int VoidCount { get; set; }
        public int ReturnCount { get; set; }
        public double ReturnAmount { get; set; }
        public double ExpectedCash { get; set; }
        public List<TopProduct> TopProducts { get; set; } = new List<TopProduct>();
    }

    public class TopProduct
    {
        public string Name { get; set; }
        public int Quantity { get; set; }
        public double Revenue { get; set; }
    }

    public class ShiftRow
    {
        public int Id { get; set; }
        public string OpenedBy { get; set; }
        public string OpenedAt { get; set; }
        public string ClosedAt { get; set; }
        public double StartingCash { get; set; }
        public double ExpectedCash { get; set; }
        public double CountedCash { get; set; }
        public double Difference { get; set; }
        public int TotalSales { get; set; }
        public double TotalRevenue { get; set; }
        public string Status { get; set; }
        public string StartCashDisplay => $"LBP {StartingCash:N0}";
        public string ExpectedDisplay => $"LBP {ExpectedCash:N0}";
        public string CountedDisplay => CountedCash > 0 ? $"LBP {CountedCash:N0}" : "—";
        public string RevenueDisplay => $"LBP {TotalRevenue:N0}";
        public string DiffDisplay => Difference >= 0
            ? $"+LBP {Difference:N0}" : $"-LBP {Math.Abs(Difference):N0}";
        public string DiffColor => Difference >= 0 ? "#107C10" : "#C42B1C";
        public string StatusColor => Status == "Open" ? "#0078D4" : "#5F5F5F";
    }

    public partial class ShiftWindow : Window
    {
        int _activeShiftId = 0;
        private bool _debugMode = false; // Set to true for debugging

        public ShiftWindow()
        {
            InitializeComponent();
            LoadShiftStatus();
            LoadHistory();
            LoadTodaySummary();
        }

        // ══════════════════════════════════════════════
        // LOAD CURRENT SHIFT STATUS
        // ══════════════════════════════════════════════
        void LoadShiftStatus()
        {
            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var r = new SQLiteCommand(
                    "SELECT * FROM Shifts WHERE Status='Open' ORDER BY Id DESC LIMIT 1",
                    con).ExecuteReader();

                if (r.Read())
                {
                    _activeShiftId = Convert.ToInt32(r["Id"]);
                    lblShiftStatus.Content = $"🟢 Shift #{_activeShiftId} Open";
                    lblShiftStatus.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 16));
                    bdShiftStatus.Background = new SolidColorBrush(Color.FromRgb(223, 246, 221));
                    bdShiftStatus.BorderBrush = new SolidColorBrush(Color.FromRgb(134, 199, 128));
                    pnlActiveShift.Visibility = Visibility.Visible;

                    lblActiveUser.Content = r["OpenedBy"].ToString();
                    lblActiveOpenedAt.Content = r["OpenedAt"].ToString();
                    lblActiveStartCash.Content = $"LBP {Convert.ToDouble(r["StartingCash"]):N0}";
                }
                else
                {
                    _activeShiftId = 0;
                    lblShiftStatus.Content = "⏸ No Active Shift";
                    lblShiftStatus.Foreground = new SolidColorBrush(Color.FromRgb(196, 43, 28));
                    bdShiftStatus.Background = new SolidColorBrush(Color.FromRgb(253, 231, 233));
                    bdShiftStatus.BorderBrush = new SolidColorBrush(Color.FromRgb(241, 187, 188));
                    pnlActiveShift.Visibility = Visibility.Collapsed;
                }
                r.Close();
            }
        }

        // ══════════════════════════════════════════════
        // LOAD TODAY'S SUMMARY - FIXED WITH ISO DATES
        // ══════════════════════════════════════════════
        void LoadTodaySummary()
        {
            string todayIso = DatabaseHelper.GetTodayIso();

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var r = new SQLiteCommand(@"
                    SELECT COUNT(*) AS cnt,
                           IFNULL(SUM(TotalAmount),0) AS rev,
                           IFNULL(SUM(CashPaid),0) AS cash,
                           IFNULL(SUM(CardPaid),0) AS card
                    FROM Sales 
                    WHERE Date LIKE @today", con);
                r.Parameters.AddWithValue("@today", $"{todayIso}%");

                var dr = r.ExecuteReader();
                if (dr.Read())
                {
                    int cnt = Convert.ToInt32(dr["cnt"]);
                    double rev = Convert.ToDouble(dr["rev"]);
                    double cash = Convert.ToDouble(dr["cash"]);
                    double card = Convert.ToDouble(dr["card"]);

                    lblSummaryCount.Content = $"{cnt} sales";
                    lblSummaryRevenue.Content = $"LBP {rev:N0}";
                    lblSummaryCash.Content = $"LBP {cash:N0}";
                    lblSummaryCard.Content = $"LBP {card:N0}";

                    if (_activeShiftId > 0)
                        lblActiveSales.Content = $"{cnt} sales / LBP {rev:N0}";
                }
                dr.Close();
            }
        }

        // ══════════════════════════════════════════════
        // OPEN SHIFT - FIXED WITH ISO DATE
        // ══════════════════════════════════════════════
        private void btnOpenShift_Click(object sender, RoutedEventArgs e)
        {
            if (_activeShiftId > 0)
            { ShowMsg("⚠️ A shift is already open! Close it first.", false); return; }

            if (!double.TryParse(txtStartingCash.Text, out double startCash) || startCash < 0)
            { ShowMsg("⚠️ Enter a valid starting cash amount!", false); return; }

            // Use ISO format for storage
            string nowIso = DatabaseHelper.ToIsoDateTime(DateTime.Now);

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var cmd = new SQLiteCommand(@"
                    INSERT INTO Shifts (OpenedBy, OpenedAt, StartingCash, Status, Notes)
                    VALUES (@user, @at, @cash, 'Open', @notes)", con);
                cmd.Parameters.AddWithValue("@user", Session.Username);
                cmd.Parameters.AddWithValue("@at", nowIso);
                cmd.Parameters.AddWithValue("@cash", startCash);
                cmd.Parameters.AddWithValue("@notes", txtOpenNotes.Text.Trim());
                cmd.ExecuteNonQuery();
            }

            DatabaseHelper.Log("Shift Opened",
                $"Starting cash: LBP {startCash:N0}", "Shift");

            txtStartingCash.Text = "0";
            txtOpenNotes.Clear();
            ShowMsg($"✅ Shift opened at {DateTime.Now:dd/MM/yyyy HH:mm}", true);
            LoadShiftStatus();
            LoadHistory();
        }

        // ══════════════════════════════════════════════
        // CLOSE SHIFT - FIXED WITH ISO DATE
        // ══════════════════════════════════════════════
        private void btnCloseShift_Click(object sender, RoutedEventArgs e)
        {
            if (_activeShiftId == 0)
            { ShowMsg("⚠️ No active shift to close!", false); return; }

            if (!double.TryParse(txtCountedCash.Text, out double counted) || counted < 0)
            { ShowMsg("⚠️ Enter the counted cash amount!", false); return; }

            double startCash = 0, cashSales = 0;
            int saleCount = 0;
            double revenue = 0;
            string todayIso = DatabaseHelper.GetTodayIso();

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();

                // Starting cash
                object sc = new SQLiteCommand(
                    $"SELECT StartingCash FROM Shifts WHERE Id={_activeShiftId}", con)
                    .ExecuteScalar();
                startCash = sc != null ? Convert.ToDouble(sc) : 0;

                // Cash from sales today - ISO format
                var sr = new SQLiteCommand(@"
                    SELECT COUNT(*), IFNULL(SUM(TotalAmount),0), IFNULL(SUM(CashPaid),0)
                    FROM Sales 
                    WHERE Date LIKE @today", con);
                sr.Parameters.AddWithValue("@today", $"{todayIso}%");
                var srr = sr.ExecuteReader();
                if (srr.Read())
                {
                    saleCount = Convert.ToInt32(srr[0]);
                    revenue = Convert.ToDouble(srr[1]);
                    cashSales = Convert.ToDouble(srr[2]);
                }
                srr.Close();
            }

            double expected = startCash + cashSales;
            double difference = counted - expected;

            string diffText = difference >= 0
                ? $"+LBP {difference:N0} (surplus)"
                : $"-LBP {Math.Abs(difference):N0} (shortage)";

            if (MessageBox.Show(
                $"Close Shift #{_activeShiftId}?\n\n" +
                $"Starting cash:  LBP {startCash:N0}\n" +
                $"Cash sales:     LBP {cashSales:N0}\n" +
                $"Expected cash:  LBP {expected:N0}\n" +
                $"Counted cash:   LBP {counted:N0}\n" +
                $"Difference:     {diffText}\n\n" +
                $"Total sales: {saleCount}  |  Revenue: LBP {revenue:N0}",
                "Confirm Close Shift",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            string nowIso = DatabaseHelper.ToIsoDateTime(DateTime.Now);

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var cmd = new SQLiteCommand(@"
                    UPDATE Shifts SET
                        ClosedBy=@user, ClosedAt=@at,
                        ExpectedCash=@exp, CountedCash=@counted,
                        Difference=@diff, TotalSales=@sales,
                        TotalRevenue=@rev, Status='Closed'
                    WHERE Id=@id", con);
                cmd.Parameters.AddWithValue("@user", Session.Username);
                cmd.Parameters.AddWithValue("@at", nowIso);
                cmd.Parameters.AddWithValue("@exp", expected);
                cmd.Parameters.AddWithValue("@counted", counted);
                cmd.Parameters.AddWithValue("@diff", difference);
                cmd.Parameters.AddWithValue("@sales", saleCount);
                cmd.Parameters.AddWithValue("@rev", revenue);
                cmd.Parameters.AddWithValue("@id", _activeShiftId);
                cmd.ExecuteNonQuery();
            }

            DatabaseHelper.Log("Shift Closed",
                $"Expected: LBP {expected:N0} | Counted: LBP {counted:N0} | Diff: {diffText}",
                "Shift");

            txtCountedCash.Text = "0";
            lblDiffPreview.Content = "LBP 0";
            ShowMsg($"✅ Shift closed. Difference: {diffText}", true);
            LoadShiftStatus();
            LoadHistory();
        }

        // ══════════════════════════════════════════════
        // LIVE DIFFERENCE PREVIEW - FIXED
        // ══════════════════════════════════════════════
        private void txtCountedCash_Changed(object sender, TextChangedEventArgs e)
        {
            if (_activeShiftId == 0) return;
            if (!double.TryParse(txtCountedCash.Text, out double counted)) return;

            double startCash = 0, cashSales = 0;
            string todayIso = DatabaseHelper.GetTodayIso();

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                object sc = new SQLiteCommand(
                    $"SELECT StartingCash FROM Shifts WHERE Id={_activeShiftId}", con)
                    .ExecuteScalar();
                startCash = sc != null ? Convert.ToDouble(sc) : 0;

                var sr = new SQLiteCommand(
                    "SELECT IFNULL(SUM(CashPaid),0) FROM Sales WHERE Date LIKE @today", con);
                sr.Parameters.AddWithValue("@today", $"{todayIso}%");
                cashSales = Convert.ToDouble(sr.ExecuteScalar());
            }

            double expected = startCash + cashSales;
            double diff = counted - expected;

            lblDiffPreview.Content = diff >= 0
                ? $"+LBP {diff:N0}" : $"-LBP {Math.Abs(diff):N0}";
            lblDiffPreview.Foreground = diff >= 0
                ? new SolidColorBrush(Color.FromRgb(16, 124, 16))
                : new SolidColorBrush(Color.FromRgb(196, 43, 28));
        }

        // ══════════════════════════════════════════════
        // LOAD HISTORY
        // ══════════════════════════════════════════════
        void LoadHistory()
        {
            lvShifts.Items.Clear();
            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var r = new SQLiteCommand(
                    "SELECT * FROM Shifts ORDER BY Id DESC", con).ExecuteReader();
                while (r.Read())
                    lvShifts.Items.Add(new ShiftRow
                    {
                        Id = Convert.ToInt32(r["Id"]),
                        OpenedBy = r["OpenedBy"].ToString(),
                        OpenedAt = r["OpenedAt"].ToString(),
                        ClosedAt = r["ClosedAt"].ToString(),
                        StartingCash = Convert.ToDouble(r["StartingCash"]),
                        ExpectedCash = Convert.ToDouble(r["ExpectedCash"]),
                        CountedCash = Convert.ToDouble(r["CountedCash"]),
                        Difference = Convert.ToDouble(r["Difference"]),
                        TotalSales = Convert.ToInt32(r["TotalSales"]),
                        TotalRevenue = Convert.ToDouble(r["TotalRevenue"]),
                        Status = r["Status"].ToString()
                    });
                r.Close();
            }
        }

        // ══════════════════════════════════════════════
        // X-REPORT - COMPLETELY FIXED
        // ══════════════════════════════════════════════
        private void btnXReport_Click(object sender, RoutedEventArgs e)
        {
            if (_activeShiftId == 0)
            {
                ShowMsg("⚠️ No active shift! Open a shift first.", false);
                return;
            }

            var xReport = GenerateXReport();

            if (_debugMode)
            {
                Debug.WriteLine($"[X-Report] Shift: {xReport.ShiftId}, Cashier: {xReport.Cashier}");
                Debug.WriteLine($"[X-Report] Transactions: {xReport.TransactionCount}, Revenue: {xReport.TotalRevenue}");
            }

            // Check if we got any data
            if (xReport.TransactionCount == 0)
            {
                var result = MessageBox.Show(
                    "No sales found for the current shift.\n\n" +
                    "Possible causes:\n" +
                    "• No sales have been made yet\n" +
                    "• Sales were made by a different cashier\n" +
                    "• Date format issue\n\n" +
                    "Show debug information?",
                    "No Sales Data",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    ShowDebugInfo();
                }
            }

            var preview = new XReportWindow();
            preview.Owner = this;
            preview.ShowDialog();
        }

        private XReportData GenerateXReport()
        {
            string todayIso = DatabaseHelper.GetTodayIso();
            var data = new XReportData
            {
                ShiftId = _activeShiftId,
                GeneratedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                Cashier = Session.Username
            };

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();

                // Get shift info
                var shiftCmd = new SQLiteCommand("SELECT * FROM Shifts WHERE Id=@id", con);
                shiftCmd.Parameters.AddWithValue("@id", _activeShiftId);
                var sr = shiftCmd.ExecuteReader();
                if (sr.Read())
                {
                    data.OpenedAt = sr["OpenedAt"].ToString();
                    data.StartingCash = Convert.ToDouble(sr["StartingCash"]);
                }
                sr.Close();

                // Get sales data - ISO format, filter by date AND cashier
                var salesCmd = new SQLiteCommand(@"
                    SELECT COUNT(*) as cnt,
                           IFNULL(SUM(TotalAmount),0) as total,
                           IFNULL(SUM(CashPaid),0) as cash,
                           IFNULL(SUM(CardPaid),0) as card,
                           IFNULL(SUM(ChangeDue),0) as change,
                           IFNULL(SUM(PointsEarned),0) as points
                    FROM Sales 
                    WHERE Date LIKE @today 
                      AND (CashierName=@cashier OR CashierName IS NULL OR CashierName='')", con);
                salesCmd.Parameters.AddWithValue("@today", $"{todayIso}%");
                salesCmd.Parameters.AddWithValue("@cashier", Session.Username);

                var salesR = salesCmd.ExecuteReader();
                if (salesR.Read())
                {
                    data.TransactionCount = Convert.ToInt32(salesR["cnt"]);
                    data.TotalRevenue = Convert.ToDouble(salesR["total"]);
                    data.CashSales = Convert.ToDouble(salesR["cash"]);
                    data.CardSales = Convert.ToDouble(salesR["card"]);
                    data.ChangeGiven = Convert.ToDouble(salesR["change"]);
                    data.PointsAwarded = Convert.ToInt32(salesR["points"]);
                }
                salesR.Close();

                // Get voids - ISO format
                var voidCmd = new SQLiteCommand(@"
                    SELECT COUNT(*) FROM VoidedSales 
                    WHERE Date LIKE @today 
                      AND (CashierName=@cashier OR CashierName IS NULL OR CashierName='')", con);
                voidCmd.Parameters.AddWithValue("@today", $"{todayIso}%");
                voidCmd.Parameters.AddWithValue("@cashier", Session.Username);
                data.VoidCount = Convert.ToInt32(voidCmd.ExecuteScalar());

                // Get returns - ISO format
                var retCmd = new SQLiteCommand(@"
                    SELECT COUNT(*), IFNULL(SUM(RefundAmount),0) 
                    FROM Returns 
                    WHERE ReturnDate LIKE @today 
                      AND (CashierName=@cashier OR CashierName IS NULL OR CashierName='')", con);
                retCmd.Parameters.AddWithValue("@today", $"{todayIso}%");
                retCmd.Parameters.AddWithValue("@cashier", Session.Username);
                var retR = retCmd.ExecuteReader();
                if (retR.Read())
                {
                    data.ReturnCount = Convert.ToInt32(retR[0]);
                    data.ReturnAmount = retR[1] == DBNull.Value ? 0 : Convert.ToDouble(retR[1]);
                }
                retR.Close();

                // Get top 5 products - ISO format
                var topCmd = new SQLiteCommand(@"
                    SELECT p.Name, SUM(si.Quantity) as qty, SUM(si.Price) as rev
                    FROM SaleItems si
                    JOIN Products p ON si.ProductId = p.Id
                    JOIN Sales s ON si.SaleId = s.Id
                    WHERE s.Date LIKE @today 
                      AND (s.CashierName=@cashier OR s.CashierName IS NULL OR s.CashierName='')
                    GROUP BY p.Name
                    ORDER BY qty DESC
                    LIMIT 5", con);
                topCmd.Parameters.AddWithValue("@today", $"{todayIso}%");
                topCmd.Parameters.AddWithValue("@cashier", Session.Username);
                var topR = topCmd.ExecuteReader();
                while (topR.Read())
                {
                    data.TopProducts.Add(new TopProduct
                    {
                        Name = topR["Name"].ToString(),
                        Quantity = Convert.ToInt32(topR["qty"]),
                        Revenue = Convert.ToDouble(topR["rev"])
                    });
                }
                topR.Close();
            }

            // Calculate expected cash
            data.ExpectedCash = data.StartingCash + data.CashSales - data.ChangeGiven;

            return data;
        }

        // ══════════════════════════════════════════════
        // DEBUG HELPERS
        // ══════════════════════════════════════════════
        private void ShowDebugInfo()
        {
            try
            {
                using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    con.Open();
                    string todayIso = DatabaseHelper.GetTodayIso();

                    // Check all sales today
                    var allSalesCmd = new SQLiteCommand(@"
                        SELECT CashierName, COUNT(*) as cnt 
                        FROM Sales 
                        WHERE Date LIKE @today 
                        GROUP BY CashierName", con);
                    allSalesCmd.Parameters.AddWithValue("@today", $"{todayIso}%");

                    var reader = allSalesCmd.ExecuteReader();
                    string msg = $"Today's sales by cashier (ISO date: {todayIso}):\n\n";
                    while (reader.Read())
                    {
                        string cashier = reader["CashierName"]?.ToString() ?? "(null)";
                        int count = Convert.ToInt32(reader["cnt"]);
                        msg += $"  '{cashier}': {count} sales\n";
                    }
                    reader.Close();

                    msg += $"\nCurrent user: '{Session.Username}'";
                    MessageBox.Show(msg, "Debug Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Debug error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        void ShowMsg(string msg, bool ok)
        {
            lblMessage.Content = msg;
            lblMessage.Foreground = ok
                ? new SolidColorBrush(Color.FromRgb(16, 124, 16))
                : new SolidColorBrush(Color.FromRgb(196, 43, 28));
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}