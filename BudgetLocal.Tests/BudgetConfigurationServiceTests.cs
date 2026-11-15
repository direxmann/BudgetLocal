using BudgetLocal.Core.Data;
using BudgetLocal.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BudgetLocal.Tests;

public sealed class BudgetConfigurationServiceTests
{
    [Fact]
    public void BudgetOverviewUsesAllTimeBalanceAndMonthlyExpenseBudgetWithoutInventingAValue()
    {
        var configured = BudgetOverview.Calculate(new TransactionSummary(900m, 391.50m), 143.11m, 200m);
        Assert.Equal(508.50m, configured.TotalBalance);
        Assert.Equal(56.89m, configured.MonthlyBudgetRemaining);

        var exceeded = BudgetOverview.Calculate(new TransactionSummary(900m, 391.50m), 243.11m, 200m);
        Assert.Equal(-43.11m, exceeded.MonthlyBudgetRemaining);

        var unset = BudgetOverview.Calculate(new TransactionSummary(0m, 0m), 0m, null);
        Assert.Null(unset.MonthlyBudgetRemaining);
    }

    [Fact]
    public async Task SchemaUpgradeCreatesInitialBudgetAndAssignsExistingTransactions()
    {
        var path = Path.Combine(Path.GetTempPath(), $"budgetlocal-legacy-{Guid.NewGuid():N}.db");
        try
        {
            await using (var connection = new SqliteConnection($"Data Source={path}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE Transactions (Id INTEGER PRIMARY KEY, Description TEXT NOT NULL, Amount TEXT NOT NULL, Date TEXT NOT NULL, Type INTEGER NOT NULL, Category TEXT NOT NULL); CREATE TABLE BudgetConfigurations (Id INTEGER PRIMARY KEY CHECK (Id = 1), MonthlyExpenseBudget TEXT NOT NULL); INSERT INTO BudgetConfigurations VALUES (1, '750.00'); INSERT INTO Transactions VALUES (1, 'Ancienne opération', '12.50', '2026-11-01', 1, 'Loisirs');";
                await command.ExecuteNonQueryAsync();
            }

            var options = new DbContextOptionsBuilder<BudgetDbContext>().UseSqlite($"Data Source={path}").Options;
            await using (var context = new BudgetDbContext(options))
            {
                await BudgetDatabaseSchema.EnsureCurrentAsync(context);
                await BudgetDatabaseSchema.EnsureCurrentAsync(context);
            }

            await using var verification = new SqliteConnection($"Data Source={path}");
            await verification.OpenAsync();
            await using var check = verification.CreateCommand();
            check.CommandText = "SELECT (SELECT COUNT(*) FROM Transactions), (SELECT COUNT(*) FROM BudgetConfigurations), (SELECT Description FROM Transactions WHERE Id = 1), (SELECT BudgetId FROM Transactions WHERE Id = 1), (SELECT Name FROM Budgets WHERE Id = 1), (SELECT MonthlyExpenseBudget FROM Budgets WHERE Id = 1), (SELECT COUNT(*) FROM pragma_foreign_key_list('Transactions') WHERE \"table\" = 'Budgets' AND \"from\" = 'BudgetId'), (SELECT COUNT(*) FROM pragma_foreign_key_check), (SELECT COUNT(*) FROM Budgets), (SELECT Value FROM ApplicationSettings WHERE Name = 'ActiveBudgetId');";
            await using var reader = await check.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(1, reader.GetInt32(0));
            Assert.Equal(1, reader.GetInt32(1));
            Assert.Equal("Ancienne opération", reader.GetString(2));
            Assert.Equal(1, reader.GetInt32(3));
            Assert.Equal("Budget personnel", reader.GetString(4));
            Assert.Equal("750.00", reader.GetString(5));
            Assert.Equal(1, reader.GetInt32(6));
            Assert.Equal(0, reader.GetInt32(7));
            Assert.Equal(1, reader.GetInt32(8));
            Assert.Equal("1", reader.GetString(9));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task BudgetIsUnsetUntilConfiguredThenPersistsAndCanBeCleared()
    {
        var path = Path.Combine(Path.GetTempPath(), $"budgetlocal-config-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<BudgetDbContext>().UseSqlite($"Data Source={path}").Options;
            var factory = new ContextFactory(options);
            await using (var context = await factory.CreateDbContextAsync())
            {
                await context.Database.EnsureCreatedAsync();
                context.Budgets.Add(new BudgetLocal.Core.Models.Budget { Id = 1, Name = "Personnel" });
                await context.SaveChangesAsync();
            }
            var settings = new BudgetConfigurationService(factory);

            Assert.Null(await settings.GetMonthlyExpenseBudgetAsync(1));
            await settings.SetMonthlyExpenseBudgetAsync(1, 750m);
            Assert.Equal(750m, await new BudgetConfigurationService(factory).GetMonthlyExpenseBudgetAsync(1));
            await settings.SetMonthlyExpenseBudgetAsync(1, null);
            Assert.Null(await settings.GetMonthlyExpenseBudgetAsync(1));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => settings.SetMonthlyExpenseBudgetAsync(1, 0m));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private sealed class ContextFactory(DbContextOptions<BudgetDbContext> options) : IDbContextFactory<BudgetDbContext>
    {
        public BudgetDbContext CreateDbContext() => new(options);
        public Task<BudgetDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}

