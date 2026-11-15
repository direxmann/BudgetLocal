namespace BudgetLocal.Core.Services;

/// <summary>Financial indicators based on recorded transactions and an optional user-defined monthly expense cap.</summary>
public sealed record BudgetOverview(decimal TotalBalance, decimal MonthlyExpenses, decimal? MonthlyExpenseBudget)
{
    public decimal? MonthlyBudgetRemaining => MonthlyExpenseBudget is { } budget ? budget - MonthlyExpenses : null;

    public static BudgetOverview Calculate(TransactionSummary allTimeSummary, decimal currentMonthExpenses, decimal? monthlyExpenseBudget)
    {
        ArgumentNullException.ThrowIfNull(allTimeSummary);
        if (currentMonthExpenses < 0) throw new ArgumentOutOfRangeException(nameof(currentMonthExpenses));
        if (monthlyExpenseBudget is <= 0) throw new ArgumentOutOfRangeException(nameof(monthlyExpenseBudget));
        return new(allTimeSummary.Balance, currentMonthExpenses, monthlyExpenseBudget);
    }
}
