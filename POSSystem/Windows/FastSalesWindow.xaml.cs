using POSSystem.Database;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace POSSystem.Windows
{
    public partial class FastSalesWindow : Window
    {
        List<CartItem> cart = new List<CartItem>();
        double totalDiscount = 0;
        double _currentTotal = 0;
        int _selectedIndex = -1;
        int _currentCustomerId = 0;
        string _currentCustomerName = "";

        System.Windows.Threading.DispatcherTimer timer =
            new System.Windows.Threading.DispatcherTimer();

        public FastSalesWindow()
        {
            InitializeComponent();
            lblDate.Content = $"📅 {DateTime.Now:dd/MM/yyyy}";
            lblTime.Content = $"🕐 {DateTime.Now:hh:mm tt}";
            lblCashier.Content = $"👤 {Session.Username}";
            LoadInvoiceNumber();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (s, e) => lblTime.Content = $"🕐 {DateTime.Now:hh:mm tt}";
            timer.Start();
            this.Closed += (s, e) => timer.Stop();
            this.Loaded += (s, e) => FocusScan();
            UpdateHeldCount();
        }

        void FocusScan()
        {
            txtScanCode.Focus();
            txtScanCode.SelectAll();
        }

        void LoadInvoiceNumber()
        {
            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                int count = Convert.ToInt32(
                    new SQLiteCommand("SELECT COUNT(*) FROM Sales", con).ExecuteScalar());
                lblInvoice.Content = $"Invoice #: {count + 1:D4}";
            }
        }

        // ── SCAN ──────────────────────────────────────
        private void txtScanCode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string code = txtScanCode.Text.Trim();
                txtScanCode.Clear();
                if (!string.IsNullOrEmpty(code)) AddByCode(code);
                e.Handled = true;
            }
        }

        void AddByCode(string code)
        {
            int qty = 1;
            if (int.TryParse(txtQty.Text, out int pq) && pq > 0) qty = pq;

            Product found = null;
            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var cmd = new SQLiteCommand(
                    "SELECT * FROM Products WHERE ProductCode=@code AND Stock>0", con);
                cmd.Parameters.AddWithValue("@code", code);
                var r = cmd.ExecuteReader();
                if (r.Read())
                    found = new Product
                    {
                        Id = Convert.ToInt32(r["Id"]),
                        Name = r["Name"].ToString(),
                        Price = Convert.ToDouble(r["Price"]),
                        Stock = Convert.ToInt32(r["Stock"]),
                        Category = r["Category"].ToString(),
                        ProductCode = r["ProductCode"].ToString()
                    };
                r.Close();
            }

            if (found != null) { AddToCart(found, qty); ShowMsg($"✅ {found.Name}", true); }
            else ShowMsg($"❌ '{code}' not found", false);

            txtQty.Text = "1";
            txtNumpad.Text = "";
            FocusScan();
        }

        // ── CART ──────────────────────────────────────
        void AddToCart(Product p, int qty = 1)
        {
            if (qty > p.Stock) { ShowMsg($"⚠️ Only {p.Stock} in stock!", false); return; }
            var existing = cart.Find(c => c.ProductId == p.Id);
            if (existing != null)
            {
                if (existing.Quantity + qty > p.Stock)
                { ShowMsg($"⚠️ Max stock for {p.Name}!", false); return; }
                existing.Quantity += qty;
                existing.TotalPrice = existing.Quantity * existing.UnitPrice
                                      * (1 - existing.DiscountPct / 100.0);
            }
            else
            {
                var item = new CartItem
                {
                    ProductId = p.Id,
                    ProductCode = p.ProductCode,
                    ProductName = p.Name,
                    Category = p.Category,
                    Quantity = qty,
                    UnitPrice = p.Price,
                    TotalPrice = p.Price * qty,
                    DiscountPct = 0
                };

                // ── APPLY ACTIVE PROMOTIONS ──
                double discPct = PromotionsWindow.GetDiscountPct(p.Name, p.Category, qty);
                double fixedOff = PromotionsWindow.GetFixedDiscount(p.Name, p.Category);

                if (discPct > 0)
                {
                    item.DiscountPct = discPct;
                    item.TotalPrice = item.UnitPrice * item.Quantity * (1 - discPct / 100.0);
                    ShowMsg($"🏷️ {discPct}% promo applied to {p.Name}!", true);
                }
                else if (fixedOff > 0)
                {
                    double fixedPct = Math.Min(100, fixedOff / item.UnitPrice * 100);
                    item.DiscountPct = fixedPct;
                    item.TotalPrice = Math.Max(0, item.UnitPrice * item.Quantity - fixedOff);
                    ShowMsg($"🏷️ LBP {fixedOff:N0} off on {p.Name}!", true);
                }

                cart.Add(item);
            }
            RefreshCart();
        }

        void RefreshCart()
        {
            lvCart.Items.Clear();
            int row = 1; double subtotal = 0;
            int totalQty = 0; totalDiscount = 0;

            foreach (var c in cart)
            {
                c.RowNumber = row++;
                lvCart.Items.Add(c);
                subtotal += c.UnitPrice * c.Quantity;
                totalQty += c.Quantity;
                totalDiscount += (c.UnitPrice * c.Quantity) - c.TotalPrice;
            }

            _currentTotal = Math.Max(0, subtotal - totalDiscount);

            // Update all labels with dual currency
            lblNetPay.Content = CurrencyHelper.FormatLBP(_currentTotal);
            if (lblNetPayUSD != null)
                lblNetPayUSD.Content = CurrencyHelper.FormatUSD(_currentTotal);

            lblFooterItems.Content = cart.Count.ToString();
            lblFooterQty.Content = totalQty.ToString();
            lblFooterDiscount.Content = CurrencyHelper.FormatLBP(totalDiscount);
            lblFooterSubtotal.Content = CurrencyHelper.FormatLBP(subtotal);
            lblFooterTotal.Content = CurrencyHelper.FormatLBP(_currentTotal);
            if (lblFooterTotalUSD != null)
                lblFooterTotalUSD.Content = CurrencyHelper.FormatUSD(_currentTotal);
        }

        private void lvCart_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => _selectedIndex = lvCart.SelectedIndex;

        private void btnRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;
            int productId = Convert.ToInt32(btn.Tag);
            cart.RemoveAll(c => c.ProductId == productId);
            RefreshCart();
            FocusScan();
        }

        // ── NUMPAD ────────────────────────────────────
        private void btnNum_Click(object sender, RoutedEventArgs e)
        {
            string d = ((Button)sender).Content.ToString();
            if (d == "." && txtNumpad.Text.Contains(".")) return;
            txtNumpad.Text += d;
        }

        private void btnNumBackspace_Click(object sender, RoutedEventArgs e)
        {
            if (txtNumpad.Text.Length > 0)
                txtNumpad.Text = txtNumpad.Text.Substring(0, txtNumpad.Text.Length - 1);
        }

        private void btnNumClear_Click(object sender, RoutedEventArgs e)
            => txtNumpad.Text = "";

        private void btnSetQty_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtNumpad.Text, out int qty) && qty > 0)
            {
                txtQty.Text = qty.ToString();
                txtNumpad.Text = "";
                ShowMsg($"Qty set to {qty} — scan item", true);
                FocusScan();
            }
            else ShowMsg("Enter a valid quantity first!", false);
        }

        // ── DISCOUNT ──────────────────────────────────
        private void btnDiscount_Click(object sender, RoutedEventArgs e)
        {
            if (cart.Count == 0) { ShowMsg("Cart is empty!", false); return; }

            string input = ShowInputDialog(
                "Enter discount % for ALL items:", "Discount", "0");

            if (double.TryParse(input, out double pct) && pct >= 0 && pct <= 100)
            {
                foreach (var item in cart)
                {
                    item.DiscountPct = pct;
                    item.TotalPrice = item.UnitPrice * item.Quantity * (1 - pct / 100.0);
                }
                RefreshCart();
                ShowMsg($"{pct}% discount applied", true);
            }
        }

        // ── VOID ──────────────────────────────────────
        private void btnVoid_Click(object sender, RoutedEventArgs e)
        {
            if (!Session.IsAdmin) { ShowMsg("⛔ Admins only!", false); return; }
            if (cart.Count == 0) { ShowMsg("Cart is empty!", false); return; }

            if (MessageBox.Show("Void this entire sale?", "⛔ Void",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            string summary = "";
            foreach (var c in cart) summary += $"{c.ProductName} x{c.Quantity}, ";

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var cmd = new SQLiteCommand(
                    "INSERT INTO VoidedSales (Date,CashierName,Items) VALUES (@d,@c,@i)", con);
                cmd.Parameters.AddWithValue("@d", DatabaseHelper.ToIsoDateTime(DateTime.Now));
                cmd.Parameters.AddWithValue("@c", Session.Username);
                cmd.Parameters.AddWithValue("@i", summary);
                cmd.ExecuteNonQuery();
            }

            // ── AUDIT LOG ──
            DatabaseHelper.Log("Void", $"Sale voided — {summary}", "Void");

            cart.Clear(); RefreshCart();
            ShowMsg("✅ Sale voided.", true);
            FocusScan();
        }

        // ── PRINT ─────────────────────────────────────
        private void btnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (cart.Count == 0) { ShowMsg("Cart is empty!", false); return; }
            PrintReceipt(0, _currentTotal);
        }

        // ── HOLD CART ─────────────────────────────────
        private void btnHold_Click(object sender, RoutedEventArgs e)
        {
            if (cart.Count == 0) { ShowMsg("Cart is empty!", false); return; }

            string holdName = ShowInputDialog(
                "Name this hold (e.g. Customer 1, Table 3):",
                "Hold Cart",
                $"Hold {DateTime.Now:HH:mm}");

            if (string.IsNullOrEmpty(holdName)) return;

            string cartData = SerializeCart();
            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var cmd = new SQLiteCommand(@"
                    INSERT INTO HeldCarts (HoldName,CartData,CreatedAt,CashierName)
                    VALUES (@name,@data,@at,@cashier)", con);
                cmd.Parameters.AddWithValue("@name", holdName);
                cmd.Parameters.AddWithValue("@data", cartData);
                cmd.Parameters.AddWithValue("@at", DatabaseHelper.ToIsoDateTime(DateTime.Now));
                cmd.Parameters.AddWithValue("@cashier", Session.Username);
                cmd.ExecuteNonQuery();
            }

            cart.Clear();
            _currentCustomerId = 0;
            _currentCustomerName = "";
            RefreshCart();
            ResetSale();
            ShowMsg($"⏸️ Cart held as '{holdName}'", true);
            UpdateHeldCount();
        }

        // ── RESUME CART ───────────────────────────────
        public void ResumeCart(int holdId, string cartData)
        {
            if (cart.Count > 0 &&
                MessageBox.Show("Replace current cart with held cart?",
                    "Resume Hold", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            cart.Clear();
            DeserializeCart(cartData);
            RefreshCart();

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                new SQLiteCommand($"DELETE FROM HeldCarts WHERE Id={holdId}", con).ExecuteNonQuery();
            }
            UpdateHeldCount();
        }

        public void UpdateHeldCount()
        {
            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                int count = Convert.ToInt32(
                    new SQLiteCommand("SELECT COUNT(*) FROM HeldCarts", con).ExecuteScalar());
                lblHeldCount.Content = count > 0 ? $"⏸️ {count} held" : "";
            }
        }

        string SerializeCart()
        {
            var parts = new List<string>();
            foreach (var c in cart)
                parts.Add($"{c.ProductId}|{c.ProductName}|{c.Quantity}|{c.UnitPrice}|{c.DiscountPct}|{c.ProductCode}|{c.Category}");
            return string.Join(";", parts);
        }

        void DeserializeCart(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            foreach (string part in data.Split(';'))
            {
                string[] f = part.Split('|');
                if (f.Length < 6) continue;
                cart.Add(new CartItem
                {
                    ProductId = int.Parse(f[0]),
                    ProductName = f[1],
                    Quantity = int.Parse(f[2]),
                    UnitPrice = double.Parse(f[3]),
                    DiscountPct = double.Parse(f[4]),
                    ProductCode = f[5],
                    Category = f.Length > 6 ? f[6] : "",
                    TotalPrice = double.Parse(f[3]) * int.Parse(f[2])
                                  * (1 - double.Parse(f[4]) / 100.0)
                });
            }
        }

        private void lblHeldCount_Click(object sender,
            System.Windows.Input.MouseButtonEventArgs e)
        {
            var win = new HeldCartsWindow(this);
            win.Owner = this;
            win.ShowDialog();
        }

        // ── CUSTOMER ──────────────────────────────────
        private void btnCustomer_Click(object sender, RoutedEventArgs e)
        {
            var win = new CustomerPickerWindow();
            win.Owner = this;
            win.ShowDialog();

            if (win.SelectedCustomer != null)
            {
                _currentCustomerId = win.SelectedCustomer.Id;
                _currentCustomerName = win.SelectedCustomer.Name;
                lblCashier.Content =
                    $"👤 {Session.Username}  |  🧑 {_currentCustomerName}";
                ShowMsg($"✅ Customer: {_currentCustomerName} ({win.SelectedCustomer.Points} pts)", true);
            }
        }

        // ── QUICK PAY ─────────────────────────────────
        private void btnQuickPay_Click(object sender, RoutedEventArgs e)
        {
            if (cart.Count == 0) { ShowMsg("Cart is empty!", false); return; }
            int saleId = SaveSaleToDb("Cash");
            AwardLoyalty(saleId);
            PrintReceipt(saleId, _currentTotal);
            ResetSale();
            ShowMsg("⚡ Quick Pay done!", true);
        }

        // ── PAY ───────────────────────────────────────
        private void btnPay_Click(object sender, RoutedEventArgs e)
        {
            if (cart.Count == 0) { ShowMsg("Cart is empty!", false); return; }

            var payWin = new PaymentWindow(_currentTotal);
            payWin.Owner = this;
            bool? result = payWin.ShowDialog();

            if (result == true && payWin.Confirmed)
            {
                int saleId = SaveSaleToDb(payWin.PaymentMethod,
                    payWin.CashPaidLBP, payWin.CardPaidLBP, payWin.ChangeDueLBP);
                AwardLoyalty(saleId);
                PrintReceipt(saleId, _currentTotal);
                ResetSale();

                if (payWin.ChangeDueLBP > 0)
                    MessageBox.Show(
                        $"💵 Change Due:\n" +
                        $"{CurrencyHelper.FormatLBP(payWin.ChangeDueLBP)}\n" +
                        $"{CurrencyHelper.FormatUSD(payWin.ChangeDueLBP)}",
                        "Change Due", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ── LOYALTY AWARD ─────────────────────────────
        void AwardLoyalty(int saleId)
        {
            if (_currentCustomerId <= 0) return;

            int pointsEarned = (int)(_currentTotal / 1000);
            int stampsEarned = 1;

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                new SQLiteCommand(
                    $"UPDATE Customers SET Points=Points+{pointsEarned}, " +
                    $"Stamps=Stamps+{stampsEarned}, " +
                    $"TotalSpent=TotalSpent+{_currentTotal} " +
                    $"WHERE Id={_currentCustomerId}", con).ExecuteNonQuery();

                CustomersWindow.LogLoyalty(con, _currentCustomerId, saleId,
                    pointsEarned, stampsEarned, "Earn",
                    $"Sale #{saleId} — LBP {_currentTotal:N0}");

                try
                {
                    new SQLiteCommand(
                        $"UPDATE Sales SET CustomerId={_currentCustomerId}, " +
                        $"PointsEarned={pointsEarned}, StampsEarned={stampsEarned} " +
                        $"WHERE Id={saleId}", con).ExecuteNonQuery();
                }
                catch { }
            }
            ShowMsg($"⭐ {_currentCustomerName} earned {pointsEarned} pts + 1 stamp!", true);
        }

        // ── SAVE TO DB ────────────────────────────────
        int SaveSaleToDb(string method,
            double cash = 0, double card = 0, double change = 0)
        {
            int saleId = 0;
            int pointsEarned = _currentCustomerId > 0 ? (int)(_currentTotal / 1000) : 0;
            int stampsEarned = _currentCustomerId > 0 ? 1 : 0;

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var cmd = new SQLiteCommand(@"
                    INSERT INTO Sales
                    (Date,TotalAmount,CashPaid,CardPaid,ChangeDue,PaymentMethod,CashierName,CustomerId,PointsEarned,StampsEarned,PointsRedeemed)
                    VALUES (@d,@t,@ca,@cr,@ch,@m,@cashier,@cid,@pe,@se,@pr)", con);
                cmd.Parameters.AddWithValue("@d", DatabaseHelper.ToIsoDateTime(DateTime.Now));
                cmd.Parameters.AddWithValue("@t", _currentTotal);
                cmd.Parameters.AddWithValue("@ca", cash);
                cmd.Parameters.AddWithValue("@cr", card);
                cmd.Parameters.AddWithValue("@ch", change);
                cmd.Parameters.AddWithValue("@m", method);
                cmd.Parameters.AddWithValue("@cashier", Session.Username);
                cmd.Parameters.AddWithValue("@cid", _currentCustomerId);
                cmd.Parameters.AddWithValue("@pe", pointsEarned);
                cmd.Parameters.AddWithValue("@se", stampsEarned);
                cmd.Parameters.AddWithValue("@pr", 0);
                cmd.ExecuteNonQuery();

                saleId = Convert.ToInt32(
                    new SQLiteCommand("SELECT last_insert_rowid()", con).ExecuteScalar());

                foreach (var item in cart)
                {
                    var ic = new SQLiteCommand(
                        "INSERT INTO SaleItems(SaleId,ProductId,Quantity,Price) VALUES(@s,@p,@q,@pr)",
                        con);
                    ic.Parameters.AddWithValue("@s", saleId);
                    ic.Parameters.AddWithValue("@p", item.ProductId);
                    ic.Parameters.AddWithValue("@q", item.Quantity);
                    ic.Parameters.AddWithValue("@pr", item.TotalPrice);
                    ic.ExecuteNonQuery();

                    new SQLiteCommand(
                        $"UPDATE Products SET Stock=Stock-{item.Quantity} WHERE Id={item.ProductId}",
                        con).ExecuteNonQuery();
                }
            }

            // ── AUDIT LOG ──
            DatabaseHelper.Log("Sale",
                $"Sale #{saleId} — LBP {_currentTotal:N0} — {cart.Count} items", "Sale");

            return saleId;
        }

        void PrintReceipt(int saleId, double total)
        {
            var items = new List<ReceiptItem>();
            foreach (var c in cart)
                items.Add(new ReceiptItem
                { Name = c.ProductName, Quantity = c.Quantity, Price = c.TotalPrice });
            var sale = new Sale { Id = saleId, Date = DatabaseHelper.ToIsoDateTime(DateTime.Now), TotalAmount = total };
            var receipt = new ReceiptWindow();
            receipt.LoadReceipt(sale, items);
            receipt.Show();
            receipt.Activate();
        }

        void ResetSale()
        {
            cart.Clear();
            totalDiscount = 0;
            _currentTotal = 0;
            _selectedIndex = -1;
            _currentCustomerId = 0;
            _currentCustomerName = "";
            txtNumpad.Text = "";
            txtQty.Text = "1";
            lblCashier.Content = $"👤 {Session.Username}";
            RefreshCart();
            LoadInvoiceNumber();
            FocusScan();
        }

        private void btnGridMode_Click(object sender, RoutedEventArgs e)
            => new GridSalesWindow().Show();

        void ShowMsg(string msg, bool success)
        {
            lblScanMsg.Content = msg;
            lblScanMsg.Foreground = success
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C62828"));

            var t = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromSeconds(3) };
            t.Tick += (s, ev) => { lblScanMsg.Content = ""; t.Stop(); };
            t.Start();
        }

        string ShowInputDialog(string message, string title, string defaultValue = "")
        {
            var dlg = new Window
            {
                Title = title,
                Width = 360,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C3E50"))
            };

            var stack = new StackPanel { Margin = new Thickness(16) };
            stack.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var txt = new TextBox
            {
                Text = defaultValue,
                Padding = new Thickness(6),
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 12)
            };
            stack.Children.Add(txt);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var btnOk = new Button { Content = "OK", Width = 80, Height = 30, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var btnCancel = new Button { Content = "Cancel", Width = 80, Height = 30, IsCancel = true };

            string result = null;
            btnOk.Click += (s, ev) => { result = txt.Text; dlg.DialogResult = true; };
            btnCancel.Click += (s, ev) => { dlg.DialogResult = false; };

            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            stack.Children.Add(btnPanel);

            dlg.Content = stack;
            dlg.Loaded += (s, ev) => { txt.Focus(); txt.SelectAll(); };
            dlg.ShowDialog();

            return result;
        }
    }
}