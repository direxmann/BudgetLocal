# Notes de développement

- `BudgetLocal.Desktop` : interface WPF .NET 10 pour Windows.
- `BudgetLocal.Core` : modèle EF Core, SQLite et services métier.
- `BudgetLocal.Tests` : tests SQLite des services, des indicateurs et des scénarios d’édition.

Les données utilisateur restent dans `%LocalAppData%\BudgetLocal`. La publication ne doit pas inclure ni déplacer la base utilisateur ou ses sauvegardes.

Chaque budget possède son plafond mensuel facultatif et ses transactions via `Transactions.BudgetId`. À la première mise à niveau, le schéma crée « Budget personnel », rattache les anciennes transactions à l’identifiant 1 et copie le plafond existant depuis `BudgetConfigurations`. L’opération est idempotente et ne supprime ni ne remplace les transactions. Les choix actif et données sont isolés par budget.

Les services `BudgetService`, `TransactionService` et `BudgetConfigurationService` sont les points d’accès métier. Les graphiques WPF proposent une période sélectionnable et sont recalculés exclusivement à partir des transactions du budget actif.

Les graphiques utilisent LiveCharts2 WPF (`LiveChartsCore.SkiaSharpView.WPF`) version 2.0.5 sous licence MIT. Le framework WPF cible Windows 10 version 2004 (build 19041) ou ultérieur.

## Vérifications locales

```powershell
dotnet restore .\BudgetLocal.slnx
dotnet build .\BudgetLocal.slnx -c Release --no-restore
dotnet test .\BudgetLocal.Tests\BudgetLocal.Tests.csproj -c Release --no-build --no-restore
 powershell -ExecutionPolicy Bypass -File .\installer\Build-Installer.ps1
```

Cette commande crée `Distribution\BudgetLocal-Setup.exe`, un installateur autonome pour Windows 10/11 64 bits. Le runtime .NET 10 de l’application est inclus; le PC destinataire n’a pas besoin du SDK ou du runtime .NET 10. L’application s’installe dans le profil de l’utilisateur et ajoute des raccourcis au Bureau et au menu Démarrer. La désinstallation conserve les données dans `%LocalAppData%\BudgetLocal`.

Le code Windows `0x800711C7` correspond à `ERROR_SYSTEM_INTEGRITY_POLICY_VIOLATION`, lié à une stratégie Device Guard / contrôle d’applications. Après reconstruction, les tests ont été chargés et exécutés sans désactiver les protections Windows. Aucun événement récent correspondant au projet n’a été trouvé dans les journaux Code Integrity et AppLocker; la politique exacte du blocage initial reste donc non confirmée.


