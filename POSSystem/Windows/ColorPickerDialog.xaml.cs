using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace POSSystem.Windows
{
    public partial class ColorPickerDialog : Window
    {
        private bool _isDragging = false;
        private WriteableBitmap _colorWheelBitmap;

        public string SelectedColor { get; private set; }

        public ColorPickerDialog(string initialColor)
        {
            InitializeComponent();
            SelectedColor = initialColor;
            txtSelectedColor.Text = initialColor;

            // Parse initial color to set sliders
            try
            {
                Color c = HexToColor(initialColor);
                double h, s, b;
                ColorToHSB(c, out h, out s, out b);

                sliderHue.Value = h;
                sliderSaturation.Value = s * 100;
                sliderBrightness.Value = b * 100;
            }
            catch { }

            // Draw color wheel
            DrawColorWheel();
            UpdatePreview();
        }

        private void DrawColorWheel()
        {
            int width = 280;
            int height = 280;
            _colorWheelBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);

            byte[] pixels = new byte[width * height * 4];
            int centerX = width / 2;
            int centerY = height / 2;
            int radius = Math.Min(centerX, centerY) - 5;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    double distance = Math.Sqrt(dx * dx + dy * dy);

                    int pixelIndex = (y * width + x) * 4;

                    if (distance <= radius)
                    {
                        double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
                        if (angle < 0) angle += 360;

                        // Hue from angle, Saturation from distance
                        double hue = angle;
                        double saturation = distance / radius;

                        Color color = HSBToColor(hue, saturation, 1.0);

                        pixels[pixelIndex] = color.B;
                        pixels[pixelIndex + 1] = color.G;
                        pixels[pixelIndex + 2] = color.R;
                        pixels[pixelIndex + 3] = 255;
                    }
                    else
                    {
                        pixels[pixelIndex] = 240;
                        pixels[pixelIndex + 1] = 240;
                        pixels[pixelIndex + 2] = 240;
                        pixels[pixelIndex + 3] = 255;
                    }
                }
            }

            _colorWheelBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);

            // Apply as brush
            colorWheel.Fill = new ImageBrush(_colorWheelBitmap);
        }

        private void colorCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            UpdateColorFromPosition(e.GetPosition(colorCanvas));
            colorCanvas.CaptureMouse();
        }

        private void colorCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                UpdateColorFromPosition(e.GetPosition(colorCanvas));
            }
        }

        private void colorCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            colorCanvas.ReleaseMouseCapture();
        }

        private void UpdateColorFromPosition(Point position)
        {
            double centerX = 175; // 35 + 140
            double centerY = 150; // 10 + 140
            double dx = position.X - centerX;
            double dy = position.Y - centerY;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double radius = 140;

            // Clamp to wheel
            if (distance > radius)
            {
                double scale = radius / distance;
                dx *= scale;
                dy *= scale;
                distance = radius;
            }

            // Update selection indicator
            Canvas.SetLeft(selectionIndicator, centerX + dx - 6);
            Canvas.SetTop(selectionIndicator, centerY + dy - 6);

            // Calculate color
            double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
            if (angle < 0) angle += 360;

            double hue = angle;
            double saturation = distance / radius;
            double brightness = sliderBrightness.Value / 100.0;

            // Update sliders without triggering events
            sliderHue.ValueChanged -= Slider_ValueChanged;
            sliderSaturation.ValueChanged -= Slider_ValueChanged;

            sliderHue.Value = hue;
            sliderSaturation.Value = saturation * 100;

            sliderHue.ValueChanged += Slider_ValueChanged;
            sliderSaturation.ValueChanged += Slider_ValueChanged;

            // Update color
            Color color = HSBToColor(hue, saturation, brightness);
            SelectedColor = ColorToHex(color);
            txtSelectedColor.Text = SelectedColor;
            UpdatePreview();
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double h = sliderHue.Value;
            double s = sliderSaturation.Value / 100.0;
            double b = sliderBrightness.Value / 100.0;

            Color color = HSBToColor(h, s, b);
            SelectedColor = ColorToHex(color);
            txtSelectedColor.Text = SelectedColor;
            UpdatePreview();

            // Update selection indicator position based on H and S
            double centerX = 175;
            double centerY = 150;
            double radius = 140;

            double angle = h * Math.PI / 180;
            double distance = s * radius;

            double dx = Math.Cos(angle) * distance;
            double dy = Math.Sin(angle) * distance;

            Canvas.SetLeft(selectionIndicator, centerX + dx - 6);
            Canvas.SetTop(selectionIndicator, centerY + dy - 6);
        }

        private void UpdatePreview()
        {
            try
            {
                previewBorder.Background = new SolidColorBrush(HexToColor(SelectedColor));
            }
            catch { }
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            SelectedColor = txtSelectedColor.Text;
            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // Color conversion helpers
        private Color HexToColor(string hex)
        {
            hex = hex.Replace("#", "");
            if (hex.Length == 6)
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                return Color.FromRgb(r, g, b);
            }
            return Colors.Gray;
        }

        private string ColorToHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private void ColorToHSB(Color color, out double hue, out double saturation, out double brightness)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            brightness = max;

            if (max == 0)
                saturation = 0;
            else
                saturation = delta / max;

            if (delta == 0)
                hue = 0;
            else
            {
                if (max == r)
                    hue = (g - b) / delta + (g < b ? 6 : 0);
                else if (max == g)
                    hue = (b - r) / delta + 2;
                else
                    hue = (r - g) / delta + 4;
                hue *= 60;
            }
        }

        private Color HSBToColor(double hue, double saturation, double brightness)
        {
            double c = brightness * saturation;
            double x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
            double m = brightness - c;

            double r, g, b;

            if (hue < 60) { r = c; g = x; b = 0; }
            else if (hue < 120) { r = x; g = c; b = 0; }
            else if (hue < 180) { r = 0; g = c; b = x; }
            else if (hue < 240) { r = 0; g = x; b = c; }
            else if (hue < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return Color.FromRgb(
                (byte)((r + m) * 255),
                (byte)((g + m) * 255),
                (byte)((b + m) * 255));
        }
    }
}