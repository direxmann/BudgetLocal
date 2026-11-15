using BudgetLocal.Core.Data;
using BudgetLocal.Core.Models;
using BudgetLocal.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace BudgetLocal.Tests;

public sealed class TransactionServiceTests
{
    private const int BudgetId = 1;
    [Fact]
    public async Task FiltersCanBeCombinedAndSortedByAmountOrDate()
    {
        await using var db = new TestDatabase();
        var service = db.Service;
        await service.AddAsync(BudgetId, New("Courses du jour", 45, new(2026, 10, 2), TransactionType.Expense, "Alimentation"));
        await service.AddAsync(BudgetId, New("Loyer", 800, new(2026, 10, 1), TransactionType.Expense, "Logement"));
        await service.AddAsync(BudgetId, New("Salaire", 2000, new(2026, 10, 3), TransactionType.Income, "Revenus"));
        await service.AddAsync(BudgetId, New("Courses août", 20, new(2026, 8, 3), TransactionType.Expense, "Alimentation"));

        var result = await service.SearchAsync(BudgetId, "courses", TransactionType.Expense, "Alimentation", new(2026, 10, 1), new(2026, 10, 31));
        Assert.Single(result);
        Assert.Equal("Courses du jour", result[0].Description);

        var amountOrder = await service.SearchAsync(BudgetId, null, null, null, null, null, "amount", true);
        Assert.Equal([20m, 45m, 800m, 2000m], amountOrder.Select(x => x.Amount));
        var dateOrder = await service.SearchAsync(BudgetId, null, null, null, null, null, "date", true);
        Assert.Equal(new DateOnly(2026, 8, 3), dateOrder[0].Date);
    }

    [Fact]
    public async Task UpdateDeleteAndSummaryPersist()
    {
        await using var db = new TestDatabase();
        var service = db.Service;
        await service.AddAsync(BudgetId, New("Courses", 45, new(2026, 10, 2), TransactionType.Expense, "Alimentation"));
        var item = (await service.GetAllAsync(BudgetId)).Single();
        item.Amount = 50;
        item.Description = "Courses modifiées";
        await service.UpdateAsync(BudgetId, item);
        Assert.Equal(new TransactionSummary(0, 50), await service.GetSummaryAsync(BudgetId));
        Assert.Equal("Courses modifiées", (await service.GetAllAsync(BudgetId)).Single().Description);
        await service.DeleteAsync(BudgetId, item.Id);
        Assert.Empty(await service.GetAllAsync(BudgetId));
        Assert.Equal(new TransactionSummary(0, 0), await service.GetSummaryAsync(BudgetId));
    }

    [Fact]
    public async Task ReadsAndAggregatesAreIsolatedByBudgetAndWritesCheckOwnership()
    {
        await using var db = new TestDatabase();
        await using (var context = new BudgetDbContext(db.Options))
        {
            context.Budgets.Add(new Budget { Id = 2, Name = "Autre" });
            await context.SaveChangesAsync();
        }

        var service = db.Service;
        await service.AddAsync(BudgetId, New("Dépense A", 40, new(2026, 11, 2), TransactionType.Expense, "Loisirs"));
        await service.AddAsync(2, New("Dépense B", 100, new(2026, 11, 2), TransactionType.Expense, "Loisirs"));
        var itemA = Assert.Single(await service.GetAllAsync(BudgetId));
        Assert.Equal(new TransactionSummary(0, 40), await service.GetSummaryAsync(BudgetId));
        Assert.Equal(new TransactionSummary(0, 100), await service.GetSummaryAsync(2));
        Assert.Equal(40m, Assert.Single(await service.GetExpensesByCategoryAsync(BudgetId, new(2026, 11, 1), new(2026, 11, 30))).Amount);
        Assert.Equal(40m, Assert.Single(await service.GetMonthlyTrendAsync(BudgetId, new(2026, 11, 1), 1)).Expenses);
        Assert.Single(await service.SearchAsync(BudgetId, null, null, null, null, null));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(2, itemA));
        await service.DeleteAsync(2, itemA.Id);
        Assert.Single(await service.GetAllAsync(BudgetId));
        Assert.DoesNotContain(itemA.Id, (await service.GetAllAsync(2)).Select(x => x.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddAsync(2, new Transaction
        {
            BudgetId = BudgetId, Description = "Mauvais budget", Amount = 10, Date = new(2026, 11, 3),
            Type = TransactionType.Expense, Category = "Loisirs"
        }));
    }

    [Fact]
    public async Task MultipleTransactionsInOneCategoryKeepSeparateIdsAndAggregateCorrectly()
    {
        await using var db = new TestDatabase();
        var service = db.Service;
        await service.AddAsync(BudgetId, New("Courses 1", 45, new(2026, 11, 1), TransactionType.Expense, "Alimentation"));
        await service.AddAsync(BudgetId, New("Courses 2", 62, new(2026, 11, 8), TransactionType.Expense, "Alimentation"));
        await service.AddAsync(BudgetId, New("Courses 3", 38, new(2026, 11, 15), TransactionType.Expense, "Alimentation"));

        var transactions = await service.GetAllAsync(BudgetId);
        Assert.Equal(3, transactions.Count);
        Assert.Equal(3, transactions.Select(x => x.Id).Distinct().Count());
        Assert.Equal(145m, Assert.Single(await service.GetExpensesByCategoryAsync(BudgetId, new(2026, 11, 1), new(2026, 11, 30))).Amount);
    }

    [Fact]
    public async Task EditSessionSavesDraftAndCancelLeavesStoredTransactionUntouched()
    {
        await using var db = new TestDatabase();
        var service = db.Service;
        await service.AddAsync(BudgetId, New("Repas", 25, new(2026, 11, 2), TransactionType.Expense, "Alimentation"));
        var original = Assert.Single(await service.GetAllAsync(BudgetId));

        var canceled = new TransactionEditSession(service, BudgetId, original);
        canceled.Draft.Description = "Changement annulé";
        canceled.Cancel();
        await Assert.ThrowsAsync<InvalidOperationException>(canceled.SaveAsync);
        Assert.Equal("Repas", Assert.Single(await service.GetAllAsync(BudgetId)).Description);

        var edit = new TransactionEditSession(service, BudgetId, original);
        edit.Draft.Description = "Repas modifié";
        edit.Draft.Amount = 31;
        edit.Draft.Type = TransactionType.Income;
        edit.Draft.Category = "Revenus";
        edit.Draft.Date = new DateOnly(2026, 11, 3);
        await edit.SaveAsync();
        var saved = Assert.Single(await service.GetAllAsync(BudgetId));
        Assert.Equal(("Repas modifié", 31m, TransactionType.Income, "Revenus", new DateOnly(2026, 11, 3)),
            (saved.Description, saved.Amount, saved.Type, saved.Category, saved.Date));
    }

    [Fact]
    public async Task AggregatesIncludeEmptyMonthsAndCategoryExpenses()
    {
        await using var db = new TestDatabase();
        var service = db.Service;
        await service.AddAsync(BudgetId, New("Loyer", 800, new(2026, 9, 2), TransactionType.Expense, "Logement"));
        await service.AddAsync(BudgetId, New("Salaire", 2000, new(2026, 9, 1), TransactionType.Income, "Revenus"));
        var category = await service.GetExpensesByCategoryAsync(BudgetId, new(2026, 9, 1), new(2026, 9, 30));
        Assert.Equal(new CategoryExpense("Logement", 800), Assert.Single(category));
        var trend = await service.GetMonthlyTrendAsync(BudgetId, new(2026, 9, 1), 2);
        Assert.Equal(new MonthlyBudgetPoint(new(2026, 9, 1), 2000, 800, 1200), trend[0]);
        Assert.Equal(new MonthlyBudgetPoint(new(2026, 10, 1), 0, 0, 0), trend[1]);
        Assert.Empty(await service.GetExpensesByCategoryAsync(BudgetId, new(2026, 10, 1), new(2026, 10, 31)));
    }

    [Fact]
    public async Task MonthlyTrendWithDateRangeExcludesTransactionsOutsideTheExactRange()
    {
        await using var db = new TestDatabase();
        await db.Service.AddAsync(BudgetId, New("Fin novembre", 25, new(2026, 11, 30), TransactionType.Expense, "Loisirs"));
        await db.Service.AddAsync(BudgetId, New("Décembre", 60, new(2026, 12, 1), TransactionType.Expense, "Loisirs"));

        var trend = await db.Service.GetMonthlyTrendForRangeAsync(BudgetId, new(2026, 11, 15), new(2026, 11, 30));
        Assert.Equal(new MonthlyBudgetPoint(new(2026, 11, 1), 0, 25, -25), Assert.Single(trend));
    }

    [Fact]
    public async Task ValidationRejectsInvalidTransactionsAndDateRange()
    {
        await using var db = new TestDatabase();
        await Assert.ThrowsAsync<ArgumentException>(() => db.Service.AddAsync(BudgetId, New(" ", 10, new(2026, 1, 1), TransactionType.Expense, "Alimentation")));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => db.Service.AddAsync(BudgetId, New("Bad amount", 0, new(2026, 1, 1), TransactionType.Expense, "Alimentation")));
        await Assert.ThrowsAsync<ArgumentException>(() => db.Service.SearchAsync(BudgetId, null, null, null, new(2026, 2, 1), new(2026, 1, 1)));
        Assert.Empty(await db.Service.GetAllAsync(BudgetId));
    }

    [Fact]
    public async Task RejectsCategoryOutsideFixedList()
    {
        await using var db = new TestDatabase();
        await Assert.ThrowsAsync<ArgumentException>(() => db.Service.AddAsync(BudgetId, New("Test", 10, new(2026, 1, 1), TransactionType.Expense, "transpor")));
        Assert.Equal(7, TransactionService.AllowedCategories.Count);
    }

    [Fact]
    public async Task ResetRemovesBudgetAndStaysEmptyAfterDatabaseReopen()
    {
        var path = Path.Combine(Path.GetTempPath(), $"budgetlocal-test-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<BudgetDbContext>().UseSqlite($"Data Source={path}").Options;
            var factory = new ContextFactory(options);
            var service = new TransactionService(factory);
            await using (var context = await factory.CreateDbContextAsync()) { await context.Database.EnsureCreatedAsync(); context.Budgets.Add(new Budget { Id = BudgetId, Name = "Test" }); await context.SaveChangesAsync(); }
            await service.AddAsync(BudgetId, New("Donnée fictive", 123.45m, new(2026, 10, 5), TransactionType.Expense, "Loisirs"));
            await service.ResetAsync(BudgetId);
            Assert.True(File.Exists(path));
            Assert.Empty(await service.GetAllAsync(BudgetId));
            Assert.Equal(new TransactionSummary(0, 0), await service.GetSummaryAsync(BudgetId));

            var reopenedService = new TransactionService(factory);
            Assert.Empty(await reopenedService.GetAllAsync(BudgetId));
            Assert.Equal(new TransactionSummary(0, 0), await reopenedService.GetSummaryAsync(BudgetId));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static Transaction New(string description, decimal amount, DateOnly date, TransactionType type, string category) =>
        new() { Description = description, Amount = amount, Date = date, Type = type, Category = category };

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), $"budgetlocal-test-{Guid.NewGuid():N}.db");
        private readonly DbContextOptions<BudgetDbContext> options;
        public TransactionService Service { get; }
        public DbContextOptions<BudgetDbContext> Options => options;
        public TestDatabase()
        {
            options = new DbContextOptionsBuilder<BudgetDbContext>().UseSqlite($"Data Source={path}").Options;
            Service = new TransactionService(new ContextFactory(options));
            using var context = new BudgetDbContext(options);
            context.Database.EnsureCreated(); context.Budgets.Add(new Budget { Id = BudgetId, Name = "Test" }); context.SaveChanges();
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

