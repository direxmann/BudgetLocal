using BudgetLocal.Core.Models;

namespace BudgetLocal.Core.Services;

/// <summary>Isolates edits in a draft until the user explicitly saves.</summary>
public sealed class TransactionEditSession
{
    private readonly TransactionService service;
    private readonly int budgetId;
    private bool completed;

    public Transaction Draft { get; }

    public TransactionEditSession(TransactionService service, int budgetId, Transaction original)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        if (budgetId <= 0) throw new ArgumentOutOfRangeException(nameof(budgetId));
        this.budgetId = budgetId;
        ArgumentNullException.ThrowIfNull(original);
        Draft = new Transaction
        {
            Id = original.Id,
            BudgetId = original.BudgetId,
            Description = original.Description,
            Amount = original.Amount,
            Type = original.Type,
            Category = original.Category,
            Date = original.Date
        };
    }

    public async Task SaveAsync()
    {
        if (completed) throw new InvalidOperationException("Cette modification est déjà terminée.");
        await service.UpdateAsync(budgetId, Draft);
        completed = true;
    }

    public void Cancel() => completed = true;
}
