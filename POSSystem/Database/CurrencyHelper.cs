using POSSystem.Database;
using System;

namespace POSSystem.Database
{
    /// <summary>
    /// Central dual-currency helper.
    /// All prices in the DB are stored in LBP.
    /// This class converts and formats for display.
    /// </summary>
    public static class CurrencyHelper
    {
        // ── Cached rate — refreshed on every window open ──────
        private static double _rate = 0;

        /// <summary>Current LBP per 1 USD (e.g. 90000)</summary>
        public static double Rate
        {
            get
            {
                if (_rate <= 0) _rate = GetRateFromSettings();
                return _rate;
            }
        }

        /// <summary>Call this after saving a new rate in Settings.</summary>
        public static void ResetRate() => _rate = 0;

        static double GetRateFromSettings()
        {
            try
            {
                double r = SettingsHelper.GetDouble("ExchangeRate");
                return r > 0 ? r : 90000;
            }
            catch { return 90000; }
        }

        // ── Conversion ─────────────────────────────────────────
        public static double ToUSD(double lbp)
            => Rate > 0 ? lbp / Rate : 0;

        public static double ToLBP(double usd)
            => usd * Rate;

        // ── Single-currency formatters ─────────────────────────
        public static string FormatLBP(double lbp)
            => $"LBP {lbp:N0}";

        public static string FormatUSD(double lbp)
            => $"${ToUSD(lbp):N2}";

        // ── Dual display (used in cart, totals, receipt) ───────
        /// <summary>e.g.  LBP 90,000  /  $1.00</summary>
        public static string FormatBoth(double lbp)
            => $"LBP {lbp:N0}  /  ${ToUSD(lbp):N2}";

        /// <summary>Short dual for tight spaces: LBP 90,000 ($1.00)</summary>
        public static string FormatBothShort(double lbp)
            => $"LBP {lbp:N0}  (${ToUSD(lbp):N2})";

        /// <summary>Just the USD part with $</summary>
        public static string FormatUSDOnly(double lbp)
            => $"${ToUSD(lbp):N2}";

        // ── Parse USD string → LBP ──────────────────────────────
        public static bool TryParseUSD(string input, out double lbp)
        {
            lbp = 0;
            string clean = input.Replace("$", "").Replace(",", "").Trim();
            if (double.TryParse(clean, out double usd))
            { lbp = ToLBP(usd); return true; }
            return false;
        }
    }
}
