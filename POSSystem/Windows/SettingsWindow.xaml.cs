using POSSystem.Database;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace POSSystem.Windows
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
            UpdateTaxState();
        }

        // ══════════════════════════════════════
        // LOAD
        // ══════════════════════════════════════
        void LoadSettings()
        {
            txtStoreName.Text = SettingsHelper.Get("StoreName");
            txtPhone.Text = SettingsHelper.Get("StorePhone");
            txtAddress.Text = SettingsHelper.Get("StoreAddress");
            txtEmail.Text = SettingsHelper.Get("StoreEmail");
            txtReceiptHeader.Text = SettingsHelper.Get("ReceiptHeader");
            txtReceiptFooter.Text = SettingsHelper.Get("ReceiptFooter");
            txtTaxPct.Text = SettingsHelper.Get("TaxPercent");
            txtCurrency.Text = SettingsHelper.Get("Currency");
            txtLowStock.Text = SettingsHelper.Get("LowStockThreshold");
            txtPrinter.Text = SettingsHelper.Get("PrinterName");
            txtLogoPath.Text = SettingsHelper.Get("LogoPath");

            // Exchange Rate with default fallback
            txtExchangeRate.Text = SettingsHelper.Get("ExchangeRate") ?? "90000";
            UpdateRatePreview();

            chkTaxEnabled.IsChecked = SettingsHelper.GetBool("TaxEnabled");

            string logo = txtLogoPath.Text;
            lblLogoPreview.Content = string.IsNullOrWhiteSpace(logo)
                ? "No logo selected"
                : "✅  " + System.IO.Path.GetFileName(logo);
        }

        // ══════════════════════════════════════
        // SAVE
        // ══════════════════════════════════════
        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(txtStoreName.Text))
            {
                ShowStatus("⚠️  Store name cannot be empty.", isError: true);
                txtStoreName.Focus();
                return;
            }

            if (!double.TryParse(txtTaxPct.Text, out double taxPct)
                || taxPct < 0 || taxPct > 100)
            {
                ShowStatus("⚠️  Tax % must be a number between 0 and 100.", isError: true);
                txtTaxPct.Focus();
                return;
            }

            if (!int.TryParse(txtLowStock.Text, out int threshold)
                || threshold < 0)
            {
                ShowStatus("⚠️  Low stock threshold must be a positive number.", isError: true);
                txtLowStock.Focus();
                return;
            }

            // Validate exchange rate
            if (!double.TryParse(txtExchangeRate.Text, out double exchangeRate)
                || exchangeRate <= 0)
            {
                ShowStatus("⚠️  Exchange rate must be a positive number.", isError: true);
                txtExchangeRate.Focus();
                return;
            }

            var values = new Dictionary<string, string>
            {
                { "StoreName",         txtStoreName.Text.Trim()     },
                { "StorePhone",        txtPhone.Text.Trim()         },
                { "StoreAddress",      txtAddress.Text.Trim()       },
                { "StoreEmail",        txtEmail.Text.Trim()         },
                { "ReceiptHeader",     txtReceiptHeader.Text.Trim() },
                { "ReceiptFooter",     txtReceiptFooter.Text.Trim() },
                { "TaxEnabled",        chkTaxEnabled.IsChecked == true
                                           ? "true" : "false"       },
                { "TaxPercent",        taxPct.ToString()            },
                { "Currency",          string.IsNullOrWhiteSpace(txtCurrency.Text)
                                           ? "LBP"
                                           : txtCurrency.Text.Trim()},
                { "LowStockThreshold", threshold.ToString()         },
                { "PrinterName",       txtPrinter.Text.Trim()       },
                { "LogoPath",          txtLogoPath.Text.Trim()      },
                { "ExchangeRate",      exchangeRate.ToString()      },
            };

            SettingsHelper.SaveAll(values);

            // Force currency helper to refresh with new rate
            CurrencyHelper.ResetRate();

            ShowStatus("✅  Settings saved successfully!");
        }

        // ══════════════════════════════════════
        // EXCHANGE RATE PREVIEW
        // ══════════════════════════════════════
        private void txtExchangeRate_TextChanged(object sender,
            System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateRatePreview();
        }

        void UpdateRatePreview()
        {
            // FIX: Simple null check - no other property access before this
            if (lblRatePreview == null)
                return;

            if (txtExchangeRate == null)
                return;

            string text = txtExchangeRate.Text ?? "";

            if (string.IsNullOrWhiteSpace(text))
            {
                lblRatePreview.Content = "Enter exchange rate";
                lblRatePreview.Foreground = new SolidColorBrush(Colors.Gray);
                return;
            }

            if (double.TryParse(text, out double rate) && rate > 0)
            {
                lblRatePreview.Content = $"Example: $10 = {rate * 10:N0} LBP";
                lblRatePreview.Foreground = new SolidColorBrush(Colors.LightGray);
            }
            else
            {
                lblRatePreview.Content = "Invalid exchange rate";
                lblRatePreview.Foreground = new SolidColorBrush(Colors.IndianRed);
            }
        }

        // ══════════════════════════════════════
        // TAX CHECKBOX
        // ══════════════════════════════════════
        private void chkTaxEnabled_Changed(object sender,
            RoutedEventArgs e)
        {
            UpdateTaxState();
        }

        void UpdateTaxState()
        {
            bool enabled = chkTaxEnabled.IsChecked == true;
            txtTaxPct.IsEnabled = enabled;
            borderTaxPct.Opacity = enabled ? 1.0 : 0.4;
            lblTaxNote.Foreground = enabled
                ? System.Windows.Media.Brushes.LightGray
                : System.Windows.Media.Brushes.Gray;
        }

        // ══════════════════════════════════════
        // BROWSE LOGO
        // ══════════════════════════════════════
        private void btnBrowseLogo_Click(object sender,
            RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Logo Image",
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp|All Files|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                txtLogoPath.Text = dlg.FileName;
                lblLogoPreview.Content =
                    "✅  " + System.IO.Path.GetFileName(dlg.FileName);
                lblLogoPreview.Foreground =
                    System.Windows.Media.Brushes.LightGreen;
            }
        }

        // ══════════════════════════════════════
        // CLOSE
        // ══════════════════════════════════════
        private void btnClose_Click(object sender,
            RoutedEventArgs e)
        {
            this.Close();
        }

        // ══════════════════════════════════════
        // STATUS FLASH
        // ══════════════════════════════════════
        void ShowStatus(string msg, bool isError = false)
        {
            lblStatus.Content = msg;
            lblStatus.Foreground = isError
                ? System.Windows.Media.Brushes.Tomato
                : System.Windows.Media.Brushes.LightGreen;

            // Auto-clear after 3 seconds
            var t = new DispatcherTimer
            { Interval = TimeSpan.FromSeconds(3) };
            t.Tick += (s, e) =>
            {
                lblStatus.Content = "";
                t.Stop();
            };
            t.Start();
        }
    }
}