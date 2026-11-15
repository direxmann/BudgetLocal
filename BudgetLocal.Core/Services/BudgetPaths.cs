namespace BudgetLocal.Core.Services;
public static class BudgetPaths
{
    public static string DataDirectory
    {
        get
        {
            var overrideDirectory = Environment.GetEnvironmentVariable("BUDGETLOCAL_DATA_DIRECTORY");
            return string.IsNullOrWhiteSpace(overrideDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BudgetLocal")
                : Path.GetFullPath(overrideDirectory);
        }
    }

    public static string DatabasePath => Path.Combine(DataDirectory, "budgetlocal.db");
    public static string BackupDirectory => Path.Combine(DataDirectory, "Backups");
}
