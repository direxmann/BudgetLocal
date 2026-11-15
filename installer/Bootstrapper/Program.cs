using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            bool testMode = Environment.GetEnvironmentVariable("BUDGETLOCAL_INSTALLER_TEST") == "1";
            if (!testMode && MessageBox.Show("Installer BudgetLocal sur ce PC ?", "Installation de BudgetLocal", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            string installDir = testMode
                ? Environment.GetEnvironmentVariable("BUDGETLOCAL_TEST_INSTALL_DIR")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "BudgetLocal");
            if (String.IsNullOrWhiteSpace(installDir)) throw new InvalidOperationException("Le dossier temporaire de test n’est pas défini.");
            Directory.CreateDirectory(installDir);
            CopyResource("BudgetLocal.Desktop.exe", Path.Combine(installDir, "BudgetLocal.exe"));
            CopyResource("Uninstall.ps1", Path.Combine(installDir, "Uninstall.ps1"));
            string appExe = Path.Combine(installDir, "BudgetLocal.exe");
            if (!testMode)
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Start Menu", "Programs", "BudgetLocal");
                Directory.CreateDirectory(startMenu);
                CreateShortcut(Path.Combine(desktop, "BudgetLocal.lnk"), appExe, installDir);
                CreateShortcut(Path.Combine(startMenu, "BudgetLocal.lnk"), appExe, installDir);
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\BudgetLocal"))
                {
                    key.SetValue("DisplayName", "BudgetLocal"); key.SetValue("DisplayVersion", "1.0.0"); key.SetValue("Publisher", "Delguste François");
                    key.SetValue("InstallLocation", installDir); key.SetValue("DisplayIcon", appExe);
                    key.SetValue("UninstallString", "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"" + installDir + "\\Uninstall.ps1\"");
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord); key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
                if (MessageBox.Show("BudgetLocal est installé. Un raccourci a été ajouté au Bureau.\n\nVoulez-vous lancer l’application maintenant?", "Installation terminée", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    Process.Start(new ProcessStartInfo(appExe) { WorkingDirectory = installDir });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("L’installation de BudgetLocal n’a pas pu se terminer.\n\n" + ex.Message, "Installation de BudgetLocal", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void CopyResource(string name, string destination)
    {
        using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        {
            if (source == null) throw new InvalidOperationException("Fichier requis absent de l’installateur : " + name);
            using (FileStream target = File.Create(destination)) source.CopyTo(target);
        }
    }

    private static void CreateShortcut(string path, string appExe, string installDir)
    {
        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null) throw new InvalidOperationException("Création des raccourcis indisponible.");
        dynamic shell = Activator.CreateInstance(shellType);
        dynamic shortcut = shell.CreateShortcut(path);
        shortcut.TargetPath = appExe; shortcut.WorkingDirectory = installDir; shortcut.Description = "Gestion de budget personnel"; shortcut.IconLocation = appExe + ",0"; shortcut.Save();
    }
}
