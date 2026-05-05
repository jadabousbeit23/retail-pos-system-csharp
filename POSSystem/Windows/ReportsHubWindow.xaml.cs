using System;
using System.Collections.Generic;
using System.Data.SQLite;  // Changed from SqlClient to SQLite
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using POSSystem.Database;  // Added for Session class

namespace POSSystem.Windows
{
    public partial class ReportsHubWindow : Window
    {
        public ReportsHubWindow()
        {
            InitializeComponent();
        }

        // Standard Reports (Daily/Monthly/Yearly)
        private void btnStandardReports_Click(object sender, RoutedEventArgs e)
        {
            var reports = new ReportsWindow();
            reports.Owner = this;
            reports.ShowDialog();
        }

        // Advanced Reports (Profit, Best Sellers, Hourly)
        private void btnAdvancedReports_Click(object sender, RoutedEventArgs e)
        {
            var advReports = new AdvancedReportsWindow();
            advReports.Owner = this;
            advReports.ShowDialog();
        }

        // X-Report (Current Shift)
        private void btnXReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // FIXED: Use XReportWindow instead of XReportPreviewWindow
                var xReport = new XReportWindow();
                xReport.Owner = this;
                xReport.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating X-Report: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Z-Report
        private void btnZReport_Click(object sender, RoutedEventArgs e)
        {
            var zReport = new ZReportWindow();
            zReport.Owner = this;
            zReport.ShowDialog();
        }

        // Excel Export
        private void btnExcelExport_Click(object sender, RoutedEventArgs e)
        {
            var excelWin = new ExcelExportWindow();
            excelWin.Owner = this;
            excelWin.ShowDialog();
        }

        // Cashier Report
        private void btnCashierReport_Click(object sender, RoutedEventArgs e)
        {
            if (!Session.IsAdmin)
            {
                MessageBox.Show("⛔ Admins only!", "Access Denied");
                return;
            }
            var cashWin = new CashierReportWindow();
            cashWin.Owner = this;
            cashWin.ShowDialog();
        }

        // Close Day
        private void btnCloseDay_Click(object sender, RoutedEventArgs e)
        {
            if (!Session.IsAdmin)
            {
                MessageBox.Show("⛔ Admins only!", "Close Day");
                return;
            }

            var closeWin = new CloseDayWindow();
            closeWin.Owner = this;
            bool? result = closeWin.ShowDialog();

            // Refresh main dashboard if day was closed
            if (result == true)
            {
                // Find main window and refresh it
                foreach (Window w in Application.Current.Windows)
                {
                    if (w is MainWindow mw)
                    {
                        mw.ClearDashboard();
                        break;
                    }
                }
            }
        }

        // Close Hub
        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}