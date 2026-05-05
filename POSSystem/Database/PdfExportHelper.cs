using Microsoft.Win32;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;

namespace POSSystem.Database
{
    // ══════════════════════════════════════════════════════════
    // FONT RESOLVER
    // ══════════════════════════════════════════════════════════
    public class WindowsFontResolver : IFontResolver
    {
        public string DefaultFontName => "Arial";

        public byte[] GetFont(string faceName)
        {
            string fontsFolder = Environment.GetFolderPath(
                Environment.SpecialFolder.Fonts);
            string fileName;
            if (faceName == "Arial-Bold") fileName = "arialbd.ttf";
            else if (faceName == "Arial-Italic") fileName = "ariali.ttf";
            else fileName = "arial.ttf";

            string path = Path.Combine(fontsFolder, fileName);
            if (!File.Exists(path))
                path = Path.Combine(fontsFolder, "arial.ttf");
            return File.ReadAllBytes(path);
        }

        public FontResolverInfo ResolveTypeface(
            string familyName, bool isBold, bool isItalic)
        {
            if (familyName.Equals("Arial",
                StringComparison.OrdinalIgnoreCase))
            {
                if (isBold) return new FontResolverInfo("Arial-Bold");
                if (isItalic) return new FontResolverInfo("Arial-Italic");
                return new FontResolverInfo("Arial");
            }
            return new FontResolverInfo("Arial");
        }
    }

    // ══════════════════════════════════════════════════════════
    // EXTENDED SALE ROW — includes items for daily report
    // ══════════════════════════════════════════════════════════
    public class SaleRow
    {
        public int Id { get; set; }
        public string Date { get; set; }
        public double TotalAmount { get; set; }
        public List<SaleItemRow> Items { get; set; } = new List<SaleItemRow>();
    }

    public class SaleItemRow
    {
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public double Price { get; set; }
    }

    public class MonthRow
    {
        public string Month { get; set; }
        public int Year { get; set; }
        public int TotalSales { get; set; }
        public double TotalAmount { get; set; }
    }

    public class YearRow
    {
        public int Year { get; set; }
        public int TotalSales { get; set; }
        public double TotalAmount { get; set; }
    }

    // ══════════════════════════════════════════════════════════
    // PDF EXPORT HELPER
    // ══════════════════════════════════════════════════════════
    public class PdfExportHelper
    {
        // ── Colors ──
        static readonly XColor CNavy = XColor.FromArgb(10, 25, 47);
        static readonly XColor CDark = XColor.FromArgb(20, 40, 65);
        static readonly XColor CMid = XColor.FromArgb(27, 42, 59);
        static readonly XColor CBlue = XColor.FromArgb(21, 101, 192);
        static readonly XColor CLBlue = XColor.FromArgb(33, 150, 243);
        static readonly XColor CGold = XColor.FromArgb(255, 193, 7);
        static readonly XColor CGreen = XColor.FromArgb(46, 125, 50);
        static readonly XColor CLGreen = XColor.FromArgb(232, 245, 233);
        static readonly XColor CWhite = XColor.FromArgb(255, 255, 255);
        static readonly XColor CLGray = XColor.FromArgb(176, 190, 197);
        static readonly XColor CDGray = XColor.FromArgb(90, 110, 130);
        static readonly XColor CRowAlt = XColor.FromArgb(245, 248, 255);
        static readonly XColor CRowSub = XColor.FromArgb(250, 252, 255);
        static readonly XColor CAccent = XColor.FromArgb(232, 240, 254);
        static readonly XColor CBorder = XColor.FromArgb(210, 220, 235);

        // ── Lazy fonts ──
        static XFont _fT, _fH, _fSH, _fB, _fSm, _fBo, _fIt;
        static XFont FTitle => _fT ?? (_fT = new XFont("Arial", 22, XFontStyleEx.Bold));
        static XFont FHead => _fH ?? (_fH = new XFont("Arial", 14, XFontStyleEx.Bold));
        static XFont FSHead => _fSH ?? (_fSH = new XFont("Arial", 11, XFontStyleEx.Bold));
        static XFont FBody => _fB ?? (_fB = new XFont("Arial", 9, XFontStyleEx.Regular));
        static XFont FSmall => _fSm ?? (_fSm = new XFont("Arial", 8, XFontStyleEx.Regular));
        static XFont FBold => _fBo ?? (_fBo = new XFont("Arial", 9, XFontStyleEx.Bold));
        static XFont FItalic => _fIt ?? (_fIt = new XFont("Arial", 8, XFontStyleEx.Italic));

        // ── Page constants ──
        const double PageW = 595;
        const double PageH = 842;
        const double Margin = 45;
        const double ContentW = PageW - Margin * 2;

        // ══════════════════════════════════════════════════════
        // INITIALIZE — call from App.xaml.cs
        // ══════════════════════════════════════════════════════
        public static void Initialize()
        {
            if (GlobalFontSettings.FontResolver == null)
                GlobalFontSettings.FontResolver = new WindowsFontResolver();
        }

        // ══════════════════════════════════════════════════════
        // FORMAT DATE — DD/MM/YYYY
        // ══════════════════════════════════════════════════════
        static string FormatDate(string raw)
        {
            if (DateTime.TryParse(raw, out DateTime dt))
                return dt.ToString("dd/MM/yyyy  HH:mm");
            return raw;
        }

        static string FormatDateShort(string raw)
        {
            if (DateTime.TryParse(raw, out DateTime dt))
                return dt.ToString("dd/MM/yyyy");
            return raw;
        }

        // ══════════════════════════════════════════════════════
        // LOAD SALE ITEMS FROM DATABASE
        // ══════════════════════════════════════════════════════
        static List<SaleItemRow> LoadItems(int saleId)
        {
            List<SaleItemRow> items = new List<SaleItemRow>();
            using (SQLiteConnection con = new SQLiteConnection(
                DatabaseHelper.ConnectionString))
            {
                con.Open();
                SQLiteCommand cmd = new SQLiteCommand(@"
                    SELECT Products.Name, SaleItems.Quantity, SaleItems.Price
                    FROM SaleItems
                    JOIN Products ON SaleItems.ProductId = Products.Id
                    WHERE SaleItems.SaleId = @id", con);
                cmd.Parameters.AddWithValue("@id", saleId);
                SQLiteDataReader r = cmd.ExecuteReader();
                while (r.Read())
                    items.Add(new SaleItemRow
                    {
                        ProductName = r["Name"].ToString(),
                        Quantity = Convert.ToInt32(r["Quantity"]),
                        Price = Convert.ToDouble(r["Price"])
                    });
                r.Close();
            }
            return items;
        }

        // ══════════════════════════════════════════════════════
        // EXPORT DAILY REPORT — with full item breakdown
        // ══════════════════════════════════════════════════════
        public static string ExportDailyReport(
            List<SaleRow> sales, double totalRevenue, int totalCount)
        {
            // Load items for each sale
            foreach (SaleRow sale in sales)
                sale.Items = LoadItems(sale.Id);

            string path = GetSavePath("Daily_Report");
            if (path == null) return null;

            PdfDocument doc = new PdfDocument();
            doc.Info.Title = "Daily Sales Report";
            doc.Info.Author = Session.Username;

            PdfPage page = doc.AddPage();
            XGraphics gfx = XGraphics.FromPdfPage(page);
            double y = 0;

            // ── Header ──
            y = DrawPageHeader(gfx, "Daily Sales Report",
                $"Date: {DateTime.Now:dd/MM/yyyy}   |   " +
                $"Prepared by: {Session.Username}   |   " +
                $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}");

            // ── Summary cards ──
            y = DrawSummaryBar(gfx, y, new[]
            {
                ("TOTAL TRANSACTIONS", totalCount.ToString(),  CBlue),
                ("TOTAL REVENUE",      $"${totalRevenue:F2}", CGreen),
                ("AVERAGE SALE",       totalCount > 0
                    ? $"${totalRevenue / totalCount:F2}" : "$0.00", CMid),
                ("REPORT DATE",        DateTime.Now.ToString("dd/MM/yyyy"), CDark)
            });

            y += 10;

            // ── Section title ──
            y = DrawSectionTitle(gfx, y, "Transaction Details");

            // ── Each sale with its items ──
            foreach (SaleRow sale in sales)
            {
                // Check if we need a new page
                double estimatedHeight = 30 + sale.Items.Count * 18 + 20;
                if (y + estimatedHeight > PageH - 60)
                {
                    DrawFooter(gfx, doc.Pages.Count);
                    page = doc.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                    y = DrawPageHeader(gfx,
                        "Daily Sales Report (continued)",
                        $"Date: {DateTime.Now:dd/MM/yyyy}");
                }

                y = DrawSaleBlock(gfx, sale, y);
                y += 8;
            }

            // ── Grand total bar ──
            y += 5;
            DrawGrandTotal(gfx, y,
                $"Grand Total — {totalCount} Transactions", totalRevenue);

            DrawFooter(gfx, doc.Pages.Count);
            doc.Save(path);
            OpenPdf(path);
            return path;
        }

        // ══════════════════════════════════════════════════════
        // EXPORT MONTHLY REPORT
        // ══════════════════════════════════════════════════════
        public static string ExportMonthlyReport(
            List<MonthRow> rows, double totalRevenue, int totalSales)
        {
            string path = GetSavePath("Monthly_Report");
            if (path == null) return null;

            PdfDocument doc = new PdfDocument();
            doc.Info.Title = "Monthly Sales Report";
            doc.Info.Author = Session.Username;

            PdfPage page = doc.AddPage();
            XGraphics gfx = XGraphics.FromPdfPage(page);
            double y = 0;

            y = DrawPageHeader(gfx, "Monthly Sales Report",
                $"Prepared by: {Session.Username}   |   " +
                $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}");

            y = DrawSummaryBar(gfx, y, new[]
            {
                ("TOTAL MONTHS",   rows.Count.ToString(),  CBlue),
                ("TOTAL SALES",    totalSales.ToString(),  CMid),
                ("TOTAL REVENUE",  $"${totalRevenue:F2}", CGreen),
                ("AVG / MONTH",    rows.Count > 0
                    ? $"${totalRevenue / rows.Count:F2}" : "$0.00", CDark)
            });

            y += 10;
            y = DrawSectionTitle(gfx, y, "Monthly Breakdown");

            // Table header
            double[] mCols = { Margin, Margin + 120, Margin + 240, Margin + 360, Margin + 460 };
            double[] mWids = { 115, 115, 115, 100, ContentW - 460 };
            y = DrawTableHeader(gfx, y,
                new[] { "Month", "Year", "Transactions", "Revenue", "Avg / Sale" },
                mCols, mWids);

            bool alt = false;
            foreach (MonthRow row in rows)
            {
                double avg = row.TotalSales > 0
                    ? row.TotalAmount / row.TotalSales : 0;
                y = DrawTableRow(gfx, y, alt,
                    new[] {
                        row.Month,
                        row.Year.ToString(),
                        row.TotalSales.ToString(),
                        $"${row.TotalAmount:F2}",
                        $"${avg:F2}"
                    },
                    mCols, mWids,
                    new[] { false, false, false, true, false });
                alt = !alt;
            }

            y += 6;
            DrawGrandTotal(gfx, y,
                $"Grand Total — {rows.Count} Months / {totalSales} Sales",
                totalRevenue);

            DrawFooter(gfx, doc.Pages.Count);
            doc.Save(path);
            OpenPdf(path);
            return path;
        }

        // ══════════════════════════════════════════════════════
        // EXPORT YEARLY REPORT
        // ══════════════════════════════════════════════════════
        public static string ExportYearlyReport(
            List<YearRow> rows, double totalRevenue, int totalSales)
        {
            string path = GetSavePath("Yearly_Report");
            if (path == null) return null;

            PdfDocument doc = new PdfDocument();
            doc.Info.Title = "Yearly Sales Report";
            doc.Info.Author = Session.Username;

            PdfPage page = doc.AddPage();
            XGraphics gfx = XGraphics.FromPdfPage(page);
            double y = 0;

            y = DrawPageHeader(gfx, "Yearly Sales Report",
                $"Prepared by: {Session.Username}   |   " +
                $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}");

            y = DrawSummaryBar(gfx, y, new[]
            {
                ("YEARS ON RECORD", rows.Count.ToString(),   CBlue),
                ("TOTAL SALES",     totalSales.ToString(),   CMid),
                ("TOTAL REVENUE",   $"${totalRevenue:F2}",  CGreen),
                ("AVG / YEAR",      rows.Count > 0
                    ? $"${totalRevenue / rows.Count:F2}" : "$0.00", CDark)
            });

            y += 10;
            y = DrawSectionTitle(gfx, y, "Yearly Breakdown");

            double[] yCols = { Margin, Margin + 140, Margin + 300, Margin + 430 };
            double[] yWids = { 135, 155, 125, ContentW - 430 };
            y = DrawTableHeader(gfx, y,
                new[] { "Year", "Total Transactions", "Total Revenue", "Avg / Sale" },
                yCols, yWids);

            bool alt = false;
            foreach (YearRow row in rows)
            {
                double avg = row.TotalSales > 0
                    ? row.TotalAmount / row.TotalSales : 0;
                y = DrawTableRow(gfx, y, alt,
                    new[] {
                        row.Year.ToString(),
                        row.TotalSales.ToString(),
                        $"${row.TotalAmount:F2}",
                        $"${avg:F2}"
                    },
                    yCols, yWids,
                    new[] { false, false, true, false });
                alt = !alt;
            }

            y += 6;
            DrawGrandTotal(gfx, y,
                $"Grand Total — {rows.Count} Years / {totalSales} Sales",
                totalRevenue);

            DrawFooter(gfx, doc.Pages.Count);
            doc.Save(path);
            OpenPdf(path);
            return path;
        }

        // ══════════════════════════════════════════════════════
        // EXPORT RECEIPT — professional narrow receipt
        // ══════════════════════════════════════════════════════
        public static string ExportReceipt(Sale sale, List<ReceiptItem> items)
        {
            string path = GetSavePath("Receipt");
            if (path == null) return null;

            PdfDocument doc = new PdfDocument();
            doc.Info.Title = $"Receipt #{sale.Id}";

            PdfPage page = doc.AddPage();
            page.Width = XUnit.FromMillimeter(80);
            page.Height = XUnit.FromMillimeter(30 + items.Count * 12 + 80);

            XGraphics gfx = XGraphics.FromPdfPage(page);
            double w = page.Width.Point;
            double m = 12;
            double y = 0;

            // ── Header ──
            gfx.DrawRectangle(new XSolidBrush(CNavy), 0, 0, w, 68);

            // Store name
            gfx.DrawString("POS SYSTEM",
                new XFont("Arial", 14, XFontStyleEx.Bold),
                new XSolidBrush(CWhite),
                new XRect(0, 10, w, 20), XStringFormats.TopCenter);

            gfx.DrawString("Point of Sale Management",
                FSmall, new XSolidBrush(CLGray),
                new XRect(0, 30, w, 14), XStringFormats.TopCenter);

            // Thin gold divider
            gfx.DrawRectangle(new XSolidBrush(CGold), 0, 50, w, 2);

            // If sale.Id is 0 (preview), show 'Preview' instead
            string receiptLabel = sale != null && sale.Id > 0
                ? $"Receipt No: #{sale.Id:D4}"
                : "Receipt Preview";

            gfx.DrawString(receiptLabel,
                FSmall, new XSolidBrush(CLGray),
                new XRect(m, 54, w - m * 2, 12), XStringFormats.TopLeft);
            y = 72;

            // Date / Cashier info box
            gfx.DrawRectangle(new XSolidBrush(CDark), 0, y, w, 32);
            gfx.DrawString($"Date:    {FormatDate(sale.Date ?? DateTime.Now.ToString())}",
                FSmall, new XSolidBrush(CLGray),
                new XRect(m, y + 4, w - m * 2, 12), XStringFormats.TopLeft);
            gfx.DrawString($"Cashier: {Session.Username}",
                FSmall, new XSolidBrush(CLGray),
                new XRect(m, y + 17, w - m * 2, 12), XStringFormats.TopLeft);
            y += 36;

            // ── Items section ──
            gfx.DrawRectangle(new XSolidBrush(CBlue), 0, y, w, 18);
            gfx.DrawString("ITEM",
                new XFont("Arial", 7, XFontStyleEx.Bold),
                new XSolidBrush(CWhite),
                new XRect(m, y + 4, 90, 12), XStringFormats.TopLeft);
            gfx.DrawString("QTY",
                new XFont("Arial", 7, XFontStyleEx.Bold),
                new XSolidBrush(CWhite),
                new XRect(m + 90, y + 4, 25, 12), XStringFormats.TopLeft);
            gfx.DrawString("AMOUNT",
                new XFont("Arial", 7, XFontStyleEx.Bold),
                new XSolidBrush(CWhite),
                new XRect(m + 115, y + 4, w - m - 115, 12),
                XStringFormats.TopLeft);
            y += 18;

            bool alt2 = false;
            foreach (ReceiptItem item in items)
            {
                XColor bg = alt2 ? XColor.FromArgb(245, 248, 255) : CWhite;
                gfx.DrawRectangle(new XSolidBrush(bg), 0, y, w, 18);
                gfx.DrawString(item.Name,
                    FSmall, new XSolidBrush(CNavy),
                    new XRect(m, y + 4, 90, 12), XStringFormats.TopLeft);
                gfx.DrawString(item.Quantity.ToString(),
                    FSmall, new XSolidBrush(CDGray),
                    new XRect(m + 90, y + 4, 25, 12), XStringFormats.TopLeft);
                // Receipt prices elsewhere in the app use LBP integer formatting
                gfx.DrawString($"LBP {item.Price:N0}",
                    FBold, new XSolidBrush(CNavy),
                    new XRect(m + 115, y + 4, w - m - 120, 12),
                    XStringFormats.TopLeft);
                y += 18;
                alt2 = !alt2;
            }

            // ── Divider ──
            gfx.DrawRectangle(new XSolidBrush(CBorder), 0, y, w, 1);
            y += 6;

            // ── Total box ──
            gfx.DrawRectangle(new XSolidBrush(CNavy), 0, y, w, 34);
            gfx.DrawString("TOTAL",
                new XFont("Arial", 10, XFontStyleEx.Bold),
                new XSolidBrush(CWhite),
                new XRect(m, y + 8, 60, 20), XStringFormats.TopLeft);
            gfx.DrawString($"LBP {sale.TotalAmount:N0}",
                new XFont("Arial", 14, XFontStyleEx.Bold),
                new XSolidBrush(CGold),
                new XRect(m, y + 4, w - m * 2, 26),
                XStringFormats.TopRight);
            y += 38;

            // ── Footer ──
            gfx.DrawRectangle(new XSolidBrush(CMid), 0, y, w, 28);
            gfx.DrawString("Thank you for your purchase!",
                FSmall, new XSolidBrush(CLGray),
                new XRect(0, y + 4, w, 12), XStringFormats.TopCenter);
            gfx.DrawString("Please come again.",
                FSmall, new XSolidBrush(CDGray),
                new XRect(0, y + 16, w, 10), XStringFormats.TopCenter);

            doc.Save(path);
            OpenPdf(path);
            return path;
        }

        // ══════════════════════════════════════════════════════
        // DRAWING COMPONENTS
        // ══════════════════════════════════════════════════════

        // ── Page header — dark navy banner ──
        static double DrawPageHeader(XGraphics gfx,
            string title, string subtitle)
        {
            // Background
            gfx.DrawRectangle(new XSolidBrush(CNavy), 0, 0, PageW, 85);

            // Gold accent bar
            gfx.DrawRectangle(new XSolidBrush(CGold), 0, 82, PageW, 3);

            // Title
            gfx.DrawString("POS SYSTEM", FSmall,
                new XSolidBrush(CLGray),
                new XRect(Margin, 12, ContentW, 14), XStringFormats.TopLeft);
            gfx.DrawString(title, FTitle,
                new XSolidBrush(CWhite),
                new XRect(Margin, 26, ContentW, 28), XStringFormats.TopLeft);
            gfx.DrawString(subtitle, FSmall,
                new XSolidBrush(CLGray),
                new XRect(Margin, 58, ContentW, 14), XStringFormats.TopLeft);

            // Page number area — right side
            gfx.DrawString("CONFIDENTIAL",
                new XFont("Arial", 7, XFontStyleEx.Italic),
                new XSolidBrush(XColor.FromArgb(60, 80, 100)),
                new XRect(Margin, 68, ContentW, 10), XStringFormats.TopRight);

            return 100;
        }

        // ── Summary bar — 4 stat cards ──
        static double DrawSummaryBar(XGraphics gfx, double y,
            (string label, string value, XColor color)[] stats)
        {
            double cardW = ContentW / stats.Length;
            double cardH = 58;

            // Background panel
            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(248, 250, 255)),
                Margin, y, ContentW, cardH);

            for (int i = 0; i < stats.Length; i++)
            {
                double cx = Margin + i * cardW;

                // Card background
                gfx.DrawRoundedRectangle(
                    new XSolidBrush(stats[i].color),
                    cx + 3, y + 4, cardW - 6, cardH - 8, 5, 5);

                // Left accent strip
                gfx.DrawRectangle(new XSolidBrush(
                    XColor.FromArgb(
                        Math.Min(255, stats[i].color.R + 40),
                        Math.Min(255, stats[i].color.G + 40),
                        Math.Min(255, stats[i].color.B + 40))),
                    cx + 3, y + 4, 4, cardH - 8);

                // Label
                gfx.DrawString(stats[i].label,
                    new XFont("Arial", 7, XFontStyleEx.Bold),
                    new XSolidBrush(CLGray),
                    new XRect(cx + 12, y + 8, cardW - 16, 12),
                    XStringFormats.TopLeft);

                // Value
                gfx.DrawString(stats[i].value,
                    new XFont("Arial", 15, XFontStyleEx.Bold),
                    new XSolidBrush(CWhite),
                    new XRect(cx + 12, y + 22, cardW - 16, 24),
                    XStringFormats.TopLeft);
            }

            return y + cardH + 6;
        }

        // ── Section title ──
        static double DrawSectionTitle(XGraphics gfx,
            double y, string title)
        {
            // Blue left bar + title
            gfx.DrawRectangle(new XSolidBrush(CBlue),
                Margin, y, 4, 20);
            gfx.DrawString(title,
                FSHead, new XSolidBrush(CNavy),
                new XRect(Margin + 10, y + 2, ContentW - 10, 18),
                XStringFormats.TopLeft);

            // Thin line
            gfx.DrawLine(new XPen(CBorder, 0.5),
                Margin, y + 22, Margin + ContentW, y + 22);

            return y + 30;
        }

        // ── Sale block — one transaction with its items ──
        static double DrawSaleBlock(XGraphics gfx,
            SaleRow sale, double y)
        {
            double blockH = 26 + sale.Items.Count * 16 + 4;

            // Sale header bar
            gfx.DrawRectangle(new XSolidBrush(CDark),
                Margin, y, ContentW, 26);

            // Sale ID badge
            gfx.DrawRoundedRectangle(new XSolidBrush(CBlue),
                Margin + 4, y + 4, 48, 18, 3, 3);
            gfx.DrawString($"# {sale.Id:D4}",
                new XFont("Arial", 8, XFontStyleEx.Bold),
                new XSolidBrush(CWhite),
                new XRect(Margin + 4, y + 6, 48, 14),
                XStringFormats.TopCenter);

            // Date
            gfx.DrawString(FormatDate(sale.Date),
                FBold, new XSolidBrush(CLGray),
                new XRect(Margin + 58, y + 7, 200, 14),
                XStringFormats.TopLeft);

            // Total — right side
            gfx.DrawString($"${sale.TotalAmount:F2}",
                new XFont("Arial", 11, XFontStyleEx.Bold),
                new XSolidBrush(CGold),
                new XRect(Margin, y + 5, ContentW - 8, 16),
                XStringFormats.TopRight);

            y += 26;

            // Items sub-rows
            foreach (SaleItemRow item in sale.Items)
            {
                gfx.DrawRectangle(new XSolidBrush(CRowSub),
                    Margin, y, ContentW, 16);

                // Item name
                gfx.DrawString($"    {item.ProductName}",
                    FBody, new XSolidBrush(CNavy),
                    new XRect(Margin + 60, y + 2, 200, 13),
                    XStringFormats.TopLeft);

                // Qty
                gfx.DrawString($"x {item.Quantity}",
                    FItalic, new XSolidBrush(CDGray),
                    new XRect(Margin + 270, y + 2, 60, 13),
                    XStringFormats.TopLeft);

                // Unit price
                gfx.DrawString($"${item.Price / item.Quantity:F2} each",
                    FItalic, new XSolidBrush(CDGray),
                    new XRect(Margin + 330, y + 2, 90, 13),
                    XStringFormats.TopLeft);

                // Line total
                gfx.DrawString($"${item.Price:F2}",
                    FBold, new XSolidBrush(CNavy),
                    new XRect(Margin, y + 2, ContentW - 8, 13),
                    XStringFormats.TopRight);

                // Bottom border on each item row
                gfx.DrawLine(new XPen(CBorder, 0.3),
                    Margin + 60, y + 15, Margin + ContentW, y + 15);

                y += 16;
            }

            // Bottom border on sale block
            gfx.DrawLine(new XPen(CBorder, 0.8),
                Margin, y, Margin + ContentW, y);

            return y + 4;
        }

        // ── Table header row ──
        static double DrawTableHeader(XGraphics gfx, double y,
            string[] headers, double[] cols, double[] widths)
        {
            double rowH = 24;
            gfx.DrawRectangle(new XSolidBrush(CNavy),
                Margin, y, ContentW, rowH);

            for (int i = 0; i < headers.Length; i++)
                gfx.DrawString(headers[i],
                    new XFont("Arial", 8, XFontStyleEx.Bold),
                    new XSolidBrush(CWhite),
                    new XRect(cols[i] + 6, y + 7, widths[i] - 8, 14),
                    XStringFormats.TopLeft);

            return y + rowH;
        }

        // ── Table data row ──
        static double DrawTableRow(XGraphics gfx, double y,
            bool alt, string[] values, double[] cols,
            double[] widths, bool[] highlight)
        {
            double rowH = 22;
            XColor bg = alt ? CRowAlt : CWhite;
            gfx.DrawRectangle(new XSolidBrush(bg),
                Margin, y, ContentW, rowH);

            // Left accent
            gfx.DrawRectangle(new XSolidBrush(alt ? CBlue : CBorder),
                Margin, y, 3, rowH);

            for (int i = 0; i < values.Length; i++)
            {
                XColor color = highlight[i] ? CGreen : CNavy;
                XFont font = highlight[i] ? FBold : FBody;
                gfx.DrawString(values[i], font,
                    new XSolidBrush(color),
                    new XRect(cols[i] + 6, y + 6, widths[i] - 8, 14),
                    XStringFormats.TopLeft);
            }

            // Bottom border
            gfx.DrawLine(new XPen(CBorder, 0.3),
                Margin, y + rowH, Margin + ContentW, y + rowH);

            return y + rowH;
        }

        // ── Grand total bar ──
        static void DrawGrandTotal(XGraphics gfx,
            double y, string label, double total)
        {
            gfx.DrawRectangle(new XSolidBrush(CNavy),
                Margin, y, ContentW, 32);

            // Gold left accent
            gfx.DrawRectangle(new XSolidBrush(CGold),
                Margin, y, 5, 32);

            gfx.DrawString(label,
                FBold, new XSolidBrush(CLGray),
                new XRect(Margin + 12, y + 9, ContentW * 0.6, 16),
                XStringFormats.TopLeft);

            gfx.DrawString($"${total:F2}",
                new XFont("Arial", 14, XFontStyleEx.Bold),
                new XSolidBrush(CGold),
                new XRect(Margin, y + 5, ContentW - 8, 22),
                XStringFormats.TopRight);
        }

        // ── Page footer ──
        static void DrawFooter(XGraphics gfx, int pageNum)
        {
            gfx.DrawLine(new XPen(CBorder, 0.5),
                Margin, PageH - 30, Margin + ContentW, PageH - 30);

            gfx.DrawString("POS System  —  Confidential",
                FSmall, new XSolidBrush(CDGray),
                new XRect(Margin, PageH - 24, ContentW * 0.5, 12),
                XStringFormats.TopLeft);

            gfx.DrawString(
                $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}   |   " +
                $"Page {pageNum}",
                FSmall, new XSolidBrush(CDGray),
                new XRect(Margin, PageH - 24, ContentW, 12),
                XStringFormats.TopRight);
        }

        // ── Save dialog ──
        static string GetSavePath(string prefix)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = $"{prefix}_{DateTime.Now:dd-MM-yyyy_HHmm}";
            dlg.DefaultExt = ".pdf";
            dlg.Filter = "PDF files (*.pdf)|*.pdf";
            dlg.InitialDirectory = Environment.GetFolderPath(
                Environment.SpecialFolder.Desktop);
            bool? result = dlg.ShowDialog();
            return result == true ? dlg.FileName : null;
        }

        // ── Open PDF ──
        static void OpenPdf(string path)
        {
            if (File.Exists(path))
                Process.Start(new ProcessStartInfo(path)
                { UseShellExecute = true });
        }

        public static void ExportPurchaseOrder(
    PurchaseOrder order,
    List<PurchaseOrderItem> items,
    double total)
        {
            SaveFileDialog dlg = new SaveFileDialog
            {
                Title = "Save Purchase Order PDF",
                FileName =
                    $"PO_{order.Id}_{order.OrderDate:dd-MM-yyyy}.pdf",
                DefaultExt = ".pdf",
                Filter = "PDF Files (*.pdf)|*.pdf"
            };

            if (dlg.ShowDialog() != true) return;

            PdfDocument doc = new PdfDocument();
            doc.Info.Title =
                $"Purchase Order #{order.Id}";

            PdfPage page = doc.AddPage();
            page.Size = PageSize.A4;
            XGraphics gfx =
                XGraphics.FromPdfPage(page);

            XFont titleFont = new XFont("Arial", 20, XFontStyleEx.Bold);
            XFont headerFont = new XFont("Arial", 11, XFontStyleEx.Bold);
            XFont normalFont = new XFont("Arial", 10, XFontStyleEx.Regular);
            XFont boldFont = new XFont("Arial", 10, XFontStyleEx.Bold);

            double margin = 40;
            double y = 40;
            double w = page.Width - margin * 2;

            // Title bar
            gfx.DrawRectangle(
                new XSolidBrush(
                    XColor.FromArgb(21, 101, 192)),
                margin, y, w, 40);
            gfx.DrawString(
                $"PURCHASE ORDER  #{order.Id}",
                titleFont,
                XBrushes.White,
                new XRect(margin, y, w, 40),
                XStringFormats.Center);
            y += 50;

            // Order info
            gfx.DrawString(
                $"Supplier: {order.SupplierName}",
                boldFont, XBrushes.Black,
                margin, y); y += 18;
            gfx.DrawString(
                $"Order Date: {order.OrderDate}",
                normalFont, XBrushes.Black,
                margin, y); y += 18;
            gfx.DrawString(
                $"Status: {order.Status}",
                normalFont, XBrushes.Black,
                margin, y); y += 18;
            if (!string.IsNullOrEmpty(order.Notes))
            {
                gfx.DrawString(
                    $"Notes: {order.Notes}",
                    normalFont, XBrushes.Black,
                    margin, y);
                y += 18;
            }
            y += 10;

            // Table header
            double col1 = margin;
            double col2 = margin + 220;
            double col3 = margin + 310;
            double col4 = margin + 390;

            gfx.DrawRectangle(
                new XSolidBrush(
                    XColor.FromArgb(240, 244, 248)),
                margin, y, w, 22);
            gfx.DrawString("Product", headerFont,
                XBrushes.Black, col1, y + 15);
            gfx.DrawString("Qty", headerFont,
                XBrushes.Black, col2, y + 15);
            gfx.DrawString("Unit Cost", headerFont,
                XBrushes.Black, col3, y + 15);
            gfx.DrawString("Total", headerFont,
                XBrushes.Black, col4, y + 15);
            y += 26;

            // Items
            bool alt = false;
            foreach (PurchaseOrderItem item in items)
            {
                if (alt)
                    gfx.DrawRectangle(
                        new XSolidBrush(
                            XColor.FromArgb(
                                232, 240, 255)),
                        margin, y, w, 20);
                gfx.DrawString(item.ProductName,
                    normalFont, XBrushes.Black,
                    col1, y + 14);
                gfx.DrawString(
                    item.Quantity.ToString(),
                    normalFont, XBrushes.Black,
                    col2, y + 14);
                gfx.DrawString(item.UnitCostText,
                    normalFont, XBrushes.Black,
                    col3, y + 14);
                gfx.DrawString(item.TotalCostText,
                    normalFont, XBrushes.Black,
                    col4, y + 14);
                y += 22;
                alt = !alt;
            }

            // Total row
            y += 8;
            gfx.DrawRectangle(
                new XSolidBrush(
                    XColor.FromArgb(21, 101, 192)),
                margin, y, w, 26);
            gfx.DrawString("ORDER TOTAL",
                headerFont, XBrushes.White,
                col1, y + 18);
            gfx.DrawString($"${total:F2}",
                headerFont, XBrushes.White,
                col4, y + 18);

            doc.Save(dlg.FileName);
            System.Diagnostics.Process
                .Start(dlg.FileName);
        }
    }
}