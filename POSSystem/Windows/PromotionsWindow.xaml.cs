using POSSystem.Database;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace POSSystem.Windows
{
    public class PromotionRow
    {
        public int    Id               { get; set; }
        public string Name             { get; set; }
        public string Type             { get; set; }
        public double DiscountValue    { get; set; }
        public string AppliesTo        { get; set; }
        public string AppliesToValue   { get; set; }
        public int    MinQty           { get; set; }
        public int    FreeQty          { get; set; }
        public string StartDate        { get; set; }
        public string EndDate          { get; set; }
        public bool   IsActive         { get; set; }
        public string ActiveLabel      => IsActive ? "ON" : "OFF";
        public string ActiveColor      => IsActive ? "#107C10" : "#8A8A8A";
        public string ValueDisplay
        {
            get
            {
                if (Type == "% Discount")       return $"{DiscountValue}%";
                if (Type == "Fixed Amount Off")  return $"LBP {DiscountValue:N0}";
                if (Type == "Buy X Get Y Free")  return $"Buy {MinQty} → Get {FreeQty} free";
                return DiscountValue.ToString();
            }
        }
    }

    public partial class PromotionsWindow : Window
    {
        public PromotionsWindow()
        {
            InitializeComponent();
            LoadPromos();
        }

        // ══════════════════════════════════════════════
        // LOAD LIST
        // ══════════════════════════════════════════════
        void LoadPromos()
        {
            lvPromos.Items.Clear();
            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var r = new SQLiteCommand(
                    "SELECT * FROM Promotions ORDER BY IsActive DESC, Id DESC", con)
                    .ExecuteReader();
                while (r.Read())
                    lvPromos.Items.Add(new PromotionRow
                    {
                        Id             = Convert.ToInt32(r["Id"]),
                        Name           = r["Name"].ToString(),
                        Type           = r["Type"].ToString(),
                        DiscountValue  = Convert.ToDouble(r["DiscountValue"]),
                        AppliesTo      = r["AppliesTo"].ToString(),
                        AppliesToValue = r["AppliesToValue"].ToString(),
                        MinQty         = Convert.ToInt32(r["MinQty"]),
                        FreeQty        = Convert.ToInt32(r["FreeQty"]),
                        StartDate      = r["StartDate"].ToString(),
                        EndDate        = r["EndDate"].ToString(),
                        IsActive       = Convert.ToInt32(r["IsActive"]) == 1
                    });
                r.Close();
            }
        }

        // ══════════════════════════════════════════════
        // SAVE PROMOTION
        // ══════════════════════════════════════════════
        private void btnSavePromo_Click(object sender, RoutedEventArgs e)
        {
            string name = txtPromoName.Text.Trim();
            if (name == "") { ShowMsg("⚠️ Enter a name!", false); return; }
            if (!double.TryParse(txtPromoValue.Text, out double val) || val < 0)
            { ShowMsg("⚠️ Enter a valid discount value!", false); return; }

            string type      = ((ComboBoxItem)cmbPromoType.SelectedItem)?.Content?.ToString() ?? "% Discount";
            string appliesTo = ((ComboBoxItem)cmbAppliesTo.SelectedItem)?.Content?.ToString() ?? "All Products";
            string appVal    = txtAppliesToValue.Text.Trim();
            int    minQty    = int.TryParse(txtMinQty.Text, out int mq)  ? mq  : 1;
            int    freeQty   = int.TryParse(txtFreeQty.Text, out int fq) ? fq  : 1;
            string start     = txtStartDate.Text.Trim();
            string end       = txtEndDate.Text.Trim();

            if (appliesTo != "All Products" && string.IsNullOrEmpty(appVal))
            { ShowMsg("⚠️ Enter the category or product name!", false); return; }

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var cmd = new SQLiteCommand(@"
                    INSERT INTO Promotions
                    (Name, Type, DiscountValue, AppliesTo, AppliesToValue,
                     MinQty, FreeQty, StartDate, EndDate, IsActive, CreatedBy, CreatedAt)
                    VALUES (@n,@t,@v,@a,@av,@mq,@fq,@sd,@ed,1,@user,@at)", con);
                cmd.Parameters.AddWithValue("@n",    name);
                cmd.Parameters.AddWithValue("@t",    type);
                cmd.Parameters.AddWithValue("@v",    val);
                cmd.Parameters.AddWithValue("@a",    appliesTo);
                cmd.Parameters.AddWithValue("@av",   appVal);
                cmd.Parameters.AddWithValue("@mq",   minQty);
                cmd.Parameters.AddWithValue("@fq",   freeQty);
                cmd.Parameters.AddWithValue("@sd",   start);
                cmd.Parameters.AddWithValue("@ed",   end);
                cmd.Parameters.AddWithValue("@user", Session.Username);
                cmd.Parameters.AddWithValue("@at",   DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                cmd.ExecuteNonQuery();
            }

            DatabaseHelper.Log("Promotion Created", $"'{name}' — {type} {val}", "Promo");
            txtPromoName.Clear();
            txtPromoValue.Text      = "10";
            txtAppliesToValue.Clear();
            cmbPromoType.SelectedIndex = 0;
            cmbAppliesTo.SelectedIndex = 0;
            ShowMsg($"✅ '{name}' saved!", true);
            LoadPromos();
        }

        // ══════════════════════════════════════════════
        // TOGGLE ACTIVE
        // ══════════════════════════════════════════════
        private void btnToggle_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is ToggleButton btn)) return;
            int id = Convert.ToInt32(btn.Tag);
            int newVal = btn.IsChecked == true ? 1 : 0;

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                new SQLiteCommand(
                    $"UPDATE Promotions SET IsActive={newVal} WHERE Id={id}", con)
                    .ExecuteNonQuery();
            }
            LoadPromos();
        }

        // ══════════════════════════════════════════════
        // DELETE
        // ══════════════════════════════════════════════
        private void btnDeletePromo_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;
            int id = Convert.ToInt32(btn.Tag);
            if (MessageBox.Show("Delete this promotion?", "Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                new SQLiteCommand($"DELETE FROM Promotions WHERE Id={id}", con).ExecuteNonQuery();
            }
            LoadPromos();
        }

        // ══════════════════════════════════════════════
        // FORM HELPERS
        // ══════════════════════════════════════════════
        private void cmbPromoType_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Guard: controls may not be initialized yet during InitializeComponent
            if (lblValueLabel == null) return;

            if (cmbPromoType.SelectedIndex == 0) lblValueLabel.Content = "DISCOUNT %";
            else if (cmbPromoType.SelectedIndex == 1) lblValueLabel.Content = "AMOUNT OFF (LBP)";
            else lblValueLabel.Content = "MIN QTY BUY";

            bool isBuyX = cmbPromoType.SelectedIndex == 2;
            if (lblMinQtyLabel != null) lblMinQtyLabel.Foreground = isBuyX
                ? new SolidColorBrush(Color.FromRgb(26, 26, 26))
                : new SolidColorBrush(Color.FromRgb(138, 138, 138));
            if (lblFreeQtyLabel != null) lblFreeQtyLabel.Foreground = isBuyX
                ? new SolidColorBrush(Color.FromRgb(26, 26, 26))
                : new SolidColorBrush(Color.FromRgb(138, 138, 138));
            if (txtMinQty != null) txtMinQty.IsEnabled = isBuyX;
            if (txtFreeQty != null) txtFreeQty.IsEnabled = isBuyX;
        }

        private void cmbAppliesTo_Changed(object sender, SelectionChangedEventArgs e)
        {
            bool specific = cmbAppliesTo.SelectedIndex > 0;
            if (txtAppliesToValue != null)
            {
                txtAppliesToValue.IsEnabled = specific;
                if (!specific) txtAppliesToValue.Clear();
            }
            if (lblAppliesToValue != null)
                lblAppliesToValue.Content = cmbAppliesTo.SelectedIndex == 1
                    ? "CATEGORY NAME" : cmbAppliesTo.SelectedIndex == 2
                    ? "PRODUCT NAME" : "—";
        }

        void ShowMsg(string msg, bool ok)
        {
            lblMessage.Content    = msg;
            lblMessage.Foreground = ok
                ? new SolidColorBrush(Color.FromRgb(16, 124, 16))
                : new SolidColorBrush(Color.FromRgb(196, 43, 28));
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();

        // ══════════════════════════════════════════════
        // STATIC: GET ACTIVE PROMOTIONS (used by sales windows)
        // ══════════════════════════════════════════════
        public static List<PromotionRow> GetActivePromotions()
        {
            var list  = new List<PromotionRow>();
            string today = DateTime.Now.ToString("dd/MM/yyyy");
            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var r = new SQLiteCommand(@"
                    SELECT * FROM Promotions
                    WHERE IsActive = 1
                      AND (StartDate = '' OR StartDate <= @today)
                      AND (EndDate   = '' OR EndDate   >= @today)", con);
                r.Parameters.AddWithValue("@today", today);
                var dr = r.ExecuteReader();
                while (dr.Read())
                    list.Add(new PromotionRow
                    {
                        Id             = Convert.ToInt32(dr["Id"]),
                        Name           = dr["Name"].ToString(),
                        Type           = dr["Type"].ToString(),
                        DiscountValue  = Convert.ToDouble(dr["DiscountValue"]),
                        AppliesTo      = dr["AppliesTo"].ToString(),
                        AppliesToValue = dr["AppliesToValue"].ToString(),
                        MinQty         = Convert.ToInt32(dr["MinQty"]),
                        FreeQty        = Convert.ToInt32(dr["FreeQty"])
                    });
                dr.Close();
            }
            return list;
        }

        // ══════════════════════════════════════════════
        // STATIC: APPLY PROMOTIONS TO A CART ITEM
        // Returns the discount percentage to apply (0 if none)
        // Call this in FastSalesWindow when adding an item
        // ══════════════════════════════════════════════
        public static double GetDiscountPct(string productName,
            string category, int quantity)
        {
            var promos = GetActivePromotions();
            double bestDiscount = 0;

            foreach (var p in promos)
            {
                // Check if promo applies to this product
                bool applies = p.AppliesTo == "All Products"
                    || (p.AppliesTo == "Category"         && string.Equals(p.AppliesToValue, category,    StringComparison.OrdinalIgnoreCase))
                    || (p.AppliesTo == "Specific Product" && string.Equals(p.AppliesToValue, productName, StringComparison.OrdinalIgnoreCase));

                if (!applies) continue;

                if (p.Type == "% Discount" && p.DiscountValue > bestDiscount)
                    bestDiscount = p.DiscountValue;

                if (p.Type == "Buy X Get Y Free" && quantity >= p.MinQty)
                {
                    // Calculate effective discount from free items
                    // e.g. Buy 3 Get 1 Free on qty=4 → 25% off
                    int total    = quantity + p.FreeQty;
                    double pct   = (double)p.FreeQty / total * 100;
                    if (pct > bestDiscount) bestDiscount = pct;
                }
            }
            return bestDiscount;
        }

        // ── For fixed LBP discounts, call this instead ──
        public static double GetFixedDiscount(string productName,
            string category)
        {
            var promos = GetActivePromotions();
            double best = 0;
            foreach (var p in promos)
            {
                if (p.Type != "Fixed Amount Off") continue;
                bool applies = p.AppliesTo == "All Products"
                    || (p.AppliesTo == "Category"         && string.Equals(p.AppliesToValue, category,    StringComparison.OrdinalIgnoreCase))
                    || (p.AppliesTo == "Specific Product" && string.Equals(p.AppliesToValue, productName, StringComparison.OrdinalIgnoreCase));
                if (applies && p.DiscountValue > best) best = p.DiscountValue;
            }
            return best;
        }
    }
}
