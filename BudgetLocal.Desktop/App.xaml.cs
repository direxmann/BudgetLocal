using System.Globalization;
using System.Windows;
using BudgetLocal.Core.Data;
using BudgetLocal.Core.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.EntityFrameworkCore;

namespace BudgetLocal.Desktop;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("fr-BE");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("fr-BE");
        LiveCharts.Configure(settings => settings.AddSkiaSharp().AddDefaultMappers().AddDefaultTheme());
        try
        {
            var factory = new Factory(new DbContextOptionsBuilder<BudgetDbContext>()
                .UseSqlite($"Data Source={BudgetPaths.DatabasePath}").Options);
            var transfer = new DataTransferService(factory);
                await transfer.EnsureDatabaseAsync();
            MainWindow = new MainWindow(new TransactionService(factory), new BudgetConfigurationService(factory), new BudgetService(factory), transfer);
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"BudgetLocal ne peut pas démarrer.\n\n{ex.Message}\n\nVos données n'ont pas été modifiées.",
                "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private sealed class Factory(DbContextOptions<BudgetDbContext> options) : IDbContextFactory<BudgetDbContext>
    {
        public BudgetDbContext CreateDbContext() => new(options);
        public Task<BudgetDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}

