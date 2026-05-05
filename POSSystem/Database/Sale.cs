namespace POSSystem.Database
{
    public class Sale
    {
        // ── Core ──────────────────────────────────────
        public int Id { get; set; }
        public string Date { get; set; }
        public double TotalAmount { get; set; }

        // ── Payment ───────────────────────────────────
        public string PaymentMethod { get; set; }
        public double CashPaid { get; set; }
        public double CardPaid { get; set; }
        public double ChangeDue { get; set; }

        // ── Staff / Customer ──────────────────────────
        public string CashierName { get; set; }
        public string CustomerName { get; set; }
        public int CustomerId { get; set; }

        // ── Loyalty ───────────────────────────────────
        public int PointsEarned { get; set; }
        public int PointsRedeemed { get; set; }
        public int StampsEarned { get; set; }
    }
}
