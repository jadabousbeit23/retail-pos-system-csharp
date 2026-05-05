using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace POSSystem.Windows
{
    public partial class LoyaltyCardWindow : Window
    {
        private readonly Customer _customer;

        public LoyaltyCardWindow(Customer customer)
        {
            InitializeComponent();
            _customer = customer;
            Loaded += (s, e) => LoadCard();
        }

        private void LoadCard()
        {
            lblCustomerName.Content = _customer?.Name?.ToUpper() ?? "CUSTOMER";
            lblPoints.Content = _customer?.Points.ToString("N0") ?? "0";
            lblBarcodeText.Content = _customer?.LoyaltyCode ?? "0000";
            lblPhone.Content = string.IsNullOrWhiteSpace(_customer?.Phone)
                ? ""
                : $"📞 {_customer.Phone}";

            lblStoreName.Content = "MY STORE";

            DrawBarcode(_customer?.LoyaltyCode ?? "0000");
        }

        // ✅ SIMPLE CLEAN BARCODE (NO EXTERNAL LIBRARIES)
        private void DrawBarcode(string code)
        {
            canvasBarcode.Children.Clear();

            double x = 2;
            double height = 46;

            foreach (char c in code)
            {
                int width = (c % 2 == 0) ? 2 : 4;

                Rectangle rect = new Rectangle
                {
                    Width = width,
                    Height = height,
                    Fill = Brushes.Black
                };

                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, 0);
                canvasBarcode.Children.Add(rect);

                x += width + 1;
            }
        }

        // 🖨️ PRINT
        private void btnPrint_Click(object sender, RoutedEventArgs e)
        {
            PrintDialog dlg = new PrintDialog();

            if (dlg.ShowDialog() == true)
            {
                cardBorder.Measure(new Size(460, 290));
                cardBorder.Arrange(new Rect(0, 0, 460, 290));
                dlg.PrintVisual(cardBorder, "Loyalty Card");
            }
        }

        // 🖼️ EXPORT PNG
        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RenderTargetBitmap bmp = new RenderTargetBitmap(460, 290, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(cardBorder);

                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));

                string path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"Card_{_customer?.Name}.png");

                using (FileStream fs = new FileStream(path, FileMode.Create))
                {
                    encoder.Save(fs);
                }

                MessageBox.Show("Saved to Desktop");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
