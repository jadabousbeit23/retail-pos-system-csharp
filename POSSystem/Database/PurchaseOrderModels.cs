using System;

namespace POSSystem.Database
{
    public class Supplier
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string Display =>
            $"{Name}  {Phone}";
    }

    public class PurchaseOrder
    {
        public string OrderId { get; set; }
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; }
        public string OrderDate { get; set; }
        public string ReceivedDate { get; set; }
        public string Status { get; set; }
        public string Notes { get; set; }
        public int ItemCount { get; set; }
        public decimal TotalAmount { get; set; }

        public string StatusColor =>
            Status == "Received" ? "#2E7D32" :
            Status == "Cancelled" ? "#B71C1C" :
                                    "#E65100";
    }

    public class PurchaseOrderItem
    {
        public int Id { get; set; }
        public int PurchaseOrderId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public double UnitCost { get; set; }

        // Computed
        public double TotalCost =>
            UnitCost * Quantity;

        // ── These are what the ListView columns display ──
        public string UnitCostText =>
            "LBP " + UnitCost.ToString("N0");

        public string TotalCostText =>
            "LBP " + TotalCost.ToString("N0");
    }
}