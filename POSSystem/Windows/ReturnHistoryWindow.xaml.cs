using POSSystem.Database;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;

namespace POSSystem.Windows
{
    public class ReturnRecord
    {
        public int Id { get; set; }
        public int OriginalSaleId { get; set; }
        public string ReturnDate { get; set; }
        public string CashierName { get; set; }
        public string ReturnType { get; set; }
        public string Reason { get; set; }
        public double RefundAmount { get; set; }
        public string Items { get; set; }
        public string RefundDisplay => $"LBP {RefundAmount:N0}";
    }

    public partial class ReturnHistoryWindow : Window
    {
        List<ReturnRecord> _all = new List<ReturnRecord>();

        public ReturnHistoryWindow()
        {
            InitializeComponent();
            LoadHistory();
        }

        void LoadHistory()
        {
            _all.Clear();
            double totalRefunded = 0;

            using (var con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                var r = new SQLiteCommand(
                    "SELECT * FROM Returns ORDER BY Id DESC", con).ExecuteReader();
                while (r.Read())
                {
                    double amt = Convert.ToDouble(r["RefundAmount"]);
                    totalRefunded += amt;
                    _all.Add(new ReturnRecord
                    {
                        Id             = Convert.ToInt32(r["Id"]),
                        OriginalSaleId = Convert.ToInt32(r["OriginalSaleId"]),
                        ReturnDate     = r["ReturnDate"].ToString(),
                        CashierName    = r["CashierName"].ToString(),
                        ReturnType     = r["ReturnType"].ToString(),
                        Reason         = r["Reason"].ToString(),
                        RefundAmount   = amt,
                        Items          = r["Items"].ToString()
                    });
                }
                r.Close();
            }

            ApplyFilter("");
            lblCount.Content = $"{_all.Count} records";
            lblTotal.Content = $"Total Refunded: LBP {totalRefunded:N0}";
        }

        void ApplyFilter(string q)
        {
            lvHistory.Items.Clear();
            q = q.ToLower();
            foreach (var rec in _all)
            {
                if (string.IsNullOrEmpty(q)
                    || rec.OriginalSaleId.ToString().Contains(q)
                    || rec.CashierName.ToLower().Contains(q)
                    || rec.ReturnType.ToLower().Contains(q)
                    || rec.Reason.ToLower().Contains(q))
                    lvHistory.Items.Add(rec);
            }
        }

        private void txtFilter_TextChanged(object sender, TextChangedEventArgs e)
            => ApplyFilter(txtFilter.Text.Trim());

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
