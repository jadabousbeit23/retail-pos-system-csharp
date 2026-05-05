using POSSystem.Database;
using System;
using System.Collections.Generic;
using System.Windows;

namespace POSSystem.Windows
{
    public partial class ReceiptWindow : Window
    {
        private Sale _currentSale;
        private List<ReceiptItem> _items;

        public ReceiptWindow()
        {
            InitializeComponent();
        }

        // ══════════════════════════════════════════════
        // LOAD RECEIPT
        // ══════════════════════════════════════════════
        public void LoadReceipt(Sale sale, List<ReceiptItem> items)
        {
            _currentSale = sale;
            _items = items;

            // ── Pull store info from Settings ─────────
            string storeName = SettingsHelper.Get("StoreName");
            string footer    = SettingsHelper.Get("ReceiptFooter");
            string header    = SettingsHelper.Get("ReceiptHeader");

            // Window title bar
            this.Title = string.IsNullOrWhiteSpace(storeName)
                ? "Receipt"
                : $"Receipt — {storeName}";

            // Header title: prefer ReceiptHeader, fall back to StoreName, then default
            if (!string.IsNullOrWhiteSpace(header))
                lblHeaderTitle.Content = "🧾  " + header;
            else if (!string.IsNullOrWhiteSpace(storeName))
                lblHeaderTitle.Content = "🧾  " + storeName;
            else
                lblHeaderTitle.Content = "🧾  RECEIPT";

            // Store badge (top-right pill)
            lblStoreBadge.Content = string.IsNullOrWhiteSpace(storeName)
                ? "POS System"
                : storeName;

            // Footer text
            lblFooter.Content = string.IsNullOrWhiteSpace(footer)
                ? "Thank you for your purchase!"
                : footer;

            // ── Sale info ─────────────────────────────
            if (sale != null)
            {
                lblSaleInfo.Content = sale.Id > 0
                    ? $"Sale #{sale.Id}  —  {sale.Date}  —  {sale.CashierName}"
                    : $"Preview  —  {sale.Date}";

                lblReceiptTotal.Content = $"LBP {sale.TotalAmount:N0}";

                // Payment method pill
                if (!string.IsNullOrWhiteSpace(sale.PaymentMethod))
                    lblPaymentMethod.Content = $"💳 {sale.PaymentMethod}";

                // Change due (cash payments only)
                if (sale.ChangeDue > 0)
                    lblChangeDue.Content = $"Change: LBP {sale.ChangeDue:N0}";
            }

            // ── Items ─────────────────────────────────
            lvReceiptItems.ItemsSource = items;
        }

        // ══════════════════════════════════════════════
        // PRINT
        // ══════════════════════════════════════════════
        private void btnPrintReceipt_Click(object sender, RoutedEventArgs e)
        {
            if (_items == null || _items.Count == 0)
            {
                MessageBox.Show("Nothing to print!", "Print",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                ReceiptPrinter.PrintReceipt(_currentSale, _items);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Print error:\n{ex.Message}", "Print Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════
        // EXPORT PDF
        // ══════════════════════════════════════════════
        private void btnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_items == null || _items.Count == 0)
            {
                MessageBox.Show("Nothing to export!", "PDF Export",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                PdfExportHelper.ExportReceipt(_currentSale, _items);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PDF export error:\n{ex.Message}", "PDF Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════
        // CLOSE
        // ══════════════════════════════════════════════
        private void btnClose_Click(object sender, RoutedEventArgs e)
            => this.Close();
    }
}
