using System.Globalization;
using BudgetLocal.Core.Data;
using BudgetLocal.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetLocal.Core.Services;

public sealed class BudgetService(IDbContextFactory<BudgetDbContext> factory)
{
    public async Task<IReadOnlyList<Budget>> GetAllAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Budgets.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync();
    }

    public async Task<Budget> CreateAsync(string name, string? description = null, decimal? monthlyExpenseBudget = null)
    {
        Validate(name, description, monthlyExpenseBudget);
        await using var db = await factory.CreateDbContextAsync();
        var budget = new Budget
        {
            Name = name.Trim(),
            Description = NormalizeDescription(description),
            MonthlyExpenseBudget = monthlyExpenseBudget
        };
        db.Budgets.Add(budget);
        await db.SaveChangesAsync();
        await SetActiveIdAsync(db, budget.Id);
        return budget;
    }

    public async Task UpdateAsync(int budgetId, string name, string? description, decimal? monthlyExpenseBudget)
    {
        Validate(name, description, monthlyExpenseBudget);
        await using var db = await factory.CreateDbContextAsync();
        var budget = await db.Budgets.SingleOrDefaultAsync(x => x.Id == budgetId)
            ?? throw new InvalidOperationException("Budget introuvable.");
        budget.Name = name.Trim();
        budget.Description = NormalizeDescription(description);
        budget.MonthlyExpenseBudget = monthlyExpenseBudget;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int budgetId)
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var budget = await db.Budgets.SingleOrDefaultAsync(x => x.Id == budgetId)
            ?? throw new InvalidOperationException("Budget introuvable.");
        if (await db.Budgets.CountAsync() <= 1)
            throw new InvalidOperationException("Le dernier budget ne peut pas être supprimé.");
        if (await db.Transactions.AnyAsync(x => x.BudgetId == budgetId))
            throw new InvalidOperationException("Ce budget contient des transactions. Transférez-les ou supprimez-les avant de supprimer le budget.");

        db.Budgets.Remove(budget);
        await db.SaveChangesAsync();
        var activeId = await GetActiveIdAsync(db);
        if (activeId == budgetId)
        {
            var fallback = await db.Budgets.OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
            await SetActiveIdAsync(db, fallback);
        }
        await transaction.CommitAsync();
    }

    public async Task<int> GetActiveBudgetIdAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var activeId = await GetActiveIdAsync(db);
        if (await db.Budgets.AnyAsync(x => x.Id == activeId)) return activeId;
        var fallback = await db.Budgets.OrderBy(x => x.Id).Select(x => (int?)x.Id).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Aucun budget n'est disponible.");
        await SetActiveIdAsync(db, fallback);
        return fallback;
    }

    public async Task SetActiveBudgetAsync(int budgetId)
    {
        await using var db = await factory.CreateDbContextAsync();
        if (!await db.Budgets.AnyAsync(x => x.Id == budgetId))
            throw new InvalidOperationException("Budget introuvable.");
        await SetActiveIdAsync(db, budgetId);
    }

    private static async Task<int> GetActiveIdAsync(BudgetDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT Value FROM ApplicationSettings WHERE Name = 'ActiveBudgetId';";
            var value = await command.ExecuteScalarAsync();
            return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var id) ? id : 1;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task SetActiveIdAsync(BudgetDbContext db, int budgetId)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ApplicationSettings (Name, Value) VALUES ('ActiveBudgetId', {budgetId.ToString(CultureInfo.InvariantCulture)}) ON CONFLICT(Name) DO UPDATE SET Value = excluded.Value;");
    }

    private static void Validate(string name, string? description, decimal? monthlyExpenseBudget)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120)
            throw new ArgumentException("Le nom du budget doit contenir entre 1 et 120 caractères.", nameof(name));
        if (description?.Length > 500)
            throw new ArgumentException("La description ne peut pas dépasser 500 caractères.", nameof(description));
        if (monthlyExpenseBudget is <= 0 or > 999999999.99m)
            throw new ArgumentOutOfRangeException(nameof(monthlyExpenseBudget), "Le plafond doit être supérieur à zéro et inférieur à 999 999 999,99 €.");
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
