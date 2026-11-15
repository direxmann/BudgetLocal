using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using BudgetLocal.Core.Models;
using BudgetLocal.Core.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Win32;
using SkiaSharp;

namespace BudgetLocal.Desktop.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-BE");
    private readonly TransactionService service;
    private readonly BudgetConfigurationService budgetConfiguration;
    private readonly BudgetService budgetService;
    private readonly DataTransferService transfer;
    private string description = "", amount = "", category = "Alimentation", status = "", searchText = "", budgetInput = "";
    private string income = "0,00 €", expenses = "0,00 €", balance = "0,00 €", count = "0 transaction ce mois";
    private string totalBalance = "0,00 €", remainingBudget = "Budget non défini", budgetDescription = "Définissez un plafond mensuel pour suivre les dépenses restantes.";
    private TransactionType type = TransactionType.Expense;
    private TransactionTypeChoice? filterType = new(null, "Tous les types");
    private string selectedSort = "Date ↓";
    private string? filterCategory;
    private DateTime? date = DateTime.Today, startDate, endDate;
    private Budget? selectedBudget;
    private string selectedGraphPeriod = "Année en cours";
    private DateTime? graphStartDate = new(DateTime.Today.Year, 1, 1), graphEndDate = new(DateTime.Today.Year, 12, 31);

    public ObservableCollection<Transaction> Transactions { get; } = [];
    public ObservableCollection<Transaction> RecentTransactions { get; } = [];
    public ObservableCollection<Budget> Budgets { get; } = [];
    public ObservableCollection<string> Categories { get; } = new(TransactionService.AllowedCategories);
    public IReadOnlyList<TransactionTypeChoice> TransactionTypes { get; } = [new(TransactionType.Expense, "Dépense"), new(TransactionType.Income, "Revenu")];
    public IReadOnlyList<TransactionTypeChoice> FilterTypes { get; } = [new(null, "Tous les types"), new(TransactionType.Expense, "Dépense"), new(TransactionType.Income, "Revenu")];
    public IReadOnlyList<string> SortOptions { get; } = ["Date ↓", "Date ↑", "Montant ↓", "Montant ↑"];
    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel(TransactionService s, BudgetConfigurationService b, BudgetService budgets, DataTransferService t)
    {
        service = s; budgetConfiguration = b; budgetService = budgets; transfer = t;
        RefreshCommand = new AsyncRelay(Load); AddCommand = new AsyncRelay(Add);
        CancelCommand = new Relay(_ => ClearAddForm()); ExportCommand = new AsyncRelay(Export);
        ImportCommand = new AsyncRelay(Import); BackupCommand = new Relay(_ => CreateBackup());
        SearchCommand = new AsyncRelay(ApplyFilters); ResetFiltersCommand = new AsyncRelay(ResetFilters);
        EditCommand = new AsyncRelay(p => Edit((Transaction)p!)); DeleteCommand = new AsyncRelay(p => Delete((Transaction)p!));
        ResetBudgetCommand = new AsyncRelay(ResetBudget);
        SaveBudgetCommand = new AsyncRelay(SaveBudget); ClearBudgetCommand = new AsyncRelay(ClearBudget);
        ManageBudgetsCommand = new AsyncRelay(ManageBudgets);
        DashboardCommand = new Relay(_ => SetPage(false)); ChartsCommand = new Relay(_ => SetPage(true));
        ApplyGraphFiltersCommand = new AsyncRelay(LoadCharts);
        _ = Load();
    }

    public string Description { get => description; set => Set(ref description, value); }
    public string Amount { get => amount; set => Set(ref amount, value); }
    public string Category { get => category; set => Set(ref category, value); }
    public TransactionType SelectedType { get => type; set => Set(ref type, value); }
    public DateTime? TransactionDate { get => date; set => Set(ref date, value); }
    public string IncomeText { get => income; private set => Set(ref income, value); }
    public string ExpensesText { get => expenses; private set => Set(ref expenses, value); }
    public string BalanceText { get => balance; private set => Set(ref balance, value); }
    public string MonthlyCountText { get => count; private set => Set(ref count, value); }
    public string TotalBalanceText { get => totalBalance; private set => Set(ref totalBalance, value); }
    public string RemainingBudgetText { get => remainingBudget; private set => Set(ref remainingBudget, value); }
    public string BudgetDescription { get => budgetDescription; private set => Set(ref budgetDescription, value); }
    public string BudgetInput { get => budgetInput; set => Set(ref budgetInput, value); }
    public Budget? SelectedBudget
    {
        get => selectedBudget;
        set
        {
            if (ReferenceEquals(selectedBudget, value)) return;
            Set(ref selectedBudget, value);
            if (value is not null && Budgets.Any(x => x.Id == value.Id)) _ = SelectBudget(value.Id);
        }
    }
    public string SelectedGraphPeriod
    {
        get => selectedGraphPeriod;
        set { if (SetValue(ref selectedGraphPeriod, value)) SetGraphRange(value); }
    }
    public DateTime? GraphStartDate { get => graphStartDate; set => Set(ref graphStartDate, value); }
    public DateTime? GraphEndDate { get => graphEndDate; set => Set(ref graphEndDate, value); }
    public IReadOnlyList<string> GraphPeriods { get; } = ["Année en cours", "Année précédente", "12 derniers mois", "30 derniers jours", "Période personnalisée"];
    public bool IsDashboardPage { get; private set; } = true;
    public bool IsChartsPage => !IsDashboardPage;
    public bool HasConfiguredBudget { get; private set; }
    public string StatusMessage { get => status; private set => Set(ref status, value); }
    public string SearchText { get => searchText; set => Set(ref searchText, value); }
    public TransactionTypeChoice? FilterType { get => filterType; set => Set(ref filterType, value); }
    public string? FilterCategory { get => filterCategory; set => Set(ref filterCategory, value); }
    public string SelectedSort { get => selectedSort; set => Set(ref selectedSort, value); }
    public DateTime? StartDate { get => startDate; set => Set(ref startDate, value); }
    public DateTime? EndDate { get => endDate; set => Set(ref endDate, value); }
    public IEnumerable<ISeries> CategorySeries { get; private set; } = [];
    public bool HasCategoryExpenses { get; private set; }
    public IEnumerable<ISeries> MonthlySeries { get; private set; } = [];
    public bool HasMonthlyActivity { get; private set; }
    public Axis[] MonthlyAxes { get; private set; } = [new Axis()];
    public IEnumerable<ISeries> IncomeByMonthSeries { get; private set; } = [];
    public bool HasIncomeByMonth { get; private set; }
    public Axis[] IncomeByMonthAxes { get; private set; } = [new Axis()];
    public IEnumerable<ISeries> BudgetIndicatorsSeries { get; private set; } = [];
    public bool HasBudgetIndicatorData { get; private set; }
    public Axis[] BudgetAxes { get; private set; } = [new Axis()];
    public Axis[] BudgetValueAxes { get; private set; } = [new Axis()];
    public ICommand RefreshCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand BackupCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ResetFiltersCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ResetBudgetCommand { get; }
    public ICommand SaveBudgetCommand { get; }
    public ICommand ClearBudgetCommand { get; }
    public ICommand ManageBudgetsCommand { get; }
    public ICommand DashboardCommand { get; }
    public ICommand ChartsCommand { get; }
    public ICommand ApplyGraphFiltersCommand { get; }

    private async Task Load()
    {
        var budgets = await budgetService.GetAllAsync();
        Budgets.Clear();
        foreach (var item in budgets) Budgets.Add(item);
        if (budgets.Count == 0) throw new InvalidOperationException("Aucun budget n'est disponible. Relancez l'application après la mise à niveau de la base.");
        var activeId = await budgetService.GetActiveBudgetIdAsync();
        var selected = budgets.FirstOrDefault(x => x.Id == activeId) ?? budgets[0];
        Set(ref selectedBudget, selected, nameof(SelectedBudget));
        Notify(nameof(Budgets));
        var all = await service.GetAllAsync(selected.Id);
        await ApplyFilters();
        RecentTransactions.Clear();
        foreach (var item in all.Take(6)) RecentTransactions.Add(item);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var month = all.Where(x => x.Date.Year == today.Year && x.Date.Month == today.Month).ToList();
        var monthlyIncome = month.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount);
        var monthlyExpenses = month.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount);
        IncomeText = monthlyIncome.ToString("C", FrenchCulture);
        ExpensesText = monthlyExpenses.ToString("C", FrenchCulture);
        BalanceText = (monthlyIncome - monthlyExpenses).ToString("C", FrenchCulture);
        MonthlyCountText = $"{month.Count} transaction{(month.Count == 1 ? "" : "s")} ce mois";

        var total = await service.GetSummaryAsync(selected.Id);
        var limit = await budgetConfiguration.GetMonthlyExpenseBudgetAsync(selected.Id);
        var overview = BudgetOverview.Calculate(total, monthlyExpenses, limit);
        TotalBalanceText = overview.TotalBalance.ToString("C", FrenchCulture);
        HasConfiguredBudget = limit.HasValue;
        BudgetInput = limit?.ToString("0.00", FrenchCulture) ?? "";
        if (limit.HasValue)
        {
            var left = overview.MonthlyBudgetRemaining!.Value;
            RemainingBudgetText = left.ToString("C", FrenchCulture);
            BudgetDescription = $"Plafond mensuel {limit.Value.ToString("C", FrenchCulture)} − dépenses du mois {monthlyExpenses.ToString("C", FrenchCulture)}.";
        }
        else
        {
            RemainingBudgetText = "Budget non défini";
            BudgetDescription = "Définissez un plafond de dépenses mensuel pour calculer le montant restant.";
        }

        await LoadCharts();
    }

    private async Task LoadCharts()
    {
        if (SelectedBudget is null) return;
        if (GraphStartDate is null || GraphEndDate is null || GraphStartDate > GraphEndDate)
        {
            StatusMessage = "Choisissez une période valide pour les graphiques.";
            return;
        }

        var budgetId = SelectedBudget.Id;
        var from = DateOnly.FromDateTime(GraphStartDate.Value);
        var to = DateOnly.FromDateTime(GraphEndDate.Value);
        var categories = await service.GetExpensesByCategoryAsync(budgetId, from, to);
        HasCategoryExpenses = categories.Any(x => x.Amount > 0);
        CategorySeries = categories.Where(x => x.Amount > 0)
            .Select(x => (ISeries)new PieSeries<double> { Name = x.Category, Values = [(double)x.Amount] }).ToArray();

        var trend = await service.GetMonthlyTrendForRangeAsync(budgetId, from, to);
        HasMonthlyActivity = trend.Any(x => x.Income > 0 || x.Expenses > 0);
        MonthlySeries =
        [
            new ColumnSeries<double> { Name = "Revenus", Values = trend.Select(x => (double)x.Income).ToArray(), Fill = new SolidColorPaint(new SKColor(24, 121, 78)) },
            new ColumnSeries<double> { Name = "Dépenses", Values = trend.Select(x => (double)x.Expenses).ToArray(), Fill = new SolidColorPaint(new SKColor(180, 35, 24)) }
        ];
        MonthlyAxes = [new Axis { Labels = trend.Select(x => x.Month.ToString("MMM yy", CultureInfo.GetCultureInfo("fr-FR"))).ToArray() }];

        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstIncomeMonth = today.AddMonths(-11);
        var incomeFrom = new DateOnly(firstIncomeMonth.Year, firstIncomeMonth.Month, 1);
        var incomeTrend = await service.GetMonthlyTrendForRangeAsync(budgetId, incomeFrom, today);
        HasIncomeByMonth = incomeTrend.Any(x => x.Income > 0);
        IncomeByMonthSeries = [new ColumnSeries<double>
        {
            Name = "Ressources entrantes",
            Values = incomeTrend.Select(x => (double)x.Income).ToArray(),
            Fill = new SolidColorPaint(new SKColor(24, 121, 78))
        }];
        IncomeByMonthAxes = [new Axis { Labels = incomeTrend.Select(x => x.Month.ToString("MMM yy", CultureInfo.GetCultureInfo("fr-FR"))).ToArray() }];

        var allTime = await service.GetSummaryAsync(budgetId);
        var thisMonth = today;
        var currentMonth = await service.SearchAsync(budgetId, null, TransactionType.Expense, null,
            new DateOnly(thisMonth.Year, thisMonth.Month, 1), thisMonth, "date", false);
        var monthlyExpenses = currentMonth.Sum(x => x.Amount);
        var limit = await budgetConfiguration.GetMonthlyExpenseBudgetAsync(budgetId);
        var remaining = limit.HasValue ? limit.Value - monthlyExpenses : (decimal?)null;
        HasBudgetIndicatorData = allTime.Income > 0 || allTime.Expenses > 0 || remaining.HasValue;
        BudgetIndicatorsSeries = [new ColumnSeries<double>
        {
            Name = "Montant",
            Values = remaining.HasValue ? [(double)allTime.Balance, (double)remaining.Value] : [(double)allTime.Balance],
            Fill = new SolidColorPaint(new SKColor(36, 87, 165)),
            DataLabelsPaint = new SolidColorPaint(new SKColor(23, 43, 77))
        }];
        BudgetAxes = [new Axis { Labels = remaining.HasValue ? ["Solde total", "Budget restant"] : ["Solde total"] }];
        BudgetValueAxes = [new Axis { Labeler = value => value.ToString("C0", FrenchCulture) }];
        Notify(nameof(CategorySeries)); Notify(nameof(HasCategoryExpenses)); Notify(nameof(MonthlySeries)); Notify(nameof(HasMonthlyActivity)); Notify(nameof(MonthlyAxes));
        Notify(nameof(IncomeByMonthSeries)); Notify(nameof(HasIncomeByMonth)); Notify(nameof(IncomeByMonthAxes));
        Notify(nameof(BudgetIndicatorsSeries)); Notify(nameof(HasBudgetIndicatorData)); Notify(nameof(BudgetAxes)); Notify(nameof(BudgetValueAxes));
    }

    private async Task SelectBudget(int budgetId)
    {
        await budgetService.SetActiveBudgetAsync(budgetId);
        await Load();
    }

    private async Task ManageBudgets()
    {
        var window = new BudgetManagerWindow(budgetService) { Owner = Application.Current.MainWindow };
        if (window.ShowDialog() == true) await Load();
    }

    private void SetPage(bool charts)
    {
        IsDashboardPage = !charts;
        Notify(nameof(IsDashboardPage));
        Notify(nameof(IsChartsPage));
        if (charts) _ = LoadCharts();
    }

    private void SetGraphRange(string period)
    {
        var today = DateTime.Today;
        switch (period)
        {
            case "Année en cours": GraphStartDate = new DateTime(today.Year, 1, 1); GraphEndDate = new DateTime(today.Year, 12, 31); break;
            case "Année précédente": GraphStartDate = new DateTime(today.Year - 1, 1, 1); GraphEndDate = new DateTime(today.Year - 1, 12, 31); break;
            case "12 derniers mois": GraphStartDate = new DateTime(today.Year, today.Month, 1).AddMonths(-11); GraphEndDate = today; break;
            case "30 derniers jours": GraphStartDate = today.AddDays(-29); GraphEndDate = today; break;
        }
    }

    private bool SetValue<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    private async Task ApplyFilters()
    {
        try
        {
            var rows = await service.SearchAsync(selectedBudget?.Id ?? 1, SearchText, FilterType?.Value, FilterCategory,
                StartDate is null ? null : DateOnly.FromDateTime(StartDate.Value),
                EndDate is null ? null : DateOnly.FromDateTime(EndDate.Value),
                SelectedSort.StartsWith("Montant", StringComparison.Ordinal) ? "amount" : "date",
                SelectedSort.EndsWith("↑", StringComparison.Ordinal));
            Transactions.Clear();
            foreach (var row in rows) Transactions.Add(row);
        }
        catch (ArgumentException ex)
        {
            Transactions.Clear();
            StatusMessage = ex.Message;
        }
    }

    private async Task ResetFilters()
    {
        SearchText = ""; FilterType = FilterTypes[0]; FilterCategory = null;
        StartDate = null; EndDate = null; SelectedSort = SortOptions[0];
        await ApplyFilters();
    }

    private async Task Add()
    {
        if (string.IsNullOrWhiteSpace(Description) || string.IsNullOrWhiteSpace(Category) || TransactionDate is null ||
            !decimal.TryParse(Amount.Replace('.', ','), NumberStyles.Number, FrenchCulture, out var value) || value <= 0)
        {
            StatusMessage = "Vérifiez le libellé, la catégorie, la date et le montant (ex. : 45,90).";
            return;
        }

        await service.AddAsync(SelectedBudget?.Id ?? throw new InvalidOperationException("Sélectionnez un budget."), new Transaction
        {
            Description = Description.Trim(), Category = Category.Trim(), Amount = value,
            Date = DateOnly.FromDateTime(TransactionDate.Value), Type = SelectedType
        });
        ClearAddForm();
        StatusMessage = "Transaction enregistrée avec succès.";
        await Load();
    }

    private async Task Edit(Transaction transaction)
    {
        var dialog = new EditTransactionWindow(service, SelectedBudget?.Id ?? 1, transaction, Categories.ToArray());
        if (dialog.ShowDialog() == true)
        {
            StatusMessage = "Transaction modifiée.";
            await Load();
        }
    }

    private async Task Delete(Transaction transaction)
    {
        if (ModernDialogWindow.Ask("Confirmer la suppression",
                $"Supprimer la transaction « {transaction.Description} » de {transaction.Amount.ToString("C", FrenchCulture)} ?",
                "Supprimer", "Annuler") != "Supprimer")
            return;

        await service.DeleteAsync(SelectedBudget?.Id ?? 1, transaction.Id);
        StatusMessage = "Transaction supprimée.";
        await Load();
    }

    private async Task SaveBudget()
    {
        if (!decimal.TryParse(BudgetInput.Replace('.', ','), NumberStyles.Number, FrenchCulture, out var amount) || amount <= 0)
        {
            StatusMessage = "Saisissez un plafond mensuel supérieur à zéro (ex. : 1 200,00).";
            return;
        }
        await budgetConfiguration.SetMonthlyExpenseBudgetAsync(SelectedBudget?.Id ?? 1, amount);
        StatusMessage = "Plafond mensuel enregistré.";
        await Load();
    }

    private async Task ClearBudget()
    {
        await budgetConfiguration.SetMonthlyExpenseBudgetAsync(SelectedBudget?.Id ?? 1, null);
        StatusMessage = "Plafond mensuel effacé.";
        await Load();
    }

    private async Task ResetBudget()
    {
        if (ModernDialogWindow.Ask("Réinitialiser mon budget",
                "Cette action effacera toutes les transactions et les données du budget. Les catégories par défaut, votre plafond mensuel, la configuration de l’application et les sauvegardes existantes seront conservés. Continuer ?",
                "Réinitialiser", "Annuler") != "Réinitialiser")
            return;
        var backup = ModernDialogWindow.Ask("Sauvegarde préalable",
            "Créer une sauvegarde avant la réinitialisation ?", "Sauvegarder", "Continuer sans sauvegarde", "Annuler");
        if (backup == "Annuler" || backup is null) return;
        if (backup == "Sauvegarder") transfer.CreateBackup();

        await service.ResetAsync(SelectedBudget?.Id ?? 1);
        await ResetFilters();
        ClearAddForm();
        await Load();
        StatusMessage = "Votre budget a été réinitialisé.";
    }

    private void ClearAddForm()
    {
        Description = Amount = "";
        Category = "Alimentation";
        SelectedType = TransactionType.Expense;
        TransactionDate = DateTime.Today;
    }

    private void CreateBackup() => ModernDialogWindow.ShowInformation("Sauvegarde créée", transfer.CreateBackup());

    private async Task Export()
    {
        var dialog = new SaveFileDialog { Filter = "Fichier BudgetLocal (*.json)|*.json", FileName = "budgetlocal-export.json" };
        if (dialog.ShowDialog() == true) await transfer.ExportAsync(SelectedBudget?.Id ?? 1, dialog.FileName);
    }

    private async Task Import()
    {
        var dialog = new OpenFileDialog { Filter = "Fichier BudgetLocal (*.json)|*.json" };
        if (dialog.ShowDialog() == true) { await transfer.ImportAsync(SelectedBudget?.Id ?? 1, dialog.FileName); await Load(); }
    }

    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed record TransactionTypeChoice(TransactionType? Value, string Label)
{
    public override string ToString() => Label;
}
public sealed class Relay(Action<object?> action) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? p) => true;
    public void Execute(object? p) => action(p);
}
public sealed class AsyncRelay : ICommand
{
    private readonly Func<object?, Task> action;
    public AsyncRelay(Func<Task> action) => this.action = _ => action();
    public AsyncRelay(Func<object?, Task> action) => this.action = action;
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? p) => true;
    public async void Execute(object? p)
    {
        try { await action(p); }
        catch (Exception ex) { ModernDialogWindow.ShowInformation("BudgetLocal", ex.Message); }
    }
}


