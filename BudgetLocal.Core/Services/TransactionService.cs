using BudgetLocal.Core.Data;
using BudgetLocal.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetLocal.Core.Services;

public sealed record TransactionSummary(decimal Income, decimal Expenses)
{
    public decimal Balance => Income - Expenses;
}

public sealed record CategoryExpense(string Category, decimal Amount);
public sealed record MonthlyBudgetPoint(DateOnly Month, decimal Income, decimal Expenses, decimal Balance);

public sealed class TransactionService(IDbContextFactory<BudgetDbContext> factory)
{
    public static IReadOnlyList<string> AllowedCategories { get; } = Array.AsReadOnly(new[] { "Alimentation", "Logement", "Transport", "Loisirs", "Santé", "Revenus", "Autre" });
    public async Task<IReadOnlyList<Transaction>> GetAllAsync(int budgetId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Transactions.AsNoTracking().Where(x => x.BudgetId == budgetId).OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).ToListAsync();
    }

    public async Task<TransactionSummary> GetSummaryAsync(int budgetId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var scoped = db.Transactions.Where(x => x.BudgetId == budgetId);
        var income = await scoped.Where(x => x.Type == TransactionType.Income).SumAsync(x => (decimal?)x.Amount) ?? 0;
        var expenses = await scoped.Where(x => x.Type == TransactionType.Expense).SumAsync(x => (decimal?)x.Amount) ?? 0;
        return new(income, expenses);
    }

    public async Task AddAsync(int budgetId, Transaction transaction)
    {
        if (budgetId <= 0) throw new ArgumentOutOfRangeException(nameof(budgetId));
        Validate(transaction);
        await using var db = await factory.CreateDbContextAsync();
        if (!await db.Budgets.AnyAsync(x => x.Id == budgetId)) throw new InvalidOperationException("Budget introuvable.");
        if (transaction.BudgetId != 0 && transaction.BudgetId != budgetId)
            throw new InvalidOperationException("La transaction est associée à un autre budget.");
        if (transaction.Budget is not null && transaction.Budget.Id != budgetId)
            throw new InvalidOperationException("La transaction est associée à un autre budget.");
        transaction.BudgetId = budgetId;
        transaction.Budget = null;
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(int budgetId, Transaction transaction)
    {
        Validate(transaction);
        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.Transactions.FirstOrDefaultAsync(x => x.Id == transaction.Id && x.BudgetId == budgetId) ?? throw new InvalidOperationException("Transaction introuvable.");
        existing.Description = transaction.Description.Trim();
        existing.Amount = transaction.Amount;
        existing.Date = transaction.Date;
        existing.Type = transaction.Type;
        existing.Category = transaction.Category.Trim();
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int budgetId, int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.Transactions.FirstOrDefaultAsync(x => x.Id == id && x.BudgetId == budgetId);
        if (existing is not null) { db.Transactions.Remove(existing); await db.SaveChangesAsync(); }
    }

    public async Task<IReadOnlyList<Transaction>> SearchAsync(int budgetId, string? text, TransactionType? type, string? category, DateOnly? from, DateOnly? to, string sortBy = "date", bool ascending = false)
    {
        if (from > to) throw new ArgumentException("La date de dÃ©but doit prÃ©cÃ©der la date de fin.");
        await using var db = await factory.CreateDbContextAsync();
        var query = db.Transactions.AsNoTracking().Where(x => x.BudgetId == budgetId).AsQueryable();
        if (!string.IsNullOrWhiteSpace(text)) { var term = text.Trim().ToLower(); query = query.Where(x => x.Description.ToLower().Contains(term) || x.Category.ToLower().Contains(term)); }
        if (type.HasValue) query = query.Where(x => x.Type == type.Value);
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(x => x.Category == category);
        if (from.HasValue) query = query.Where(x => x.Date >= from.Value);
        if (to.HasValue) query = query.Where(x => x.Date <= to.Value);
        return sortBy.ToLowerInvariant() switch
        {
            "amount" => await (ascending ? query.OrderBy(x => x.Amount) : query.OrderByDescending(x => x.Amount)).ThenByDescending(x => x.Date).ToListAsync(),
            "date" => await (ascending ? query.OrderBy(x => x.Date) : query.OrderByDescending(x => x.Date)).ThenByDescending(x => x.Id).ToListAsync(),
            _ => throw new ArgumentException("Tri non pris en charge.", nameof(sortBy))
        };
    }

    public async Task<IReadOnlyList<CategoryExpense>> GetExpensesByCategoryAsync(int budgetId, DateOnly from, DateOnly to)
    {
        if (from > to) throw new ArgumentException("La date de dÃ©but doit prÃ©cÃ©der la date de fin.");
        await using var db = await factory.CreateDbContextAsync();
        var values = await db.Transactions.AsNoTracking().Where(x => x.BudgetId == budgetId && x.Type == TransactionType.Expense && x.Date >= from && x.Date <= to).ToListAsync();
        return values.GroupBy(x => x.Category).Select(g => new CategoryExpense(g.Key, g.Sum(x => x.Amount))).OrderByDescending(x => x.Amount).ToList();
    }

    public async Task<IReadOnlyList<MonthlyBudgetPoint>> GetMonthlyTrendAsync(int budgetId, DateOnly firstMonth, int monthCount)
    {
        if (monthCount < 0 || monthCount > 120) throw new ArgumentOutOfRangeException(nameof(monthCount));
        await using var db = await factory.CreateDbContextAsync();
        var all = await db.Transactions.AsNoTracking().Where(x => x.BudgetId == budgetId && x.Date >= firstMonth).ToListAsync();
        return BuildMonthlyTrend(all, firstMonth, monthCount);
    }

    public async Task<IReadOnlyList<MonthlyBudgetPoint>> GetMonthlyTrendForRangeAsync(int budgetId, DateOnly from, DateOnly to)
    {
        if (from > to) throw new ArgumentException("La date de début doit précéder la date de fin.");
        var firstMonth = new DateOnly(from.Year, from.Month, 1);
        var lastMonth = new DateOnly(to.Year, to.Month, 1);
        var monthCount = (lastMonth.Year - firstMonth.Year) * 12 + lastMonth.Month - firstMonth.Month + 1;
        if (monthCount > 120) throw new ArgumentOutOfRangeException(nameof(to), "La période des graphiques ne peut pas dépasser 120 mois.");
        await using var db = await factory.CreateDbContextAsync();
        var all = await db.Transactions.AsNoTracking().Where(x => x.BudgetId == budgetId && x.Date >= from && x.Date <= to).ToListAsync();
        return BuildMonthlyTrend(all, firstMonth, monthCount);
    }

    private static IReadOnlyList<MonthlyBudgetPoint> BuildMonthlyTrend(IReadOnlyCollection<Transaction> all, DateOnly firstMonth, int monthCount)
    {
        var result = new List<MonthlyBudgetPoint>();
        for (var i = 0; i < monthCount; i++)
        {
            var month = firstMonth.AddMonths(i);
            var items = all.Where(x => x.Date.Year == month.Year && x.Date.Month == month.Month);
            var income = items.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount);
            var expenses = items.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount);
            result.Add(new(month, income, expenses, income - expenses));
        }
        return result;
    }

    public async Task ResetAsync(int budgetId)
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Transactions.Where(x => x.BudgetId == budgetId).ExecuteDeleteAsync();
        await transaction.CommitAsync();
    }

    private static void Validate(Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        if (string.IsNullOrWhiteSpace(transaction.Description) || transaction.Description.Trim().Length > 120)
            throw new ArgumentException("Le libellÃ© doit contenir entre 1 et 120 caractÃ¨res.", nameof(transaction));
        if (string.IsNullOrWhiteSpace(transaction.Category) || !AllowedCategories.Contains(transaction.Category.Trim(), StringComparer.Ordinal))
            throw new ArgumentException("Sélectionnez une catégorie prédéfinie.", nameof(transaction));
        if (transaction.Amount <= 0 || transaction.Amount > 999999999.99m)
            throw new ArgumentOutOfRangeException(nameof(transaction), "Le montant doit Ãªtre supÃ©rieur Ã  zÃ©ro et ne pas dÃ©passer 999 999 999,99.");
        if (!Enum.IsDefined(transaction.Type)) throw new ArgumentException("Type de transaction invalide.", nameof(transaction));
    }
}


