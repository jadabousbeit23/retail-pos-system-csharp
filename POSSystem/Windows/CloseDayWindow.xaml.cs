using Microsoft.Win32;
using POSSystem.Database;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace POSSystem.Windows
{
    public partial class CloseDayWindow : Window
    {
        private int _salesCount = 0;
        private double _totalAmount = 0;
        private Dictionary<string, double> _paymentTotals = new Dictionary<string, double>();

        public CloseDayWindow()
        {
            InitializeComponent();
            lblDate.Content = DateTime.Now.ToString("dddd, dd MMMM yyyy");
            lblCashier.Content = Session.Username;
            LoadDaySummary();
        }

        // ════════════════════════════════════════════════════════
        // LOADING WITH MINIMUM DISPLAY TIME
        // ════════════════════════════════════════════════════════
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
            btnCancel.IsEnabled = enabled;
            btnPrint.IsEnabled = enabled;
            btnExport.IsEnabled = enabled;
            btnCloseDay.IsEnabled = enabled;
        }

        // ════════════════════════════════════════════════════════
        // LOAD DAY SUMMARY (WITH LOADING)
        // ════════════════════════════════════════════════════════
        async void LoadDaySummary()
        {
            await ShowLoadingWithMinimumTime("Loading day summary...", async () =>
            {
                await Task.Run(() =>
                {
                    // FIXED: Use ISO 8601 format
                    string todayIso = DatabaseHelper.GetTodayIso();

                    using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
                    {
                        con.Open();

                        // Get sales count and total
                        var cmd = new SQLiteCommand(
                            "SELECT COUNT(*), IFNULL(SUM(TotalAmount),0) FROM Sales WHERE Date LIKE @d", con);
                        cmd.Parameters.AddWithValue("@d", $"{todayIso}%");
                        using (var r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                _salesCount = Convert.ToInt32(r[0]);
                                _totalAmount = Convert.ToDouble(r[1]);
                            }
                        }

                        // Get payment method breakdown
                        var payCmd = new SQLiteCommand(
                            "SELECT PaymentMethod, SUM(TotalAmount) FROM Sales WHERE Date LIKE @d GROUP BY PaymentMethod", con);
                        payCmd.Parameters.AddWithValue("@d", $"{todayIso}%");
                        using (var r = payCmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                string method = r[0].ToString();
                                double amount = Convert.ToDouble(r[1]);
                                _paymentTotals[method] = amount;
                            }
                        }
                    }

                    Dispatcher.Invoke(() =>
                    {
                        lblSalesCount.Content = _salesCount.ToString("N0");
                        lblTotalAmount.Content = CurrencyHelper.FormatLBP(_totalAmount);

                        // Build payment breakdown
                        spPaymentBreakdown.Children.Clear();
                        foreach (var kvp in _paymentTotals)
                        {
                            var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                            var lblMethod = new Label
                            {
                                Content = kvp.Key,
                                FontFamily = new FontFamily("Segoe UI, Tahoma"),
                                FontSize = 12,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                                Padding = new Thickness(0)
                            };

                            var lblAmount = new Label
                            {
                                Content = CurrencyHelper.FormatLBP(kvp.Value),
                                FontFamily = new FontFamily("Segoe UI, Tahoma"),
                                FontSize = 12,
                                FontWeight = FontWeights.Bold,
                                Foreground = new SolidColorBrush(Color.FromRgb(26, 122, 26)),
                                Padding = new Thickness(0)
                            };

                            Grid.SetColumn(lblMethod, 0);
                            Grid.SetColumn(lblAmount, 1);
                            grid.Children.Add(lblMethod);
                            grid.Children.Add(lblAmount);

                            spPaymentBreakdown.Children.Add(grid);
                        }

                        if (_paymentTotals.Count == 0)
                        {
                            spPaymentBreakdown.Children.Add(new Label
                            {
                                Content = "No sales today",
                                FontFamily = new FontFamily("Segoe UI, Tahoma"),
                                FontSize = 12,
                                Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                                Padding = new Thickness(0)
                            });
                        }
                    });
                });
            });
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnPrint_Click(object sender, RoutedEventArgs e)
        {
            // Pass data from CloseDayWindow to ZReportWindow
            var report = new ZReportWindow(_salesCount, _totalAmount, _paymentTotals);
            report.ShowDialog();
        }

        // ════════════════════════════════════════════════════════
        // EXPORT (WITH LOADING)
        // ════════════════════════════════════════════════════════
        private async void btnExport_Click(object sender, RoutedEventArgs e)
        {
            await ShowLoadingWithMinimumTime("Exporting data...", async () =>
            {
                await Task.Run(() =>
                {
                    try
                    {
                        var saveDialog = new SaveFileDialog
                        {
                            FileName = $"DayClose_{DateTime.Now:yyyyMMdd}",
                            DefaultExt = ".csv",
                            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
                        };

                        bool? result = null;
                        Dispatcher.Invoke(() => result = saveDialog.ShowDialog());

                        if (result == true)
                        {
                            var csv = new StringBuilder();
                            csv.AppendLine("Close Date," + DateTime.Now.ToString("dd/MM/yyyy"));
                            csv.AppendLine("Cashier," + Session.Username);
                            csv.AppendLine();
                            csv.AppendLine("Payment Method,Amount");

                            foreach (var kvp in _paymentTotals)
                            {
                                csv.AppendLine($"{kvp.Key},{kvp.Value:F2}");
                            }

                            csv.AppendLine();
                            csv.AppendLine($"Total Amount,{_totalAmount:F2}");
                            csv.AppendLine($"Sales Count,{_salesCount}");

                            File.WriteAllText(saveDialog.FileName, csv.ToString(), Encoding.UTF8);

                            Dispatcher.Invoke(() => MessageBox.Show("Export completed successfully!", "Export",
                                MessageBoxButton.OK, MessageBoxImage.Information));
                        }
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => MessageBox.Show($"Export failed: {ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error));
                    }
                });
            });
        }

        // ════════════════════════════════════════════════════════
        // CLOSE DAY - ARCHIVES TO MONTHLY/YEARLY AND CLEARS DAILY DATA
        // ════════════════════════════════════════════════════════
        private async void btnCloseDay_Click(object sender, RoutedEventArgs e)
        {
            if (_salesCount == 0)
            {
                if (MessageBox.Show("No sales today. Close day anyway?", "Confirm",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
            }
            else
            {
                if (MessageBox.Show($"Close day with {CurrencyHelper.FormatLBP(_totalAmount)} in sales?", "Confirm Close Day",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
            }

            await ShowLoadingWithMinimumTime("Closing day...", async () =>
            {
                await Task.Run(() =>
                {
                    try
                    {
                        string todayIso = DatabaseHelper.GetTodayIso();
                        string month = DateTime.Now.ToString("MMMM");
                        int year = DateTime.Now.Year;

                        using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
                        {
                            con.Open();

                            // Archive to Monthly Reports
                            var mCheck = new SQLiteCommand(
                                "SELECT Id FROM MonthlyReports WHERE Month=@m AND Year=@y", con);
                            mCheck.Parameters.AddWithValue("@m", month);
                            mCheck.Parameters.AddWithValue("@y", year);
                            var mId = mCheck.ExecuteScalar();

                            if (mId != null)
                            {
                                var upd = new SQLiteCommand(
                                    "UPDATE MonthlyReports SET TotalSales=TotalSales+@s, TotalAmount=TotalAmount+@a " +
                                    "WHERE Month=@m AND Year=@y", con);
                                upd.Parameters.AddWithValue("@s", _salesCount);
                                upd.Parameters.AddWithValue("@a", _totalAmount);
                                upd.Parameters.AddWithValue("@m", month);
                                upd.Parameters.AddWithValue("@y", year);
                                upd.ExecuteNonQuery();
                            }
                            else
                            {
                                var ins = new SQLiteCommand(
                                    "INSERT INTO MonthlyReports (Month,Year,TotalSales,TotalAmount) VALUES (@m,@y,@s,@a)", con);
                                ins.Parameters.AddWithValue("@m", month);
                                ins.Parameters.AddWithValue("@y", year);
                                ins.Parameters.AddWithValue("@s", _salesCount);
                                ins.Parameters.AddWithValue("@a", _totalAmount);
                                ins.ExecuteNonQuery();
                            }

                            // Archive to Yearly Reports
                            var yCheck = new SQLiteCommand("SELECT Id FROM YearlyReports WHERE Year=@y", con);
                            yCheck.Parameters.AddWithValue("@y", year);
                            var yId = yCheck.ExecuteScalar();

                            if (yId != null)
                            {
                                var upd = new SQLiteCommand(
                                    "UPDATE YearlyReports SET TotalSales=TotalSales+@s, TotalAmount=TotalAmount+@a WHERE Year=@y", con);
                                upd.Parameters.AddWithValue("@s", _salesCount);
                                upd.Parameters.AddWithValue("@a", _totalAmount);
                                upd.Parameters.AddWithValue("@y", year);
                                upd.ExecuteNonQuery();
                            }
                            else
                            {
                                var ins = new SQLiteCommand(
                                    "INSERT INTO YearlyReports (Year,TotalSales,TotalAmount) VALUES (@y,@s,@a)", con);
                                ins.Parameters.AddWithValue("@y", year);
                                ins.Parameters.AddWithValue("@s", _salesCount);
                                ins.Parameters.AddWithValue("@a", _totalAmount);
                                ins.ExecuteNonQuery();
                            }

                            // Delete today's sales using ISO format
                            var delCmd = new SQLiteCommand(
                                "DELETE FROM Sales WHERE Date LIKE @today", con);
                            delCmd.Parameters.AddWithValue("@today", $"{todayIso}%");
                            delCmd.ExecuteNonQuery();

                            // Clean up orphaned SaleItems
                            new SQLiteCommand("DELETE FROM SaleItems WHERE SaleId NOT IN (SELECT Id FROM Sales)", con).ExecuteNonQuery();

                            // Log the day close
                            DatabaseHelper.Log("CloseDay",
                                $"Day closed - {_salesCount} sales, {CurrencyHelper.FormatLBP(_totalAmount)}",
                                "Admin");
                        }

                        // Refresh all open windows
                        Dispatcher.Invoke(() =>
                        {
                            foreach (Window w in Application.Current.Windows)
                            {
                                if (w is MainWindow mw)
                                {
                                    mw.ClearDashboard();
                                    mw.LoadDashboard();
                                }
                            }

                            MessageBox.Show(
                                $"✅ Day closed successfully!\n\n" +
                                $"Archived: {_salesCount} sales\n" +
                                $"Total: {CurrencyHelper.FormatLBP(_totalAmount)}\n" +
                                $"→ Monthly: {month} {year}\n" +
                                $"→ Yearly: {year}",
                                "Day Closed",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);

                            DialogResult = true;
                            Close();
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => MessageBox.Show($"Error closing day: {ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error));
                    }
                });
            });
        }
    }
}