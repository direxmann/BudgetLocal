namespace BudgetLocal.Core.Models;

/// <summary>A single, optional monthly cap for expense tracking. No amount is implied when absent.</summary>
public sealed class BudgetConfiguration
{
    public int Id { get; set; } = 1;
    public decimal MonthlyExpenseBudget { get; set; }
}
