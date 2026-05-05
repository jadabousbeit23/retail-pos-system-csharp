using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace POSSystem.Database    // ✅ FIXED: Changed from Possystem to POSSystem
{
    /// <summary>
    /// Static helper — call anywhere in the app:
    ///     SettingsHelper.Get("StoreName")
    ///     SettingsHelper.Set("StoreName", "My Shop")
    /// </summary>
    public static class SettingsHelper
    {
        // ── Default values used when key doesn't exist yet ──
        static readonly Dictionary<string, string> _defaults =
            new Dictionary<string, string>
        {
            { "StoreName",          "My Store"              },
            { "StoreAddress",       ""                      },
            { "StorePhone",         ""                      },
            { "StoreEmail",         ""                      },
            { "ReceiptHeader",      "Welcome!"              },
            { "ReceiptFooter",      "Thank you for shopping with us!" },
            { "TaxEnabled",         "false"                 },
            { "TaxPercent",         "0"                     },
            { "Currency",           "LBP"                   },
            { "ExchangeRate",       "90000"                 },
            { "LowStockThreshold",  "5"                     },
            { "PrinterName",        ""                      },
            { "LogoPath",           ""                      },
        };

        // ── CREATE TABLE (called from DatabaseHelper.CreateTables) ──
        public static void CreateTable(SQLiteConnection con)
        {
            new SQLiteCommand(@"
                CREATE TABLE IF NOT EXISTS Settings (
                    Key   TEXT PRIMARY KEY,
                    Value TEXT NOT NULL DEFAULT ''
                )", con).ExecuteNonQuery();

            // Seed defaults if table was just created
            foreach (var kv in _defaults)
            {
                SQLiteCommand ins = new SQLiteCommand(@"
                    INSERT OR IGNORE INTO Settings (Key, Value)
                    VALUES (@k, @v)", con);
                ins.Parameters.AddWithValue("@k", kv.Key);
                ins.Parameters.AddWithValue("@v", kv.Value);
                ins.ExecuteNonQuery();
            }
        }

        // ── GET ──────────────────────────────────────────────────────
        public static string Get(string key)
        {
            try
            {
                using (SQLiteConnection con = new SQLiteConnection(
                    DatabaseHelper.ConnectionString))
                {
                    con.Open();
                    SQLiteCommand cmd = new SQLiteCommand(
                        "SELECT Value FROM Settings WHERE Key = @k",
                        con);
                    cmd.Parameters.AddWithValue("@k", key);
                    object result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                        return result.ToString();
                }
            }
            catch { }

            // Fall back to default
            return _defaults.ContainsKey(key)
                ? _defaults[key] : "";
        }

        // ── GET with fallback ────────────────────────────────────────
        public static string Get(string key, string fallback)
        {
            string val = Get(key);
            return string.IsNullOrWhiteSpace(val) ? fallback : val;
        }

        // ── GET typed helpers ────────────────────────────────────────
        public static bool GetBool(string key)
        {
            string v = Get(key).ToLower();
            return v == "true" || v == "1" || v == "yes";
        }

        public static int GetInt(string key)
        {
            int.TryParse(Get(key), out int result);
            return result;
        }

        // ── GET DOUBLE (with default value) ──────────────────────────
        public static double GetDouble(string key, double defaultVal = 0)
        {
            string val = Get(key);
            return double.TryParse(val, out double d) ? d : defaultVal;
        }

        // ── SET ──────────────────────────────────────────────────────
        public static void Set(string key, string value)
        {
            try
            {
                using (SQLiteConnection con = new SQLiteConnection(
                    DatabaseHelper.ConnectionString))
                {
                    con.Open();
                    SQLiteCommand cmd = new SQLiteCommand(@"
                        INSERT INTO Settings (Key, Value)
                        VALUES (@k, @v)
                        ON CONFLICT(Key) DO UPDATE SET Value = @v",
                        con);
                    cmd.Parameters.AddWithValue("@k", key);
                    cmd.Parameters.AddWithValue("@v", value ?? "");
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "Settings.Set error: " + ex.Message);
            }
        }

        // ── SAVE MANY AT ONCE ────────────────────────────────────────
        public static void SaveAll(Dictionary<string, string> values)
        {
            try
            {
                using (SQLiteConnection con = new SQLiteConnection(
                    DatabaseHelper.ConnectionString))
                {
                    con.Open();
                    using (SQLiteTransaction tx = con.BeginTransaction())
                    {
                        foreach (var kv in values)
                        {
                            SQLiteCommand cmd = new SQLiteCommand(@"
                                INSERT INTO Settings (Key, Value)
                                VALUES (@k, @v)
                                ON CONFLICT(Key) DO UPDATE SET Value = @v",
                                con, tx);
                            cmd.Parameters.AddWithValue("@k", kv.Key);
                            cmd.Parameters.AddWithValue("@v", kv.Value ?? "");
                            cmd.ExecuteNonQuery();
                        }
                        tx.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "Settings.SaveAll error: " + ex.Message);
            }
        }

        // ── FORMAT CURRENCY ──────────────────────────────────────────
        public static string FormatCurrency(double amount)
        {
            string currency = Get("Currency", "LBP");
            return $"{currency} {amount:N0}";
        }

        // ── SEED DEFAULT SETTINGS ────────────────────────────────────
        public static void SeedDefaults()
        {
            SeedIfEmpty("StoreName", "My Store");
            SeedIfEmpty("StorePhone", "");
            SeedIfEmpty("StoreAddress", "");
            SeedIfEmpty("StoreFooter", "Thank you for your purchase!");
            SeedIfEmpty("ExchangeRate", "90000");
            SeedIfEmpty("LowStockThreshold", "5");
            SeedIfEmpty("TaxPercent", "0");
        }

        static void SeedIfEmpty(string key, string value)
        {
            try
            {
                string existing = Get(key);
                if (string.IsNullOrEmpty(existing))
                    Set(key, value);
            }
            catch { Set(key, value); }
        }
    }
}