using POSSystem.Database;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;

namespace POSSystem.Windows
{
    // Model for display rows
    public class DisplayCartItem
    {
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public string UnitPriceText { get; set; }
        public string TotalPriceText { get; set; }
    }

    public partial class CustomerDisplayWindow : Window
    {
        DispatcherTimer _clockTimer;

        public CustomerDisplayWindow()
        {
            InitializeComponent();

            // Move to second monitor if available
            MoveToSecondMonitor();

            // Start clock
            _clockTimer = new DispatcherTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (s, e) => UpdateClock();
            _clockTimer.Start();
            UpdateClock();

            // Show idle screen on open
            ShowIdle();
        }

        // ══════════════════════════════════════
        // MOVE TO SECOND MONITOR
        // ══════════════════════════════════════
        void MoveToSecondMonitor()
        {
            var screens =
                System.Windows.Forms.Screen.AllScreens;

            if (screens.Length >= 2)
            {
                // Find the non-primary screen
                System.Windows.Forms.Screen second = null;
                foreach (var s in screens)
                    if (!s.Primary) { second = s; break; }

                if (second != null)
                {
                    this.WindowState = WindowState.Normal;
                    this.Left = second.WorkingArea.Left;
                    this.Top = second.WorkingArea.Top;
                    this.Width = second.WorkingArea.Width;
                    this.Height = second.WorkingArea.Height;
                    this.WindowState = WindowState.Maximized;
                }
            }
        }

        // ══════════════════════════════════════
        // CLOCK
        // ══════════════════════════════════════
        void UpdateClock()
        {
            lblDate.Content =
                DateTime.Now.ToString("dddd, dd MMM yyyy");
            lblTime.Content =
                DateTime.Now.ToString("hh:mm:ss tt");
        }

        // ══════════════════════════════════════
        // IDLE SCREEN
        // ══════════════════════════════════════
        public void ShowIdle()
        {
            pnlIdle.Visibility = Visibility.Visible;
            pnlCart.Visibility = Visibility.Collapsed;
            pnlThankYou.Visibility = Visibility.Collapsed;
        }

        // ══════════════════════════════════════
        // UPDATE CART
        // ══════════════════════════════════════
        public void UpdateCart(List<CartItem> cart,
            double subtotal,
            double discountAmount,
            double total)
        {
            if (cart.Count == 0)
            {
                ShowIdle();
                return;
            }

            pnlIdle.Visibility = Visibility.Collapsed;
            pnlThankYou.Visibility = Visibility.Collapsed;
            pnlCart.Visibility = Visibility.Visible;

            // Build display items
            var displayItems =
                new List<DisplayCartItem>();
            foreach (CartItem c in cart)
                displayItems.Add(new DisplayCartItem
                {
                    ProductName = c.ProductName,
                    Quantity = c.Quantity,
                    UnitPriceText =
                        $"${c.UnitPrice:F2}",
                    TotalPriceText =
                        $"${c.TotalPrice:F2}"
                });

            icCartItems.ItemsSource = displayItems;

            // Totals
            lblSubtotal.Content =
                $"${subtotal:F2}";
            lblTotal.Content =
                $"${total:F2}";

            // Discount
            if (discountAmount > 0)
            {
                pnlDiscount.Visibility =
                    Visibility.Visible;
                lblDiscount.Content =
                    $"-${discountAmount:F2}";
            }
            else
            {
                pnlDiscount.Visibility =
                    Visibility.Collapsed;
            }

            // Item count
            int totalItems = 0;
            foreach (CartItem c in cart)
                totalItems += c.Quantity;
            lblItemCount.Content =
                $"{totalItems} item(s)";
        }

        // ══════════════════════════════════════
        // THANK YOU SCREEN
        // ══════════════════════════════════════
        public void ShowThankYou(double total)
        {
            pnlIdle.Visibility = Visibility.Collapsed;
            pnlCart.Visibility = Visibility.Collapsed;
            pnlThankYou.Visibility = Visibility.Visible;

            lblThankYouTotal.Content = $"${total:F2}";

            // Auto-return to idle after 5 seconds
            DispatcherTimer t = new DispatcherTimer();
            t.Interval = TimeSpan.FromSeconds(5);
            t.Tick += (s, e) =>
            {
                ShowIdle();
                t.Stop();
            };
            t.Start();
        }

        // ══════════════════════════════════════
        // CLEANUP
        // ══════════════════════════════════════
        protected override void OnClosed(
            EventArgs e)
        {
            _clockTimer?.Stop();
            base.OnClosed(e);
        }
    }
}