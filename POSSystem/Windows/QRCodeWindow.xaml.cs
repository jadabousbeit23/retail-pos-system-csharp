using POSSystem.Database;
using QRCoder;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace POSSystem.Windows
{
    public partial class QRCodeWindow : Window
    {
        Product _product;
        BitmapImage _qrImage;

        public QRCodeWindow(Product product)
        {
            InitializeComponent();
            _product = product;
            lblProductName.Content = product.Name;
            lblProductCode.Content = $"Code: {product.ProductCode}";
            lblProductPrice.Content = $"${product.Price:F2}";
            GenerateQR(product.ProductCode);
        }

        void GenerateQR(string code)
        {
            QRCodeGenerator gen = new QRCodeGenerator();
            QRCodeData data = gen.CreateQrCode(code,
                QRCodeGenerator.ECCLevel.Q);
            PngByteQRCode qr = new PngByteQRCode(data);
            byte[] png = qr.GetGraphic(10);

            using (MemoryStream ms = new MemoryStream(png))
            {
                _qrImage = new BitmapImage();
                _qrImage.BeginInit();
                _qrImage.StreamSource = ms;
                _qrImage.CacheOption = BitmapCacheOption.OnLoad;
                _qrImage.EndInit();
                _qrImage.Freeze();
            }
            imgQR.Source = _qrImage;
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dlg =
                new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = $"QR_{_product.ProductCode}";
            dlg.DefaultExt = ".png";
            dlg.Filter = "PNG Image (*.png)|*.png";

            if (dlg.ShowDialog() == true)
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(_qrImage));
                using (FileStream fs = new FileStream(
                    dlg.FileName, FileMode.Create))
                    encoder.Save(fs);

                MessageBox.Show("✅ QR Code saved!");
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}