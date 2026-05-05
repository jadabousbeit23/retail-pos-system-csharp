using POSSystem.Database;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using iTextDocument = iTextSharp.text.Document;
using iTextParagraph = iTextSharp.text.Paragraph;
using iTextChunk = iTextSharp.text.Chunk;
using iTextFontFactory = iTextSharp.text.FontFactory;
using iTextBaseColor = iTextSharp.text.BaseColor;
using iTextPageSize = iTextSharp.text.PageSize;
using iTextPdfWriter = iTextSharp.text.pdf.PdfWriter;

namespace POSSystem.Windows
{
    public partial class ZReportWindow : Window
    {
        private ZReportData _data;
        private bool _dayClosed = false;
        private double _exchangeRate = 89000;

        // Event to notify other windows that day was closed
        public static event EventHandler DayClosed;

        public ZReportWindow(int salesCount, double totalAmount, Dictionary<string, double> paymentTotals)
        {
            InitializeComponent();
            lblDate.Content = $"Date: {DateTime.Now:dd/MM/yyyy}";
            LoadExchangeRate();

            _data = new ZReportData
            {
                Date = DateTime.Now.ToString("dd/MM/yyyy"),
                GeneratedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                TotalTransactions = salesCount,
                TotalRevenue = totalAmount,
                CashSales = paymentTotals.ContainsKey("Cash") ? paymentTotals["Cash"] : 0,
                CardSales = paymentTotals.ContainsKey("Card") ? paymentTotals["Card"] : 0
            };

            double otherPayments = 0;
            foreach (var kvp in paymentTotals)
            {
                if (kvp.Key != "Cash" && kvp.Key != "Card")
                {
                    otherPayments += kvp.Value;
                }
            }
            _data.OtherPayments = otherPayments;

            LoadAdditionalData();
            BuildReport();
        }

        public ZReportWindow()
        {
            InitializeComponent();
            lblDate.Content = $"Date: {DateTime.Now:dd/MM/yyyy}";
            LoadExchangeRate();
            _data = GenerateZReport();
            BuildReport();
        }

        private void LoadExchangeRate()
        {
            try
            {
                using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    con.Open();
                    var cmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key='ExchangeRate'", con);
                    var result = cmd.ExecuteScalar();
                    if (result != null && double.TryParse(result.ToString(), out double rate))
                    {
                        _exchangeRate = rate;
                    }
                }
            }
            catch { }
        }

        private void LoadAdditionalData()
        {
            string todayIso = DateTime.Now.ToString("yyyy-MM-dd");

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();

                var salesCmd = new SQLiteCommand(@"
                    SELECT COUNT(DISTINCT CashierName) as cashiers,
                           IFNULL(SUM(ChangeDue),0) as change
                    FROM Sales 
                    WHERE Date LIKE @today", con);
                salesCmd.Parameters.AddWithValue("@today", $"{todayIso}%");

                var sr = salesCmd.ExecuteReader();
                if (sr.Read())
                {
                    _data.ActiveCashiers = Convert.ToInt32(sr["cashiers"]);
                    _data.ChangeGiven = Convert.ToDouble(sr["change"]);
                }
                sr.Close();

                var shiftCmd = new SQLiteCommand(@"
                    SELECT COUNT(*), 
                           IFNULL(SUM(StartingCash),0),
                           IFNULL(SUM(ExpectedCash),0),
                           IFNULL(SUM(CountedCash),0),
                           IFNULL(SUM(Difference),0)
                    FROM Shifts 
                    WHERE OpenedAt LIKE @today", con);
                shiftCmd.Parameters.AddWithValue("@today", $"{todayIso}%");

                var shr = shiftCmd.ExecuteReader();
                if (shr.Read())
                {
                    _data.ShiftCount = Convert.ToInt32(shr[0]);
                    _data.TotalStartingCash = Convert.ToDouble(shr[1]);
                    _data.TotalExpectedCash = Convert.ToDouble(shr[2]);
                    _data.TotalCountedCash = Convert.ToDouble(shr[3]);
                    _data.TotalDifference = Convert.ToDouble(shr[4]);
                }
                shr.Close();

                var voidCmd = new SQLiteCommand(@"
                    SELECT COUNT(*) FROM VoidedSales 
                    WHERE Date LIKE @today", con);
                voidCmd.Parameters.AddWithValue("@today", $"{todayIso}%");
                _data.VoidCount = Convert.ToInt32(voidCmd.ExecuteScalar());

                var retCmd = new SQLiteCommand(@"
                    SELECT COUNT(*), IFNULL(SUM(RefundAmount),0) 
                    FROM Returns 
                    WHERE ReturnDate LIKE @today", con);
                retCmd.Parameters.AddWithValue("@today", $"{todayIso}%");
                var rr = retCmd.ExecuteReader();
                if (rr.Read())
                {
                    _data.ReturnCount = Convert.ToInt32(rr[0]);
                    _data.ReturnAmount = rr[1] == DBNull.Value ? 0 : Convert.ToDouble(rr[1]);
                }
                rr.Close();

                var discCmd = new SQLiteCommand(@"
                    SELECT IFNULL(SUM((SELECT SUM(Price) FROM SaleItems WHERE SaleId = s.Id) - s.TotalAmount), 0)
                    FROM Sales s
                    WHERE Date LIKE @today
                      AND s.TotalAmount < (SELECT SUM(Price) FROM SaleItems WHERE SaleId = s.Id)", con);
                discCmd.Parameters.AddWithValue("@today", $"{todayIso}%");
                _data.DiscountsGiven = Convert.ToDouble(discCmd.ExecuteScalar());

                // Get PRODUCTS SOLD TODAY ONLY (End of Day)
                var soldProductsCmd = new SQLiteCommand(@"
                    SELECT 
                        p.Id,
                        p.Name,
                        p.ProductCode,
                        p.Stock as CurrentStock,
                        p.MinStock,
                        IFNULL(SUM(si.Quantity), 0) as SoldQty,
                        IFNULL(SUM(si.Price), 0) as TotalRevenue,
                        p.CostPrice
                    FROM Products p
                    INNER JOIN SaleItems si ON si.ProductId = p.Id
                    INNER JOIN Sales s ON si.SaleId = s.Id AND s.Date LIKE @today
                    GROUP BY p.Id, p.Name, p.ProductCode, p.Stock, p.MinStock, p.CostPrice
                    HAVING SoldQty > 0
                    ORDER BY SoldQty DESC, p.Name", con);
                soldProductsCmd.Parameters.AddWithValue("@today", $"{todayIso}%");

                var pr = soldProductsCmd.ExecuteReader();
                while (pr.Read())
                {
                    _data.AllProducts.Add(new ProductSalesItem
                    {
                        ProductId = Convert.ToInt32(pr["Id"]),
                        Name = pr["Name"].ToString(),
                        ProductCode = pr["ProductCode"].ToString(),
                        CurrentStock = Convert.ToInt32(pr["CurrentStock"]),
                        MinStock = Convert.ToInt32(pr["MinStock"]),
                        SoldQuantity = Convert.ToInt32(pr["SoldQty"]),
                        Revenue = Convert.ToDouble(pr["TotalRevenue"]),
                        CostPrice = Convert.ToDouble(pr["CostPrice"])
                    });
                }
                pr.Close();

                var hourlyCmd = new SQLiteCommand(@"
                    SELECT SUBSTR(Date, 12, 2) as hour, 
                           COUNT(*) as cnt, 
                           SUM(TotalAmount) as amt
                    FROM Sales 
                    WHERE Date LIKE @today
                    GROUP BY hour
                    ORDER BY hour", con);
                hourlyCmd.Parameters.AddWithValue("@today", $"{todayIso}%");
                var hr = hourlyCmd.ExecuteReader();
                while (hr.Read())
                {
                    _data.HourlyBreakdown.Add(new HourlyData
                    {
                        Hour = hr["hour"].ToString(),
                        Transactions = Convert.ToInt32(hr["cnt"]),
                        Amount = hr["amt"] == DBNull.Value ? 0 : Convert.ToDouble(hr["amt"])
                    });
                }
                hr.Close();
            }
        }

        private ZReportData GenerateZReport()
        {
            string todayIso = DateTime.Now.ToString("yyyy-MM-dd");

            var data = new ZReportData
            {
                Date = DateTime.Now.ToString("dd/MM/yyyy"),
                GeneratedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")
            };

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();

                var retCmd = new SQLiteCommand(@"
                    SELECT COUNT(*), IFNULL(SUM(RefundAmount),0) 
                    FROM Returns 
                    WHERE ReturnDate LIKE @today", con);
                retCmd.Parameters.AddWithValue("@today", $"{todayIso}%");
                var rr = retCmd.ExecuteReader();
                double totalReturns = 0;
                int returnCount = 0;
                if (rr.Read())
                {
                    returnCount = Convert.ToInt32(rr[0]);
                    totalReturns = rr[1] == DBNull.Value ? 0 : Convert.ToDouble(rr[1]);
                }
                rr.Close();

                data.ReturnCount = returnCount;
                data.ReturnAmount = totalReturns;

                var salesCmd = new SQLiteCommand(@"
                    SELECT COUNT(*) as cnt,
                           IFNULL(SUM(TotalAmount),0) as total,
                           IFNULL(SUM(CashPaid),0) as cash,
                           IFNULL(SUM(CardPaid),0) as card,
                           IFNULL(SUM(ChangeDue),0) as change,
                           COUNT(DISTINCT CashierName) as cashiers
                    FROM Sales 
                    WHERE Date LIKE @today", con);
                salesCmd.Parameters.AddWithValue("@today", $"{todayIso}%");

                var sr = salesCmd.ExecuteReader();
                if (sr.Read())
                {
                    data.TotalTransactions = Convert.ToInt32(sr["cnt"]);
                    double grossSales = Convert.ToDouble(sr["total"]);
                    data.TotalRevenue = grossSales - totalReturns;
                    data.CashSales = Convert.ToDouble(sr["cash"]);
                    data.CardSales = Convert.ToDouble(sr["card"]);
                    data.ChangeGiven = Convert.ToDouble(sr["change"]);
                    data.ActiveCashiers = Convert.ToInt32(sr["cashiers"]);
                }
                sr.Close();

                var shiftCmd = new SQLiteCommand(@"
                    SELECT COUNT(*), 
                           IFNULL(SUM(StartingCash),0),
                           IFNULL(SUM(ExpectedCash),0),
                           IFNULL(SUM(CountedCash),0),
                           IFNULL(SUM(Difference),0)
                    FROM Shifts 
                    WHERE OpenedAt LIKE @today", con);
                shiftCmd.Parameters.AddWithValue("@today", $"{todayIso}%");

                var shr = shiftCmd.ExecuteReader();
                if (shr.Read())
                {
                    data.ShiftCount = Convert.ToInt32(shr[0]);
                    data.TotalStartingCash = Convert.ToDouble(shr[1]);
                    data.TotalExpectedCash = Convert.ToDouble(shr[2]);
                    data.TotalCountedCash = Convert.ToDouble(shr[3]);
                    data.TotalDifference = Convert.ToDouble(shr[4]);
                }
                shr.Close();

                var voidCmd = new SQLiteCommand(@"
                    SELECT COUNT(*) FROM VoidedSales 
                    WHERE Date LIKE @today", con);
                voidCmd.Parameters.AddWithValue("@today", $"{todayIso}%");
                data.VoidCount = Convert.ToInt32(voidCmd.ExecuteScalar());

                var discCmd = new SQLiteCommand(@"
                    SELECT IFNULL(SUM((SELECT SUM(Price) FROM SaleItems WHERE SaleId = s.Id) - s.TotalAmount), 0)
                    FROM Sales s
                    WHERE Date LIKE @today
                      AND s.TotalAmount < (SELECT SUM(Price) FROM SaleItems WHERE SaleId = s.Id)", con);
                discCmd.Parameters.AddWithValue("@today", $"{todayIso}%");
                data.DiscountsGiven = Convert.ToDouble(discCmd.ExecuteScalar());

                // Get PRODUCTS SOLD TODAY ONLY (End of Day)
                var soldProductsCmd = new SQLiteCommand(@"
                    SELECT 
                        p.Id,
                        p.Name,
                        p.ProductCode,
                        p.Stock as CurrentStock,
                        p.MinStock,
                        IFNULL(SUM(si.Quantity), 0) as SoldQty,
                        IFNULL(SUM(si.Price), 0) as TotalRevenue,
                        p.CostPrice
                    FROM Products p
                    INNER JOIN SaleItems si ON si.ProductId = p.Id
                    INNER JOIN Sales s ON si.SaleId = s.Id AND s.Date LIKE @today
                    GROUP BY p.Id, p.Name, p.ProductCode, p.Stock, p.MinStock, p.CostPrice
                    HAVING SoldQty > 0
                    ORDER BY SoldQty DESC, p.Name", con);
                soldProductsCmd.Parameters.AddWithValue("@today", $"{todayIso}%");

                var pr = soldProductsCmd.ExecuteReader();
                while (pr.Read())
                {
                    data.AllProducts.Add(new ProductSalesItem
                    {
                        ProductId = Convert.ToInt32(pr["Id"]),
                        Name = pr["Name"].ToString(),
                        ProductCode = pr["ProductCode"].ToString(),
                        CurrentStock = Convert.ToInt32(pr["CurrentStock"]),
                        MinStock = Convert.ToInt32(pr["MinStock"]),
                        SoldQuantity = Convert.ToInt32(pr["SoldQty"]),
                        Revenue = Convert.ToDouble(pr["TotalRevenue"]),
                        CostPrice = Convert.ToDouble(pr["CostPrice"])
                    });
                }
                pr.Close();

                var hourlyCmd = new SQLiteCommand(@"
                    SELECT SUBSTR(Date, 12, 2) as hour, 
                           COUNT(*) as cnt, 
                           SUM(TotalAmount) as amt
                    FROM Sales 
                    WHERE Date LIKE @today
                    GROUP BY hour
                    ORDER BY hour", con);
                hourlyCmd.Parameters.AddWithValue("@today", $"{todayIso}%");
                var hr = hourlyCmd.ExecuteReader();
                while (hr.Read())
                {
                    data.HourlyBreakdown.Add(new HourlyData
                    {
                        Hour = hr["hour"].ToString(),
                        Transactions = Convert.ToInt32(hr["cnt"]),
                        Amount = hr["amt"] == DBNull.Value ? 0 : Convert.ToDouble(hr["amt"])
                    });
                }
                hr.Close();
            }

            return data;
        }

        private string FormatDualCurrency(double lbpAmount)
        {
            double usdAmount = lbpAmount / _exchangeRate;
            return $"LBP {lbpAmount:N0}  |  ${usdAmount:N2}";
        }

        private string FormatUsdOnly(double lbpAmount)
        {
            double usdAmount = lbpAmount / _exchangeRate;
            return $"${usdAmount:N2}";
        }

        private void BuildReport()
        {
            spZReport.Children.Clear();

            // BOLD THEME styling
            AddLine($"Report Generated: {_data.GeneratedAt}", false, 12, "#666666");
            AddLine($"Active Cashiers: {_data.ActiveCashiers}  |  Shifts: {_data.ShiftCount}", true, 14, "#444444");
            AddLine($"Exchange Rate: 1 USD = {_exchangeRate:N0} LBP", true, 12, "#666666");
            AddSeparator();

            AddSection("📊 SALES SUMMARY");
            AddLine($"Total Transactions:     {_data.TotalTransactions}", true, 18);

            double grossSales = _data.TotalRevenue + _data.ReturnAmount;
            if (_data.ReturnAmount > 0)
            {
                AddLine($"Gross Sales:            {FormatDualCurrency(grossSales)}", true, 16);
                AddLine($"Less: Returns:          - LBP {_data.ReturnAmount:N0}", true, 16, "#B00000");
                AddLine($"═══════════════════════════════════════════════════════════", true, 14, "#2B6CC4");
            }

            AddLine($"Discounts Given:        - LBP {_data.DiscountsGiven:N0}", true, 16, _data.DiscountsGiven > 0 ? "#B00000" : "#1A1A1A");
            AddLine($"═══════════════════════════════════════════════════════════", true, 14, "#2B6CC4");
            AddLine($"NET SALES:              {FormatDualCurrency(_data.TotalRevenue)}", true, 20, "#1A7A1A");
            AddSeparator();

            AddSection("💳 PAYMENT BREAKDOWN");
            AddLine($"Cash:                   {FormatDualCurrency(_data.CashSales)}", true, 16);
            AddLine($"Card:                   {FormatDualCurrency(_data.CardSales)}", true, 16);
            if (_data.OtherPayments > 0)
            {
                AddLine($"Other Payments:         {FormatDualCurrency(_data.OtherPayments)}", true, 16);
            }
            AddLine($"Change Given:           LBP {_data.ChangeGiven:N0}", true, 16);
            AddSeparator();

            AddSection("🧾 CASH DRAWER SUMMARY");
            AddLine($"Total Starting Cash:    {FormatDualCurrency(_data.TotalStartingCash)}", true, 16);
            AddLine($"Total Expected Cash:    {FormatDualCurrency(_data.TotalExpectedCash)}", true, 16);
            AddLine($"Total Counted Cash:     {FormatDualCurrency(_data.TotalCountedCash)}", true, 16);
            AddLine($"═══════════════════════════════════════════════════════════", true, 14, "#2B6CC4");
            AddLine($"Total Difference:       {FormatDualCurrency(_data.TotalDifference)}", true, 18,
                _data.TotalDifference >= 0 ? "#107C10" : "#C42B1C");
            AddSeparator();

            AddSection("⚠️ ADJUSTMENTS");
            AddLine($"Voids:                  {_data.VoidCount} transaction(s)", true, 16, _data.VoidCount > 0 ? "#B00000" : "#1A1A1A");
            AddLine($"Returns:                {_data.ReturnCount} transaction(s)  LBP {_data.ReturnAmount:N0}", true, 16, _data.ReturnCount > 0 ? "#B00000" : "#1A1A1A");
            AddSeparator();

            if (_data.HourlyBreakdown.Count > 0)
            {
                AddSection("⏰ HOURLY BREAKDOWN");
                foreach (var h in _data.HourlyBreakdown)
                {
                    AddLine($"{h.Hour}:00  │  {h.Transactions,3} sales  │  {FormatDualCurrency(h.Amount)}", true, 14);
                }
                AddSeparator();
            }

            // PRODUCTS SOLD TODAY ONLY - FIXED COLUMN WIDTHS & BOLD
            if (_data.AllProducts.Count > 0)
            {
                AddSection($"📦 PRODUCTS SOLD TODAY ({_data.AllProducts.Count} items)");

                // FIXED: Proper column widths for alignment
                var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
                headerPanel.Children.Add(CreateHeaderText("Product", 200));
                headerPanel.Children.Add(CreateHeaderText("Code", 100));
                headerPanel.Children.Add(CreateHeaderText("Stock", 60));
                headerPanel.Children.Add(CreateHeaderText("Min", 50));
                headerPanel.Children.Add(CreateHeaderText("Sold", 50));
                headerPanel.Children.Add(CreateHeaderText("Revenue (LBP)", 120));
                headerPanel.Children.Add(CreateHeaderText("Revenue ($)", 100));
                spZReport.Children.Add(headerPanel);

                AddLine("────────────────────────────────────────────────────────────────────────────────", true, 12, "#CCCCCC");

                int totalSold = 0;
                double totalRevenue = 0;

                foreach (var p in _data.AllProducts)
                {
                    string stockColor = p.CurrentStock <= p.MinStock ? "#C42B1C" :
                                       p.CurrentStock <= p.MinStock * 2 ? "#FF9800" : "#1A7A1A";

                    // FIXED: Matching column widths with bold text
                    var rowPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };

                    rowPanel.Children.Add(CreateCellText(Truncate(p.Name, 25), 200, "#1A1A1A", true));
                    rowPanel.Children.Add(CreateCellText(p.ProductCode, 100, "#666666", true));
                    rowPanel.Children.Add(CreateCellText(p.CurrentStock.ToString(), 60, stockColor, true));
                    rowPanel.Children.Add(CreateCellText(p.MinStock.ToString(), 50, "#666666", true));
                    rowPanel.Children.Add(CreateCellText(p.SoldQuantity.ToString(), 50, "#2B6CC4", true));
                    rowPanel.Children.Add(CreateCellText($"LBP {p.Revenue:N0}", 120, "#1A1A1A", true));
                    rowPanel.Children.Add(CreateCellText(FormatUsdOnly(p.Revenue), 100, "#1A7A1A", true));

                    spZReport.Children.Add(rowPanel);

                    totalSold += p.SoldQuantity;
                    totalRevenue += p.Revenue;
                }

                AddLine("────────────────────────────────────────────────────────────────────────────────", true, 12, "#CCCCCC");

                // FIXED: Totals row with bold styling
                var totalsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
                totalsPanel.Children.Add(CreateCellText("TOTALS:", 200, "#2B6CC4", true));
                totalsPanel.Children.Add(CreateCellText("", 100, "#666666", true));
                totalsPanel.Children.Add(CreateCellText("", 60, "#666666", true));
                totalsPanel.Children.Add(CreateCellText("", 50, "#666666", true));
                totalsPanel.Children.Add(CreateCellText(totalSold.ToString(), 50, "#2B6CC4", true));
                totalsPanel.Children.Add(CreateCellText($"LBP {totalRevenue:N0}", 120, "#1A7A1A", true));
                totalsPanel.Children.Add(CreateCellText(FormatUsdOnly(totalRevenue), 100, "#1A7A1A", true));
                spZReport.Children.Add(totalsPanel);

                AddSeparator();
            }
            else
            {
                AddSection("📦 PRODUCTS SOLD TODAY");
                AddLine("No products sold today.", true, 14, "#666666");
                AddSeparator();
            }

            AddLine("═══════════════════════════════════════════════════════════", true, 16, "#2B6CC4");
            AddLine("═══ END OF DAY REPORT ═══", true, 18, "#2B6CC4");
            AddLine("This report finalizes the day's transactions.", true, 12, "#666666");
        }

        private string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength - 3) + "...";
        }

        private TextBlock CreateHeaderText(string text, double width)
        {
            return new TextBlock
            {
                Text = text,
                Width = width,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2B6CC4")),
                FontFamily = new FontFamily("Segoe UI, Tahoma"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
        }

        private TextBlock CreateCellText(string text, double width, string color, bool bold = false)
        {
            return new TextBlock
            {
                Text = text,
                Width = width,
                FontSize = 13,  // INCREASED for better readability
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                FontFamily = new FontFamily("Consolas, Courier New, Segoe UI"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Padding = new Thickness(0, 2, 0, 2)
            };
        }

        private void AddSection(string title)
        {
            var tb = new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2B6CC4")),
                FontFamily = new FontFamily("Segoe UI, Tahoma"),
                Margin = new Thickness(0, 16, 0, 8)
            };
            spZReport.Children.Add(tb);
        }

        private void AddLine(string text, bool bold, double size, string color = "#1A1A1A")
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = size,
                FontWeight = bold ? FontWeights.Bold : FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                FontFamily = new FontFamily("Consolas, Courier New, Segoe UI"),
                Margin = new Thickness(0, 3, 0, 3)
            };
            spZReport.Children.Add(tb);
        }

        private void AddSeparator()
        {
            spZReport.Children.Add(new Border
            {
                Height = 2,
                Background = new SolidColorBrush(Colors.Gray),
                Margin = new Thickness(0, 12, 0, 12)
            });
        }

        private void btnPrint_Click(object sender, RoutedEventArgs e)
        {
            var pd = new PrintDialog();
            if (pd.ShowDialog() == true)
            {
                MessageBox.Show("Z-Report sent to printer!", "Print", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnSavePdf_Click(object sender, RoutedEventArgs e)
        {
            var saveDlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"Z-Report-{DateTime.Now:yyyyMMdd}",
                DefaultExt = ".pdf",
                Filter = "PDF documents (.pdf)|*.pdf"
            };

            if (saveDlg.ShowDialog() == true)
            {
                try
                {
                    GeneratePdf(saveDlg.FileName);
                    MessageBox.Show($"Z-Report saved to:\n{saveDlg.FileName}", "PDF Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnCloseDay_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(
                "Are you ABSOLUTELY SURE you want to close the day?\n\n" +
                $"This will archive {_data.TotalTransactions} transactions totaling LBP {_data.TotalRevenue:N0} " +
                "to the Monthly and Yearly reports, then CLEAR all daily sales data.\n\n" +
                "⚠️ This action CANNOT be undone!",
                "🔒 FINAL CONFIRMATION",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
            {
                return;
            }

            string todayIso = DateTime.Now.ToString("yyyy-MM-dd");
            string month = DateTime.Now.ToString("MMMM");
            int year = DateTime.Now.Year;

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();

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
                    upd.Parameters.AddWithValue("@s", _data.TotalTransactions);
                    upd.Parameters.AddWithValue("@a", _data.TotalRevenue);
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
                    ins.Parameters.AddWithValue("@s", _data.TotalTransactions);
                    ins.Parameters.AddWithValue("@a", _data.TotalRevenue);
                    ins.ExecuteNonQuery();
                }

                var yCheck = new SQLiteCommand("SELECT Id FROM YearlyReports WHERE Year=@y", con);
                yCheck.Parameters.AddWithValue("@y", year);
                var yId = yCheck.ExecuteScalar();

                if (yId != null)
                {
                    var upd = new SQLiteCommand(
                        "UPDATE YearlyReports SET TotalSales=TotalSales+@s, TotalAmount=TotalAmount+@a WHERE Year=@y", con);
                    upd.Parameters.AddWithValue("@s", _data.TotalTransactions);
                    upd.Parameters.AddWithValue("@a", _data.TotalRevenue);
                    upd.Parameters.AddWithValue("@y", year);
                    upd.ExecuteNonQuery();
                }
                else
                {
                    var ins = new SQLiteCommand(
                        "INSERT INTO YearlyReports (Year,TotalSales,TotalAmount) VALUES (@y,@s,@a)", con);
                    ins.Parameters.AddWithValue("@y", year);
                    ins.Parameters.AddWithValue("@s", _data.TotalTransactions);
                    ins.Parameters.AddWithValue("@a", _data.TotalRevenue);
                    ins.ExecuteNonQuery();
                }

                var delCmd = new SQLiteCommand(
                    "DELETE FROM Sales WHERE Date LIKE @today", con);
                delCmd.Parameters.AddWithValue("@today", $"{todayIso}%");
                delCmd.ExecuteNonQuery();

                new SQLiteCommand("DELETE FROM SaleItems WHERE SaleId NOT IN (SELECT Id FROM Sales)", con).ExecuteNonQuery();
            }

            DatabaseHelper.Log("Z-Report", $"Day closed. Net Revenue: LBP {_data.TotalRevenue:N0} (Returns: LBP {_data.ReturnAmount:N0})", "Z-Report");

            _dayClosed = true;

            // TRIGGER EVENT to notify other windows to refresh
            DayClosed?.Invoke(this, EventArgs.Empty);

            MessageBox.Show(
                $"✅ Day closed successfully!\n\n" +
                $"Archived to:\n• Monthly: {month} {year}\n• Yearly: {year}\n\n" +
                $"Daily sales data has been cleared.",
                "Day Closed",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }

        private void GeneratePdf(string filename)
        {
            using (var doc = new iTextDocument(iTextPageSize.A4.Rotate(), 30, 30, 30, 30))
            {
                iTextPdfWriter.GetInstance(doc, new FileStream(filename, FileMode.Create));
                doc.Open();

                var titleFont = iTextFontFactory.GetFont(iTextFontFactory.HELVETICA_BOLD, 20, iTextBaseColor.BLUE);
                var sectionFont = iTextFontFactory.GetFont(iTextFontFactory.HELVETICA_BOLD, 12, iTextBaseColor.BLUE);
                var normalFont = iTextFontFactory.GetFont(iTextFontFactory.COURIER, 9);
                var boldFont = iTextFontFactory.GetFont(iTextFontFactory.COURIER_BOLD, 10);
                var headerFont = iTextFontFactory.GetFont(iTextFontFactory.HELVETICA_BOLD, 10, iTextBaseColor.BLUE);

                var titleParagraph = new iTextParagraph("Z-REPORT (END OF DAY)", titleFont);
                titleParagraph.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                doc.Add(titleParagraph);

                var subtitleParagraph = new iTextParagraph($"Date: {_data.Date} | Generated: {_data.GeneratedAt} | Rate: 1 USD = {_exchangeRate:N0} LBP", normalFont);
                subtitleParagraph.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                doc.Add(subtitleParagraph);
                doc.Add(iTextChunk.NEWLINE);

                doc.Add(new iTextParagraph("SALES SUMMARY", sectionFont));

                double grossSales = _data.TotalRevenue + _data.ReturnAmount;
                if (_data.ReturnAmount > 0)
                {
                    doc.Add(new iTextParagraph($"Gross Sales: LBP {grossSales:N0} (${grossSales / _exchangeRate:N2})", normalFont));
                    doc.Add(new iTextParagraph($"Returns: - LBP {_data.ReturnAmount:N0}", normalFont));
                    doc.Add(new iTextParagraph($"NET SALES: LBP {_data.TotalRevenue:N0} (${_data.TotalRevenue / _exchangeRate:N2})", boldFont));
                }
                else
                {
                    doc.Add(new iTextParagraph($"Net Sales: LBP {_data.TotalRevenue:N0} (${_data.TotalRevenue / _exchangeRate:N2})", boldFont));
                }

                doc.Add(new iTextParagraph($"Transactions: {_data.TotalTransactions}", normalFont));
                doc.Add(iTextChunk.NEWLINE);

                doc.Add(new iTextParagraph("PAYMENT BREAKDOWN", sectionFont));
                doc.Add(new iTextParagraph($"Cash: LBP {_data.CashSales:N0} (${_data.CashSales / _exchangeRate:N2})", normalFont));
                doc.Add(new iTextParagraph($"Card: LBP {_data.CardSales:N0} (${_data.CardSales / _exchangeRate:N2})", normalFont));
                if (_data.OtherPayments > 0)
                {
                    doc.Add(new iTextParagraph($"Other: LBP {_data.OtherPayments:N0} (${_data.OtherPayments / _exchangeRate:N2})", normalFont));
                }
                doc.Add(iTextChunk.NEWLINE);

                doc.Add(new iTextParagraph("CASH POSITION", sectionFont));
                doc.Add(new iTextParagraph($"Expected: LBP {_data.TotalExpectedCash:N0} (${_data.TotalExpectedCash / _exchangeRate:N2})", normalFont));
                doc.Add(new iTextParagraph($"Counted:  LBP {_data.TotalCountedCash:N0} (${_data.TotalCountedCash / _exchangeRate:N2})", normalFont));
                doc.Add(new iTextParagraph($"Difference: LBP {_data.TotalDifference:N0} (${_data.TotalDifference / _exchangeRate:N2})", boldFont));
                doc.Add(iTextChunk.NEWLINE);

                doc.Add(new iTextParagraph("ADJUSTMENTS", sectionFont));
                doc.Add(new iTextParagraph($"Voids: {_data.VoidCount}", normalFont));
                doc.Add(new iTextParagraph($"Returns: {_data.ReturnCount} (LBP {_data.ReturnAmount:N0})", normalFont));
                doc.Add(iTextChunk.NEWLINE);

                // PRODUCTS SOLD TODAY ONLY - PDF
                if (_data.AllProducts.Count > 0)
                {
                    doc.Add(new iTextParagraph("PRODUCTS SOLD TODAY", sectionFont));

                    var table = new iTextSharp.text.pdf.PdfPTable(7);
                    table.WidthPercentage = 100;
                    table.SetWidths(new float[] { 25f, 12f, 8f, 6f, 6f, 18f, 15f });

                    string[] headers = { "Product", "Code", "Stock", "Min", "Sold", "Revenue (LBP)", "Revenue ($)" };
                    foreach (var h in headers)
                    {
                        var cell = new iTextSharp.text.pdf.PdfPCell(new iTextParagraph(h, headerFont));
                        cell.BackgroundColor = new iTextBaseColor(200, 220, 255);
                        cell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                        table.AddCell(cell);
                    }

                    int totalSold = 0;
                    double totalRevenue = 0;

                    foreach (var p in _data.AllProducts)
                    {
                        table.AddCell(new iTextParagraph(p.Name, normalFont));
                        table.AddCell(new iTextParagraph(p.ProductCode, normalFont));

                        var stockCell = new iTextSharp.text.pdf.PdfPCell(new iTextParagraph(p.CurrentStock.ToString(), normalFont));
                        stockCell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                        if (p.CurrentStock <= p.MinStock)
                            stockCell.BackgroundColor = new iTextBaseColor(255, 200, 200);
                        else if (p.CurrentStock <= p.MinStock * 2)
                            stockCell.BackgroundColor = new iTextBaseColor(255, 230, 200);
                        table.AddCell(stockCell);

                        table.AddCell(new iTextParagraph(p.MinStock.ToString(), normalFont));

                        var soldCell = new iTextSharp.text.pdf.PdfPCell(new iTextParagraph(p.SoldQuantity.ToString(), normalFont));
                        soldCell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                        table.AddCell(soldCell);

                        var revLbpCell = new iTextSharp.text.pdf.PdfPCell(new iTextParagraph($"LBP {p.Revenue:N0}", normalFont));
                        revLbpCell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_RIGHT;
                        table.AddCell(revLbpCell);

                        var revUsdCell = new iTextSharp.text.pdf.PdfPCell(new iTextParagraph($"${p.Revenue / _exchangeRate:N2}", normalFont));
                        revUsdCell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_RIGHT;
                        table.AddCell(revUsdCell);

                        totalSold += p.SoldQuantity;
                        totalRevenue += p.Revenue;
                    }

                    table.AddCell(new iTextParagraph("TOTALS", boldFont));
                    table.AddCell("");
                    table.AddCell("");
                    table.AddCell("");
                    table.AddCell(new iTextParagraph(totalSold.ToString(), boldFont));

                    var totalLbpCell = new iTextSharp.text.pdf.PdfPCell(new iTextParagraph($"LBP {totalRevenue:N0}", boldFont));
                    totalLbpCell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_RIGHT;
                    table.AddCell(totalLbpCell);

                    var totalUsdCell = new iTextSharp.text.pdf.PdfPCell(new iTextParagraph($"${totalRevenue / _exchangeRate:N2}", boldFont));
                    totalUsdCell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_RIGHT;
                    table.AddCell(totalUsdCell);

                    doc.Add(table);
                    doc.Add(iTextChunk.NEWLINE);
                }
                else
                {
                    doc.Add(new iTextParagraph("No products sold today.", normalFont));
                    doc.Add(iTextChunk.NEWLINE);
                }

                var footerParagraph = new iTextParagraph("═══ END OF DAY ═══", normalFont);
                footerParagraph.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                doc.Add(footerParagraph);

                doc.Close();
            }
        }
    }

    // DATA CLASSES
    public class ZReportData
    {
        public string Date { get; set; }
        public string GeneratedAt { get; set; }
        public int TotalTransactions { get; set; }
        public double TotalRevenue { get; set; }
        public double CashSales { get; set; }
        public double CardSales { get; set; }
        public double OtherPayments { get; set; }
        public double ChangeGiven { get; set; }
        public int ActiveCashiers { get; set; }
        public int ShiftCount { get; set; }
        public double TotalStartingCash { get; set; }
        public double TotalExpectedCash { get; set; }
        public double TotalCountedCash { get; set; }
        public double TotalDifference { get; set; }
        public int VoidCount { get; set; }
        public int ReturnCount { get; set; }
        public double ReturnAmount { get; set; }
        public double DiscountsGiven { get; set; }
        public List<ProductSalesItem> AllProducts { get; set; } = new List<ProductSalesItem>();
        public List<HourlyData> HourlyBreakdown { get; set; } = new List<HourlyData>();
    }

    public class ProductSalesItem
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public string ProductCode { get; set; }
        public int CurrentStock { get; set; }
        public int MinStock { get; set; }
        public int SoldQuantity { get; set; }
        public double Revenue { get; set; }
        public double CostPrice { get; set; }
    }

    public class HourlyData
    {
        public string Hour { get; set; }
        public int Transactions { get; set; }
        public double Amount { get; set; }
    }
}