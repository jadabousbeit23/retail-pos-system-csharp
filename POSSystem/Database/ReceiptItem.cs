namespace POSSystem.Database
{
    public class ReceiptItem
    {
        public string Name     { get; set; }
        public int    Quantity { get; set; }
        public double Price    { get; set; }   // line total (qty × unit)

        // Used by ReceiptWindow ListView binding
        public string PriceDisplay =>
            $"LBP {Price:N0}";
    }
}
