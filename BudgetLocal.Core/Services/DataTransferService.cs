using System.Text.Json;
using Microsoft.Data.Sqlite;
using Data = BudgetLocal.Core.Data;
using BudgetLocal.Core.Models;
using Microsoft.EntityFrameworkCore;
namespace BudgetLocal.Core.Services;
public sealed class DataTransferService(IDbContextFactory<Data.BudgetDbContext> factory, Func<string>? backupCreator = null) {
 public async Task EnsureDatabaseAsync() { Directory.CreateDirectory(BudgetPaths.DataDirectory); Directory.CreateDirectory(BudgetPaths.BackupDirectory); await using var db=await factory.CreateDbContextAsync(); await Data.BudgetDatabaseSchema.EnsureCurrentAsync(db); }
 public string CreateBackup() { if(backupCreator is not null) return backupCreator(); Directory.CreateDirectory(BudgetPaths.BackupDirectory); var target=Path.Combine(BudgetPaths.BackupDirectory,$"budgetlocal-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.db"); if(File.Exists(BudgetPaths.DatabasePath)) { using var source=new SqliteConnection($"Data Source={BudgetPaths.DatabasePath};Mode=ReadOnly"); using var destination=new SqliteConnection($"Data Source={target}"); source.Open(); destination.Open(); source.BackupDatabase(destination); } return target; }
 public async Task ExportAsync(int budgetId, string path) { await using var db=await factory.CreateDbContextAsync(); var data=await db.Transactions.AsNoTracking().Where(x=>x.BudgetId==budgetId).ToListAsync(); await File.WriteAllTextAsync(path,JsonSerializer.Serialize(data,new JsonSerializerOptions { WriteIndented=true })); }
 public async Task ImportAsync(int budgetId, string path) { var content=await File.ReadAllTextAsync(path); var data=JsonSerializer.Deserialize<List<Transaction>>(content) ?? throw new InvalidDataException("Fichier d'import invalide."); await using var db=await factory.CreateDbContextAsync(); if(!await db.Budgets.AnyAsync(x=>x.Id==budgetId)) throw new InvalidOperationException("Budget introuvable."); CreateBackup(); await using var transaction=await db.Database.BeginTransactionAsync(); db.Transactions.RemoveRange(db.Transactions.Where(x=>x.BudgetId==budgetId)); foreach(var item in data) { item.Id=0; item.BudgetId=budgetId; item.Budget=null; db.Transactions.Add(item); } await db.SaveChangesAsync(); await transaction.CommitAsync(); }
}
