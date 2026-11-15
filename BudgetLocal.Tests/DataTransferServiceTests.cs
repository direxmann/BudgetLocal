using BudgetLocal.Core.Data;
using BudgetLocal.Core.Models;
using BudgetLocal.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BudgetLocal.Tests;

public sealed class DataTransferServiceTests
{
    [Fact]
    public async Task ImportAndExportStayWithinTheSelectedBudgetAndImportCreatesBackup()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"budgetlocal-transfer-{Guid.NewGuid():N}.db");
        var exportPath = Path.Combine(Path.GetTempPath(), $"budgetlocal-transfer-{Guid.NewGuid():N}.json");
        try
        {
            var options = new DbContextOptionsBuilder<BudgetDbContext>().UseSqlite($"Data Source={databasePath}").Options;
            var factory = new ContextFactory(options);
            await using (var context = new BudgetDbContext(options))
            {
                await BudgetDatabaseSchema.EnsureCurrentAsync(context);
                context.Budgets.Add(new Budget { Id = 2, Name = "Vacances" });
                await context.SaveChangesAsync();
            }
            var transactions = new TransactionService(factory);
            await transactions.AddAsync(1, New("Personnel", 25));
            await transactions.AddAsync(2, New("Vacances", 90));
            var backupCreated = false;
            var transfer = new DataTransferService(factory, () => { backupCreated = true; return "temp-backup"; });

            await transfer.ExportAsync(1, exportPath);
            await transactions.AddAsync(1, New("À remplacer", 10));
            await transfer.ImportAsync(1, exportPath);

            Assert.True(backupCreated);
            Assert.Equal(["Personnel"], (await transactions.GetAllAsync(1)).Select(x => x.Description));
            Assert.Equal(["Vacances"], (await transactions.GetAllAsync(2)).Select(x => x.Description));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
            if (File.Exists(exportPath)) File.Delete(exportPath);
        }
    }

    private static Transaction New(string description, decimal amount) => new()
    {
        Description = description, Amount = amount, Date = new(2026, 11, 15),
        Type = TransactionType.Expense, Category = "Loisirs"
    };

    private sealed class ContextFactory(DbContextOptions<BudgetDbContext> options) : IDbContextFactory<BudgetDbContext>
    {
        public BudgetDbContext CreateDbContext() => new(options);
        public Task<BudgetDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}
