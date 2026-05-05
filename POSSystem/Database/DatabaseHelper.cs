using POSSystem.Database;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace POSSystem.Database
{
    internal class DatabaseHelper
    {
        // ══════════════════════════════════════════════
        // DYNAMIC CONNECTION STRING (supports network DB)
        // ══════════════════════════════════════════════
        private static string _cs = null;

        public static string ConnectionString
        {
            get
            {
                if (_cs != null) return _cs;
                _cs = BuildConnectionString();
                return _cs;
            }
        }

        public static void ResetConnectionString() => _cs = null;

        static string BuildConnectionString()
        {
            string saved = ReadSavedDbPath();
            if (!string.IsNullOrEmpty(saved) && File.Exists(saved))
                return $"Data Source={saved};Version=3;";

            string local = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "pos.db");
            return $"Data Source={local};Version=3;";
        }

        static string ConfigPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db_config.txt");

        public static string ReadSavedDbPath()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string p = File.ReadAllText(ConfigPath).Trim();
                    return string.IsNullOrEmpty(p) ? null : p;
                }
            }
            catch { }
            return null;
        }

        public static void SaveDbPath(string path)
        {
            try { File.WriteAllText(ConfigPath, path.Trim()); }
            catch { }
            ResetConnectionString();
        }

        public static void ClearDbPath()
        {
            try { if (File.Exists(ConfigPath)) File.Delete(ConfigPath); }
            catch { }
            ResetConnectionString();
        }

        public static bool TestConnection(string dbPath)
        {
            try
            {
                if (!File.Exists(dbPath)) return false;
                using (var con = new SQLiteConnection(
                    $"Data Source={dbPath};Version=3;"))
                {
                    con.Open();
                    return true;
                }
            }
            catch { return false; }
        }

        public static bool IsNetworkDatabase()
        {
            string p = ReadSavedDbPath();
            if (string.IsNullOrEmpty(p)) return false;
            return p.StartsWith(@"\\") || p.StartsWith("//");
        }

        public static string GetCurrentDbPath()
        {
            string saved = ReadSavedDbPath();
            if (!string.IsNullOrEmpty(saved) && File.Exists(saved))
                return saved;
            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "pos.db");
        }

        public static bool CopyLocalToNetwork(string networkPath)
        {
            try
            {
                string local = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "pos.db");
                if (!File.Exists(local))
                {
                    MessageBox.Show("Local database not found!");
                    return false;
                }
                File.Copy(local, networkPath, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Copy failed: {ex.Message}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════
        // DATE STANDARDIZATION - ISO 8601 FORMAT
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Converts DateTime to ISO format for database storage
        /// Format: yyyy-MM-dd HH:mm:ss
        /// </summary>
        public static string ToIsoDateTime(DateTime date)
        {
            return date.ToString("yyyy-MM-dd HH:mm:ss");
        }

        /// <summary>
        /// Gets today's date in ISO format for queries
        /// Format: yyyy-MM-dd
        /// </summary>
        public static string GetTodayIso()
        {
            return DateTime.Now.ToString("yyyy-MM-dd");
        }

        /// <summary>
        /// Creates a WHERE clause for today's date using LIKE
        /// </summary>
        public static string GetTodayWhereClause(string dateColumn)
        {
            string today = GetTodayIso();
            return $"{dateColumn} LIKE '{today}%'";
        }

        /// <summary>
        /// Migrates existing dd/MM/yyyy dates to ISO format
        /// Run this once when app starts
        /// </summary>
        public static void MigrateDatesToIsoFormat()
        {
            try
            {
                using (var con = new SQLiteConnection(ConnectionString))
                {
                    con.Open();

                    // Check if migration is needed by sampling a date
                    var sampleCmd = new SQLiteCommand("SELECT Date FROM Sales LIMIT 1", con);
                    var sample = sampleCmd.ExecuteScalar()?.ToString();

                    // If already ISO format (starts with 20xx-), skip
                    if (sample != null && sample.StartsWith("20") && sample.Contains("-"))
                    {
                        Debug.WriteLine("[DateMigration] Dates already in ISO format.");
                        return;
                    }

                    // Check if we have any dates in old format (contains /)
                    var checkCmd = new SQLiteCommand("SELECT COUNT(*) FROM Sales WHERE Date LIKE '%/%'", con);
                    int oldFormatCount = Convert.ToInt32(checkCmd.ExecuteScalar());

                    if (oldFormatCount == 0)
                    {
                        Debug.WriteLine("[DateMigration] No old format dates found.");
                        return;
                    }

                    // Migrate Sales table from dd/MM/yyyy HH:mm:ss to yyyy-MM-dd HH:mm:ss
                    var salesCmd = new SQLiteCommand(@"
                        UPDATE Sales SET Date = 
                            SUBSTR(Date, 7, 4) || '-' || 
                            SUBSTR(Date, 4, 2) || '-' || 
                            SUBSTR(Date, 1, 2) || ' ' ||
                            SUBSTR(Date, 12, 8)
                        WHERE Date LIKE '__/__/____%'", con);
                    int salesUpdated = salesCmd.ExecuteNonQuery();
                    Debug.WriteLine($"[DateMigration] Updated {salesUpdated} Sales records");

                    // Migrate Shifts table
                    var shiftsCmd = new SQLiteCommand(@"
                        UPDATE Shifts SET 
                            OpenedAt = CASE 
                                WHEN OpenedAt LIKE '__/__/____%' THEN
                                    SUBSTR(OpenedAt, 7, 4) || '-' || 
                                    SUBSTR(OpenedAt, 4, 2) || '-' || 
                                    SUBSTR(OpenedAt, 1, 2) || ' ' ||
                                    SUBSTR(OpenedAt, 12, 8)
                                ELSE OpenedAt
                            END,
                            ClosedAt = CASE 
                                WHEN ClosedAt = '' THEN ''
                                WHEN ClosedAt LIKE '__/__/____%' THEN
                                    SUBSTR(ClosedAt, 7, 4) || '-' || 
                                    SUBSTR(ClosedAt, 4, 2) || '-' || 
                                    SUBSTR(ClosedAt, 1, 2) || ' ' ||
                                    SUBSTR(ClosedAt, 12, 8)
                                ELSE ClosedAt
                            END
                        WHERE OpenedAt LIKE '__/__/____%' OR ClosedAt LIKE '__/__/____%'", con);
                    int shiftsUpdated = shiftsCmd.ExecuteNonQuery();
                    Debug.WriteLine($"[DateMigration] Updated {shiftsUpdated} Shifts records");

                    // Migrate VoidedSales
                    var voidCmd = new SQLiteCommand(@"
                        UPDATE VoidedSales SET Date = 
                            SUBSTR(Date, 7, 4) || '-' || 
                            SUBSTR(Date, 4, 2) || '-' || 
                            SUBSTR(Date, 1, 2) || ' ' ||
                            SUBSTR(Date, 12, 8)
                        WHERE Date LIKE '__/__/____%'", con);
                    int voidUpdated = voidCmd.ExecuteNonQuery();
                    Debug.WriteLine($"[DateMigration] Updated {voidUpdated} VoidedSales records");

                    // Migrate Returns
                    var retCmd = new SQLiteCommand(@"
                        UPDATE Returns SET ReturnDate = 
                            SUBSTR(ReturnDate, 7, 4) || '-' || 
                            SUBSTR(ReturnDate, 4, 2) || '-' || 
                            SUBSTR(ReturnDate, 1, 2) || ' ' ||
                            SUBSTR(ReturnDate, 12, 8)
                        WHERE ReturnDate LIKE '__/__/____%'", con);
                    int retUpdated = retCmd.ExecuteNonQuery();
                    Debug.WriteLine($"[DateMigration] Updated {retUpdated} Returns records");

                    // Migrate HeldCarts
                    var heldCmd = new SQLiteCommand(@"
                        UPDATE HeldCarts SET CreatedAt = 
                            SUBSTR(CreatedAt, 7, 4) || '-' || 
                            SUBSTR(CreatedAt, 4, 2) || '-' || 
                            SUBSTR(CreatedAt, 1, 2) || ' ' ||
                            SUBSTR(CreatedAt, 12, 8)
                        WHERE CreatedAt LIKE '__/__/____%'", con);
                    int heldUpdated = heldCmd.ExecuteNonQuery();
                    Debug.WriteLine($"[DateMigration] Updated {heldUpdated} HeldCarts records");

                    // Migrate LoyaltyTransactions
                    var loyaltyCmd = new SQLiteCommand(@"
                        UPDATE LoyaltyTransactions SET CreatedAt = 
                            SUBSTR(CreatedAt, 7, 4) || '-' || 
                            SUBSTR(CreatedAt, 4, 2) || '-' || 
                            SUBSTR(CreatedAt, 1, 2) || ' ' ||
                            SUBSTR(CreatedAt, 12, 8)
                        WHERE CreatedAt LIKE '__/__/____%'", con);
                    int loyaltyUpdated = loyaltyCmd.ExecuteNonQuery();
                    Debug.WriteLine($"[DateMigration] Updated {loyaltyUpdated} LoyaltyTransactions records");

                    // Migrate AuditLog
                    var auditCmd = new SQLiteCommand(@"
                        UPDATE AuditLog SET LoggedAt = 
                            SUBSTR(LoggedAt, 7, 4) || '-' || 
                            SUBSTR(LoggedAt, 4, 2) || '-' || 
                            SUBSTR(LoggedAt, 1, 2) || ' ' ||
                            SUBSTR(LoggedAt, 12, 8)
                        WHERE LoggedAt LIKE '__/__/____%'", con);
                    int auditUpdated = auditCmd.ExecuteNonQuery();
                    Debug.WriteLine($"[DateMigration] Updated {auditUpdated} AuditLog records");

                    Debug.WriteLine("[DateMigration] Date migration completed successfully");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DateMigration] Error: {ex.Message}");
                // Don't throw - let the app continue even if migration fails
            }
        }

        // ══════════════════════════════════════════════
        // CREATE TABLES
        // ══════════════════════════════════════════════
        public static void CreateTables()
        {
            using (SQLiteConnection con = new SQLiteConnection(ConnectionString))
            {
                con.Open();
                string[] queries = {
                    @"CREATE TABLE IF NOT EXISTS Users (
                        Id       INTEGER PRIMARY KEY AUTOINCREMENT,
                        Username TEXT    NOT NULL,
                        FullName TEXT    NOT NULL DEFAULT '',
                        Password TEXT    NOT NULL,
                        Role     TEXT    NOT NULL DEFAULT 'cashier',
                        Code     TEXT    NOT NULL DEFAULT '0000')",

                    @"CREATE TABLE IF NOT EXISTS Products (
                        Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name        TEXT    NOT NULL,
                        Price       REAL    NOT NULL,
                        CostPrice   REAL    NOT NULL DEFAULT 0,
                        Stock       INTEGER NOT NULL,
                        MinStock    INTEGER NOT NULL DEFAULT 5,
                        Category    TEXT    NOT NULL DEFAULT 'General',
                        ProductCode TEXT    NOT NULL DEFAULT '')",

                    @"CREATE TABLE IF NOT EXISTS Categories (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL UNIQUE,
                        ColorCode TEXT NOT NULL DEFAULT '#4CAF50')" ,

                    @"CREATE TABLE IF NOT EXISTS Sales (
                        Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                        Date          TEXT    NOT NULL,
                        TotalAmount   REAL    NOT NULL,
                        CashPaid      REAL    NOT NULL DEFAULT 0,
                        CardPaid      REAL    NOT NULL DEFAULT 0,
                        ChangeDue     REAL    NOT NULL DEFAULT 0,
                        PaymentMethod TEXT    NOT NULL DEFAULT 'Cash',
                        CashierName   TEXT    NOT NULL DEFAULT '',
                        CustomerId    INTEGER          DEFAULT 0,
                        PointsEarned  INTEGER          DEFAULT 0,
                        StampsEarned  INTEGER          DEFAULT 0,
                        PointsRedeemed INTEGER         DEFAULT 0)",

                    @"CREATE TABLE IF NOT EXISTS SaleItems (
                        Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                        SaleId    INTEGER NOT NULL,
                        ProductId INTEGER NOT NULL,
                        Quantity  INTEGER NOT NULL,
                        Price     REAL    NOT NULL,
                        FOREIGN KEY(SaleId)    REFERENCES Sales(Id),
                        FOREIGN KEY(ProductId) REFERENCES Products(Id))",

                    @"CREATE TABLE IF NOT EXISTS VoidedSales (
                        Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                        Date        TEXT    NOT NULL,
                        CashierName TEXT    NOT NULL,
                        Reason      TEXT,
                        Items       TEXT    NOT NULL)",

                    @"CREATE TABLE IF NOT EXISTS DailyReports (
                        Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                        Date        TEXT    NOT NULL,
                        TotalSales  INTEGER NOT NULL,
                        TotalAmount REAL    NOT NULL,
                        ClosedAt    TEXT)",

                    @"CREATE TABLE IF NOT EXISTS MonthlyReports (
                        Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                        Month       TEXT    NOT NULL,
                        Year        INTEGER NOT NULL,
                        TotalSales  INTEGER NOT NULL,
                        TotalAmount REAL    NOT NULL)",

                    @"CREATE TABLE IF NOT EXISTS YearlyReports (
                        Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                        Year        INTEGER NOT NULL,
                        TotalSales  INTEGER NOT NULL,
                        TotalAmount REAL    NOT NULL)",

                    @"CREATE TABLE IF NOT EXISTS Suppliers (
                        Id      INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name    TEXT    NOT NULL,
                        Phone   TEXT    NOT NULL DEFAULT '',
                        Email   TEXT    NOT NULL DEFAULT '',
                        Address TEXT    NOT NULL DEFAULT '')",

                    @"CREATE TABLE IF NOT EXISTS PurchaseOrders (
                        Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                        SupplierId   INTEGER NOT NULL,
                        OrderDate    TEXT    NOT NULL,
                        ReceivedDate TEXT,
                        Status       TEXT    NOT NULL DEFAULT 'Pending',
                        Notes        TEXT    NOT NULL DEFAULT '',
                        FOREIGN KEY(SupplierId) REFERENCES Suppliers(Id))",

                    @"CREATE TABLE IF NOT EXISTS PurchaseOrderItems (
                        Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                        PurchaseOrderId INTEGER NOT NULL,
                        ProductId       INTEGER NOT NULL,
                        Quantity        INTEGER NOT NULL,
                        UnitCost        REAL    NOT NULL DEFAULT 0,
                        FOREIGN KEY(PurchaseOrderId) REFERENCES PurchaseOrders(Id),
                        FOREIGN KEY(ProductId)       REFERENCES Products(Id))",

                    @"CREATE TABLE IF NOT EXISTS ProductBatches (
                        Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                        ProductId    INTEGER NOT NULL,
                        BatchNumber  TEXT    NOT NULL DEFAULT '',
                        ExpiryDate   TEXT    NOT NULL DEFAULT '',
                        Quantity     INTEGER NOT NULL DEFAULT 0,
                        ReceivedDate TEXT    NOT NULL DEFAULT '',
                        FOREIGN KEY(ProductId) REFERENCES Products(Id))",

                    @"CREATE TABLE IF NOT EXISTS StockAdjustments (
                        Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                        ProductId    INTEGER NOT NULL,
                        ChangeAmount INTEGER NOT NULL,
                        Reason       TEXT    NOT NULL DEFAULT '',
                        AdjustedBy   TEXT    NOT NULL DEFAULT '',
                        AdjustedAt   TEXT    NOT NULL DEFAULT '',
                        NewStock     INTEGER NOT NULL DEFAULT 0,
                        FOREIGN KEY(ProductId) REFERENCES Products(Id))",

                    @"CREATE TABLE IF NOT EXISTS Customers (
                        Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name         TEXT    NOT NULL,
                        Phone        TEXT    NOT NULL DEFAULT '',
                        Email        TEXT    NOT NULL DEFAULT '',
                        LoyaltyCode  TEXT    NOT NULL DEFAULT '',
                        Points       INTEGER NOT NULL DEFAULT 0,
                        Stamps       INTEGER NOT NULL DEFAULT 0,
                        TotalSpent   REAL    NOT NULL DEFAULT 0,
                        CreatedAt    TEXT    NOT NULL DEFAULT '',
                        Notes        TEXT    NOT NULL DEFAULT '')",

                    @"CREATE TABLE IF NOT EXISTS Returns (
                        Id             INTEGER PRIMARY KEY AUTOINCREMENT,
                        OriginalSaleId INTEGER NOT NULL DEFAULT 0,
                        ReturnDate     TEXT    NOT NULL,
                        CashierName    TEXT    NOT NULL,
                        Reason         TEXT    NOT NULL DEFAULT '',
                        RefundAmount   REAL    NOT NULL DEFAULT 0,
                        ReturnType     TEXT    NOT NULL DEFAULT 'Refund',
                        Items          TEXT    NOT NULL DEFAULT '')",

                    @"CREATE TABLE IF NOT EXISTS ReturnItems (
                        Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                        ReturnId  INTEGER NOT NULL,
                        ProductId INTEGER NOT NULL,
                        Quantity  INTEGER NOT NULL,
                        UnitPrice REAL    NOT NULL DEFAULT 0,
                        Restocked INTEGER NOT NULL DEFAULT 1)",

                    @"CREATE TABLE IF NOT EXISTS HeldCarts (
                        Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                        HoldName    TEXT    NOT NULL DEFAULT 'Hold',
                        CartData    TEXT    NOT NULL DEFAULT '',
                        CreatedAt   TEXT    NOT NULL DEFAULT '',
                        CashierName TEXT    NOT NULL DEFAULT '')",

                    @"CREATE TABLE IF NOT EXISTS LoyaltyTransactions (
                        Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                        CustomerId  INTEGER NOT NULL,
                        SaleId      INTEGER NOT NULL DEFAULT 0,
                        Points      INTEGER NOT NULL DEFAULT 0,
                        Stamps      INTEGER NOT NULL DEFAULT 0,
                        Type        TEXT    NOT NULL DEFAULT 'Earn',
                        Description TEXT    NOT NULL DEFAULT '',
                        CreatedAt   TEXT    NOT NULL DEFAULT '')",

                    // ── NEW TABLES ──────────────────────────────
                    @"CREATE TABLE IF NOT EXISTS Shifts (
                        Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                        OpenedBy        TEXT    NOT NULL DEFAULT '',
                        OpenedAt        TEXT    NOT NULL DEFAULT '',
                        ClosedBy        TEXT    NOT NULL DEFAULT '',
                        ClosedAt        TEXT    NOT NULL DEFAULT '',
                        StartingCash    REAL    NOT NULL DEFAULT 0,
                        ExpectedCash    REAL    NOT NULL DEFAULT 0,
                        CountedCash     REAL    NOT NULL DEFAULT 0,
                        Difference      REAL    NOT NULL DEFAULT 0,
                        TotalSales      INTEGER NOT NULL DEFAULT 0,
                        TotalRevenue    REAL    NOT NULL DEFAULT 0,
                        Status          TEXT    NOT NULL DEFAULT 'Open',
                        Notes           TEXT    NOT NULL DEFAULT '')",

                    @"CREATE TABLE IF NOT EXISTS AuditLog (
                        Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                        Username    TEXT    NOT NULL DEFAULT '',
                        Action      TEXT    NOT NULL DEFAULT '',
                        Details     TEXT    NOT NULL DEFAULT '',
                        Module      TEXT    NOT NULL DEFAULT '',
                        LoggedAt    TEXT    NOT NULL DEFAULT '')",

                    @"CREATE TABLE IF NOT EXISTS Promotions (
                        Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name            TEXT    NOT NULL DEFAULT '',
                        Type            TEXT    NOT NULL DEFAULT 'Percent',
                        DiscountValue   REAL    NOT NULL DEFAULT 0,
                        AppliesTo       TEXT    NOT NULL DEFAULT 'All',
                        AppliesToValue  TEXT    NOT NULL DEFAULT '',
                        MinQty          INTEGER NOT NULL DEFAULT 1,
                        FreeQty         INTEGER NOT NULL DEFAULT 0,
                        StartDate       TEXT    NOT NULL DEFAULT '',
                        EndDate         TEXT    NOT NULL DEFAULT '',
                        IsActive        INTEGER NOT NULL DEFAULT 1,
                        CreatedBy       TEXT    NOT NULL DEFAULT '',
                        CreatedAt       TEXT    NOT NULL DEFAULT '')"
                };

                foreach (string query in queries)
                    new SQLiteCommand(query, con).ExecuteNonQuery();

                SettingsHelper.CreateTable(con);
            }

            MigratePasswordsToHash();
            MigrateSalesColumns();
            MigrateCustomerColumns();
            MigrateInventoryColumns();
            MigrateCategoriesTable();

            // Run date migration on startup
            MigrateDatesToIsoFormat();
        }

        // ══════════════════════════════════════════════
        // AUDIT LOG - Uses ISO format
        // ══════════════════════════════════════════════
        public static void Log(string action, string details, string module = "")
        {
            try
            {
                using (var con = new SQLiteConnection(ConnectionString))
                {
                    con.Open();
                    var cmd = new SQLiteCommand(@"
                        INSERT INTO AuditLog (Username, Action, Details, Module, LoggedAt)
                        VALUES (@u, @a, @d, @m, @t)", con);
                    cmd.Parameters.AddWithValue("@u", Session.Username ?? "System");
                    cmd.Parameters.AddWithValue("@a", action);
                    cmd.Parameters.AddWithValue("@d", details);
                    cmd.Parameters.AddWithValue("@m", module);
                    cmd.Parameters.AddWithValue("@t", ToIsoDateTime(DateTime.Now));
                    cmd.ExecuteNonQuery();
                }
            }
            catch { } // Never crash the app over a log entry
        }

        // ══════════════════════════════════════════════
        // MIGRATE SALES TABLE
        // ══════════════════════════════════════════════
        public static void MigrateSalesColumns()
        {
            using (SQLiteConnection con = new SQLiteConnection(ConnectionString))
            {
                con.Open();
                string[] migrations = {
                    "ALTER TABLE Sales ADD COLUMN CashPaid       REAL    NOT NULL DEFAULT 0",
                    "ALTER TABLE Sales ADD COLUMN CardPaid       REAL    NOT NULL DEFAULT 0",
                    "ALTER TABLE Sales ADD COLUMN ChangeDue      REAL    NOT NULL DEFAULT 0",
                    "ALTER TABLE Sales ADD COLUMN PaymentMethod  TEXT    NOT NULL DEFAULT 'Cash'",
                    "ALTER TABLE Sales ADD COLUMN CashierName    TEXT    NOT NULL DEFAULT ''",
                    "ALTER TABLE Sales ADD COLUMN CustomerId     INTEGER DEFAULT 0",
                    "ALTER TABLE Sales ADD COLUMN PointsEarned   INTEGER DEFAULT 0",
                    "ALTER TABLE Sales ADD COLUMN StampsEarned   INTEGER DEFAULT 0",
                    "ALTER TABLE Sales ADD COLUMN PointsRedeemed INTEGER DEFAULT 0"
                };
                foreach (string m in migrations)
                {
                    try { new SQLiteCommand(m, con).ExecuteNonQuery(); }
                    catch { }
                }
            }
        }

        // ══════════════════════════════════════════════
        // MIGRATE CUSTOMER COLUMNS
        // ══════════════════════════════════════════════
        public static void MigrateCustomerColumns()
        {
            using (SQLiteConnection con = new SQLiteConnection(ConnectionString))
            {
                con.Open();
                string[] migrations = {
                    "ALTER TABLE Sales ADD COLUMN CustomerId      INTEGER DEFAULT 0",
                    "ALTER TABLE Sales ADD COLUMN PointsEarned    INTEGER DEFAULT 0",
                    "ALTER TABLE Sales ADD COLUMN StampsEarned    INTEGER DEFAULT 0",
                    "ALTER TABLE Sales ADD COLUMN PointsRedeemed  INTEGER DEFAULT 0"
                };
                foreach (string m in migrations)
                {
                    try { new SQLiteCommand(m, con).ExecuteNonQuery(); }
                    catch { }
                }
            }
        }

        // ══════════════════════════════════════════════
        // MIGRATE INVENTORY COLUMNS
        // ══════════════════════════════════════════════
        public static void MigrateInventoryColumns()
        {
            using (SQLiteConnection con = new SQLiteConnection(ConnectionString))
            {
                con.Open();
                string[] migrations = {
                    "ALTER TABLE Products ADD COLUMN CostPrice REAL    NOT NULL DEFAULT 0",
                    "ALTER TABLE Products ADD COLUMN MinStock  INTEGER NOT NULL DEFAULT 5"
                };
                foreach (string m in migrations)
                {
                    try { new SQLiteCommand(m, con).ExecuteNonQuery(); }
                    catch { }
                }
            }
        }

        // ══════════════════════════════════════════════
        // MIGRATE PLAIN TEXT CODES → BCRYPT
        // ══════════════════════════════════════════════
        public static void MigratePasswordsToHash()
        {
            using (SQLiteConnection con = new SQLiteConnection(ConnectionString))
            {
                con.Open();

                var reader = new SQLiteCommand(
                    "SELECT Id, Code FROM Users", con).ExecuteReader();

                var toUpdate = new List<(int Id, string Code)>();
                while (reader.Read())
                {
                    string code = reader["Code"].ToString();
                    if (!HashHelper.IsHashed(code))
                        toUpdate.Add((Convert.ToInt32(reader["Id"]), code));
                }
                reader.Close();

                foreach (var (id, code) in toUpdate)
                {
                    var upd = new SQLiteCommand(
                        "UPDATE Users SET Code=@hash WHERE Id=@id", con);
                    upd.Parameters.AddWithValue("@hash", HashHelper.Hash(code));
                    upd.Parameters.AddWithValue("@id", id);
                    upd.ExecuteNonQuery();
                }

                if (toUpdate.Count > 0)
                    Debug.WriteLine(
                        $"[Security] Migrated {toUpdate.Count} plain text codes to BCrypt.");
            }
        }

        // ══════════════════════════════════════════════
        // SEED DATA
        // ══════════════════════════════════════════════
        public static void SeedData()
        {
            using (SQLiteConnection con = new SQLiteConnection(ConnectionString))
            {
                con.Open();
                int userCount = Convert.ToInt32(
                    new SQLiteCommand(
                        "SELECT COUNT(*) FROM Users", con).ExecuteScalar());

                if (userCount == 0)
                {
                    var cmd = new SQLiteCommand(@"
                        INSERT INTO Users
                        (Username, FullName, Password, Role, Code)
                        VALUES (@u, @fn, @p, @role, @code)", con);
                    cmd.Parameters.AddWithValue("@u", "admin");
                    cmd.Parameters.AddWithValue("@fn", "System Admin");
                    cmd.Parameters.AddWithValue("@p", "");
                    cmd.Parameters.AddWithValue("@role", "admin");
                    cmd.Parameters.AddWithValue("@code", HashHelper.Hash("1234"));
                    cmd.ExecuteNonQuery();
                }
            }

            // Seed default settings including ExchangeRate
            SettingsHelper.SeedDefaults();
        }

        // ══════════════════════════════════════════════
        // MIGRATE CATEGORIES TABLE (Add ColorCode)
        // ══════════════════════════════════════════════
        public static void MigrateCategoriesTable()
        {
            using (SQLiteConnection con = new SQLiteConnection(ConnectionString))
            {
                con.Open();

                // Create Categories table if not exists
                string createTable = @"
                    CREATE TABLE IF NOT EXISTS Categories (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL UNIQUE,
                        ColorCode TEXT NOT NULL DEFAULT '#4CAF50'
                    )";
                new SQLiteCommand(createTable, con).ExecuteNonQuery();

                // Add ColorCode column if not exists
                string[] migrations = {
                    "ALTER TABLE Categories ADD COLUMN ColorCode TEXT NOT NULL DEFAULT '#4CAF50'"
                };

                foreach (string m in migrations)
                {
                    try { new SQLiteCommand(m, con).ExecuteNonQuery(); }
                    catch { }
                }

                // Auto-populate categories from existing products
                try
                {
                    var cmd = new SQLiteCommand(@"
                        INSERT OR IGNORE INTO Categories (Name, ColorCode)
                        SELECT DISTINCT Category, '#4CAF50' FROM Products 
                        WHERE Category IS NOT NULL AND Category != ''", con);
                    cmd.ExecuteNonQuery();
                }
                catch { }
            }
        }
    }
}