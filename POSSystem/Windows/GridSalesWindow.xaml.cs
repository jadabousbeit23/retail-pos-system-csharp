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
    public partial class GridSalesWindow : Window
    {
        List<CartItem> cart = new List<CartItem>();
        double _currentTotal = 0;
        double totalDiscount = 0;
        int _selectedIndex = -1;
        int _currentCustomerId = 0;
        string _currentCustomerName = "";

        bool _inProducts = false;
        string _currentCategory = "";
        int _pendingQty = 1;

        Dictionary<string, string> _categoryColors = new Dictionary<string, string>();

        string[] _fallbackColors = {
            "#1565C0","#0277BD","#00695C","#2E7D32",
            "#558B2F","#6A1B9A","#AD1457","#C62828",
            "#E65100","#4E342E","#37474F","#0063B1"
        };
        int _colorIndex = 0;

        System.Windows.Threading.DispatcherTimer _timer =
            new System.Windows.Threading.DispatcherTimer();

        public GridSalesWindow()
        {
            InitializeComponent();
            lblDate.Content = $"📅 {DateTime.Now:dd/MM/yyyy}";
            lblTime.Content = $"🕐 {DateTime.Now:hh:mm tt}";
            lblCashier.Content = $"👤 {Session.Username}";
            LoadInvoiceNumber();

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) => lblTime.Content = $"🕐 {DateTime.Now:hh:mm tt}";
            _timer.Start();
            this.Closed += (s, e) => _timer.Stop();

            UpdateHeldCount();
            LoadCategoryColors();
            ShowCategories();
        }

        void LoadCategoryColors()
        {
            _categoryColors.Clear();
            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                try
                {
                    new SQLiteCommand(@"
                        CREATE TABLE IF NOT EXISTS Categories (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Name TEXT NOT NULL UNIQUE,
                            ColorCode TEXT NOT NULL DEFAULT '#4CAF50'
                        )", con).ExecuteNonQuery();
                }
                catch { }

                var cmd = new SQLiteCommand(
                    "SELECT Name, ColorCode FROM Categories ORDER BY Name", con);
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string name = reader["Name"].ToString();
                    string color = reader["ColorCode"].ToString();
                    if (!string.IsNullOrEmpty(color))
                        _categoryColors[name] = color;
                }
                reader.Close();
            }
        }

        string GetCategoryColor(string categoryName)
        {
            if (_categoryColors.ContainsKey(categoryName))
                return _categoryColors[categoryName];

            string color = _fallbackColors[_colorIndex % _fallbackColors.Length];
            _colorIndex++;
            return color;
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

        void ShowCategories()
        {
            _inProducts = false;
            _currentCategory = "";
            lblCategory.Content = "Select a Category";
            btnBack.Visibility = Visibility.Collapsed;
            wpTiles.Children.Clear();
            _colorIndex = 0;

            var cats = new List<(string Name, string Color)>();
            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var r = new SQLiteCommand(
                    "SELECT DISTINCT Category FROM Products WHERE Stock > 0 ORDER BY Category",
                    con).ExecuteReader();
                while (r.Read())
                {
                    string catName = r["Category"].ToString();
                    string color = GetCategoryColor(catName);
                    cats.Add((catName, color));
                }
                r.Close();
            }

            foreach (var cat in cats)
                AddTile(cat.Name, cat.Name.Length >= 2 ? cat.Name.Substring(0, 2).ToUpper() : cat.Name.ToUpper(),
                    isCat: true, colorHex: cat.Color);

            lblTileCount.Content = $"{cats.Count} categories";
        }

        void ShowProducts(string category)
        {
            _inProducts = true;
            _currentCategory = category;
            lblCategory.Content = category;
            btnBack.Visibility = Visibility.Visible;
            wpTiles.Children.Clear();
            _colorIndex = 0;

            var products = new List<(int Id, string Name, double Price, int Stock, string Code)>();
            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var cmd = new SQLiteCommand(
                    "SELECT Id, Name, Price, Stock, ProductCode FROM Products " +
                    "WHERE Category=@cat AND Stock > 0 ORDER BY Name", con);
                cmd.Parameters.AddWithValue("@cat", category);
                var r = cmd.ExecuteReader();
                while (r.Read())
                    products.Add((
                        Convert.ToInt32(r["Id"]),
                        r["Name"].ToString(),
                        Convert.ToDouble(r["Price"]),
                        Convert.ToInt32(r["Stock"]),
                        r["ProductCode"].ToString()));
                r.Close();
            }

            string categoryColor = GetCategoryColor(category);

            foreach (var p in products)
            {
                string initials = p.Name.Length >= 2
                    ? p.Name.Substring(0, 2).ToUpper() : p.Name.ToUpper();
                string priceLine = CurrencyHelper.FormatBothShort(p.Price);
                AddTile(p.Name, initials, isCat: false,
                    subtitle: priceLine,
                    productId: p.Id, price: p.Price,
                    stock: p.Stock, code: p.Code,
                    colorHex: categoryColor);
            }

            lblTileCount.Content = $"{products.Count} products";
        }

        void AddTile(string label, string initials, bool isCat,
            string subtitle = "", int productId = 0,
            double price = 0, int stock = 0, string code = "",
            string colorHex = null)
        {
            string color = colorHex ?? GetCategoryColor(label);

            var tile = new Button
            {
                Width = 130,
                Height = 110,
                Margin = new Thickness(6),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = isCat ? (object)label : productId
            };

            tile.Template = CreateTileTemplate(color);

            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(new System.Windows.Controls.Label
            {
                Content = initials,
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center
            });
            sp.Children.Add(new System.Windows.Controls.Label
            {
                Content = label,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center
            });

            if (!string.IsNullOrEmpty(subtitle))
                sp.Children.Add(new System.Windows.Controls.Label
                {
                    Content = subtitle,
                    FontSize = 9,
                    Foreground = Brushes.White,
                    Padding = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Opacity = 0.85
                });

            if (stock > 0 && !isCat)
                sp.Children.Add(new System.Windows.Controls.Label
                {
                    Content = $"Stock: {stock}",
                    FontSize = 9,
                    Foreground = Brushes.White,
                    Padding = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Opacity = 0.75
                });

            tile.Content = sp;

            if (isCat)
                tile.Click += (s, e) => ShowProducts(label);
            else
            {
                double priceCopy = price;
                int idCopy = productId;
                int stockCopy = stock;
                string codeCopy = code;
                string nameCopy = label;
                tile.Click += (s, e) =>
                {
                    int qty = _pendingQty;
                    _pendingQty = 1;
                    if (stockCopy <= 0) { ShowMsg("⚠️ Out of stock!", false); return; }
                    var p = new Product
                    {
                        Id = idCopy,
                        Name = nameCopy,
                        Price = priceCopy,
                        Stock = stockCopy,
                        ProductCode = codeCopy,
                        Category = _currentCategory
                    };
                    AddToCart(p, qty);
                    ShowMsg($"✅ {nameCopy} added", true);
                };
            }

            wpTiles.Children.Add(tile);
        }

        ControlTemplate CreateTileTemplate(string color)
        {
            var template = new ControlTemplate(typeof(Button));
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetValue(Border.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)));
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            var effect = new System.Windows.Media.Effects.DropShadowEffect
            { BlurRadius = 6, ShadowDepth = 2, Opacity = 0.15, Color = Colors.Black };
            factory.SetValue(Border.EffectProperty, effect);
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.AppendChild(presenter);
            template.VisualTree = factory;
            return template;
        }

        private void txtScanCode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            string code = txtScanCode.Text.Trim();
            txtScanCode.Clear();
            if (string.IsNullOrEmpty(code)) return;

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
            e.Handled = true;
        }

        void AddToCart(Product p, int qty = 1)
        {
            if (qty > p.Stock) { ShowMsg($"⚠️ Only {p.Stock} in stock!", false); return; }

            double discPct = PromotionsWindow.GetDiscountPct(p.Name, p.Category, qty);
            double fixedOff = PromotionsWindow.GetFixedDiscount(p.Name, p.Category);

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
                double total = p.Price * qty;
                double disc = 0;

                if (discPct > 0)
                {
                    disc = discPct;
                    total = p.Price * qty * (1 - discPct / 100.0);
                    ShowMsg($"🏷️ {discPct}% promo applied!", true);
                }
                else if (fixedOff > 0)
                {
                    disc = Math.Min(100, fixedOff / p.Price * 100);
                    total = Math.Max(0, p.Price * qty - fixedOff);
                    ShowMsg($"🏷️ LBP {fixedOff:N0} off!", true);
                }

                cart.Add(new CartItem
                {
                    ProductId = p.Id,
                    ProductCode = p.ProductCode,
                    ProductName = p.Name,
                    Quantity = qty,
                    UnitPrice = p.Price,
                    TotalPrice = total,
                    DiscountPct = disc,
                    Category = p.Category
                });
            }
            RefreshCart();
        }

        void RefreshCart()
        {
            lvCart.Items.Clear();
            int row = 1; double subtotal = 0; int totalQty = 0;
            totalDiscount = 0;

            foreach (var c in cart)
            {
                c.RowNumber = row++;
                lvCart.Items.Add(c);
                subtotal += c.UnitPrice * c.Quantity;
                totalQty += c.Quantity;
                totalDiscount += (c.UnitPrice * c.Quantity) - c.TotalPrice;
            }

            _currentTotal = Math.Max(0, subtotal - totalDiscount);

            lblNetPayLBP.Content = CurrencyHelper.FormatLBP(_currentTotal);
            lblNetPayUSD.Content = CurrencyHelper.FormatUSD(_currentTotal);
            lblFooterItems.Content = cart.Count.ToString();
            lblFooterQty.Content = totalQty.ToString();
            lblFooterDiscount.Content = CurrencyHelper.FormatLBP(totalDiscount);
            lblFooterSubtotal.Content = CurrencyHelper.FormatLBP(subtotal);
            lblFooterTotal.Content = CurrencyHelper.FormatLBP(_currentTotal);
            lblFooterTotalUSD.Content = CurrencyHelper.FormatUSD(_currentTotal);
        }

        private void lvCart_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => _selectedIndex = lvCart.SelectedIndex;

        private void btnRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;
            cart.RemoveAll(c => c.ProductId == Convert.ToInt32(btn.Tag));
            RefreshCart();
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
            => ShowCategories();

        private void btnSetQty_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtQty.Text, out int qty) && qty > 0)
            {
                _pendingQty = qty;
                ShowMsg($"Qty set to {qty} — tap a product", true);
            }
            else ShowMsg("⚠️ Enter a valid quantity first!", false);
        }

        private void btnNumClear_Click(object sender, RoutedEventArgs e)
        {
            txtQty.Text = "1";
            _pendingQty = 1;
        }

        private void btnDiscount_Click(object sender, RoutedEventArgs e)
        {
            if (cart.Count == 0) { ShowMsg("Cart is empty!", false); return; }
            string input = ShowInputDialog("Enter discount % for ALL items:", "Discount", "0");
            if (!double.TryParse(input, out double pct) || pct < 0 || pct > 100) return;
            foreach (var item in cart)
            {
                item.DiscountPct = pct;
                item.TotalPrice = item.UnitPrice * item.Quantity * (1 - pct / 100.0);
            }
            RefreshCart();
            ShowMsg($"{pct}% discount applied", true);
        }

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
            DatabaseHelper.Log("Void", $"Cart voided — {summary}", "Void");
            cart.Clear();
            RefreshCart();
            ShowMsg("✅ Sale voided.", true);
        }

        private void btnHold_Click(object sender, RoutedEventArgs e)
        {
            if (cart.Count == 0) { ShowMsg("Cart is empty!", false); return; }
            string holdName = ShowInputDialog("Name this hold:", "Hold Cart", $"Hold {DateTime.Now:HH:mm}");
            if (string.IsNullOrEmpty(holdName)) return;

            string cartData = SerializeCart();
            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var cmd = new SQLiteCommand(@"
                    INSERT INTO HeldCarts (HoldName,CartData,CreatedAt,CashierName)
                    VALUES (@n,@d,@at,@c)", con);
                cmd.Parameters.AddWithValue("@n", holdName);
                cmd.Parameters.AddWithValue("@d", cartData);
                cmd.Parameters.AddWithValue("@at", DatabaseHelper.ToIsoDateTime(DateTime.Now));
                cmd.Parameters.AddWithValue("@c", Session.Username);
                cmd.ExecuteNonQuery();
            }
            cart.Clear();
            _currentCustomerId = 0;
            _currentCustomerName = "";
            RefreshCart();
            ResetLabels();
            ShowMsg($"⏸️ Held as '{holdName}'", true);
            UpdateHeldCount();
        }

        public void ResumeCart(int holdId, string cartData)
        {
            if (cart.Count > 0 &&
                MessageBox.Show("Replace current cart with held cart?",
                    "Resume", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
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

        private void lblHeldCount_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var win = new HeldCartsWindow(this);
            win.Owner = this;
            win.ShowDialog();
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
                if (f.Length < 5) continue;
                cart.Add(new CartItem
                {
                    ProductId = int.Parse(f[0]),
                    ProductName = f[1],
                    Quantity = int.Parse(f[2]),
                    UnitPrice = double.Parse(f[3]),
                    DiscountPct = double.Parse(f[4]),
                    ProductCode = f.Length > 5 ? f[5] : "",
                    Category = f.Length > 6 ? f[6] : "",
                    TotalPrice = double.Parse(f[3]) * int.Parse(f[2])
                                  * (1 - double.Parse(f[4]) / 100.0)
                });
            }
        }

        private void btnCustomer_Click(object sender, RoutedEventArgs e)
        {
            var win = new CustomerPickerWindow();
            win.Owner = this;
            win.ShowDialog();
            if (win.SelectedCustomer != null)
            {
                _currentCustomerId = win.SelectedCustomer.Id;
                _currentCustomerName = win.SelectedCustomer.Name;
                lblCashier.Content = $"👤 {Session.Username}  |  🧑 {_currentCustomerName}";
                lblCustomerFooter.Content = $"🧑 {_currentCustomerName}";
                ShowMsg($"✅ Customer: {_currentCustomerName}", true);
            }
        }

        private void btnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (cart.Count == 0) { ShowMsg("Cart is empty!", false); return; }
            PrintReceipt(0, _currentTotal);
        }

        private void btnQuickPay_Click(object sender, RoutedEventArgs e)
        {
            if (cart.Count == 0) { ShowMsg("Cart is empty!", false); return; }
            int saleId = SaveSaleToDb("Cash LBP");
            AwardLoyalty(saleId);
            PrintReceipt(saleId, _currentTotal);
            ResetSale();
            ShowMsg("⚡ Quick Pay done!", true);
        }

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
                        $"💵 Change:\n{CurrencyHelper.FormatLBP(payWin.ChangeDueLBP)}\n{CurrencyHelper.FormatUSD(payWin.ChangeDueLBP)}",
                        "Change Due", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

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
            ShowMsg($"⭐ {_currentCustomerName} earned {pointsEarned} pts!", true);
        }

        int SaveSaleToDb(string method, double cash = 0, double card = 0, double change = 0)
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
                        "INSERT INTO SaleItems(SaleId,ProductId,Quantity,Price) VALUES(@s,@p,@q,@pr)", con);
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
            DatabaseHelper.Log("Sale", $"Sale #{saleId} — {CurrencyHelper.FormatLBP(_currentTotal)} — {method}", "Sale");
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
        }

        void ResetSale()
        {
            cart.Clear();
            totalDiscount = 0;
            _currentTotal = 0;
            _currentCustomerId = 0;
            _currentCustomerName = "";
            _pendingQty = 1;
            txtQty.Text = "1";
            ResetLabels();
            RefreshCart();
            LoadInvoiceNumber();
        }

        void ResetLabels()
        {
            lblCashier.Content = $"👤 {Session.Username}";
            lblCustomerFooter.Content = "";
        }

        private void btnFastMode_Click(object sender, RoutedEventArgs e)
            => new FastSalesWindow().Show();

        void ShowMsg(string msg, bool success)
        {
            lblScanMsg.Content = msg;
            lblScanMsg.Foreground = success
                ? new SolidColorBrush(Color.FromRgb(16, 124, 16))
                : new SolidColorBrush(Color.FromRgb(196, 43, 28));

            var t = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromSeconds(3) };
            t.Tick += (s, ev) => { lblScanMsg.Content = ""; t.Stop(); };
            t.Start();
        }
    }
}