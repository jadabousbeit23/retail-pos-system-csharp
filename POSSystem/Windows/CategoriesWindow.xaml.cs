using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using POSSystem.Database;

namespace POSSystem.Windows
{
    public partial class CategoriesWindow : Window
    {
        private int _selectedId = 0;
        private bool _updatingSliders = false;

        // Preset color list
        private List<PresetColor> _presetColors = new List<PresetColor>
        {
            // Sales - Green family
            new PresetColor { Name = "Sales Green", ColorCode = "#4CAF50", Category = "Sales" },
            new PresetColor { Name = "Success Dark", ColorCode = "#1A7A1A", Category = "Sales" },
            new PresetColor { Name = "Mint", ColorCode = "#81C784", Category = "Sales" },
            
            // Inventory - Blue family
            new PresetColor { Name = "Inventory Blue", ColorCode = "#2196F3", Category = "Inventory" },
            new PresetColor { Name = "Accent Blue", ColorCode = "#2B6CC4", Category = "Inventory" },
            new PresetColor { Name = "Sky Blue", ColorCode = "#64B5F6", Category = "Inventory" },
            new PresetColor { Name = "Navy", ColorCode = "#1565C0", Category = "Inventory" },
            
            // Reports - Purple family
            new PresetColor { Name = "Reports Purple", ColorCode = "#9C27B0", Category = "Reports" },
            new PresetColor { Name = "Deep Purple", ColorCode = "#7B1FA2", Category = "Reports" },
            new PresetColor { Name = "Lavender", ColorCode = "#BA68C8", Category = "Reports" },
            
            // Admin - Orange/Amber family
            new PresetColor { Name = "Admin Orange", ColorCode = "#FF9800", Category = "Admin" },
            new PresetColor { Name = "Amber", ColorCode = "#FFC107", Category = "Admin" },
            new PresetColor { Name = "Deep Orange", ColorCode = "#F57C00", Category = "Admin" },
            
            // Tools - Gray-Blue family
            new PresetColor { Name = "Tools Gray", ColorCode = "#607D8B", Category = "Tools" },
            new PresetColor { Name = "Blue Gray", ColorCode = "#78909C", Category = "Tools" },
            new PresetColor { Name = "Slate", ColorCode = "#455A64", Category = "Tools" },
            
            // Danger - Red family
            new PresetColor { Name = "Danger Red", ColorCode = "#B71C1C", Category = "Danger" },
            new PresetColor { Name = "Warning Red", ColorCode = "#B00000", Category = "Danger" },
            new PresetColor { Name = "Crimson", ColorCode = "#C62828", Category = "Danger" },
            
            // Common
            new PresetColor { Name = "Teal", ColorCode = "#009688", Category = "Common" },
            new PresetColor { Name = "Cyan", ColorCode = "#00BCD4", Category = "Common" },
            new PresetColor { Name = "Indigo", ColorCode = "#3F51B5", Category = "Common" },
            new PresetColor { Name = "Pink", ColorCode = "#E91E63", Category = "Common" },
            new PresetColor { Name = "Brown", ColorCode = "#795548", Category = "Common" },
            new PresetColor { Name = "General", ColorCode = "#9E9E9E", Category = "Common" },
        };

        // Quick palette colors
        private string[] _quickPalette = new string[]
        {
            "#4CAF50", "#2196F3", "#9C27B0", "#FF9800", "#F44336",
            "#009688", "#00BCD4", "#3F51B5", "#E91E63", "#795548",
            "#607D8B", "#9E9E9E", "#2B6CC4", "#1A7A1A", "#B71C1C",
            "#FFC107", "#8BC34A", "#03A9F4", "#673AB7", "#FF5722"
        };

        public CategoriesWindow()
        {
            InitializeComponent();
            LoadPresetColors();
            LoadQuickPalette();
            LoadCategories();
        }

        public class PresetColor
        {
            public string Name { get; set; }
            public string ColorCode { get; set; }
            public string Category { get; set; }
            public Brush ColorBrush
            {
                get
                {
                    try
                    {
                        return (Brush)new BrushConverter().ConvertFrom(ColorCode);
                    }
                    catch
                    {
                        return Brushes.Gray;
                    }
                }
            }
        }

        public class CategoryViewModel
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string ColorCode { get; set; }
            public Brush ColorBrush { get; set; }
            public int ProductCount { get; set; }
        }

        // ═══════════════════════════════════════════════════════
        // LOAD PRESET COLORS INTO COMBOBOX
        // ═══════════════════════════════════════════════════════
        private void LoadPresetColors()
        {
            cmbPresetColors.ItemsSource = _presetColors;
        }

        // ═══════════════════════════════════════════════════════
        // LOAD QUICK PALETTE BUTTONS
        // ═══════════════════════════════════════════════════════
        private void LoadQuickPalette()
        {
            wpQuickPalette.Children.Clear();

            foreach (var color in _quickPalette)
            {
                var btn = new Button
                {
                    Width = 28,
                    Height = 28,
                    Margin = new Thickness(2),
                    Background = ParseColor(color),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Colors.Gray),
                    Cursor = Cursors.Hand,
                    Tag = color
                };

                btn.Click += (s, e) =>
                {
                    if (s is Button b)
                    {
                        string colorCode = b.Tag.ToString();
                        SetColor(colorCode);
                    }
                };

                wpQuickPalette.Children.Add(btn);
            }
        }

        // ═══════════════════════════════════════════════════════
        // COLOR CONVERSION HELPERS
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

        private Color HexToColor(string hex)
        {
            try
            {
                hex = hex.Replace("#", "");
                if (hex.Length == 6)
                {
                    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    return Color.FromRgb(r, g, b);
                }
            }
            catch { }
            return Colors.Gray;
        }

        private string ColorToHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        // Convert RGB to HSB
        private void ColorToHSB(Color color, out double hue, out double saturation, out double brightness)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            brightness = max;

            if (max == 0)
                saturation = 0;
            else
                saturation = delta / max;

            if (delta == 0)
                hue = 0;
            else
            {
                if (max == r)
                    hue = (g - b) / delta + (g < b ? 6 : 0);
                else if (max == g)
                    hue = (b - r) / delta + 2;
                else
                    hue = (r - g) / delta + 4;
                hue *= 60;
            }
        }

        // Convert HSB to RGB
        private Color HSBToColor(double hue, double saturation, double brightness)
        {
            double c = brightness * saturation;
            double x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
            double m = brightness - c;

            double r, g, b;

            if (hue < 60) { r = c; g = x; b = 0; }
            else if (hue < 120) { r = x; g = c; b = 0; }
            else if (hue < 180) { r = 0; g = c; b = x; }
            else if (hue < 240) { r = 0; g = x; b = c; }
            else if (hue < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return Color.FromRgb(
                (byte)((r + m) * 255),
                (byte)((g + m) * 255),
                (byte)((b + m) * 255));
        }

        // ═══════════════════════════════════════════════════════
        // SET COLOR (updates all UI elements)
        // ═══════════════════════════════════════════════════════
        private void SetColor(string colorCode)
        {
            if (txtColorCode == null || borderColorPreview == null)
                return; // Controls not initialized yet

            txtColorCode.Text = colorCode;
            borderColorPreview.Background = ParseColor(colorCode);

            // Update sliders to match color
            _updatingSliders = true;
            try
            {
                Color color = HexToColor(colorCode);
                double h, s, b;
                ColorToHSB(color, out h, out s, out b);

                if (sliderHue != null) sliderHue.Value = h;
                if (sliderSaturation != null) sliderSaturation.Value = s * 100;
                if (sliderBrightness != null) sliderBrightness.Value = b * 100;
            }
            catch { }
            _updatingSliders = false;

            // Update preset dropdown selection if matches
            UpdatePresetSelection(colorCode);
        }

        private void UpdatePresetSelection(string colorCode)
        {
            foreach (var item in cmbPresetColors.Items)
            {
                if (item is PresetColor pc && pc.ColorCode.Equals(colorCode, StringComparison.OrdinalIgnoreCase))
                {
                    cmbPresetColors.SelectedItem = item;
                    return;
                }
            }
            cmbPresetColors.SelectedIndex = -1;
        }

        // ═══════════════════════════════════════════════════════
        // EVENT HANDLERS
        // ═══════════════════════════════════════════════════════
        private void cmbPresetColors_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbPresetColors.SelectedItem is PresetColor pc)
            {
                SetColor(pc.ColorCode);
            }
        }

        private void txtColorCode_TextChanged(object sender, TextChangedEventArgs e)
        {
            string color = txtColorCode.Text.Trim();
            if (!string.IsNullOrEmpty(color) && color.StartsWith("#"))
            {
                borderColorPreview.Background = ParseColor(color);

                if (!_updatingSliders)
                {
                    // Update sliders from text input
                    _updatingSliders = true;
                    try
                    {
                        Color c = HexToColor(color);
                        double h, s, b;
                        ColorToHSB(c, out h, out s, out b);

                        sliderHue.Value = h;
                        sliderSaturation.Value = s * 100;
                        sliderBrightness.Value = b * 100;
                    }
                    catch { }
                    _updatingSliders = false;
                }

                UpdatePresetSelection(color);
            }
        }

        private void sliderHue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_updatingSliders)
                UpdateColorFromSliders();
        }

        private void sliderSaturation_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_updatingSliders)
                UpdateColorFromSliders();
        }

        private void sliderBrightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_updatingSliders && sliderSaturation != null && sliderBrightness != null)
                UpdateColorFromSliders();
        }

        private void UpdateColorFromSliders()
        {
            // Prevent null reference during initialization
            if (sliderHue == null || sliderSaturation == null || sliderBrightness == null)
                return;

            double h = sliderHue.Value;
            double s = sliderSaturation.Value / 100.0;
            double b = sliderBrightness.Value / 100.0;

            Color color = HSBToColor(h, s, b);
            string hex = ColorToHex(color);

            // Update text box without triggering its event
            if (txtColorCode != null && txtColorCode.Text != hex)
            {
                txtColorCode.TextChanged -= txtColorCode_TextChanged;
                txtColorCode.Text = hex;
                txtColorCode.TextChanged += txtColorCode_TextChanged;
            }

            // Update preview
            if (borderColorPreview != null)
                borderColorPreview.Background = new SolidColorBrush(color);
        }

        // ═══════════════════════════════════════════════════════
        // COLOR PICKER DIALOG
        // ═══════════════════════════════════════════════════════
        private void btnPickColor_Click(object sender, RoutedEventArgs e)
        {
            var pickerDialog = new ColorPickerDialog(txtColorCode.Text);
            pickerDialog.Owner = this;

            if (pickerDialog.ShowDialog() == true)
            {
                SetColor(pickerDialog.SelectedColor);
            }
        }

        // ═══════════════════════════════════════════════════════
        // DATABASE OPERATIONS
        // ═══════════════════════════════════════════════════════
        private void LoadCategories()
        {
            var categories = new List<CategoryViewModel>();

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();

                // Create table if not exists
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
                        categories.Add(new CategoryViewModel
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

            lvCategories.ItemsSource = categories;
            lblTotalCategories.Content = $"{categories.Count} categories";
            _selectedId = 0;
            ClearForm();
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            string name = txtCategoryName.Text.Trim();
            string color = txtColorCode.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                ShowMessage("Please enter a category name", true);
                return;
            }

            try
            {
                using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    con.Open();

                    // Check duplicate
                    var checkCmd = new SQLiteCommand(
                        "SELECT COUNT(*) FROM Categories WHERE Name = @name", con);
                    checkCmd.Parameters.AddWithValue("@name", name);

                    if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
                    {
                        ShowMessage("Category already exists!", true);
                        return;
                    }

                    // Insert
                    var cmd = new SQLiteCommand(@"
                        INSERT INTO Categories (Name, ColorCode) 
                        VALUES (@name, @color)", con);
                    cmd.Parameters.AddWithValue("@name", name);
                    cmd.Parameters.AddWithValue("@color", color);
                    cmd.ExecuteNonQuery();

                    DatabaseHelper.Log("Category Added", $"Name: {name}, Color: {color}", "Categories");
                }

                ShowMessage("Category added successfully", false);
                LoadCategories();
            }
            catch (Exception ex)
            {
                ShowMessage($"Error: {ex.Message}", true);
            }
        }

        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedId == 0)
            {
                ShowMessage("Please select a category to update", true);
                return;
            }

            string name = txtCategoryName.Text.Trim();
            string color = txtColorCode.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                ShowMessage("Please enter a category name", true);
                return;
            }

            try
            {
                using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    con.Open();

                    // Get old name
                    var getOldCmd = new SQLiteCommand(
                        "SELECT Name FROM Categories WHERE Id = @id", con);
                    getOldCmd.Parameters.AddWithValue("@id", _selectedId);
                    string oldName = getOldCmd.ExecuteScalar()?.ToString();

                    // Update category
                    var cmd = new SQLiteCommand(@"
                        UPDATE Categories 
                        SET Name = @name, ColorCode = @color 
                        WHERE Id = @id", con);
                    cmd.Parameters.AddWithValue("@name", name);
                    cmd.Parameters.AddWithValue("@color", color);
                    cmd.Parameters.AddWithValue("@id", _selectedId);
                    cmd.ExecuteNonQuery();

                    // Update products if name changed
                    if (oldName != name)
                    {
                        var updateProducts = new SQLiteCommand(@"
                            UPDATE Products SET Category = @newName 
                            WHERE Category = @oldName", con);
                        updateProducts.Parameters.AddWithValue("@newName", name);
                        updateProducts.Parameters.AddWithValue("@oldName", oldName);
                        updateProducts.ExecuteNonQuery();
                    }

                    DatabaseHelper.Log("Category Updated", $"ID: {_selectedId}, Name: {name}, Color: {color}", "Categories");
                }

                ShowMessage("Category updated successfully", false);
                LoadCategories();
            }
            catch (Exception ex)
            {
                ShowMessage($"Error: {ex.Message}", true);
            }
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedId == 0)
            {
                ShowMessage("Please select a category to delete", true);
                return;
            }

            string name = txtCategoryName.Text.Trim();

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var checkCmd = new SQLiteCommand(@"
                    SELECT COUNT(*) FROM Products WHERE Category = @name", con);
                checkCmd.Parameters.AddWithValue("@name", name);

                int productCount = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (productCount > 0)
                {
                    var result = MessageBox.Show(
                        $"This category has {productCount} products. Move them to 'General'?",
                        "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.No) return;

                    var moveCmd = new SQLiteCommand(@"
                        UPDATE Products SET Category = 'General' 
                        WHERE Category = @name", con);
                    moveCmd.Parameters.AddWithValue("@name", name);
                    moveCmd.ExecuteNonQuery();
                }
                else
                {
                    var result = MessageBox.Show(
                        "Delete this category?",
                        "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result == MessageBoxResult.No) return;
                }

                var delCmd = new SQLiteCommand(
                    "DELETE FROM Categories WHERE Id = @id", con);
                delCmd.Parameters.AddWithValue("@id", _selectedId);
                delCmd.ExecuteNonQuery();

                DatabaseHelper.Log("Category Deleted", $"ID: {_selectedId}, Name: {name}", "Categories");
            }

            ShowMessage("Category deleted", false);
            LoadCategories();
        }

        private void lvCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lvCategories.SelectedItem is CategoryViewModel cat)
            {
                _selectedId = cat.Id;
                txtCategoryName.Text = cat.Name;
                SetColor(cat.ColorCode);
            }
        }

        private void ClearForm()
        {
            txtCategoryName.Clear();
            SetColor("#4CAF50");
            lvCategories.SelectedItem = null;
            _selectedId = 0;
        }

        private void ShowMessage(string msg, bool isError)
        {
            lblMessage.Content = msg;
            lblMessage.Foreground = isError ?
                new SolidColorBrush(Colors.Red) :
                new SolidColorBrush(Color.FromRgb(0x1A, 0x7A, 0x1A));
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}