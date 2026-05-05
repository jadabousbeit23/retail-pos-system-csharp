namespace POSSystem.Database
{
    public class CartItem
    {
        public int    ProductId   { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public string Category    { get; set; }   // needed for promotions
        public int    Quantity    { get; set; }
        public double UnitPrice   { get; set; }
        public double TotalPrice  { get; set; }
        public double DiscountPct { get; set; }
        public int    RowNumber   { get; set; }

        // LBP display
        public string UnitPriceDisplay  => CurrencyHelper.FormatLBP(UnitPrice);
        public string TotalPriceDisplay => CurrencyHelper.FormatLBP(TotalPrice);
        public string DiscountDisplay   => DiscountPct > 0 ? $"{DiscountPct:F1}%" : "—";

        // USD display (second line in cart)
        public string UnitPriceUSD  => CurrencyHelper.FormatUSD(UnitPrice);
        public string TotalPriceUSD => CurrencyHelper.FormatUSD(TotalPrice);
    }
}
