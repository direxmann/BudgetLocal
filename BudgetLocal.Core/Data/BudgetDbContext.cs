using BudgetLocal.Core.Models;
using Microsoft.EntityFrameworkCore;
namespace BudgetLocal.Core.Data;
public sealed class BudgetDbContext(DbContextOptions<BudgetDbContext> options) : DbContext(options)
{
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<BudgetConfiguration> BudgetConfigurations => Set<BudgetConfiguration>();
    public DbSet<Budget> Budgets => Set<Budget>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Transaction>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Description).HasMaxLength(120).IsRequired();
            e.Property(x => x.Category).HasMaxLength(80).IsRequired();
            e.HasIndex(x => x.Date);
            e.HasOne(x => x.Budget).WithMany(x => x.Transactions).HasForeignKey(x => x.BudgetId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Budget>(e => { e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(120).IsRequired(); });
        b.Entity<BudgetConfiguration>(e =>
        {
            e.ToTable("BudgetConfigurations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.MonthlyExpenseBudget).HasConversion<string>().HasColumnType("TEXT");
        });
    }
}
