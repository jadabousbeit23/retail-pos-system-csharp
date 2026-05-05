using POSSystem.Database;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;  // ← ADD THIS LINE

namespace POSSystem.Windows
{
    public partial class ExcelExportWindow : Window
    {
        public ExcelExportWindow()
        {
            InitializeComponent();
        }

        // ═══════════════════════════════════════════════════════════
        // LOADING WITH MINIMUM DISPLAY TIME
        // ═══════════════════════════════════════════════════════════
        private async Task ShowLoadingWithMinimumTime(string message, Func<Task> operation)
        {
            ShowLoading(message);
            SetButtonsEnabled(false);

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

                HideLoading();
                SetButtonsEnabled(true);
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

        void SetButtonsEnabled(bool enabled)
        {
            // Find all buttons in the window and disable/enable them
            foreach (var child in LogicalTreeHelper.GetChildren(this))
            {
                if (child is Button btn && btn.Name != "btnClose")
                {
                    btn.IsEnabled = enabled;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        // DAILY EXPORT
        // ═══════════════════════════════════════════════════════════
        private async void btnExportDaily_Click(object sender, RoutedEventArgs e)
        {
            await ShowLoadingWithMinimumTime("Exporting daily report...", async () =>
            {
                await Task.Run(() =>
                {
                    var rows = new List<SaleRow>();
                    double revenue = 0;
                    string todayIso = DatabaseHelper.GetTodayIso();

                    using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
                    {
                        con.Open();
                        var cmd = new SQLiteCommand(
                            "SELECT * FROM Sales WHERE Date LIKE @d ORDER BY Id DESC", con);
                        cmd.Parameters.AddWithValue("@d", $"{todayIso}%");
                        var r = cmd.ExecuteReader();
                        while (r.Read())
                        {
                            double amt = Convert.ToDouble(r["TotalAmount"]);
                            rows.Add(new SaleRow
                            {
                                Id = Convert.ToInt32(r["Id"]),
                                Date = r["Date"].ToString(),
                                TotalAmount = amt
                            });
                            revenue += amt;
                        }
                        r.Close();
                    }

                    if (rows.Count == 0)
                    {
                        Dispatcher.Invoke(() => MessageBox.Show("No sales today to export!"));
                        return;
                    }

                    Dispatcher.Invoke(() => ExcelExportHelper.ExportDaily(rows, revenue, rows.Count));
                });
            });
        }

        // ═══════════════════════════════════════════════════════════
        // MONTHLY EXPORT
        // ═══════════════════════════════════════════════════════════
        private async void btnExportMonthly_Click(object sender, RoutedEventArgs e)
        {
            await ShowLoadingWithMinimumTime("Exporting monthly report...", async () =>
            {
                await Task.Run(() =>
                {
                    var rows = new List<MonthRow>();
                    double revenue = 0;
                    int total = 0;

                    using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
                    {
                        con.Open();
                        var r = new SQLiteCommand(
                            "SELECT * FROM MonthlyReports ORDER BY Year DESC, Month DESC", con)
                            .ExecuteReader();
                        while (r.Read())
                        {
                            double amt = Convert.ToDouble(r["TotalAmount"]);
                            int s = Convert.ToInt32(r["TotalSales"]);
                            rows.Add(new MonthRow
                            {
                                Month = r["Month"].ToString(),
                                Year = Convert.ToInt32(r["Year"]),
                                TotalSales = s,
                                TotalAmount = amt
                            });
                            revenue += amt;
                            total += s;
                        }
                        r.Close();
                    }

                    if (rows.Count == 0)
                    {
                        Dispatcher.Invoke(() => MessageBox.Show("No monthly data available!"));
                        return;
                    }

                    Dispatcher.Invoke(() => ExcelExportHelper.ExportMonthly(rows, revenue, total));
                });
            });
        }

        // ═══════════════════════════════════════════════════════════
        // YEARLY EXPORT
        // ═══════════════════════════════════════════════════════════
        private async void btnExportYearly_Click(object sender, RoutedEventArgs e)
        {
            await ShowLoadingWithMinimumTime("Exporting yearly report...", async () =>
            {
                await Task.Run(() =>
                {
                    var rows = new List<YearRow>();
                    double revenue = 0;
                    int total = 0;

                    using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
                    {
                        con.Open();
                        var r = new SQLiteCommand(
                            "SELECT * FROM YearlyReports ORDER BY Year DESC", con)
                            .ExecuteReader();
                        while (r.Read())
                        {
                            double amt = Convert.ToDouble(r["TotalAmount"]);
                            int s = Convert.ToInt32(r["TotalSales"]);
                            rows.Add(new YearRow
                            {
                                Year = Convert.ToInt32(r["Year"]),
                                TotalSales = s,
                                TotalAmount = amt
                            });
                            revenue += amt;
                            total += s;
                        }
                        r.Close();
                    }

                    if (rows.Count == 0)
                    {
                        Dispatcher.Invoke(() => MessageBox.Show("No yearly data available!"));
                        return;
                    }

                    Dispatcher.Invoke(() => ExcelExportHelper.ExportYearly(rows, revenue, total));
                });
            });
        }

        // ═══════════════════════════════════════════════════════════
        // FULL HISTORY EXPORT
        // ═══════════════════════════════════════════════════════════
        private async void btnExportHistory_Click(object sender, RoutedEventArgs e)
        {
            await ShowLoadingWithMinimumTime("Exporting sales history...", async () =>
            {
                await Task.Run(() =>
                {
                    Dispatcher.Invoke(() => ExcelExportHelper.ExportSalesHistory());
                });
            });
        }

        // ═══════════════════════════════════════════════════════════
        // PRODUCTS EXPORT
        // ═══════════════════════════════════════════════════════════
        private async void btnExportProducts_Click(object sender, RoutedEventArgs e)
        {
            await ShowLoadingWithMinimumTime("Exporting products...", async () =>
            {
                await Task.Run(() =>
                {
                    Dispatcher.Invoke(() => ExcelExportHelper.ExportProducts());
                });
            });
        }

        // ═══════════════════════════════════════════════════════════
        // CLOSE
        // ═══════════════════════════════════════════════════════════
        private void btnClose_Click(object sender, RoutedEventArgs e)
            => this.Close();
    }
}