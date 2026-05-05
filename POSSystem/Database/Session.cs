namespace POSSystem.Database
{
    public static class Session
    {
        public static string Username { get; set; }
        public static string Role { get; set; }

        public static bool IsAdmin => Role == "admin";
    }
}