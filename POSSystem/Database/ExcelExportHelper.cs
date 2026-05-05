using Microsoft.Win32;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.IO;

namespace POSSystem.Database
{
    public static class ExcelExportHelper
    {
        // ══════════════════════════════════════
        // SAVE DIALOG + OPEN
        // ══════════════════════════════════════
        static string PickSavePath(string defaultName)
        {
            SaveFileDialog dlg = new SaveFileDialog
            {
                Title = "Save Excel Report",
                FileName = defaultName,
                DefaultExt = ".xlsx",
                Filter = "Excel Files (*.xlsx)|*.xlsx"
            };
            return dlg.ShowDialog() == true
                ? dlg.FileName : null;
        }

        static void OpenFile(string path)
        {
            try
            {
                System.Diagnostics.Process.Start(path);
            }
            catch { }
        }

        // ══════════════════════════════════════
        // STYLE HELPERS
        // ══════════════════════════════════════
        static void StyleHeader(ExcelRange cell,
            string text, Color bgColor)
        {
            cell.Value = text;
            cell.Style.Font.Bold = true;
            cell.Style.Font.Size = 11;
            cell.Style.Font.Color.SetColor(Color.White);
            cell.Style.Fill.PatternType =
                ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor
                .SetColor(bgColor);
            cell.Style.HorizontalAlignment =
                ExcelHorizontalAlignment.Center;
            cell.Style.VerticalAlignment =
                ExcelVerticalAlignment.Center;
            cell.Style.Border.BorderAround(
                ExcelBorderStyle.Thin, Color.White);
        }

        static void StyleTitle(ExcelRange cell,
            string text)
        {
            cell.Value = text;
            cell.Style.Font.Bold = true;
            cell.Style.Font.Size = 16;
            cell.Style.Font.Color.SetColor(
                Color.FromArgb(13, 27, 42));
            cell.Style.HorizontalAlignment =
                ExcelHorizontalAlignment.Center;
        }

        static void StyleSubTitle(ExcelRange cell,
            string text)
        {
            cell.Value = text;
            cell.Style.Font.Size = 11;
            cell.Style.Font.Color.SetColor(
                Color.FromArgb(69, 90, 100));
            cell.Style.HorizontalAlignment =
                ExcelHorizontalAlignment.Center;
        }

        static void StyleDataRow(ExcelRange row,
            bool alt)
        {
            row.Style.Fill.PatternType =
                ExcelFillStyle.Solid;
            row.Style.Fill.BackgroundColor.SetColor(
                alt
                    ? Color.FromArgb(232, 240, 255)
                    : Color.White);
            row.Style.Border.BorderAround(
                ExcelBorderStyle.Hair,
                Color.FromArgb(187, 222, 251));
        }

        static void StyleTotalRow(ExcelRange row)
        {
            row.Style.Font.Bold = true;
            row.Style.Font.Size = 11;
            row.Style.Fill.PatternType =
                ExcelFillStyle.Solid;
            row.Style.Fill.BackgroundColor.SetColor(
                Color.FromArgb(21, 101, 192));
            row.Style.Font.Color.SetColor(Color.White);
        }

        // ══════════════════════════════════════
        // 1 — DAILY REPORT
        // ══════════════════════════════════════
        public static void ExportDaily(
            List<SaleRow> rows,
            double revenue, int count)
        {
            string path = PickSavePath(
                $"Daily_Report_" +
                $"{DateTime.Now:dd-MM-yyyy}.xlsx");
            if (path == null) return;

            using (ExcelPackage pkg =
                new ExcelPackage())
            {
                ExcelWorksheet ws =
                    pkg.Workbook.Worksheets.Add(
                        "Daily Report");

                ws.Cells["A1:E1"].Merge = true;
                StyleTitle(ws.Cells["A1"],
                    "POS SYSTEM — DAILY SALES REPORT");
                ws.Row(1).Height = 30;

                ws.Cells["A2:E2"].Merge = true;
                StyleSubTitle(ws.Cells["A2"],
                    $"Date: {DateTime.Now:dd/MM/yyyy}" +
                    $"   Generated: " +
                    $"{DateTime.Now:HH:mm:ss}");
                ws.Row(2).Height = 20;

                Color hdrColor =
                    Color.FromArgb(21, 101, 192);
                StyleHeader(ws.Cells["A4"],
                    "ID", hdrColor);
                StyleHeader(ws.Cells["B4"],
                    "Date & Time", hdrColor);
                StyleHeader(ws.Cells["C4"],
                    "Total Amount ($)", hdrColor);
                StyleHeader(ws.Cells["D4"],
                    "Cashier", hdrColor);
                StyleHeader(ws.Cells["E4"],
                    "Items", hdrColor);
                ws.Row(4).Height = 22;

                int r = 5;
                foreach (SaleRow row in rows)
                {
                    ws.Cells[r, 1].Value =
                        row.Id;
                    ws.Cells[r, 2].Value =
                        row.Date;
                    ws.Cells[r, 3].Value =
                        row.TotalAmount;
                    ws.Cells[r, 3].Style
                        .Numberformat.Format =
                        "#,##0.00";
                    ws.Cells[r, 4].Value =
                        Session.Username;
                    ws.Cells[r, 5].Value =
                        GetSaleItemCount(row.Id);
                    StyleDataRow(
                        ws.Cells[r, 1, r, 5],
                        r % 2 == 0);
                    r++;
                }

                ws.Cells[r, 1].Value = "TOTAL";
                ws.Cells[r, 2].Value =
                    $"{count} sales";
                ws.Cells[r, 3].Value = revenue;
                ws.Cells[r, 3].Style
                    .Numberformat.Format =
                    "#,##0.00";
                StyleTotalRow(
                    ws.Cells[r, 1, r, 5]);
                ws.Row(r).Height = 22;

                ws.Column(1).Width = 8;
                ws.Column(2).Width = 26;
                ws.Column(3).Width = 18;
                ws.Column(4).Width = 18;
                ws.Column(5).Width = 10;

                pkg.SaveAs(new FileInfo(path));
            }

            OpenFile(path);
        }

        // ══════════════════════════════════════
        // 2 — MONTHLY REPORT
        // ══════════════════════════════════════
        public static void ExportMonthly(
            List<MonthRow> rows,
            double totalRevenue, int totalSales)
        {
            string path = PickSavePath(
                $"Monthly_Report_" +
                $"{DateTime.Now:yyyy}.xlsx");
            if (path == null) return;

            using (ExcelPackage pkg =
                new ExcelPackage())
            {
                ExcelWorksheet ws =
                    pkg.Workbook.Worksheets.Add(
                        "Monthly Report");

                ws.Cells["A1:D1"].Merge = true;
                StyleTitle(ws.Cells["A1"],
                    "POS SYSTEM — MONTHLY SALES REPORT");
                ws.Row(1).Height = 30;

                ws.Cells["A2:D2"].Merge = true;
                StyleSubTitle(ws.Cells["A2"],
                    $"Year: {DateTime.Now.Year}" +
                    $"   Generated: " +
                    $"{DateTime.Now:dd/MM/yyyy HH:mm}");
                ws.Row(2).Height = 20;

                Color hdrColor =
                    Color.FromArgb(21, 101, 192);
                StyleHeader(ws.Cells["A4"],
                    "Month", hdrColor);
                StyleHeader(ws.Cells["B4"],
                    "Year", hdrColor);
                StyleHeader(ws.Cells["C4"],
                    "Total Sales", hdrColor);
                StyleHeader(ws.Cells["D4"],
                    "Total Revenue ($)", hdrColor);
                ws.Row(4).Height = 22;

                int r = 5;
                foreach (MonthRow row in rows)
                {
                    ws.Cells[r, 1].Value =
                        row.Month;
                    ws.Cells[r, 2].Value =
                        row.Year;
                    ws.Cells[r, 3].Value =
                        row.TotalSales;
                    ws.Cells[r, 4].Value =
                        row.TotalAmount;
                    ws.Cells[r, 4].Style
                        .Numberformat.Format =
                        "#,##0.00";
                    StyleDataRow(
                        ws.Cells[r, 1, r, 4],
                        r % 2 == 0);
                    r++;
                }

                ws.Cells[r, 1].Value = "TOTAL";
                ws.Cells[r, 3].Value = totalSales;
                ws.Cells[r, 4].Value = totalRevenue;
                ws.Cells[r, 4].Style
                    .Numberformat.Format =
                    "#,##0.00";
                StyleTotalRow(
                    ws.Cells[r, 1, r, 4]);
                ws.Row(r).Height = 22;

                ws.Column(1).Width = 16;
                ws.Column(2).Width = 10;
                ws.Column(3).Width = 14;
                ws.Column(4).Width = 20;

                pkg.SaveAs(new FileInfo(path));
            }

            OpenFile(path);
        }

        // ══════════════════════════════════════
        // 3 — YEARLY REPORT
        // ══════════════════════════════════════
        public static void ExportYearly(
            List<YearRow> rows,
            double totalRevenue, int totalSales)
        {
            string path = PickSavePath(
                $"Yearly_Report_" +
                $"{DateTime.Now:yyyy}.xlsx");
            if (path == null) return;

            using (ExcelPackage pkg =
                new ExcelPackage())
            {
                ExcelWorksheet ws =
                    pkg.Workbook.Worksheets.Add(
                        "Yearly Report");

                ws.Cells["A1:C1"].Merge = true;
                StyleTitle(ws.Cells["A1"],
                    "POS SYSTEM — YEARLY SALES REPORT");
                ws.Row(1).Height = 30;

                ws.Cells["A2:C2"].Merge = true;
                StyleSubTitle(ws.Cells["A2"],
                    $"Generated: " +
                    $"{DateTime.Now:dd/MM/yyyy HH:mm}");
                ws.Row(2).Height = 20;

                Color hdrColor =
                    Color.FromArgb(21, 101, 192);
                StyleHeader(ws.Cells["A4"],
                    "Year", hdrColor);
                StyleHeader(ws.Cells["B4"],
                    "Total Sales", hdrColor);
                StyleHeader(ws.Cells["C4"],
                    "Total Revenue ($)", hdrColor);
                ws.Row(4).Height = 22;

                int r = 5;
                foreach (YearRow row in rows)
                {
                    ws.Cells[r, 1].Value =
                        row.Year;
                    ws.Cells[r, 2].Value =
                        row.TotalSales;
                    ws.Cells[r, 3].Value =
                        row.TotalAmount;
                    ws.Cells[r, 3].Style
                        .Numberformat.Format =
                        "#,##0.00";
                    StyleDataRow(
                        ws.Cells[r, 1, r, 3],
                        r % 2 == 0);
                    r++;
                }

                ws.Cells[r, 1].Value = "TOTAL";
                ws.Cells[r, 2].Value = totalSales;
                ws.Cells[r, 3].Value = totalRevenue;
                ws.Cells[r, 3].Style
                    .Numberformat.Format =
                    "#,##0.00";
                StyleTotalRow(
                    ws.Cells[r, 1, r, 3]);
                ws.Row(r).Height = 22;

                ws.Column(1).Width = 10;
                ws.Column(2).Width = 14;
                ws.Column(3).Width = 20;

                pkg.SaveAs(new FileInfo(path));
            }

            OpenFile(path);
        }

        // ══════════════════════════════════════
        // 4 — SALES HISTORY
        // ══════════════════════════════════════
        public static void ExportSalesHistory()
        {
            string path = PickSavePath(
                $"Sales_History_" +
                $"{DateTime.Now:dd-MM-yyyy}.xlsx");
            if (path == null) return;

            using (ExcelPackage pkg =
                new ExcelPackage())
            {
                ExcelWorksheet ws =
                    pkg.Workbook.Worksheets.Add(
                        "Sales History");

                ws.Cells["A1:F1"].Merge = true;
                StyleTitle(ws.Cells["A1"],
                    "POS SYSTEM — COMPLETE SALES HISTORY");
                ws.Row(1).Height = 30;

                ws.Cells["A2:F2"].Merge = true;
                StyleSubTitle(ws.Cells["A2"],
                    $"Generated: " +
                    $"{DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                ws.Row(2).Height = 20;

                Color hdrColor =
                    Color.FromArgb(46, 125, 50);
                StyleHeader(ws.Cells["A4"],
                    "Sale ID", hdrColor);
                StyleHeader(ws.Cells["B4"],
                    "Date & Time", hdrColor);
                StyleHeader(ws.Cells["C4"],
                    "Total Amount ($)", hdrColor);
                StyleHeader(ws.Cells["D4"],
                    "Item", hdrColor);
                StyleHeader(ws.Cells["E4"],
                    "Qty", hdrColor);
                StyleHeader(ws.Cells["F4"],
                    "Item Total ($)", hdrColor);
                ws.Row(4).Height = 22;

                int r = 5;
                double grandTotal = 0;
                int saleCount = 0;

                using (SQLiteConnection con =
                    new SQLiteConnection(
                        DatabaseHelper.ConnectionString))
                {
                    con.Open();

                    SQLiteDataReader salesR =
                        new SQLiteCommand(
                            "SELECT * FROM Sales " +
                            "ORDER BY Id DESC", con)
                        .ExecuteReader();

                    List<(int Id, string Date,
                        double Total)> sales =
                        new List<(int, string, double)>();

                    while (salesR.Read())
                        sales.Add((
                            Convert.ToInt32(salesR["Id"]),
                            salesR["Date"].ToString(),
                            Convert.ToDouble(
                                salesR["TotalAmount"])));
                    salesR.Close();

                    foreach (var sale in sales)
                    {
                        SQLiteCommand itemCmd =
                            new SQLiteCommand(@"
                            SELECT Products.Name,
                                   SaleItems.Quantity,
                                   SaleItems.Price
                            FROM SaleItems
                            JOIN Products ON
                                SaleItems.ProductId =
                                Products.Id
                            WHERE SaleItems.SaleId = @id",
                            con);
                        itemCmd.Parameters
                            .AddWithValue("@id", sale.Id);
                        SQLiteDataReader itemR =
                            itemCmd.ExecuteReader();

                        bool firstItem = true;
                        while (itemR.Read())
                        {
                            int qty =
                                Convert.ToInt32(
                                    itemR["Quantity"]);
                            double price =
                                Convert.ToDouble(
                                    itemR["Price"]);

                            if (firstItem)
                            {
                                ws.Cells[r, 1].Value =
                                    sale.Id;
                                ws.Cells[r, 2].Value =
                                    sale.Date;
                                ws.Cells[r, 3].Value =
                                    sale.Total;
                                ws.Cells[r, 3].Style
                                    .Numberformat.Format =
                                    "#,##0.00";
                                firstItem = false;
                            }

                            ws.Cells[r, 4].Value =
                                itemR["Name"].ToString();
                            ws.Cells[r, 5].Value = qty;
                            ws.Cells[r, 6].Value = price;
                            ws.Cells[r, 6].Style
                                .Numberformat.Format =
                                "#,##0.00";

                            StyleDataRow(
                                ws.Cells[r, 1, r, 6],
                                saleCount % 2 == 0);
                            r++;
                        }
                        itemR.Close();

                        grandTotal += sale.Total;
                        saleCount++;
                    }
                }

                ws.Cells[r, 1].Value = "GRAND TOTAL";
                ws.Cells[r, 2].Value =
                    $"{saleCount} sales";
                ws.Cells[r, 3].Value = grandTotal;
                ws.Cells[r, 3].Style
                    .Numberformat.Format = "#,##0.00";
                StyleTotalRow(
                    ws.Cells[r, 1, r, 6]);
                ws.Row(r).Height = 22;

                ws.Column(1).Width = 10;
                ws.Column(2).Width = 24;
                ws.Column(3).Width = 18;
                ws.Column(4).Width = 22;
                ws.Column(5).Width = 8;
                ws.Column(6).Width = 16;

                pkg.SaveAs(new FileInfo(path));
            }

            OpenFile(path);
        }

        // ══════════════════════════════════════
        // 5 — PRODUCTS / INVENTORY
        // ══════════════════════════════════════
        public static void ExportProducts()
        {
            string path = PickSavePath(
                $"Products_Inventory_" +
                $"{DateTime.Now:dd-MM-yyyy}.xlsx");
            if (path == null) return;

            using (ExcelPackage pkg =
                new ExcelPackage())
            {
                ExcelWorksheet ws =
                    pkg.Workbook.Worksheets.Add(
                        "Inventory");

                ws.Cells["A1:F1"].Merge = true;
                StyleTitle(ws.Cells["A1"],
                    "POS SYSTEM — PRODUCTS & INVENTORY");
                ws.Row(1).Height = 30;

                ws.Cells["A2:F2"].Merge = true;
                StyleSubTitle(ws.Cells["A2"],
                    $"Generated: " +
                    $"{DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                ws.Row(2).Height = 20;

                Color hdrColor =
                    Color.FromArgb(74, 20, 140);
                StyleHeader(ws.Cells["A4"],
                    "ID", hdrColor);
                StyleHeader(ws.Cells["B4"],
                    "Product Name", hdrColor);
                StyleHeader(ws.Cells["C4"],
                    "Category", hdrColor);
                StyleHeader(ws.Cells["D4"],
                    "Code", hdrColor);
                StyleHeader(ws.Cells["E4"],
                    "Price ($)", hdrColor);
                StyleHeader(ws.Cells["F4"],
                    "Stock (Qty)", hdrColor);
                ws.Row(4).Height = 22;

                int r = 5;
                double totalValue = 0;
                int lowStock = 0;

                using (SQLiteConnection con =
                    new SQLiteConnection(
                        DatabaseHelper.ConnectionString))
                {
                    con.Open();
                    SQLiteDataReader reader =
                        new SQLiteCommand(
                            "SELECT * FROM Products " +
                            "ORDER BY Category, Name",
                            con).ExecuteReader();

                    while (reader.Read())
                    {
                        double price =
                            Convert.ToDouble(
                                reader["Price"]);
                        int stock =
                            Convert.ToInt32(
                                reader["Stock"]);

                        ws.Cells[r, 1].Value =
                            Convert.ToInt32(reader["Id"]);
                        ws.Cells[r, 2].Value =
                            reader["Name"].ToString();
                        ws.Cells[r, 3].Value =
                            reader["Category"].ToString();
                        ws.Cells[r, 4].Value =
                            reader["ProductCode"]
                            .ToString();
                        ws.Cells[r, 5].Value = price;
                        ws.Cells[r, 5].Style
                            .Numberformat.Format =
                            "#,##0.00";
                        ws.Cells[r, 6].Value = stock;

                        if (stock <= 5)
                        {
                            ws.Cells[r, 6].Style
                                .Font.Color.SetColor(
                                    Color.White);
                            ws.Cells[r, 6].Style
                                .Fill.PatternType =
                                ExcelFillStyle.Solid;
                            ws.Cells[r, 6].Style
                                .Fill.BackgroundColor
                                .SetColor(
                                    Color.FromArgb(
                                        198, 40, 40));
                            lowStock++;
                        }
                        else
                        {
                            StyleDataRow(
                                ws.Cells[r, 1, r, 6],
                                r % 2 == 0);
                        }

                        totalValue += price * stock;
                        r++;
                    }
                    reader.Close();
                }

                ws.Cells[r, 1].Value =
                    "TOTAL INVENTORY VALUE";
                ws.Cells[r, 5].Value = totalValue;
                ws.Cells[r, 5].Style
                    .Numberformat.Format = "#,##0.00";
                ws.Cells[r, 6].Value =
                    $"{lowStock} low stock";
                StyleTotalRow(
                    ws.Cells[r, 1, r, 6]);
                ws.Row(r).Height = 22;

                ws.Column(1).Width = 8;
                ws.Column(2).Width = 24;
                ws.Column(3).Width = 16;
                ws.Column(4).Width = 18;
                ws.Column(5).Width = 12;
                ws.Column(6).Width = 12;

                pkg.SaveAs(new FileInfo(path));
            }

            OpenFile(path);
        }

        // ══════════════════════════════════════
        // HELPER — item count per sale
        // ══════════════════════════════════════
        static int GetSaleItemCount(int saleId)
        {
            using (SQLiteConnection con =
                new SQLiteConnection(
                    DatabaseHelper.ConnectionString))
            {
                con.Open();
                SQLiteCommand cmd = new SQLiteCommand(
                    "SELECT SUM(Quantity) " +
                    "FROM SaleItems " +
                    "WHERE SaleId = @id", con);
                cmd.Parameters.AddWithValue(
                    "@id", saleId);
                object result = cmd.ExecuteScalar();
                return result == DBNull.Value ||
                       result == null
                    ? 0
                    : Convert.ToInt32(result);
            }
        }
    }
}