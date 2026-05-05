using System;
using System.IO;

namespace POSSystem.Database
{
    public static class BackupManager
    {
        // ── Folder where backups are stored ──
        public static string BackupFolder =>
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..\\..\\Backups");

        // ── Source database path ──
        static string DbPath =>
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..\\..\\pos.db");

        // ══════════════════════════════════════
        // AUTO BACKUP ON STARTUP
        // ══════════════════════════════════════
        public static void AutoBackup()
        {
            try
            {
                if (!File.Exists(DbPath)) return;

                Directory.CreateDirectory(BackupFolder);

                string fileName =
                    $"POS_Backup_" +
                    $"{DateTime.Now:dd-MM-yyyy_HH-mm-ss}.db";
                string destPath =
                    Path.Combine(BackupFolder, fileName);

                File.Copy(DbPath, destPath, overwrite: false);

                // Keep only last 30 auto-backups
                PruneOldBackups(30);

                System.Diagnostics.Debug.WriteLine(
                    $"[Backup] Auto-backup created: {fileName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Backup] Auto-backup failed: {ex.Message}");
            }
        }

        // ══════════════════════════════════════
        // MANUAL BACKUP
        // ══════════════════════════════════════
        public static (bool Success, string Message,
            string FilePath) CreateBackup(
            string destinationFolder)
        {
            try
            {
                if (!File.Exists(DbPath))
                    return (false,
                        "❌ Database file not found!", "");

                Directory.CreateDirectory(destinationFolder);

                string fileName =
                    $"POS_Backup_" +
                    $"{DateTime.Now:dd-MM-yyyy_HH-mm-ss}.db";
                string destPath =
                    Path.Combine(destinationFolder, fileName);

                File.Copy(DbPath, destPath, overwrite: false);

                return (true,
                    $"✅ Backup saved:\n{fileName}",
                    destPath);
            }
            catch (Exception ex)
            {
                return (false,
                    $"❌ Backup failed: {ex.Message}", "");
            }
        }

        // ══════════════════════════════════════
        // RESTORE
        // ══════════════════════════════════════
        public static (bool Success, string Message)
            RestoreBackup(string backupFilePath)
        {
            try
            {
                if (!File.Exists(backupFilePath))
                    return (false,
                        "❌ Backup file not found!");

                // Create a safety backup before restoring
                if (File.Exists(DbPath))
                {
                    string safetyName =
                        $"POS_PreRestore_" +
                        $"{DateTime.Now:dd-MM-yyyy_HH-mm-ss}.db";
                    string safetyPath = Path.Combine(
                        BackupFolder, safetyName);
                    Directory.CreateDirectory(BackupFolder);
                    File.Copy(DbPath, safetyPath,
                        overwrite: false);
                }

                File.Copy(backupFilePath, DbPath,
                    overwrite: true);

                return (true,
                    "✅ Database restored successfully!\n" +
                    "The application will now restart.");
            }
            catch (Exception ex)
            {
                return (false,
                    $"❌ Restore failed: {ex.Message}");
            }
        }

        // ══════════════════════════════════════
        // GET ALL BACKUPS
        // ══════════════════════════════════════
        public static System.Collections.Generic
            .List<BackupFile> GetAllBackups()
        {
            var list = new System.Collections.Generic
                .List<BackupFile>();

            if (!Directory.Exists(BackupFolder))
                return list;

            string[] files = Directory.GetFiles(
                BackupFolder, "*.db");

            foreach (string f in files)
            {
                FileInfo fi = new FileInfo(f);
                list.Add(new BackupFile
                {
                    FileName = fi.Name,
                    FilePath = fi.FullName,
                    CreatedAt = fi.CreationTime,
                    DisplayDate = fi.CreationTime
                        .ToString("dd/MM/yyyy  HH:mm:ss"),
                    SizeText = FormatSize(fi.Length),
                    IsAutoBackup = fi.Name.StartsWith(
                        "POS_Backup_"),
                    IsPreRestore = fi.Name.StartsWith(
                        "POS_PreRestore_")
                });
            }

            // Sort newest first
            list.Sort((a, b) =>
                b.CreatedAt.CompareTo(a.CreatedAt));

            return list;
        }

        // ══════════════════════════════════════
        // DELETE BACKUP
        // ══════════════════════════════════════
        public static (bool Success, string Message)
            DeleteBackup(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return (false, "❌ File not found!");

                File.Delete(filePath);
                return (true, "✅ Backup deleted.");
            }
            catch (Exception ex)
            {
                return (false,
                    $"❌ Delete failed: {ex.Message}");
            }
        }

        // ══════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════
        static void PruneOldBackups(int keepCount)
        {
            try
            {
                var backups = GetAllBackups();
                // Only prune auto-backups
                var autoBackups = backups.FindAll(
                    b => b.IsAutoBackup);

                for (int i = keepCount;
                     i < autoBackups.Count; i++)
                    File.Delete(autoBackups[i].FilePath);
            }
            catch { }
        }

        static string FormatSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024):F1} MB";
        }
    }

    // ══════════════════════════════════════
    // MODEL
    // ══════════════════════════════════════
    public class BackupFile
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public DateTime CreatedAt { get; set; }
        public string DisplayDate { get; set; }
        public string SizeText { get; set; }
        public bool IsAutoBackup { get; set; }
        public bool IsPreRestore { get; set; }

        public string TypeText =>
            IsPreRestore ? "🛡️ Pre-Restore" :
            IsAutoBackup ? "🔄 Auto" :
                           "💾 Manual";

        public string TypeColor =>
            IsPreRestore ? "#E65100" :
            IsAutoBackup ? "#1565C0" :
                           "#2E7D32";
    }
}