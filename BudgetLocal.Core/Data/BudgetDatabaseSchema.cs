using Microsoft.EntityFrameworkCore;

namespace BudgetLocal.Core.Data;

public static class BudgetDatabaseSchema
{
    /// <summary>Creates missing schema objects and upgrades the legacy single-budget database additively.</summary>
    public static async Task EnsureCurrentAsync(BudgetDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS BudgetConfigurations (Id INTEGER NOT NULL PRIMARY KEY CHECK (Id = 1), MonthlyExpenseBudget TEXT NOT NULL);");

        await db.Database.OpenConnectionAsync();
        try
        {
            var connection = db.Database.GetDbConnection();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    CREATE TABLE IF NOT EXISTS Budgets (
                        Id INTEGER NOT NULL CONSTRAINT PK_Budgets PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Description TEXT NULL,
                        MonthlyExpenseBudget TEXT NULL,
                        CreatedAt TEXT NOT NULL
                    );
                    """;
                await command.ExecuteNonQueryAsync();
            }

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    INSERT INTO Budgets (Id, Name, Description, MonthlyExpenseBudget, CreatedAt)
                    SELECT 1, 'Budget personnel', NULL,
                           (SELECT MonthlyExpenseBudget FROM BudgetConfigurations WHERE Id = 1),
                           $createdAt
                    WHERE NOT EXISTS (SELECT 1 FROM Budgets WHERE Id = 1);
                    """;
                var parameter = command.CreateParameter();
                parameter.ParameterName = "$createdAt";
                parameter.Value = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                command.Parameters.Add(parameter);
                await command.ExecuteNonQueryAsync();
            }

            var hasBudgetId = false;
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA table_info('Transactions');";
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    if (string.Equals(reader.GetString(1), "BudgetId", StringComparison.OrdinalIgnoreCase)) hasBudgetId = true;
            }

            if (!hasBudgetId)
            {
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA foreign_keys = OFF;";
                    await command.ExecuteNonQueryAsync();
                }
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = "ALTER TABLE Transactions ADD COLUMN BudgetId INTEGER NOT NULL DEFAULT 1 REFERENCES Budgets(Id) ON DELETE RESTRICT;";
                    await command.ExecuteNonQueryAsync();
                }
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA foreign_keys = ON;";
                    await command.ExecuteNonQueryAsync();
                }
            }

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "CREATE INDEX IF NOT EXISTS IX_Transactions_BudgetId ON Transactions (BudgetId);";
                await command.ExecuteNonQueryAsync();
            }

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "CREATE TABLE IF NOT EXISTS ApplicationSettings (Name TEXT NOT NULL PRIMARY KEY, Value TEXT NOT NULL); INSERT INTO ApplicationSettings (Name, Value) VALUES ('ActiveBudgetId', '1') ON CONFLICT(Name) DO NOTHING;";
                await command.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
