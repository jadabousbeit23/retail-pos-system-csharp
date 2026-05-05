namespace POSSystem.Database
{
    public static class HashHelper
    {
        // WorkFactor 12 = ~300ms per hash — slow enough
        // to make brute force impractical
        const int WorkFactor = 12;

        public static string Hash(string input)
        {
            return BCrypt.Net.BCrypt.HashPassword(
                input, WorkFactor);
        }

        public static bool Verify(string input, string hash)
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(input, hash);
            }
            catch { return false; }
        }

        // BCrypt hashes always start with $2a$ or $2b$
        public static bool IsHashed(string value)
        {
            return value != null &&
                   (value.StartsWith("$2a$") ||
                    value.StartsWith("$2b$"));
        }
    }
}