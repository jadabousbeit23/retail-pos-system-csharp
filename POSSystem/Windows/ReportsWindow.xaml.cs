using POSSystem.Database;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;

namespace POSSystem.Windows
{
    public class ReportRow
    {
        public int Id { get; set; }
        public string Date { get; set; }
        public double TotalAmount { get; set; }
        public string Extra { get; set; }
    }

    public partial class ReportsWindow : Window
    {
        string currentTab = "daily";

        public ReportsWindow()
        {
            InitializeComponent();
            LoadDaily();
        }

        // ══════════════════════════════════════
        // TAB SWITCHING
        // ══════════════════════════════════════
        private void btnTabDaily_Click(object sender, RoutedEventArgs e)
        {
            currentTab = "daily";
            btnTabDaily.Background = System.Windows.Media.Brushes.DodgerBlue;
            btnTabMonthly.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#37474F"));
            btnTabYearly.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#37474F"));
            pnlSearch.Visibility = Visibility.Visible;
            LoadDaily();
        }

        private void btnTabMonthly_Click(object sender, RoutedEventArgs e)
        {
            currentTab = "monthly";
            btnTabMonthly.Background = System.Windows.Media.Brushes.DodgerBlue;
            btnTabDaily.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#37474F"));
            btnTabYearly.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#37474F"));
            pnlSearch.Visibility = Visibility.Collapsed;
            LoadMonthly();
        }

        private void btnTabYearly_Click(object sender, RoutedEventArgs e)
        {
            currentTab = "yearly";
            btnTabYearly.Background = System.Windows.Media.Brushes.DodgerBlue;
            btnTabDaily.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#37474F"));
            btnTabMonthly.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#37474F"));
            pnlSearch.Visibility = Visibility.Collapsed;
            LoadYearly();
        }

        // ══════════════════════════════════════
        // DAILY - FIXED WITH ISO DATES
        // ══════════════════════════════════════
        void LoadDaily()
        {
            lvSales.Items.Clear();
            colDate.Header = "Date & Time";
            colAmount.Header = "Total Amount";
            colExtra.Header = "";
            lblPeriodLabel.Content = "TODAY'S SALES";
            lblRevLabel.Content = "TODAY'S REVENUE";
            lblPeriod.Content = DateTime.Now.ToString("MMM dd, yyyy");

            // Use ISO date format: yyyy-MM-dd
            string todayIso = DatabaseHelper.GetTodayIso();
            double revenue = 0;
            int count = 0;

            using (SQLiteConnection con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                SQLiteCommand cmd = new SQLiteCommand(
                    "SELECT * FROM Sales WHERE Date LIKE @today ORDER BY Id DESC", con);
                cmd.Parameters.AddWithValue("@today", $"{todayIso}%");

                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    double amt = Convert.ToDouble(reader["TotalAmount"]);
                    lvSales.Items.Add(new ReportRow
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        Date = reader["Date"].ToString(),
                        TotalAmount = amt,
                        Extra = ""
                    });
                    revenue += amt;
                    count++;
                }
                reader.Close();
            }
            lblTotalSales.Content = count.ToString();
            lblTotalRevenue.Content = $"LBP {revenue:N0}";
        }

        // ══════════════════════════════════════
        // MONTHLY
        // ══════════════════════════════════════
        void LoadMonthly()
        {
            lvSales.Items.Clear();
            colDate.Header = "Month";
            colAmount.Header = "Total Revenue";
            colExtra.Header = "Total Sales";
            lblPeriodLabel.Content = "THIS MONTH SALES";
            lblRevLabel.Content = "THIS MONTH REVENUE";
            lblPeriod.Content = DateTime.Now.ToString("MMMM yyyy");

            double totalRevenue = 0;
            int totalSales = 0;

            using (SQLiteConnection con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                SQLiteDataReader reader = new SQLiteCommand(
                    "SELECT * FROM MonthlyReports ORDER BY Year DESC, Month DESC", con).ExecuteReader();

                while (reader.Read())
                {
                    double amount = Convert.ToDouble(reader["TotalAmount"]);
                    int sales = Convert.ToInt32(reader["TotalSales"]);
                    lvSales.Items.Add(new ReportRow
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        Date = $"{reader["Month"]} {reader["Year"]}",
                        TotalAmount = amount,
                        Extra = $"{sales} sales"
                    });
                    totalRevenue += amount;
                    totalSales += sales;
                }
                reader.Close();
            }
            lblTotalSales.Content = totalSales.ToString();
            lblTotalRevenue.Content = $"LBP {totalRevenue:N0}";
        }

        // ══════════════════════════════════════
        // YEARLY
        // ══════════════════════════════════════
        void LoadYearly()
        {
            lvSales.Items.Clear();
            colDate.Header = "Year";
            colAmount.Header = "Total Revenue";
            colExtra.Header = "Total Sales";
            lblPeriodLabel.Content = "YEARLY SALES";
            lblRevLabel.Content = "YEARLY REVENUE";
            lblPeriod.Content = DateTime.Now.Year.ToString();

            double totalRevenue = 0;
            int totalSales = 0;

            using (SQLiteConnection con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                SQLiteDataReader reader = new SQLiteCommand(
                    "SELECT * FROM YearlyReports ORDER BY Year DESC", con).ExecuteReader();

                while (reader.Read())
                {
                    double amount = Convert.ToDouble(reader["TotalAmount"]);
                    int sales = Convert.ToInt32(reader["TotalSales"]);
                    lvSales.Items.Add(new ReportRow
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        Date = reader["Year"].ToString(),
                        TotalAmount = amount,
                        Extra = $"{sales} sales"
                    });
                    totalRevenue += amount;
                    totalSales += sales;
                }
                reader.Close();
            }
            lblTotalSales.Content = totalSales.ToString();
            lblTotalRevenue.Content = $"LBP {totalRevenue:N0}";
        }

        // ══════════════════════════════════════
        // EXPORT PDF
        // ══════════════════════════════════════
        private void btnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (currentTab == "daily")
            {
                List<SaleRow> rows = new List<SaleRow>();
                double revenue = 0;
                foreach (ReportRow r in lvSales.Items)
                {
                    rows.Add(new SaleRow
                    {
                        Id = r.Id,
                        Date = r.Date,
                        TotalAmount = r.TotalAmount
                    });
                    revenue += r.TotalAmount;
                }
                if (rows.Count == 0)
                {
                    MessageBox.Show("No data to export!");
                    return;
                }
                PdfExportHelper.ExportDailyReport(rows, revenue, rows.Count);
            }
            else if (currentTab == "monthly")
            {
                List<MonthRow> rows = new List<MonthRow>();
                double revenue = 0;
                int totalSales = 0;
                using (SQLiteConnection con = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    con.Open();
                    SQLiteDataReader reader = new SQLiteCommand(
                        "SELECT * FROM MonthlyReports ORDER BY Year DESC, Month DESC", con).ExecuteReader();

                    while (reader.Read())
                    {
                        double amt = Convert.ToDouble(reader["TotalAmount"]);
                        int s = Convert.ToInt32(reader["TotalSales"]);
                        rows.Add(new MonthRow
                        {
                            Month = reader["Month"].ToString(),
                            Year = Convert.ToInt32(reader["Year"]),
                            TotalSales = s,
                            TotalAmount = amt
                        });
                        revenue += amt;
                        totalSales += s;
                    }
                    reader.Close();
                }
                if (rows.Count == 0)
                {
                    MessageBox.Show("No data to export!");
                    return;
                }
                PdfExportHelper.ExportMonthlyReport(rows, revenue, totalSales);
            }
            else if (currentTab == "yearly")
            {
                List<YearRow> rows = new List<YearRow>();
                double revenue = 0;
                int totalSales = 0;
                using (SQLiteConnection con = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    con.Open();
                    SQLiteDataReader reader = new SQLiteCommand(
                        "SELECT * FROM YearlyReports ORDER BY Year DESC", con).ExecuteReader();

                    while (reader.Read())
                    {
                        double amt = Convert.ToDouble(reader["TotalAmount"]);
                        int s = Convert.ToInt32(reader["TotalSales"]);
                        rows.Add(new YearRow
                        {
                            Year = Convert.ToInt32(reader["Year"]),
                            TotalSales = s,
                            TotalAmount = amt
                        });
                        revenue += amt;
                        totalSales += s;
                    }
                    reader.Close();
                }
                if (rows.Count == 0)
                {
                    MessageBox.Show("No data to export!");
                    return;
                }
                PdfExportHelper.ExportYearlyReport(rows, revenue, totalSales);
            }
        }

        // ══════════════════════════════════════
        // EXPORT EXCEL
        // ══════════════════════════════════════
        private void btnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (currentTab == "daily")
            {
                List<SaleRow> rows = new List<SaleRow>();
                double revenue = 0;
                foreach (ReportRow r in lvSales.Items)
                {
                    rows.Add(new SaleRow
                    {
                        Id = r.Id,
                        Date = r.Date,
                        TotalAmount = r.TotalAmount
                    });
                    revenue += r.TotalAmount;
                }
                if (rows.Count == 0)
                {
                    MessageBox.Show("No data to export!");
                    return;
                }
                ExcelExportHelper.ExportDaily(rows, revenue, rows.Count);
            }
            else if (currentTab == "monthly")
            {
                List<MonthRow> rows = new List<MonthRow>();
                double revenue = 0;
                int totalSales = 0;
                using (SQLiteConnection con = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    con.Open();
                    SQLiteDataReader reader = new SQLiteCommand(
                        "SELECT * FROM MonthlyReports ORDER BY Year DESC, Month DESC", con).ExecuteReader();

                    while (reader.Read())
                    {
                        double amt = Convert.ToDouble(reader["TotalAmount"]);
                        int s = Convert.ToInt32(reader["TotalSales"]);
                        rows.Add(new MonthRow
                        {
                            Month = reader["Month"].ToString(),
                            Year = Convert.ToInt32(reader["Year"]),
                            TotalSales = s,
                            TotalAmount = amt
                        });
                        revenue += amt;
                        totalSales += s;
                    }
                    reader.Close();
                }
                if (rows.Count == 0)
                {
                    MessageBox.Show("No data to export!");
                    return;
                }
                ExcelExportHelper.ExportMonthly(rows, revenue, totalSales);
            }
            else if (currentTab == "yearly")
            {
                List<YearRow> rows = new List<YearRow>();
                double revenue = 0;
                int totalSales = 0;
                using (SQLiteConnection con = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    con.Open();
                    SQLiteDataReader reader = new SQLiteCommand(
                        "SELECT * FROM YearlyReports ORDER BY Year DESC", con).ExecuteReader();

                    while (reader.Read())
                    {
                        double amt = Convert.ToDouble(reader["TotalAmount"]);
                        int s = Convert.ToInt32(reader["TotalSales"]);
                        rows.Add(new YearRow
                        {
                            Year = Convert.ToInt32(reader["Year"]),
                            TotalSales = s,
                            TotalAmount = amt
                        });
                        revenue += amt;
                        totalSales += s;
                    }
                    reader.Close();
                }
                if (rows.Count == 0)
                {
                    MessageBox.Show("No data to export!");
                    return;
                }
                ExcelExportHelper.ExportYearly(rows, revenue, totalSales);
            }
        }

        // ══════════════════════════════════════
        // CLOSE DAY - FIXED WITH ISO DATES
        // ══════════════════════════════════════
        private void btnCloseDay_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult confirm = MessageBox.Show(
                "Close today's day?\n\n" +
                "This will archive today's sales to Monthly & Yearly reports\n" +
                "and clear the daily sales records.",
                "🔒 Close Day",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            // Use ISO date format
            string todayIso = DatabaseHelper.GetTodayIso();
            string month = DateTime.Now.ToString("MMMM");
            int year = DateTime.Now.Year;

            int salesCount = 0;
            double salesTotal = 0;

            using (SQLiteConnection con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();

                SQLiteCommand countCmd = new SQLiteCommand(
                    "SELECT COUNT(*), SUM(TotalAmount) FROM Sales WHERE Date LIKE @today", con);
                countCmd.Parameters.AddWithValue("@today", $"{todayIso}%");
                SQLiteDataReader cr = countCmd.ExecuteReader();

                if (cr.Read())
                {
                    salesCount = Convert.ToInt32(cr[0]);
                    object val = cr[1];
                    salesTotal = val == DBNull.Value ? 0 : Convert.ToDouble(val);
                }
                cr.Close();

                if (salesCount == 0)
                {
                    MessageBox.Show("No sales today to archive!");
                    return;
                }

                // Monthly
                SQLiteCommand mCheck = new SQLiteCommand(
                    "SELECT Id FROM MonthlyReports WHERE Month=@month AND Year=@year", con);
                mCheck.Parameters.AddWithValue("@month", month);
                mCheck.Parameters.AddWithValue("@year", year);
                object mExisting = mCheck.ExecuteScalar();

                if (mExisting != null)
                {
                    SQLiteCommand mUpdate = new SQLiteCommand(
                        "UPDATE MonthlyReports SET TotalSales=TotalSales+@s, TotalAmount=TotalAmount+@a " +
                        "WHERE Month=@month AND Year=@year", con);
                    mUpdate.Parameters.AddWithValue("@s", salesCount);
                    mUpdate.Parameters.AddWithValue("@a", salesTotal);
                    mUpdate.Parameters.AddWithValue("@month", month);
                    mUpdate.Parameters.AddWithValue("@year", year);
                    mUpdate.ExecuteNonQuery();
                }
                else
                {
                    SQLiteCommand mInsert = new SQLiteCommand(
                        "INSERT INTO MonthlyReports (Month, Year, TotalSales, TotalAmount) VALUES (@month, @year, @s, @a)", con);
                    mInsert.Parameters.AddWithValue("@month", month);
                    mInsert.Parameters.AddWithValue("@year", year);
                    mInsert.Parameters.AddWithValue("@s", salesCount);
                    mInsert.Parameters.AddWithValue("@a", salesTotal);
                    mInsert.ExecuteNonQuery();
                }

                // Yearly
                SQLiteCommand yCheck = new SQLiteCommand(
                    "SELECT Id FROM YearlyReports WHERE Year=@year", con);
                yCheck.Parameters.AddWithValue("@year", year);
                object yExisting = yCheck.ExecuteScalar();

                if (yExisting != null)
                {
                    SQLiteCommand yUpdate = new SQLiteCommand(
                        "UPDATE YearlyReports SET TotalSales=TotalSales+@s, TotalAmount=TotalAmount+@a WHERE Year=@year", con);
                    yUpdate.Parameters.AddWithValue("@s", salesCount);
                    yUpdate.Parameters.AddWithValue("@a", salesTotal);
                    yUpdate.Parameters.AddWithValue("@year", year);
                    yUpdate.ExecuteNonQuery();
                }
                else
                {
                    SQLiteCommand yInsert = new SQLiteCommand(
                        "INSERT INTO YearlyReports (Year, TotalSales, TotalAmount) VALUES (@year, @s, @a)", con);
                    yInsert.Parameters.AddWithValue("@year", year);
                    yInsert.Parameters.AddWithValue("@s", salesCount);
                    yInsert.Parameters.AddWithValue("@a", salesTotal);
                    yInsert.ExecuteNonQuery();
                }

                // Delete today's sales using ISO format
                SQLiteCommand delCmd = new SQLiteCommand(
                    "DELETE FROM Sales WHERE Date LIKE @today", con);
                delCmd.Parameters.AddWithValue("@today", $"{todayIso}%");
                delCmd.ExecuteNonQuery();

                // Clean up orphaned SaleItems
                new SQLiteCommand("DELETE FROM SaleItems WHERE SaleId NOT IN (SELECT Id FROM Sales)", con).ExecuteNonQuery();
            }

            MessageBox.Show(
                $"✅ Day closed successfully!\n\n" +
                $"Archived: {salesCount} sales (LBP {salesTotal:N0})\n" +
                $"→ Monthly: {month} {year}\n" +
                $"→ Yearly:  {year}",
                "Day Closed",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // Refresh the reports list
            LoadDaily();

            // Also refresh the main dashboard if it's open
            foreach (Window w in Application.Current.Windows)
            {
                if (w is POSSystem.MainWindow mw)
                {
                    mw.lblTodaySales.Content = "0";
                    mw.lblTodayRevenue.Content = "LBP 0,00";
                    mw.LoadDashboard();
                    break;
                }
            }
        }

        // ══════════════════════════════════════
        // SEARCH - FIXED WITH ISO DATES
        // ══════════════════════════════════════
        private void btnDateSearch_Click(object sender, RoutedEventArgs e)
        {
            if (txtDateSearch.Text.Trim() == "")
            {
                LoadDaily();
                return;
            }

            lvSales.Items.Clear();
            double revenue = 0;
            int count = 0;

            using (SQLiteConnection con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();

                // Support both ISO format (user types yyyy-MM-dd) and old format (dd/MM/yyyy)
                string searchTerm = txtDateSearch.Text.Trim();

                SQLiteCommand cmd = new SQLiteCommand(
                    "SELECT * FROM Sales WHERE Date LIKE @search ORDER BY Id DESC", con);
                cmd.Parameters.AddWithValue("@search", $"%{searchTerm}%");

                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    double amount = Convert.ToDouble(reader["TotalAmount"]);
                    lvSales.Items.Add(new ReportRow
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        Date = reader["Date"].ToString(),
                        TotalAmount = amount,
                        Extra = ""
                    });
                    revenue += amount;
                    count++;
                }
                reader.Close();
            }
            lblTotalSales.Content = count.ToString();
            lblTotalRevenue.Content = $"LBP {revenue:N0}";
        }

        private void btnShowAll_Click(object sender, RoutedEventArgs e)
        {
            txtDateSearch.Text = "";
            LoadDaily();
        }

        private void btnLoadReport_Click(object sender, RoutedEventArgs e)
        {
            LoadDaily();
        }

        // ══════════════════════════════════════
        // CLICK SALE — show receipt
        // ══════════════════════════════════════
        private void lvSales_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (currentTab != "daily") return;
            if (lvSales.SelectedItem == null) return;

            ReportRow row = (ReportRow)lvSales.SelectedItem;
            List<ReceiptItem> items = new List<ReceiptItem>();

            using (SQLiteConnection con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                SQLiteCommand cmd = new SQLiteCommand(
                    @"SELECT Products.Name, SaleItems.Quantity, SaleItems.Price
                      FROM SaleItems
                      JOIN Products ON SaleItems.ProductId = Products.Id
                      WHERE SaleItems.SaleId = @saleId", con);
                cmd.Parameters.AddWithValue("@saleId", row.Id);

                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    items.Add(new ReceiptItem
                    {
                        Name = reader["Name"].ToString(),
                        Quantity = Convert.ToInt32(reader["Quantity"]),
                        Price = Convert.ToDouble(reader["Price"])
                    });
                }
                reader.Close();
            }

            Sale sale = new Sale
            {
                Id = row.Id,
                Date = row.Date,
                TotalAmount = row.TotalAmount
            };

            ReceiptWindow receipt = new ReceiptWindow();
            receipt.LoadReceipt(sale, items);
            receipt.ShowDialog();
        }

        // Public refresh helper so other windows can notify this window
        public void RefreshCurrentTab()
        {
            if (currentTab == "daily") LoadDaily();
            else if (currentTab == "monthly") LoadMonthly();
            else if (currentTab == "yearly") LoadYearly();
        }

        // ══════════════════════════════════════
        // Z-REPORT (End of Day Report)
        // ══════════════════════════════════════
        private void btnZReport_Click(object sender, RoutedEventArgs e)
        {
            // Open Z-Report window for end-of-day summary
            var zReport = new ZReportWindow();
            zReport.Owner = this;

            // If day was closed, refresh the daily view
            if (zReport.ShowDialog() == true)
            {
                LoadDaily();
            }
        }
    }
}