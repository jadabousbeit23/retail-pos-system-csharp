using POSSystem.Database;
using System.Windows;

namespace POSSystem
{
    public partial class FirstRunWizardWindow : Window
    {
        public FirstRunWizardWindow()
        {
            InitializeComponent();
            lblVersion.Text = AppVersion.DisplayName;

            // Pre-fill if settings already exist (shouldn't on first run, but safe)
            txtStoreName.Text = SettingsHelper.Get("StoreName");
            txtPhone.Text = SettingsHelper.Get("StorePhone");
            txtAddress.Text = SettingsHelper.Get("StoreAddress");

            double rate = SettingsHelper.GetDouble("ExchangeRate");
            txtRate.Text = rate > 0 ? rate.ToString() : "90000";

            double tax = SettingsHelper.GetDouble("TaxRate");
            txtTax.Text = tax > 0 ? tax.ToString() : "0";
        }

        private void BtnFinish_Click(object sender, RoutedEventArgs e)
        {
            // Validate
            if (string.IsNullOrWhiteSpace(txtStoreName.Text))
            {
                lblError.Text = "Please enter your store name.";
                lblError.Visibility = Visibility.Visible;
                return;
            }

            if (!double.TryParse(txtRate.Text.Trim(), out double rate) || rate <= 0)
            {
                lblError.Text = "Exchange rate must be a positive number (e.g. 90000).";
                lblError.Visibility = Visibility.Visible;
                return;
            }

            if (!double.TryParse(txtTax.Text.Trim(), out double tax) || tax < 0)
            {
                lblError.Text = "Tax rate must be 0 or greater.";
                lblError.Visibility = Visibility.Visible;
                return;
            }

            // Save
            SettingsHelper.Set("StoreName", txtStoreName.Text.Trim());
            SettingsHelper.Set("StorePhone", txtPhone.Text.Trim());
            SettingsHelper.Set("StoreAddress", txtAddress.Text.Trim());
            SettingsHelper.Set("ExchangeRate", rate.ToString());
            SettingsHelper.Set("TaxRate", tax.ToString());

            CurrencyHelper.ResetRate();

            DialogResult = true;
            Close();
        }
    }
}