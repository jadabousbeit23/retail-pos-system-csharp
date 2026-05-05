namespace POSSystem.Database
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Price { get; set; }
        public int Stock { get; set; }
        public string Category { get; set; }
        public string ProductCode { get; set; }

        // Optional: Category color for UI display
        public string CategoryColorCode { get; set; }
    }
}