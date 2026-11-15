using System.ComponentModel.DataAnnotations;
namespace BudgetLocal.Core.Models;
public sealed class Transaction { public int Id { get; set; } public int BudgetId { get; set; } public Budget? Budget { get; set; } [Required, StringLength(120)] public string Description { get; set; } = ""; [Range(typeof(decimal), "0.01", "999999999.99")] public decimal Amount { get; set; } public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today); public TransactionType Type { get; set; } [Required, StringLength(80)] public string Category { get; set; } = ""; }
