# BudgetLocal

Application de gestion de budget pour Windows, développée en C# avec WPF, .NET 10, EF Core et SQLite.

Les transactions sont stockées localement dans `%LocalAppData%\BudgetLocal\budgetlocal.db`; les sauvegardes se trouvent dans `%LocalAppData%\BudgetLocal\Backups`.

## Développer

```powershell
dotnet restore .\BudgetLocal.slnx
dotnet run --project .\BudgetLocal.Desktop\BudgetLocal.Desktop.csproj
```

## Vérifier et publier

```powershell
dotnet test .\BudgetLocal.Tests\BudgetLocal.Tests.csproj
dotnet publish .\BudgetLocal.Desktop\BudgetLocal.Desktop.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false -o .\Distribution\BudgetLocal-Light
```

Le guide utilisateur se trouve dans [docs/Guide-utilisateur.md](docs/Guide-utilisateur.md).

La distribution légère requiert le runtime **.NET 10 Desktop Runtime x64** installé sur Windows.

