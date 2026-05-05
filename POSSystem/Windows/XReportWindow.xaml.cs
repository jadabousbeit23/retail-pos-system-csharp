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
    public partial class XReportWindow : Window
    {
        private ZReportData _data;
        private double _exchangeRate = 89000;

        public XReportWindow()
        {
            InitializeComponent();

            // Subscribe to day closed event to auto-refresh
            ZReportWindow.DayClosed += OnDayClosed;

            lblDate.Content = $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
            LoadExchangeRate();
            _data = GenerateXReport();
            BuildReport();
        }

        private void OnDayClosed(object sender, EventArgs e)
        {
            // Auto-refresh when day is closed
            Dispatcher.Invoke(() =>
            {
                _data = GenerateXReport();
                BuildReport();
                lblDate.Content = $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss} (Auto-refreshed after day close)";
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            // Unsubscribe from event when window closes
            ZReportWindow.DayClosed -= OnDayClosed;
            base.OnClosed(e);
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

        private double SafeDouble(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            decimal decimalValue = Convert.ToDecimal(value);
            return (double)decimalValue;
        }

        private ZReportData GenerateXReport()
        {
            string todayIso = DateTime.Now.ToString("yyyy-MM-dd");
            string nowTime = DateTime.Now.ToString("HH:mm:ss");

            var data = new ZReportData
            {
                Date = DateTime.Now.ToString("dd/MM/yyyy"),
                GeneratedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                TotalTransactions = 0,
                TotalRevenue = 0,
                CashSales = 0,
                CardSales = 0,
                ChangeGiven = 0,
                ActiveCashiers = 0,
                VoidCount = 0,
                ReturnCount = 0,
                ReturnAmount = 0,
                AllProducts = new List<ProductSalesItem>(),
                HourlyBreakdown = new List<HourlyData>()
            };

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();

                // Get returns first (up to current time)
                var retCmd = new SQLiteCommand(@"
                    SELECT COUNT(*), IFNULL(SUM(RefundAmount),0) 
                    FROM Returns 
                    WHERE ReturnDate LIKE @today 
                    AND ReturnDate <= @now", con);
                retCmd.Parameters.AddWithValue("@today", $"{todayIso}%");
                retCmd.Parameters.AddWithValue("@now", $"{todayIso} {nowTime}");

                var rr = retCmd.ExecuteReader();
                double totalReturns = 0;
                int returnCount = 0;
                if (rr.Read())
                {
                    returnCount = Convert.ToInt32(rr[0]);
                    totalReturns = rr[1] == DBNull.Value ? 0 : SafeDouble(rr[1]);
                }
                rr.Close();

                data.ReturnCount = returnCount;
                data.ReturnAmount = totalReturns;

                // Get sales UP TO CURRENT TIME ONLY
                var salesCmd = new SQLiteCommand(@"
                    SELECT COUNT(*) as cnt,
                           IFNULL(SUM(TotalAmount),0) as total,
                           IFNULL(SUM(CashPaid),0) as cash,
                           IFNULL(SUM(CardPaid),0) as card,
                           IFNULL(SUM(ChangeDue),0) as change,
                           COUNT(DISTINCT CashierName) as cashiers
                    FROM Sales 
                    WHERE Date LIKE @today 
                    AND Date <= @now", con);

                salesCmd.Parameters.AddWithValue("@today", $"{todayIso}%");
                salesCmd.Parameters.AddWithValue("@now", $"{todayIso} {nowTime}");

                var sr = salesCmd.ExecuteReader();
                if (sr.Read())
                {
                    data.TotalTransactions = Convert.ToInt32(sr["cnt"]);
                    double grossSales = SafeDouble(sr["total"]);
                    data.TotalRevenue = grossSales - totalReturns;
                    data.CashSales = SafeDouble(sr["cash"]);
                    data.CardSales = SafeDouble(sr["card"]);
                    data.ChangeGiven = SafeDouble(sr["change"]);
                    data.ActiveCashiers = Convert.ToInt32(sr["cashiers"]);
                }
                sr.Close();

                var voidCmd = new SQLiteCommand(@"
                    SELECT COUNT(*) FROM VoidedSales 
                    WHERE Date LIKE @today 
                    AND Date <= @now", con);
                voidCmd.Parameters.AddWithValue("@today", $"{todayIso}%");
                voidCmd.Parameters.AddWithValue("@now", $"{todayIso} {nowTime}");
                data.VoidCount = Convert.ToInt32(voidCmd.ExecuteScalar());

                // Get PRODUCTS SOLD TODAY (up to current time) ONLY
                var allProductsCmd = new SQLiteCommand(@"
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
                    INNER JOIN Sales s ON si.SaleId = s.Id 
                        AND s.Date LIKE @today 
                        AND s.Date <= @now
                    GROUP BY p.Id, p.Name, p.ProductCode, p.Stock, p.MinStock, p.CostPrice
                    HAVING SoldQty > 0
                    ORDER BY SoldQty DESC, p.Name", con);
                allProductsCmd.Parameters.AddWithValue("@today", $"{todayIso}%");
                allProductsCmd.Parameters.AddWithValue("@now", $"{todayIso} {nowTime}");

                var pr = allProductsCmd.ExecuteReader();
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

                // Hourly breakdown up to current time
                var hourlyCmd = new SQLiteCommand(@"
                    SELECT SUBSTR(Date, 12, 2) as hour, 
                           COUNT(*) as cnt, 
                           SUM(TotalAmount) as amt
                    FROM Sales 
                    WHERE Date LIKE @today
                    AND Date <= @now
                    AND CAST(SUBSTR(Date, 12, 2) AS INTEGER) <= CAST(@currentHour AS INTEGER)
                    GROUP BY hour
                    ORDER BY hour", con);
                hourlyCmd.Parameters.AddWithValue("@today", $"{todayIso}%");
                hourlyCmd.Parameters.AddWithValue("@now", $"{todayIso} {nowTime}");
                hourlyCmd.Parameters.AddWithValue("@currentHour", DateTime.Now.Hour.ToString("D2"));

                var hr = hourlyCmd.ExecuteReader();
                while (hr.Read())
                {
                    data.HourlyBreakdown.Add(new HourlyData
                    {
                        Hour = hr["hour"].ToString(),
                        Transactions = Convert.ToInt32(hr["cnt"]),
                        Amount = SafeDouble(hr["amt"])
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
            spXReport.Children.Clear();

            // BOLD THEME - Matching Z-Report style
            AddLine($"Report Type: Mid-Day Report", true, 16, "#1B5E20");
            AddLine($"Generated: {_data.GeneratedAt}", false, 12, "#666666");
            AddLine($"Current User: {Session.Username}", true, 14, "#444444");
            AddLine($"Exchange Rate: 1 USD = {_exchangeRate:N0} LBP", true, 13, "#666666");
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

            AddLine($"NET REVENUE:            {FormatDualCurrency(_data.TotalRevenue)}", true, 20, "#1A7A1A");
            AddSeparator();

            AddSection("💳 PAYMENT BREAKDOWN");
            AddLine($"Cash Sales:             {FormatDualCurrency(_data.CashSales)}", true, 16);
            AddLine($"Card Sales:             {FormatDualCurrency(_data.CardSales)}", true, 16);
            AddLine($"Change Given:           LBP {_data.ChangeGiven:N0}", true, 16);
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
                spXReport.Children.Add(headerPanel);

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

                    spXReport.Children.Add(rowPanel);

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
                spXReport.Children.Add(totalsPanel);

                AddSeparator();
            }
            else
            {
                AddSection("📦 PRODUCTS SOLD TODAY");
                AddLine("No products sold yet today.", true, 14, "#666666");
                AddSeparator();
            }

            AddLine("═══════════════════════════════════════════════════════════", true, 16, "#2B6CC4");
            AddLine("═══ MID-DAY REPORT ═══", true, 18, "#2B6CC4");
            AddLine("This report does NOT close the day.", true, 12, "#666666");
            AddLine("Use Z-Report to close day and finalize.", true, 12, "#666666");
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
            spXReport.Children.Add(tb);
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
            spXReport.Children.Add(tb);
        }

        private void AddSeparator()
        {
            spXReport.Children.Add(new Border
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
                MessageBox.Show("X-Report sent to printer!", "Print",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnSavePdf_Click(object sender, RoutedEventArgs e)
        {
            var saveDlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"X-Report-{DateTime.Now:yyyyMMdd-HHmmss}",
                DefaultExt = ".pdf",
                Filter = "PDF documents (.pdf)|*.pdf"
            };

            if (saveDlg.ShowDialog() == true)
            {
                try
                {
                    GeneratePdf(saveDlg.FileName);
                    MessageBox.Show($"X-Report saved to:\n{saveDlg.FileName}", "PDF Saved",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception)
                {
                    MessageBox.Show("Error saving PDF", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _data = GenerateXReport();
            BuildReport();
            lblDate.Content = $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";

            MessageBox.Show("Report refreshed with latest data!", "Refreshed",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
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

                var titleParagraph = new iTextParagraph("X-REPORT (MID-DAY REPORT)", titleFont);
                titleParagraph.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                doc.Add(titleParagraph);

                var subtitleParagraph = new iTextParagraph($"Generated: {_data.GeneratedAt} | Rate: 1 USD = {_exchangeRate:N0} LBP", normalFont);
                subtitleParagraph.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                doc.Add(subtitleParagraph);
                doc.Add(iTextChunk.NEWLINE);

                doc.Add(new iTextParagraph("SALES SUMMARY", sectionFont));

                double grossSales = _data.TotalRevenue + _data.ReturnAmount;
                if (_data.ReturnAmount > 0)
                {
                    doc.Add(new iTextParagraph($"Gross Sales: LBP {grossSales:N0} (${grossSales / _exchangeRate:N2})", normalFont));
                    doc.Add(new iTextParagraph($"Returns: - LBP {_data.ReturnAmount:N0}", normalFont));
                    doc.Add(new iTextParagraph($"NET REVENUE: LBP {_data.TotalRevenue:N0} (${_data.TotalRevenue / _exchangeRate:N2})", boldFont));
                }
                else
                {
                    doc.Add(new iTextParagraph($"Revenue: LBP {_data.TotalRevenue:N0} (${_data.TotalRevenue / _exchangeRate:N2})", boldFont));
                }

                doc.Add(new iTextParagraph($"Transactions: {_data.TotalTransactions}", normalFont));
                doc.Add(iTextChunk.NEWLINE);

                doc.Add(new iTextParagraph("PAYMENT BREAKDOWN", sectionFont));
                doc.Add(new iTextParagraph($"Cash: LBP {_data.CashSales:N0} (${_data.CashSales / _exchangeRate:N2})", normalFont));
                doc.Add(new iTextParagraph($"Card: LBP {_data.CardSales:N0} (${_data.CardSales / _exchangeRate:N2})", normalFont));
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

                    // Totals row
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
                    doc.Add(new iTextParagraph("No products sold yet today.", normalFont));
                    doc.Add(iTextChunk.NEWLINE);
                }

                var footerParagraph = new iTextParagraph("═══ MID-DAY REPORT ═══", normalFont);
                footerParagraph.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                doc.Add(footerParagraph);

                doc.Close();
            }
        }
    }
}