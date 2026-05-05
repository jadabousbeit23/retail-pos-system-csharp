using POSSystem.Database;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace POSSystem.Windows
{
    public partial class LoginWindow : Window
    {
        string _code = "";

        Dictionary<string, Button> _digitButtons;

        public LoginWindow()
        {
            InitializeComponent();

            // ★ Show version on login screen
            lblVersion.Content = $"POS System {AppVersion.Current} — Code Login";

            _digitButtons = new Dictionary<string, Button>
            {
                { "0", btn0 }, { "1", btn1 }, { "2", btn2 },
                { "3", btn3 }, { "4", btn4 }, { "5", btn5 },
                { "6", btn6 }, { "7", btn7 }, { "8", btn8 },
                { "9", btn9 },
            };

            this.PreviewKeyDown += LoginWindow_PreviewKeyDown;
        }

        // ── Visual press flash: scale + opacity, mirrors mouse IsPressed ──
        async void FlashButton(Button btn)
        {
            if (btn == null) return;

            var tf = new ScaleTransform(1, 1);
            btn.RenderTransform = tf;
            btn.RenderTransformOrigin = new Point(0.5, 0.5);

            var down = new DoubleAnimation(0.93, TimeSpan.FromMilliseconds(55));
            tf.BeginAnimation(ScaleTransform.ScaleXProperty, down);
            tf.BeginAnimation(ScaleTransform.ScaleYProperty, down);
            btn.Opacity = 0.72;

            await Task.Delay(85);

            var up = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(75));
            tf.BeginAnimation(ScaleTransform.ScaleXProperty, up);
            tf.BeginAnimation(ScaleTransform.ScaleYProperty, up);
            btn.Opacity = 1.0;
        }

        // ── Dot display ──
        void UpdateDots()
        {
            string display = "";
            for (int i = 0; i < 6; i++)
            {
                display += i < _code.Length ? "●" : "○";
                if (i < 5) display += "  ";
            }
            lblCodeDisplay.Content = display;
        }

        // ── Mouse click handlers ──
        private void btnNum_Click(object sender, RoutedEventArgs e)
        {
            if (_code.Length >= 6) return;
            _code += ((Button)sender).Content.ToString();
            UpdateDots();
            lblError.Content = "";
            if (_code.Length >= 4) TryLogin();
        }

        private void btnBackspace_Click(object sender, RoutedEventArgs e)
        {
            if (_code.Length > 0)
                _code = _code.Substring(0, _code.Length - 1);
            UpdateDots();
            lblError.Content = "";
        }

        private void btnEnter_Click(object sender, RoutedEventArgs e)
        {
            TryLogin();
        }

        // ── Keyboard handler — flash matching button ──
        private void LoginWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key >= Key.D0 && e.Key <= Key.D9)
            {
                string digit = (e.Key - Key.D0).ToString();
                if (_digitButtons.TryGetValue(digit, out Button b)) FlashButton(b);
                if (_code.Length < 6)
                {
                    _code += digit;
                    UpdateDots();
                    lblError.Content = "";
                    if (_code.Length >= 4) TryLogin();
                }
                e.Handled = true;
            }
            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
            {
                string digit = (e.Key - Key.NumPad0).ToString();
                if (_digitButtons.TryGetValue(digit, out Button b)) FlashButton(b);
                if (_code.Length < 6)
                {
                    _code += digit;
                    UpdateDots();
                    lblError.Content = "";
                    if (_code.Length >= 4) TryLogin();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Back || e.Key == Key.Delete)
            {
                FlashButton(btnBack);
                if (_code.Length > 0)
                    _code = _code.Substring(0, _code.Length - 1);
                UpdateDots();
                lblError.Content = "";
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                FlashButton(btnEnterKey);
                TryLogin();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                _code = "";
                UpdateDots();
                lblError.Content = "";
                e.Handled = true;
            }
        }

        // ── Login logic ──
        void TryLogin()
        {
            if (_code.Length < 4)
            {
                lblError.Content = "⚠️ Please enter your code (min 4 digits).";
                return;
            }

            using (SQLiteConnection con = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                con.Open();
                SQLiteCommand cmd = new SQLiteCommand("SELECT Username, Role, Code FROM Users", con);
                SQLiteDataReader r = cmd.ExecuteReader();

                string foundUser = null, foundRole = null;

                while (r.Read())
                {
                    if (HashHelper.Verify(_code, r["Code"].ToString()))
                    {
                        foundUser = r["Username"].ToString();
                        foundRole = r["Role"].ToString();
                        break;
                    }
                }
                r.Close();

                if (foundUser != null)
                {
                    Session.Username = foundUser;
                    Session.Role = foundRole;
                    DatabaseHelper.Log("Login", $"User '{Session.Username}' logged in", "Login");
                    new MainWindow().Show();
                    this.Close();
                }
                else
                {
                    _code = "";
                    UpdateDots();
                    lblError.Content = "❌ Wrong code! Try again.";
                }
            }
        }
    }
}