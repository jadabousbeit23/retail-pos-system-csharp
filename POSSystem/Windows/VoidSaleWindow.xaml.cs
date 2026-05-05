using POSSystem.Database;
using System;
using System.Data.SQLite;
using System.Windows;

namespace POSSystem.Windows
{
    public class VoidedSale
    {
        public int Id { get; set; }
        public string Date { get; set; }
        public string CashierName { get; set; }
        public string Items { get; set; }
    }

    public partial class VoidedSalesWindow : Window
    {
        public VoidedSalesWindow()
        {
            InitializeComponent();
            LoadVoidedSales();
        }

        void LoadVoidedSales()
        {
            lvVoided.Items.Clear();
            using (SQLiteConnection con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                SQLiteCommand cmd = new SQLiteCommand(
                    "SELECT * FROM VoidedSales ORDER BY Id DESC", con);
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    VoidedSale v = new VoidedSale();
                    v.Id = Convert.ToInt32(reader["Id"]);
                    // Date is now stored in ISO 8601 format (yyyy-MM-dd HH:mm:ss)
                    // Display as-is or format for readability if needed
                    v.Date = reader["Date"].ToString();
                    v.CashierName = reader["CashierName"].ToString();
                    v.Items = reader["Items"].ToString();
                    lvVoided.Items.Add(v);
                }
                reader.Close();
            }
            lblVoidCount.Content = $"{lvVoided.Items.Count} records";
        }

        // ════════════════════════════════════════════════════════
        // FILTER BY DATE - FIXED WITH ISO DATES
        // ════════════════════════════════════════════════════════
        void FilterByDate(string dateFilter)
        {
            lvVoided.Items.Clear();
            using (SQLiteConnection con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                // Use LIKE with ISO format (yyyy-MM-dd)
                SQLiteCommand cmd = new SQLiteCommand(
                    "SELECT * FROM VoidedSales WHERE Date LIKE @date ORDER BY Id DESC", con);
                cmd.Parameters.AddWithValue("@date", $"%{dateFilter}%");

                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    VoidedSale v = new VoidedSale();
                    v.Id = Convert.ToInt32(reader["Id"]);
                    v.Date = reader["Date"].ToString();
                    v.CashierName = reader["CashierName"].ToString();
                    v.Items = reader["Items"].ToString();
                    lvVoided.Items.Add(v);
                }
                reader.Close();
            }
            lblVoidCount.Content = $"{lvVoided.Items.Count} records";
        }

        // ════════════════════════════════════════════════════════
        // LOAD TODAY'S VOIDED SALES - FIXED WITH ISO DATES
        // ════════════════════════════════════════════════════════
        void LoadTodaysVoidedSales()
        {
            string todayIso = DatabaseHelper.GetTodayIso();
            FilterByDate(todayIso);
        }

        // ════════════════════════════════════════════════════════
        // REFRESH BUTTON HANDLER
        // ════════════════════════════════════════════════════════
        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadVoidedSales();
        }

        // ════════════════════════════════════════════════════════
        // FILTER BUTTON HANDLER
        // ════════════════════════════════════════════════════════
        private void btnFilterToday_Click(object sender, RoutedEventArgs e)
        {
            LoadTodaysVoidedSales();
        }

        // ════════════════════════════════════════════════════════
        // CLEAR FILTER BUTTON HANDLER
        // ════════════════════════════════════════════════════════
        private void btnClearFilter_Click(object sender, RoutedEventArgs e)
        {
            LoadVoidedSales();
        }
    }
}