using POSSystem.Database;
using System.Windows;

namespace POSSystem
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            PdfExportHelper.Initialize();
            DatabaseHelper.CreateTables();
            DatabaseHelper.SeedData();

            // ★ Auto-backup on every startup
            BackupManager.AutoBackup();

            // ★ First-run wizard — runs once ever
            string firstRunDone = SettingsHelper.Get("FirstRunComplete");
            if (firstRunDone != "true")
            {
                var wizard = new FirstRunWizardWindow();
                wizard.ShowDialog();
                // Mark complete regardless of how wizard was closed
                // (user can adjust settings later in SettingsWindow)
                SettingsHelper.Set("FirstRunComplete", "true");
            }
        }
    }
}