using BudgetLocal.Core.Data;
using BudgetLocal.Core.Models;
using BudgetLocal.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BudgetLocal.Tests;

public sealed class BudgetServiceTests
{
    [Fact]
    public async Task BudgetsHaveIndependentLimitsAndActiveSelectionPersists()
    {
        await using var database = new TestDatabase();
        var service = database.Budgets;
        var initial = await service.GetAllAsync();
        var personal = Assert.Single(initial);
        var vehicle = await service.CreateAsync("Voiture", "Frais du véhicule", 300m);
        await service.SetActiveBudgetAsync(vehicle.Id);

        Assert.Equal(vehicle.Id, await service.GetActiveBudgetIdAsync());
        Assert.Equal(300m, await database.Configuration.GetMonthlyExpenseBudgetAsync(vehicle.Id));
        Assert.Null(await database.Configuration.GetMonthlyExpenseBudgetAsync(personal.Id));

        await database.Configuration.SetMonthlyExpenseBudgetAsync(personal.Id, 900m);
        Assert.Equal(900m, await database.Configuration.GetMonthlyExpenseBudgetAsync(personal.Id));
        Assert.Equal(300m, await database.Configuration.GetMonthlyExpenseBudgetAsync(vehicle.Id));

        await service.SetActiveBudgetAsync(personal.Id);
        await service.DeleteAsync(personal.Id);
        Assert.Equal(vehicle.Id, await service.GetActiveBudgetIdAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(vehicle.Id));
    }

    [Fact]
    public async Task BudgetWithTransactionsCannotBeDeletedAndInputIsValidated()
    {
        await using var database = new TestDatabase();
        var travel = await database.Budgets.CreateAsync("Voyage");
        await database.Transactions.AddAsync(travel.Id, new Transaction
        {
            Description = "Train", Amount = 50m, Date = new(2026, 11, 1),
            Type = TransactionType.Expense, Category = "Transport"
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => database.Budgets.DeleteAsync(travel.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => database.Budgets.CreateAsync("  "));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => database.Budgets.CreateAsync("Invalide", monthlyExpenseBudget: 0));
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), $"budgetlocal-budgets-{Guid.NewGuid():N}.db");
        private readonly DbContextOptions<BudgetDbContext> options;
        public BudgetService Budgets { get; }
        public BudgetConfigurationService Configuration { get; }
        public TransactionService Transactions { get; }

        public TestDatabase()
        {
            options = new DbContextOptionsBuilder<BudgetDbContext>().UseSqlite($"Data Source={path}").Options;
            var factory = new ContextFactory(options);
            Budgets = new BudgetService(factory);
            Configuration = new BudgetConfigurationService(factory);
            Transactions = new TransactionService(factory);
            using var context = new BudgetDbContext(options);
            BudgetDatabaseSchema.EnsureCurrentAsync(context).GetAwaiter().GetResult();
        }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ContextFactory(DbContextOptions<BudgetDbContext> options) : IDbContextFactory<BudgetDbContext>
    {
        public BudgetDbContext CreateDbContext() => new(options);
        public Task<BudgetDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}
