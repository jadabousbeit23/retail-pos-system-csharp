using System;
using System.Data.SQLite;

namespace POSSystem.Database
{
    /// <summary>
    /// Utility methods for expiry-aware stock checks.
    /// Call these from the Sales window before allowing a product to be added to the cart.
    /// </summary>
    public static class ExpiryHelper
    {
        // ──────────────────────────────────────────────────────────────
        // Returns the number of units available that are NOT expired.
        // If a product has no batches at all, the full product Stock is
        // returned (legacy products without batch tracking can still sell).
        // ──────────────────────────────────────────────────────────────
        public static int GetSellableStock(int productId)
        {
            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();

                // Check if this product has ANY batch records
                int batchCount = Convert.ToInt32(
                    new SQLiteCommand(
                        $"SELECT COUNT(*) FROM ProductBatches WHERE ProductId={productId}", con)
                    .ExecuteScalar());

                if (batchCount == 0)
                {
                    // No batch tracking — fall back to product stock
                    object val = new SQLiteCommand(
                        $"SELECT Stock FROM Products WHERE Id={productId}", con).ExecuteScalar();
                    return val != null ? Convert.ToInt32(val) : 0;
                }

                // Sum only batches that are not expired
                int sellable = 0;
                var r = new SQLiteCommand(
                    "SELECT Quantity, ExpiryDate FROM ProductBatches WHERE ProductId=@pid AND Quantity>0",
                    con);
                r.Parameters.AddWithValue("@pid", productId);
                var reader = r.ExecuteReader();

                while (reader.Read())
                {
                    string expStr = reader["ExpiryDate"].ToString();
                    int    qty    = Convert.ToInt32(reader["Quantity"]);

                    // No expiry date → always sellable
                    if (string.IsNullOrEmpty(expStr))
                    { sellable += qty; continue; }

                    // Parse with the same flexible helper
                    DateTime expiry;
                    if (!TryParseFlexibleDate(expStr, out expiry))
                    { sellable += qty; continue; }  // unparseable → don't block

                    if (expiry >= DateTime.Today)
                        sellable += qty;
                    // else: expired → excluded from sellable count
                }
                reader.Close();
                return sellable;
            }
        }

        // ──────────────────────────────────────────────────────────────
        // Returns true when ALL stock for this product is expired
        // (safe call to use in the sales window before adding to cart).
        // ──────────────────────────────────────────────────────────────
        public static bool IsFullyExpired(int productId)
        {
            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();

                int batchCount = Convert.ToInt32(
                    new SQLiteCommand(
                        $"SELECT COUNT(*) FROM ProductBatches WHERE ProductId={productId}", con)
                    .ExecuteScalar());

                if (batchCount == 0) return false;  // no batch tracking → not expired

                // If there is at least one non-expired, non-empty batch → not fully expired
                var r = new SQLiteCommand(@"
                    SELECT Quantity, ExpiryDate
                    FROM   ProductBatches
                    WHERE  ProductId=@pid AND Quantity>0", con);
                r.Parameters.AddWithValue("@pid", productId);
                var reader = r.ExecuteReader();

                bool hasGoodStock = false;
                while (reader.Read())
                {
                    string expStr = reader["ExpiryDate"].ToString();
                    if (string.IsNullOrEmpty(expStr)) { hasGoodStock = true; break; }

                    if (TryParseFlexibleDate(expStr, out DateTime expiry) &&
                        expiry >= DateTime.Today)
                    { hasGoodStock = true; break; }
                }
                reader.Close();
                return !hasGoodStock;
            }
        }

        // ──────────────────────────────────────────────────────────────
        // Returns a short warning string if a product is close to expiry
        // (≤ 7 days) but not yet expired.  Returns null if all is fine.
        // ──────────────────────────────────────────────────────────────
        public static string GetExpiryWarning(int productId, string productName)
        {
            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var r = new SQLiteCommand(@"
                    SELECT MIN(ExpiryDate) AS Nearest
                    FROM   ProductBatches
                    WHERE  ProductId = @pid AND Quantity > 0
                      AND  ExpiryDate IS NOT NULL AND ExpiryDate != ''", con);
                r.Parameters.AddWithValue("@pid", productId);
                object val = r.ExecuteScalar();
                if (val == null || val == System.DBNull.Value) return null;

                if (!TryParseFlexibleDate(val.ToString(), out DateTime expiry)) return null;

                int days = (expiry - DateTime.Today).Days;
                if (days < 0)  return null; // fully expired — blocked upstream
                if (days <= 7) return $"⚠️ '{productName}' expires in {days} day(s) — sell asap!";
                return null;
            }
        }

        // ──────────────────────────────────────────────────────────────
        // Private helper — mirrors the one in the Windows layer
        // ──────────────────────────────────────────────────────────────
        static bool TryParseFlexibleDate(string input, out DateTime result)
        {
            result = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(input)) return false;
            string n = input.Trim().Replace('-', '/');
            string[] formats = {
                "dd/MM/yyyy", "d/M/yyyy", "d/MM/yyyy", "dd/M/yyyy",
                "MM/dd/yyyy", "yyyy/MM/dd"
            };
            return DateTime.TryParseExact(n, formats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out result);
        }
    }
}
