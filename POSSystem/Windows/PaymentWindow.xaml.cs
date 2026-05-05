using POSSystem.Database;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace POSSystem.Windows
{
    public partial class PaymentWindow : Window
    {
        // ── Public results read by FastSalesWindow after Close ──
        public bool Confirmed { get; private set; }
        public string PaymentMethod { get; private set; }
        public double CashPaidLBP { get; private set; }
        public double CardPaidLBP { get; private set; }
        public double ChangeDueLBP { get; private set; }
        public double CashPaidUSD { get; private set; }
        public double ChangeDueUSD { get; private set; }

        double _totalLBP;
        string _mode = "CashUSD";

        // Which text box the numpad is currently targeting
        TextBox _numTarget = null;
        bool _numIsUSD = true;   // so we can do the right conversion

        public PaymentWindow(double totalLBP)
        {
            InitializeComponent();
            _totalLBP = totalLBP;

            // Show totals in both currencies
            double rate = CurrencyHelper.Rate;
            lblRateInfo.Content = $"Exchange rate:  LBP {rate:N0}  =  $1.00";
            lblTotalLBP.Content = CurrencyHelper.FormatLBP(totalLBP);
            lblTotalUSD.Content = CurrencyHelper.FormatUSD(totalLBP);

            // Default numpad target = USD field
            _numTarget = txtSingleAmount;
            _numIsUSD = true;
            lblNumUnit.Content = "USD";

            BuildQuickAmounts();
            SetMode("CashUSD");

            // Subscribe to window key events for numpad functionality
            this.PreviewKeyDown += PaymentWindow_PreviewKeyDown;
        }

        // ══════════════════════════════════════════════════════
        // KEYBOARD NUMPAD SUPPORT
        // ══════════════════════════════════════════════════════
        private void PaymentWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Only handle if no textbox has focus (or if the focused textbox is not for typing)
            // Actually, let's handle numbers and backspace globally

            string digit = null;

            switch (e.Key)
            {
                case Key.D0: case Key.NumPad0: digit = "0"; break;
                case Key.D1: case Key.NumPad1: digit = "1"; break;
                case Key.D2: case Key.NumPad2: digit = "2"; break;
                case Key.D3: case Key.NumPad3: digit = "3"; break;
                case Key.D4: case Key.NumPad4: digit = "4"; break;
                case Key.D5: case Key.NumPad5: digit = "5"; break;
                case Key.D6: case Key.NumPad6: digit = "6"; break;
                case Key.D7: case Key.NumPad7: digit = "7"; break;
                case Key.D8: case Key.NumPad8: digit = "8"; break;
                case Key.D9: case Key.NumPad9: digit = "9"; break;
                case Key.Decimal: case Key.OemPeriod: digit = "."; break;
                case Key.Back:
                    HandleBackspace();
                    e.Handled = true;
                    return;
                case Key.Enter:
                    // Apply current entry
                    numApply_Click(null, null);
                    e.Handled = true;
                    return;
                case Key.Escape:
                    // Clear current entry
                    numClear_Click(null, null);
                    e.Handled = true;
                    return;
            }

            if (digit != null && _numTarget != null)
            {
                AppendDigit(digit);
                e.Handled = true;
            }
        }

        private void txtInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Let the window handle it if it's a numpad key
            // This prevents double-handling while still allowing typing
            if (IsNumpadKey(e.Key))
            {
                // Let the window's PreviewKeyDown handle it
                return;
            }
        }

        private bool IsNumpadKey(Key key)
        {
            return (key >= Key.D0 && key <= Key.D9) ||
                   (key >= Key.NumPad0 && key <= Key.NumPad9) ||
                   key == Key.Decimal || key == Key.OemPeriod ||
                   key == Key.Back || key == Key.Enter || key == Key.Escape;
        }

        private void AppendDigit(string digit)
        {
            if (_numTarget == null) return;

            string current = txtNumDisplay.Text;
            if (digit == "." && current.Contains(".")) return;
            if (current == "0" && digit != ".") current = "";
            current += digit;

            txtNumDisplay.Text = current;
            _numTarget.Text = current == "" ? "0" : current;
        }

        private void HandleBackspace()
        {
            if (_numTarget == null) return;

            string current = txtNumDisplay.Text;
            if (current.Length > 0)
                current = current.Substring(0, current.Length - 1);
            txtNumDisplay.Text = current;
            _numTarget.Text = current == "" ? "0" : current;
        }

        // ══════════════════════════════════════════════════════
        // MODE SWITCHING
        // ══════════════════════════════════════════════════════
        private void btnMode_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;
            string mode = btn.Name.Replace("btnMode", "");
            SetMode(mode);
        }

        void SetMode(string mode)
        {
            _mode = mode;

            // Reset button styles
            var inactive = (Style)FindResource("ModeBtn");
            var active = (Style)FindResource("ModeBtnActive");
            btnModeCashUSD.Style = inactive;
            btnModeCashLBP.Style = inactive;
            btnModeCard.Style = inactive;
            btnModeMixed.Style = inactive;

            pnlSingle.Visibility = Visibility.Collapsed;
            pnlMixed.Visibility = Visibility.Collapsed;
            pnlChange.Visibility = Visibility.Collapsed;

            switch (mode)
            {
                case "CashUSD":
                    btnModeCashUSD.Style = active;
                    pnlSingle.Visibility = Visibility.Visible;
                    pnlChange.Visibility = Visibility.Visible;
                    lblSingleTitle.Content = "CASH PAID (USD)";
                    lblSinglePrefix.Content = "$";
                    bdSingleInput.BorderBrush = new SolidColorBrush(Color.FromRgb(43, 108, 196));
                    txtSingleAmount.Foreground = new SolidColorBrush(Color.FromRgb(43, 108, 196));
                    bdSingleInput.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    _numTarget = txtSingleAmount;
                    _numIsUSD = true;
                    lblNumUnit.Content = "USD";
                    txtSingleAmount.Text = "0";
                    break;

                case "CashLBP":
                    btnModeCashLBP.Style = active;
                    pnlSingle.Visibility = Visibility.Visible;
                    pnlChange.Visibility = Visibility.Visible;
                    lblSingleTitle.Content = "CASH PAID (LBP)";
                    lblSinglePrefix.Content = "LBP";
                    bdSingleInput.BorderBrush = new SolidColorBrush(Color.FromRgb(160, 82, 10));
                    txtSingleAmount.Foreground = new SolidColorBrush(Color.FromRgb(160, 82, 10));
                    bdSingleInput.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    _numTarget = txtSingleAmount;
                    _numIsUSD = false;
                    lblNumUnit.Content = "LBP";
                    txtSingleAmount.Text = "0";
                    break;

                case "Card":
                    btnModeCard.Style = active;
                    pnlSingle.Visibility = Visibility.Visible;
                    lblSingleTitle.Content = "CARD — FULL AMOUNT";
                    lblSinglePrefix.Content = "LBP";
                    lblSingleConvert.Content = CurrencyHelper.FormatBothShort(_totalLBP);
                    bdSingleInput.BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85));
                    txtSingleAmount.Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85));
                    bdSingleInput.Background = new SolidColorBrush(Color.FromRgb(250, 250, 250));
                    txtSingleAmount.Text = _totalLBP.ToString("F0");
                    txtSingleAmount.IsReadOnly = true;
                    _numTarget = null; // card = no numpad entry needed
                    break;

                case "Mixed":
                    btnModeMixed.Style = active;
                    pnlMixed.Visibility = Visibility.Visible;
                    _numTarget = txtMixedUSD;
                    _numIsUSD = true;
                    lblNumUnit.Content = "USD";
                    txtMixedUSD.Text = "0";
                    txtMixedLBP.Text = "0";
                    txtMixedCard.Text = "0";
                    break;
            }

            if (mode != "Card") txtSingleAmount.IsReadOnly = false;
            RecalcChange();
        }

        // ══════════════════════════════════════════════════════
        // RECALCULATE CHANGE LIVE
        // ══════════════════════════════════════════════════════
        private void txtPayment_Changed(object sender, TextChangedEventArgs e)
            => RecalcChange();

        void RecalcChange()
        {
            // FIX: Add null checks to prevent NullReferenceException
            if (lblError == null || lblSingleConvert == null || lblChangeLine1 == null ||
                lblChangeLine2 == null || lblMixedPaid == null || lblMixedRemaining == null ||
                lblMixedChange == null)
                return;

            lblError.Content = "";

            if (_mode == "CashUSD")
            {
                // FIX: Check if textbox is null before accessing
                if (txtSingleAmount == null) return;

                double usd = ParseField(txtSingleAmount.Text);
                double paidLBP = CurrencyHelper.ToLBP(usd);
                double changeLBP = paidLBP - _totalLBP;

                lblSingleConvert.Content = paidLBP > 0
                    ? $"= {CurrencyHelper.FormatLBP(paidLBP)}" : "";

                if (changeLBP >= 0)
                {
                    lblChangeLine1.Content = CurrencyHelper.FormatUSD(changeLBP);
                    lblChangeLine2.Content = CurrencyHelper.FormatLBP(changeLBP);
                    lblChangeLine1.Foreground = new SolidColorBrush(Color.FromRgb(26, 122, 26));
                    lblChangeLine2.Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85));
                }
                else
                {
                    lblChangeLine1.Content = $"Short {CurrencyHelper.FormatUSD(Math.Abs(changeLBP))}";
                    lblChangeLine2.Content = $"Short {CurrencyHelper.FormatLBP(Math.Abs(changeLBP))}";
                    lblChangeLine1.Foreground = new SolidColorBrush(Color.FromRgb(176, 0, 0));
                    lblChangeLine2.Foreground = new SolidColorBrush(Color.FromRgb(176, 0, 0));
                }
            }
            else if (_mode == "CashLBP")
            {
                // FIX: Check if textbox is null before accessing
                if (txtSingleAmount == null) return;

                double paidLBP = ParseField(txtSingleAmount.Text);
                double changeLBP = paidLBP - _totalLBP;

                lblSingleConvert.Content = paidLBP > 0
                    ? $"= {CurrencyHelper.FormatUSD(paidLBP)}" : "";

                if (changeLBP >= 0)
                {
                    lblChangeLine1.Content = CurrencyHelper.FormatLBP(changeLBP);
                    lblChangeLine2.Content = CurrencyHelper.FormatUSD(changeLBP);
                    lblChangeLine1.Foreground = new SolidColorBrush(Color.FromRgb(26, 122, 26));
                    lblChangeLine2.Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85));
                }
                else
                {
                    lblChangeLine1.Content = $"Short {CurrencyHelper.FormatLBP(Math.Abs(changeLBP))}";
                    lblChangeLine2.Content = $"Short {CurrencyHelper.FormatUSD(Math.Abs(changeLBP))}";
                    lblChangeLine1.Foreground = new SolidColorBrush(Color.FromRgb(176, 0, 0));
                    lblChangeLine2.Foreground = new SolidColorBrush(Color.FromRgb(176, 0, 0));
                }
            }
            else if (_mode == "Mixed")
            {
                // FIX: Check if textboxes are null before accessing
                if (txtMixedUSD == null || txtMixedLBP == null || txtMixedCard == null) return;

                double usdLBP = CurrencyHelper.ToLBP(ParseField(txtMixedUSD.Text));
                double cashLBP = ParseField(txtMixedLBP.Text);
                double cardLBP = ParseField(txtMixedCard.Text);
                double totalPaid = usdLBP + cashLBP + cardLBP;
                double remaining = _totalLBP - totalPaid;
                double change = totalPaid - _totalLBP;

                lblMixedPaid.Content = CurrencyHelper.FormatBothShort(totalPaid);
                lblMixedRemaining.Content = remaining > 0
                    ? CurrencyHelper.FormatBothShort(remaining) : "—";
                lblMixedRemaining.Foreground = remaining > 0
                    ? new SolidColorBrush(Color.FromRgb(176, 0, 0))
                    : new SolidColorBrush(Color.FromRgb(85, 85, 85));
                lblMixedChange.Content = change > 0
                    ? CurrencyHelper.FormatBothShort(change) : "—";
            }
        }

        static double ParseField(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            string clean = text.Replace(",", "").Trim();
            return double.TryParse(clean, out double v) ? Math.Max(0, v) : 0;
        }

        // ══════════════════════════════════════════════════════
        // NUMPAD
        // ══════════════════════════════════════════════════════
        private void txtInput_GotFocus(object sender, RoutedEventArgs e)
        {
            if (!(sender is TextBox tb)) return;
            _numTarget = tb;

            // Determine if this field is USD or LBP
            if (tb == txtSingleAmount)
                _numIsUSD = _mode == "CashUSD";
            else if (tb == txtMixedUSD)
                _numIsUSD = true;
            else
                _numIsUSD = false;

            lblNumUnit.Content = _numIsUSD ? "USD" : "LBP";
            txtNumDisplay.Text = tb.Text == "0" ? "" : tb.Text;
        }

        private void numKey_Click(object sender, RoutedEventArgs e)
        {
            if (_numTarget == null) return;
            string digit = ((Button)sender).Content.ToString();

            string current = txtNumDisplay.Text;
            if (digit == "." && current.Contains(".")) return;
            if (current == "0" && digit != ".") current = "";
            current += digit;

            txtNumDisplay.Text = current;
            _numTarget.Text = current == "" ? "0" : current;
        }

        private void numBackspace_Click(object sender, RoutedEventArgs e)
        {
            HandleBackspace();
        }

        private void numApply_Click(object sender, RoutedEventArgs e)
        {
            // Already applied live — just clear the display
            txtNumDisplay.Text = "";
        }

        private void numClear_Click(object sender, RoutedEventArgs e)
        {
            txtNumDisplay.Text = "";
            if (_numTarget != null) _numTarget.Text = "0";
        }

        // ══════════════════════════════════════════════════════
        // QUICK AMOUNTS - UPDATED LBP AMOUNTS
        // ══════════════════════════════════════════════════════
        void BuildQuickAmounts()
        {
            // FIX: Check if wpQuick is null before using
            if (wpQuick == null) return;

            wpQuick.Children.Clear();

            // USD quick amounts
            double[] usdAmounts = { 1, 5, 10, 20, 50, 100 };
            foreach (double amt in usdAmounts)
            {
                var btn = new Button
                {
                    Content = $"${amt:N0}",
                    Height = 36,
                    Padding = new Thickness(14, 0, 14, 0),
                    Margin = new Thickness(0, 0, 6, 6),
                    Foreground = new SolidColorBrush(Color.FromRgb(43, 108, 196)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(43, 108, 196)),
                    BorderThickness = new Thickness(1),
                    FontSize = 12,
                    FontWeight = FontWeights.Normal,
                    FontFamily = new FontFamily("Segoe UI, Tahoma"),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = amt,
                    Style = (Style)FindResource("BigBtn")
                };
                btn.Click += quickUSD_Click;
                wpQuick.Children.Add(btn);
            }

            // LBP quick amounts - UPDATED: 5k, 10k, 20k, 50k, 100k, 500k, 1M
            double[] lbpAmounts = { 5000, 10000, 20000, 50000, 100000, 500000, 1000000 };
            foreach (double amt in lbpAmounts)
            {
                string label;
                if (amt >= 1000000)
                    label = "1M";
                else if (amt >= 1000)
                    label = $"{amt / 1000:N0}K";
                else
                    label = amt.ToString("N0");

                var btn = new Button
                {
                    Content = $"LBP {label}",
                    Height = 36,
                    Padding = new Thickness(14, 0, 14, 0),
                    Margin = new Thickness(0, 0, 6, 6),
                    Foreground = new SolidColorBrush(Color.FromRgb(160, 82, 10)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(160, 82, 10)),
                    BorderThickness = new Thickness(1),
                    FontSize = 12,
                    FontWeight = FontWeights.Normal,
                    FontFamily = new FontFamily("Segoe UI, Tahoma"),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = amt,
                    Style = (Style)FindResource("BigBtn")
                };
                btn.Click += quickLBP_Click;
                wpQuick.Children.Add(btn);
            }
        }

        // MODIFIED: Now accumulates (adds to current value) instead of replacing
        private void quickUSD_Click(object sender, RoutedEventArgs e)
        {
            double amt = Convert.ToDouble(((Button)sender).Tag);
            if (_mode == "CashUSD")
            {
                double cur = ParseField(txtSingleAmount.Text);
                txtSingleAmount.Text = (cur + amt).ToString("F2");
                txtNumDisplay.Text = txtSingleAmount.Text;
            }
            else if (_mode == "Mixed")
            {
                double cur = ParseField(txtMixedUSD.Text);
                txtMixedUSD.Text = (cur + amt).ToString("F2");
            }
        }

        // MODIFIED: Now accumulates (adds to current value) instead of replacing
        private void quickLBP_Click(object sender, RoutedEventArgs e)
        {
            double amt = Convert.ToDouble(((Button)sender).Tag);
            if (_mode == "CashLBP")
            {
                double cur = ParseField(txtSingleAmount.Text);
                txtSingleAmount.Text = (cur + amt).ToString("F0");
                txtNumDisplay.Text = txtSingleAmount.Text;
            }
            else if (_mode == "Mixed")
            {
                double cur = ParseField(txtMixedLBP.Text);
                txtMixedLBP.Text = (cur + amt).ToString("F0");
            }
        }

        // ══════════════════════════════════════════════════════
        // CONFIRM
        // ══════════════════════════════════════════════════════
        private void btnConfirm_Click(object sender, RoutedEventArgs e)
        {
            lblError.Content = "";

            switch (_mode)
            {
                case "CashUSD":
                    {
                        double usd = ParseField(txtSingleAmount.Text);
                        double paidLBP = CurrencyHelper.ToLBP(usd);
                        if (paidLBP < _totalLBP)
                        { lblError.Content = "⚠️ Amount paid is less than total due!"; return; }
                        CashPaidLBP = paidLBP;
                        CashPaidUSD = usd;
                        CardPaidLBP = 0;
                        ChangeDueLBP = paidLBP - _totalLBP;
                        ChangeDueUSD = CurrencyHelper.ToUSD(ChangeDueLBP);
                        PaymentMethod = "Cash USD";
                        break;
                    }
                case "CashLBP":
                    {
                        double paidLBP = ParseField(txtSingleAmount.Text);
                        if (paidLBP < _totalLBP)
                        { lblError.Content = "⚠️ Amount paid is less than total due!"; return; }
                        CashPaidLBP = paidLBP;
                        CashPaidUSD = CurrencyHelper.ToUSD(paidLBP);
                        CardPaidLBP = 0;
                        ChangeDueLBP = paidLBP - _totalLBP;
                        ChangeDueUSD = CurrencyHelper.ToUSD(ChangeDueLBP);
                        PaymentMethod = "Cash LBP";
                        break;
                    }
                case "Card":
                    {
                        CashPaidLBP = 0;
                        CashPaidUSD = 0;
                        CardPaidLBP = _totalLBP;
                        ChangeDueLBP = 0;
                        ChangeDueUSD = 0;
                        PaymentMethod = "Card";
                        break;
                    }
                case "Mixed":
                    {
                        double usdLBP = CurrencyHelper.ToLBP(ParseField(txtMixedUSD.Text));
                        double cashLBP = ParseField(txtMixedLBP.Text);
                        double cardLBP = ParseField(txtMixedCard.Text);
                        double totalPaid = usdLBP + cashLBP + cardLBP;
                        if (totalPaid < _totalLBP)
                        { lblError.Content = "⚠️ Total paid is less than total due!"; return; }
                        CashPaidLBP = usdLBP + cashLBP;
                        CashPaidUSD = ParseField(txtMixedUSD.Text);
                        CardPaidLBP = cardLBP;
                        ChangeDueLBP = totalPaid - _totalLBP;
                        ChangeDueUSD = CurrencyHelper.ToUSD(ChangeDueLBP);
                        PaymentMethod = "Mixed";
                        break;
                    }
            }

            Confirmed = true;
            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            DialogResult = false;
            Close();
        }
    }
}