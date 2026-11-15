using BudgetLocal.Core.Data;
using BudgetLocal.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetLocal.Core.Services;

public sealed class BudgetConfigurationService(IDbContextFactory<BudgetDbContext> factory)
{
    public async Task<decimal?> GetMonthlyExpenseBudgetAsync(int budgetId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Budgets.AsNoTracking().Where(x => x.Id == budgetId)
            .Select(x => x.MonthlyExpenseBudget).SingleOrDefaultAsync();
    }

    public async Task SetMonthlyExpenseBudgetAsync(int budgetId, decimal? amount)
    {
        if (amount is <= 0 or > 999999999.99m)
            throw new ArgumentOutOfRangeException(nameof(amount), "Le budget mensuel doit être supérieur à zéro et inférieur à 999 999 999,99 €.");

        await using var db = await factory.CreateDbContextAsync();
        var budget = await db.Budgets.SingleOrDefaultAsync(x => x.Id == budgetId)
            ?? throw new InvalidOperationException("Budget introuvable.");
        budget.MonthlyExpenseBudget = amount;
        await db.SaveChangesAsync();
    }
}
