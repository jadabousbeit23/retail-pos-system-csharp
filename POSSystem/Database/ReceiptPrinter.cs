using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace POSSystem.Database
{
    public class ReceiptPrinter
    {
        // ══════════════════════════════════════
        // PRINT RECEIPT — shows print dialog
        // ══════════════════════════════════════
        public static void PrintReceipt(Sale sale, List<ReceiptItem> items)
        {
            PrintDialog pd = new PrintDialog();
            if (pd.ShowDialog() != true) return;

            FlowDocument doc = BuildReceiptDocument(sale, items, pd);
            IDocumentPaginatorSource src = doc;
            pd.PrintDocument(
                src.DocumentPaginator,
                $"Receipt #{sale.Id:D4}");
        }

        // ══════════════════════════════════════
        // BUILD RECEIPT DOCUMENT
        // ══════════════════════════════════════
        static FlowDocument BuildReceiptDocument(
            Sale sale, List<ReceiptItem> items, PrintDialog pd)
        {
            double pageW = pd.PrintableAreaWidth;

            FlowDocument doc = new FlowDocument
            {
                PageWidth   = pageW,
                PagePadding = new Thickness(20, 10, 20, 10),
                ColumnWidth = pageW,
                FontFamily  = new FontFamily("Courier New"),
                FontSize    = 11,
                Foreground  = Brushes.Black,
                Background  = Brushes.White
            };

            // ── Pull all settings up front ────────────────────────────
            string storeName     = SettingsHelper.Get("StoreName",     "POS SYSTEM");
            string storeAddress  = SettingsHelper.Get("StoreAddress",  "");
            string storePhone    = SettingsHelper.Get("StorePhone",    "");
            string receiptHeader = SettingsHelper.Get("ReceiptHeader", "");
            string receiptFooter = SettingsHelper.Get("ReceiptFooter", "Thank you for your purchase!");
            string logoPath      = SettingsHelper.Get("LogoPath",      "");

            // ── Top separator ─────────────────────────────────────────
            doc.Blocks.Add(Sep('='));

            // ── Store name ────────────────────────────────────────────
            doc.Blocks.Add(CenteredPara(storeName.ToUpper(), 16, true));

            // ── LOGO — below store name, centered, max 120 px wide ────
            // Loads from the path saved in Settings → Logo.
            // Fails silently so the receipt always prints even if the
            // image file has been moved or deleted.
            if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
            {
                try
                {
                    BitmapImage bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource        = new Uri(logoPath, UriKind.Absolute);
                    bmp.CacheOption      = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 120;   // cap width — thermal-friendly
                    bmp.EndInit();
                    bmp.Freeze();                 // safe to use across threads

                    System.Windows.Controls.Image img =
                        new System.Windows.Controls.Image
                        {
                            Source              = bmp,
                            Width               = 120,
                            Height              = double.NaN,  // auto height
                            Stretch             = Stretch.Uniform,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin              = new Thickness(0, 4, 0, 4)
                        };

                    doc.Blocks.Add(new BlockUIContainer(img)
                    {
                        TextAlignment = TextAlignment.Center,
                        Margin        = new Thickness(0, 2, 0, 2)
                    });
                }
                catch
                {
                    // Image load failed — skip logo, continue printing
                }
            }

            // ── Store address / phone / custom header ─────────────────
            if (!string.IsNullOrWhiteSpace(storeAddress))
                doc.Blocks.Add(CenteredPara(storeAddress, 10, false));
            if (!string.IsNullOrWhiteSpace(storePhone))
                doc.Blocks.Add(CenteredPara("Tel: " + storePhone, 10, false));
            if (!string.IsNullOrWhiteSpace(receiptHeader))
                doc.Blocks.Add(CenteredPara(receiptHeader, 10, false));

            doc.Blocks.Add(Sep('='));

            // ── Sale info ─────────────────────────────────────────────
            // Use sale.CashierName saved at transaction time — not
            // Session.Username — so admin reprints show the right cashier.
            string cashierName = !string.IsNullOrWhiteSpace(sale.CashierName)
                ? sale.CashierName
                : Session.Username;

            doc.Blocks.Add(LinePair("Receipt #:", $"{sale.Id:D4}"));
            doc.Blocks.Add(LinePair("Date:",      FormatDate(sale.Date ?? DateTime.Now.ToString())));
            doc.Blocks.Add(LinePair("Time:",      DateTime.Now.ToString("HH:mm:ss")));
            doc.Blocks.Add(LinePair("Cashier:",   cashierName));

            if (!string.IsNullOrWhiteSpace(sale.CustomerName))
                doc.Blocks.Add(LinePair("Customer:", sale.CustomerName));

            doc.Blocks.Add(Sep('-'));

            // ── Column headers ────────────────────────────────────────
            doc.Blocks.Add(ColumnHeader());
            doc.Blocks.Add(Sep('-'));

            // ── Items ─────────────────────────────────────────────────
            double subtotal = 0;
            foreach (ReceiptItem item in items)
            {
                doc.Blocks.Add(ItemLine(item));
                subtotal += item.Price;
            }

            doc.Blocks.Add(Sep('-'));

            // ── Subtotal / discount ───────────────────────────────────
            double discountAmt = subtotal - sale.TotalAmount;
            doc.Blocks.Add(LinePair("Subtotal:", $"LBP {subtotal:N0}"));

            if (discountAmt > 0.01)
                doc.Blocks.Add(LinePair("Discount:", $"-LBP {discountAmt:N0}"));

            doc.Blocks.Add(Sep('='));

            // ── Big total ─────────────────────────────────────────────
            Paragraph totalPara = new Paragraph
            {
                TextAlignment = TextAlignment.Center,
                Margin        = new Thickness(0, 2, 0, 2)
            };
            totalPara.Inlines.Add(new Run($"TOTAL: LBP {sale.TotalAmount:N0}")
            {
                FontSize   = 16,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Courier New")
            });
            doc.Blocks.Add(totalPara);

            doc.Blocks.Add(Sep('='));

            // ── Payment ───────────────────────────────────────────────
            string payMethod = !string.IsNullOrWhiteSpace(sale.PaymentMethod)
                ? sale.PaymentMethod.ToUpper()
                : "CASH";

            doc.Blocks.Add(LinePair("Payment:", payMethod));

            switch (payMethod)
            {
                case "SPLIT":
                    if (sale.CashPaid > 0)
                        doc.Blocks.Add(LinePair("  Cash:", $"LBP {sale.CashPaid:N0}"));
                    if (sale.CardPaid > 0)
                        doc.Blocks.Add(LinePair("  Card:", $"LBP {sale.CardPaid:N0}"));
                    break;
                case "CASH":
                    if (sale.CashPaid > 0)
                        doc.Blocks.Add(LinePair("Cash Paid:", $"LBP {sale.CashPaid:N0}"));
                    break;
                case "CARD":
                    if (sale.CardPaid > 0)
                        doc.Blocks.Add(LinePair("Card Paid:", $"LBP {sale.CardPaid:N0}"));
                    break;
            }

            if (sale.ChangeDue > 0)
                doc.Blocks.Add(LinePair("Change Due:", $"LBP {sale.ChangeDue:N0}"));

            // ── Loyalty ───────────────────────────────────────────────
            if (sale.PointsEarned   > 0)
                doc.Blocks.Add(LinePair("Points Earned:", sale.PointsEarned.ToString("N0")));
            if (sale.PointsRedeemed > 0)
                doc.Blocks.Add(LinePair("Points Used:",   sale.PointsRedeemed.ToString("N0")));
            if (sale.StampsEarned   > 0)
                doc.Blocks.Add(LinePair("Stamps:",        sale.StampsEarned.ToString()));

            doc.Blocks.Add(Sep('-'));

            // ── Footer ────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(receiptFooter))
            {
                foreach (string line in receiptFooter.Split(
                    new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    doc.Blocks.Add(CenteredPara(line, 11, true));
            }

            doc.Blocks.Add(CenteredPara(" ", 8, false));
            doc.Blocks.Add(CenteredPara(
                $"Printed: {DateTime.Now:dd/MM/yyyy HH:mm}", 8, false));
            doc.Blocks.Add(Sep('='));

            // Extra blank lines for clean thermal cut
            doc.Blocks.Add(CenteredPara(" ", 10, false));
            doc.Blocks.Add(CenteredPara(" ", 10, false));
            doc.Blocks.Add(CenteredPara(" ", 10, false));

            return doc;
        }

        // ══════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════
        static Paragraph Sep(char ch)
            => CenteredPara(new string(ch, 32), 11, false);

        static Paragraph CenteredPara(string text, double size, bool bold)
        {
            Paragraph p = new Paragraph
            {
                TextAlignment = TextAlignment.Center,
                Margin        = new Thickness(0, 1, 0, 1)
            };
            p.Inlines.Add(new Run(text)
            {
                FontSize   = size,
                FontFamily = new FontFamily("Courier New"),
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal
            });
            return p;
        }

        static Paragraph LinePair(string label, string value)
        {
            Paragraph p = new Paragraph
            {
                TextAlignment = TextAlignment.Left,
                Margin        = new Thickness(0, 1, 0, 1)
            };
            int total   = 32;
            int padding = total - label.Length - value.Length;
            string pad  = padding > 0 ? new string(' ', padding) : " ";
            p.Inlines.Add(new Run(label + pad + value)
            {
                FontSize   = 11,
                FontFamily = new FontFamily("Courier New")
            });
            return p;
        }

        static Paragraph ColumnHeader()
        {
            Paragraph p = new Paragraph
            {
                TextAlignment = TextAlignment.Left,
                Margin        = new Thickness(0, 1, 0, 1)
            };
            p.Inlines.Add(new Run(
                PadRight("ITEM", 16) +
                PadLeft("QTY",   4) +
                PadLeft("PRICE", 7) +
                PadLeft("TOTAL", 7))
            {
                FontSize   = 11,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Courier New")
            });
            return p;
        }

        static Paragraph ItemLine(ReceiptItem item)
        {
            Paragraph p = new Paragraph
            {
                TextAlignment = TextAlignment.Left,
                Margin        = new Thickness(0, 1, 0, 1)
            };

            double unitPrice = item.Quantity > 0
                ? item.Price / item.Quantity
                : item.Price;

            string name = item.Name.Length > 15
                ? item.Name.Substring(0, 14) + "."
                : item.Name;

            string line =
                PadRight(name, 16) +
                PadLeft(item.Quantity.ToString(), 4) +
                PadLeft($"{unitPrice:N0}",         7) +
                PadLeft($"{item.Price:N0}",         7);

            p.Inlines.Add(new Run(line)
            {
                FontSize   = 11,
                FontFamily = new FontFamily("Courier New")
            });
            return p;
        }

        static string PadRight(string s, int total)
        {
            if (s.Length >= total) return s.Substring(0, total);
            return s + new string(' ', total - s.Length);
        }

        static string PadLeft(string s, int total)
        {
            if (s.Length >= total) return s;
            return new string(' ', total - s.Length) + s;
        }

        static string FormatDate(string raw)
        {
            if (DateTime.TryParse(raw, out DateTime dt))
                return dt.ToString("dd/MM/yyyy");
            return raw;
        }
    }
}
