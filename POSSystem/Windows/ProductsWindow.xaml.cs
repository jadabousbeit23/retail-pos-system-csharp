using POSSystem.Database;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace POSSystem.Windows
{
    public partial class ProductsWindow : Window
    {
        string _statusFilter = "All";

        // ═══════════════════════════════════════════════════════
        // CATEGORY COMBOBOX VIEW MODEL - MUST BE PUBLIC
        // ═══════════════════════════════════════════════════════
        public class CategoryComboItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string ColorCode { get; set; }
            public Brush ColorBrush { get; set; }
            public int ProductCount { get; set; }
            public string ProductCountText => ProductCount > 0 ? $"({ProductCount})" : "";

            // Override ToString for editable combo fallback
            public override string ToString() => Name;
        }

        public ProductsWindow()
        {
            InitializeComponent();
            LoadCategoriesIntoComboBox();
            LoadProducts();
        }

        // ═══════════════════════════════════════════════════════
        // LOAD CATEGORIES INTO COMBOBOX
        // ═══════════════════════════════════════════════════════
        private void LoadCategoriesIntoComboBox()
        {
            var categories = new List<CategoryComboItem>();

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();

                // Ensure Categories table exists
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

                // Load categories with product counts
                string sql = @"
             SELECT c.Id, c.Name, c.ColorCode,
                    (SELECT COUNT(*) FROM Products WHERE Category = c.Name) as ProductCount
             FROM Categories c
             ORDER BY c.Name";

                using (var cmd = new SQLiteCommand(sql, con))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var colorCode = reader["ColorCode"].ToString();
                        categories.Add(new CategoryComboItem
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            Name = reader["Name"].ToString(),
                            ColorCode = colorCode,
                            ColorBrush = ParseColor(colorCode),
                            ProductCount = Convert.ToInt32(reader["ProductCount"])
                        });
                    }
                }
            }

            // Add "General" if no categories exist
            if (categories.Count == 0)
            {
                categories.Add(new CategoryComboItem
                {
                    Id = 0,
                    Name = "General",
                    ColorCode = "#607D8B",
                    ColorBrush = new SolidColorBrush(Color.FromRgb(0x60, 0x7D, 0x8B)),
                    ProductCount = 0
                });
            }

            cmbCategory.ItemsSource = categories;

            // Select "General" or first item by default
            var general = categories.FirstOrDefault(c => c.Name == "General");
            cmbCategory.SelectedItem = general ?? categories.First();
        }

        // ═══════════════════════════════════════════════════════
        // PARSE COLOR (helper)
        // ═══════════════════════════════════════════════════════
        private Brush ParseColor(string colorCode)
        {
            try
            {
                if (string.IsNullOrEmpty(colorCode))
                    return new SolidColorBrush(Colors.Gray);
                return (Brush)new BrushConverter().ConvertFrom(colorCode);
            }
            catch
            {
                return new SolidColorBrush(Colors.Gray);
            }
        }

        // ═══════════════════════════════════════════════════════
        // GET SELECTED CATEGORY NAME
        // ═══════════════════════════════════════════════════════
        private string GetSelectedCategoryName()
        {
            if (cmbCategory.SelectedItem is CategoryComboItem cat)
                return cat.Name;

            // If user typed custom text
            return cmbCategory.Text.Trim();
        }

        // ═══════════════════════════════════════════════════════
        // MANAGE CATEGORIES BUTTON
        // ═══════════════════════════════════════════════════════
        private void btnManageCategories_Click(object sender, RoutedEventArgs e)
        {
            var win = new CategoriesWindow();
            win.ShowDialog();

            // Reload categories after closing
            LoadCategoriesIntoComboBox();
        }

        // ═══════════════════════════════════════════════════════
        // CATEGORY SELECTION CHANGED
        // ═══════════════════════════════════════════════════════
        private void cmbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Optional: Auto-filter products by category
            // if (cmbCategory.SelectedItem is CategoryComboItem cat)
            //     LoadProducts(txtSearch.Text, cat.Name, txtSearchCode.Text);
        }

        static bool TryParseFlexibleDate(string input, out DateTime result)
        {
            result = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(input)) return false;

            string normalised = input.Trim().Replace('-', '/');
            string[] formats = {
                "dd/MM/yyyy", "d/M/yyyy", "d/MM/yyyy", "dd/M/yyyy",
                "MM/dd/yyyy", "yyyy/MM/dd"
            };
            return DateTime.TryParseExact(normalised, formats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out result);
        }

        void LoadProducts(string nameFilter = "",
                          string catFilter = "",
                          string codeFilter = "")
        {
            lvProducts.Items.Clear();
            int lowCount = 0, criticalCount = 0;
            int expiryAlertCount = 0;

            var rows = new List<ProductViewModel>();

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();

                var cmd = new SQLiteCommand(@"
                    SELECT * FROM Products
                    WHERE Name        LIKE @name
                      AND Category    LIKE @cat
                      AND ProductCode LIKE @code
                    ORDER BY Name", con);
                cmd.Parameters.AddWithValue("@name", $"%{nameFilter}%");
                cmd.Parameters.AddWithValue("@cat", $"%{catFilter}%");
                cmd.Parameters.AddWithValue("@code", $"%{codeFilter}%");

                var reader = cmd.ExecuteReader();
                var products = new List<(int Id, string Name, double Price, double Cost,
                                         int Stock, int MinStock, string Category, string Code)>();
                while (reader.Read())
                {
                    products.Add((
                        Convert.ToInt32(reader["Id"]),
                        reader["Name"].ToString(),
                        Convert.ToDouble(reader["Price"]),
                        Convert.ToDouble(reader["CostPrice"]),
                        Convert.ToInt32(reader["Stock"]),
                        Convert.ToInt32(reader["MinStock"]),
                        reader["Category"].ToString(),
                        reader["ProductCode"].ToString()
                    ));
                }
                reader.Close();

                foreach (var p in products)
                {
                    string stockText, stockColor;
                    if (p.Stock == 0)
                    { stockText = "🔴 OUT"; stockColor = "#922B21"; criticalCount++; }
                    else if (p.Stock <= p.MinStock)
                    { stockText = "⚠️ LOW"; stockColor = "#D35400"; lowCount++; }
                    else
                    { stockText = "✅ OK"; stockColor = "#1E8449"; }

                    double margin = p.Cost > 0 && p.Price > 0
                        ? Math.Round((p.Price - p.Cost) / p.Price * 100, 1) : 0;

                    var batchCmd = new SQLiteCommand(
                        "SELECT * FROM ProductBatches WHERE ProductId=@pid ORDER BY ExpiryDate", con);
                    batchCmd.Parameters.AddWithValue("@pid", p.Id);
                    var bReader = batchCmd.ExecuteReader();

                    bool hasBatches = false;
                    var batchRows = new List<(string BatchNum, string Expiry, int Qty, string RecvDate, int DaysLeft)>();

                    while (bReader.Read())
                    {
                        hasBatches = true;
                        string expStr = bReader["ExpiryDate"].ToString();
                        int bQty = Convert.ToInt32(bReader["Quantity"]);
                        string recvDate = bReader["ReceivedDate"].ToString();
                        string batchNum = bReader["BatchNumber"].ToString();

                        int daysLeft = int.MaxValue;
                        if (!string.IsNullOrEmpty(expStr) &&
                            TryParseFlexibleDate(expStr, out DateTime expDt))
                            daysLeft = (expDt - DateTime.Today).Days;

                        batchRows.Add((batchNum, expStr, bQty, recvDate, daysLeft));
                    }
                    bReader.Close();

                    batchRows.Sort((a, b) =>
                    {
                        bool aNoExp = a.DaysLeft == int.MaxValue;
                        bool bNoExp = b.DaysLeft == int.MaxValue;
                        if (aNoExp && bNoExp) return 0;
                        if (aNoExp) return 1;
                        if (bNoExp) return -1;
                        return a.DaysLeft.CompareTo(b.DaysLeft);
                    });

                    if (!hasBatches)
                    {
                        var vm = BuildProductRow(p.Id, p.Name, p.Price, p.Cost, p.Stock,
                                                 p.MinStock, p.Category, p.Code,
                                                 stockText, stockColor, margin,
                                                 "", "—", "Transparent", int.MaxValue, false);
                        if (PassesStatusFilter(vm)) rows.Add(vm);
                    }
                    else
                    {
                        foreach (var br in batchRows)
                        {
                            string expText, expColor;
                            GetExpiryDisplay(br.DaysLeft, out expText, out expColor);

                            bool isExpired = br.DaysLeft < 0;
                            if (br.DaysLeft <= 30 && br.DaysLeft != int.MaxValue)
                                expiryAlertCount++;

                            var vm = BuildProductRow(p.Id, p.Name, p.Price, p.Cost, p.Stock,
                                                     p.MinStock, p.Category, p.Code,
                                                     stockText, stockColor, margin,
                                                     br.BatchNum, expText, expColor, br.DaysLeft, isExpired);
                            if (PassesStatusFilter(vm)) rows.Add(vm);
                        }
                    }
                }
            }

            rows.Sort((a, b) =>
            {
                if (a.IsExpired && !b.IsExpired) return -1;
                if (!a.IsExpired && b.IsExpired) return 1;
                bool aNoExp = a.SortDays == int.MaxValue;
                bool bNoExp = b.SortDays == int.MaxValue;
                if (aNoExp && bNoExp) return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                if (aNoExp) return 1;
                if (bNoExp) return -1;
                int cmp = a.SortDays.CompareTo(b.SortDays);
                return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            foreach (var row in rows)
                lvProducts.Items.Add(row);

            int totalStockAlerts = lowCount + criticalCount;
            lblLowStock.Content = "";
            if (totalStockAlerts > 0 || expiryAlertCount > 0)
            {
                string msg = "";
                if (criticalCount > 0) msg += $"🔴 {criticalCount} out of stock  |  ";
                if (lowCount > 0) msg += $"⚠️ {lowCount} low stock  |  ";
                if (expiryAlertCount > 0) msg += $"🟡 {expiryAlertCount} expiry alert(s)";
                lblLowStock.Content = msg.TrimEnd(' ', '|', ' ');
            }
        }

        ProductViewModel BuildProductRow(
            int id, string name, double price, double cost,
            int stock, int minStock, string category, string code,
            string stockText, string stockColor, double margin,
            string batchNum, string expText, string expColor, int sortDays,
            bool isExpired)
        {
            return new ProductViewModel
            {
                Id = id,
                Name = name,
                Price = price,
                CostPrice = cost,
                Stock = stock,
                MinStock = minStock,
                Category = category,
                ProductCode = code,
                StatusText = stockText,
                StatusColor = stockColor,
                ExpiryText = expText,
                ExpiryColor = expColor,
                PriceDisplay = $"LBP {price:N0}",
                CostPriceDisplay = cost > 0 ? $"LBP {cost:N0}" : "—",
                MarginDisplay = cost > 0 ? $"{margin}%" : "—",
                MarginLBPDisplay = cost > 0 ? $"LBP {Math.Round(price - cost):N0}" : "—",
                BatchDisplay = string.IsNullOrEmpty(batchNum) ? "—" : batchNum,
                SortDays = sortDays,
                IsExpired = isExpired,
                RowBackground = isExpired ? "#4A1515" : "Transparent"
            };
        }

        bool PassesStatusFilter(ProductViewModel vm)
        {
            if (_statusFilter == "Low"
                && vm.StatusText != "⚠️ LOW"
                && vm.StatusText != "🔴 OUT") return false;
            if (_statusFilter == "OK"
                && vm.StatusText != "✅ OK") return false;
            return true;
        }

        void GetExpiryDisplay(int daysLeft, out string text, out string color)
        {
            if (daysLeft == int.MaxValue)
            { text = "—"; color = "Transparent"; return; }
            if (daysLeft == int.MinValue)
            { text = "Invalid Date"; color = "#922B21"; return; }

            if (daysLeft < 0) { text = $"🔴 Expired ({Math.Abs(daysLeft)}d ago)"; color = "#7B241C"; }
            else if (daysLeft == 0) { text = "🔴 Expires TODAY"; color = "#922B21"; }
            else if (daysLeft <= 3) { text = $"🚨 {daysLeft}d — URGENT"; color = "#C0392B"; }
            else if (daysLeft <= 7) { text = $"⚠️ {daysLeft}d left"; color = "#D35400"; }
            else if (daysLeft <= 30) { text = $"🟡 {daysLeft}d left"; color = "#B7950B"; }
            else { text = $"✅ {daysLeft}d left"; color = "#1E8449"; }
        }

        private void lvProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lvProducts.SelectedItem == null) return;
            var p = (ProductViewModel)lvProducts.SelectedItem;

            txtName.Text = p.Name;
            txtPrice.Text = p.Price.ToString();
            txtCostPrice.Text = p.CostPrice > 0 ? p.CostPrice.ToString() : "";
            txtStock.Text = p.Stock.ToString();
            txtMinStock.Text = p.MinStock.ToString();
            txtProductCode.Text = p.ProductCode;
            lblMessage.Content = "";

            // Set category in ComboBox
            var catItem = cmbCategory.Items.Cast<CategoryComboItem>()
                .FirstOrDefault(c => c.Name == p.Category);

            if (catItem != null)
                cmbCategory.SelectedItem = catItem;
            else
                cmbCategory.Text = p.Category; // For custom/legacy categories

            if (p.IsExpired)
                ShowWarning($"⚠️ Batch '{p.BatchDisplay}' is EXPIRED — remove from stock when ready.");
        }

        // ── ADD ───────────────────────────────────────
        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            string name = txtName.Text.Trim();
            string category = GetSelectedCategoryName();
            string codeVal = txtProductCode.Text.Trim();

            if (name == "") { ShowError("⚠️ Product name is required!"); return; }
            if (!double.TryParse(txtPrice.Text, out double price))
            { ShowError("⚠️ Enter a valid price!"); return; }
            if (!int.TryParse(txtStock.Text, out int stock))
            { ShowError("⚠️ Enter a valid stock quantity!"); return; }

            double.TryParse(txtCostPrice.Text, out double costPrice);
            int.TryParse(txtMinStock.Text, out int minStock);
            if (minStock <= 0) minStock = 5;
            if (string.IsNullOrEmpty(category)) category = "General";
            if (codeVal == "") codeVal = GenerateCode();

            if (CodeExists(codeVal, -1))
            { ShowError("⚠️ Product code already in use!"); return; }

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var cmd = new SQLiteCommand(@"
                    INSERT INTO Products
                    (Name, Price, CostPrice, Stock, MinStock, Category, ProductCode)
                    VALUES (@n, @p, @cp, @s, @ms, @c, @code)", con);
                cmd.Parameters.AddWithValue("@n", name);
                cmd.Parameters.AddWithValue("@p", price);
                cmd.Parameters.AddWithValue("@cp", costPrice);
                cmd.Parameters.AddWithValue("@s", stock);
                cmd.Parameters.AddWithValue("@ms", minStock);
                cmd.Parameters.AddWithValue("@c", category);
                cmd.Parameters.AddWithValue("@code", codeVal);
                cmd.ExecuteNonQuery();
            }

            // ── AUDIT LOG ──
            DatabaseHelper.Log("Product Added", $"'{name}' — LBP {price:N0}", "Product");

            ShowSuccess($"✅ '{name}' added!");
            ClearForm();
            LoadProducts();

            // Refresh category counts
            LoadCategoriesIntoComboBox();
        }

        // ── UPDATE ────────────────────────────────────
        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (lvProducts.SelectedItem == null)
            { ShowError("⚠️ Select a product first!"); return; }

            var selected = (ProductViewModel)lvProducts.SelectedItem;
            string name = txtName.Text.Trim();
            string category = GetSelectedCategoryName();
            string codeVal = txtProductCode.Text.Trim();

            if (name == "") { ShowError("⚠️ Product name is required!"); return; }
            if (!double.TryParse(txtPrice.Text, out double price))
            { ShowError("⚠️ Enter a valid price!"); return; }
            if (!int.TryParse(txtStock.Text, out int stock))
            { ShowError("⚠️ Enter a valid stock quantity!"); return; }

            double.TryParse(txtCostPrice.Text, out double costPrice);
            int.TryParse(txtMinStock.Text, out int minStock);
            if (minStock <= 0) minStock = 5;
            if (string.IsNullOrEmpty(category)) category = "General";
            if (codeVal == "") codeVal = GenerateCode();

            if (CodeExists(codeVal, selected.Id))
            { ShowError("⚠️ Product code already used by another product!"); return; }

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var cmd = new SQLiteCommand(@"
                    UPDATE Products SET
                        Name=@n, Price=@p, CostPrice=@cp,
                        Stock=@s, MinStock=@ms,
                        Category=@c, ProductCode=@code
                    WHERE Id=@id", con);
                cmd.Parameters.AddWithValue("@n", name);
                cmd.Parameters.AddWithValue("@p", price);
                cmd.Parameters.AddWithValue("@cp", costPrice);
                cmd.Parameters.AddWithValue("@s", stock);
                cmd.Parameters.AddWithValue("@ms", minStock);
                cmd.Parameters.AddWithValue("@c", category);
                cmd.Parameters.AddWithValue("@code", codeVal);
                cmd.Parameters.AddWithValue("@id", selected.Id);
                cmd.ExecuteNonQuery();
            }

            // ── AUDIT LOG ──
            DatabaseHelper.Log("Product Updated", $"'{name}'", "Product");

            ShowSuccess($"✅ '{name}' updated!");
            ClearForm();
            LoadProducts();

            // Refresh category counts
            LoadCategoriesIntoComboBox();
        }

        // ── DELETE ────────────────────────────────────
        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (lvProducts.SelectedItem == null)
            { ShowError("⚠️ Select a product first!"); return; }

            var selected = (ProductViewModel)lvProducts.SelectedItem;
            if (MessageBox.Show($"Delete '{selected.Name}'?", "Delete Product",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                new SQLiteCommand($"DELETE FROM ProductBatches   WHERE ProductId={selected.Id}", con).ExecuteNonQuery();
                new SQLiteCommand($"DELETE FROM StockAdjustments WHERE ProductId={selected.Id}", con).ExecuteNonQuery();
                new SQLiteCommand($"DELETE FROM Products         WHERE Id={selected.Id}", con).ExecuteNonQuery();
            }

            // ── AUDIT LOG ──
            DatabaseHelper.Log("Product Deleted", $"'{selected.Name}'", "Product");

            ShowSuccess("✅ Product deleted!");
            ClearForm();
            LoadProducts();

            // Refresh category counts
            LoadCategoriesIntoComboBox();
        }

        private void btnBatches_Click(object sender, RoutedEventArgs e)
        {
            if (lvProducts.SelectedItem == null)
            { ShowError("⚠️ Select a product first!"); return; }

            var p = (ProductViewModel)lvProducts.SelectedItem;
            var win = new BatchManagementWindow(p.Id, p.Name);
            win.ShowDialog();
            LoadProducts(txtSearch.Text, txtFilterCat.Text, txtSearchCode.Text);
        }

        private void btnAdjustStock_Click(object sender, RoutedEventArgs e)
        {
            if (lvProducts.SelectedItem == null)
            { ShowError("⚠️ Select a product first!"); return; }

            var p = (ProductViewModel)lvProducts.SelectedItem;
            var win = new StockAdjustmentWindow(p.Id, p.Name, p.Stock);
            win.ShowDialog();
            LoadProducts(txtSearch.Text, txtFilterCat.Text, txtSearchCode.Text);
        }

        private void btnAlerts_Click(object sender, RoutedEventArgs e)
        {
            new InventoryAlertsWindow().ShowDialog();
        }

        private void btnShowQR_Click(object sender, RoutedEventArgs e)
        {
            if (lvProducts.SelectedItem == null)
            { ShowError("⚠️ Select a product to show its QR code!"); return; }

            var p = (ProductViewModel)lvProducts.SelectedItem;
            if (string.IsNullOrEmpty(p.ProductCode))
            { ShowError("⚠️ This product has no code!"); return; }

            var prod = new Product
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                Stock = p.Stock,
                Category = p.Category,
                ProductCode = p.ProductCode
            };
            new QRCodeWindow(prod).ShowDialog();
        }

        private void btnGenerateCode_Click(object sender, RoutedEventArgs e)
            => txtProductCode.Text = GenerateCode();

        static string GenerateCode() =>
            "PRD-" + DateTime.Now.ToString("yyyyMMddHHmmss").Substring(6)
                   + new Random().Next(10, 99);

        private void btnFilterStatus_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;
            _statusFilter = btn.Name == "btnFilterLow" ? "Low"
                          : btn.Name == "btnFilterOk" ? "OK"
                          : "All";

            btnFilterAll.Background = System.Windows.Media.Brushes.Gray;
            btnFilterLow.Background = System.Windows.Media.Brushes.Gray;
            btnFilterOk.Background = System.Windows.Media.Brushes.Gray;
            btn.Background = System.Windows.Media.Brushes.White;

            LoadProducts(txtSearch.Text, txtFilterCat.Text, txtSearchCode.Text);
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
            => LoadProducts(txtSearch.Text, txtFilterCat.Text, txtSearchCode.Text);
        private void txtFilterCat_TextChanged(object sender, TextChangedEventArgs e)
            => LoadProducts(txtSearch.Text, txtFilterCat.Text, txtSearchCode.Text);
        private void txtSearchCode_TextChanged(object sender, TextChangedEventArgs e)
            => LoadProducts(txtSearch.Text, txtFilterCat.Text, txtSearchCode.Text);

        bool CodeExists(string code, int excludeId)
        {
            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var cmd = new SQLiteCommand(
                    "SELECT COUNT(*) FROM Products WHERE ProductCode=@code AND Id!=@id", con);
                cmd.Parameters.AddWithValue("@code", code);
                cmd.Parameters.AddWithValue("@id", excludeId);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        void ClearForm()
        {
            txtName.Clear(); txtPrice.Clear(); txtCostPrice.Clear();
            txtStock.Clear(); txtMinStock.Text = "5";
            cmbCategory.Text = "";
            txtProductCode.Clear();
            lvProducts.SelectedItem = null;
            lblMessage.Content = "";

            // Reset to General category
            var general = cmbCategory.Items.Cast<CategoryComboItem>()
                .FirstOrDefault(c => c.Name == "General");
            if (general != null)
                cmbCategory.SelectedItem = general;
        }

        void ShowError(string msg)
        {
            lblMessage.Content = msg;
            lblMessage.Foreground = System.Windows.Media.Brushes.Tomato;
        }

        void ShowSuccess(string msg)
        {
            lblMessage.Content = msg;
            lblMessage.Foreground = System.Windows.Media.Brushes.LightGreen;
        }

        void ShowWarning(string msg)
        {
            lblMessage.Content = msg;
            lblMessage.Foreground = System.Windows.Media.Brushes.Orange;
        }
    }

    public class ProductViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Price { get; set; }
        public double CostPrice { get; set; }
        public int Stock { get; set; }
        public int MinStock { get; set; }
        public string Category { get; set; }
        public string ProductCode { get; set; }
        public string StatusText { get; set; }
        public string StatusColor { get; set; }
        public string ExpiryText { get; set; }
        public string ExpiryColor { get; set; }
        public string PriceDisplay { get; set; }
        public string CostPriceDisplay { get; set; }
        public string MarginDisplay { get; set; }
        public string MarginLBPDisplay { get; set; }
        public string BatchDisplay { get; set; }
        public int SortDays { get; set; }
        public bool IsExpired { get; set; }
        public string RowBackground { get; set; }
    }
}