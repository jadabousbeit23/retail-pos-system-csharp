using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace POSSystem
{
    public static class UpdateChecker
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        public static async Task<string> GetLatestVersionAsync()
        {
            try
            {
                // Use .ConfigureAwait(false) to prevent WPF deadlock
                string raw = await _http.GetStringAsync(AppVersion.UpdateCheckUrl)
                                        .ConfigureAwait(false);

                string latest = raw?.Trim();

                // DEBUG
                System.Diagnostics.Debug.WriteLine($"[UPDATE] Raw: '{raw}'");
                System.Diagnostics.Debug.WriteLine($"[UPDATE] Trimmed: '{latest}'");
                System.Diagnostics.Debug.WriteLine($"[UPDATE] Current: '{AppVersion.Current}'");

                if (string.IsNullOrEmpty(latest)) return null;

                if (IsNewer(latest, AppVersion.Current))
                {
                    System.Diagnostics.Debug.WriteLine($"[UPDATE] Newer found: {latest}");
                    return latest;
                }

                System.Diagnostics.Debug.WriteLine("[UPDATE] Not newer or parse failed");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UPDATE ERROR] {ex.Message}");
                return null;
            }
        }

        private static bool IsNewer(string latest, string current)
        {
            latest = latest?.Trim();
            current = current?.Trim();

            // Handle versions like "v1.0.1" → strip 'v'
            if (latest.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                latest = latest.Substring(1);
            if (current.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                current = current.Substring(1);

            if (Version.TryParse(latest, out var v1) &&
                Version.TryParse(current, out var v2))
            {
                System.Diagnostics.Debug.WriteLine($"[UPDATE] v1={v1}, v2={v2}, newer={v1 > v2}");
                return v1 > v2;
            }

            System.Diagnostics.Debug.WriteLine($"[UPDATE] Parse failed: '{latest}' vs '{current}'");
            return false;
        }
    }
}