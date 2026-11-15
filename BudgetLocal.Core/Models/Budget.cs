namespace BudgetLocal.Core.Models;

public sealed class Budget
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal? MonthlyExpenseBudget { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    public override string ToString() => Name;
}
