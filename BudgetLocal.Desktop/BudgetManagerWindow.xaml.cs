using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using BudgetLocal.Core.Models;
using BudgetLocal.Core.Services;

namespace BudgetLocal.Desktop;

public partial class BudgetManagerWindow : Window
{
    private readonly BudgetService service;
    private bool loading;
    private Budget? Selected => BudgetList.SelectedItem as Budget;

    public BudgetManagerWindow(BudgetService service)
    {
        this.service = service;
        InitializeComponent();
        Loaded += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync(int? selectedId = null)
    {
        loading = true;
        var items = await service.GetAllAsync();
        BudgetList.ItemsSource = items;
        BudgetList.DisplayMemberPath = nameof(Budget.Name);
        BudgetList.SelectedItem = items.FirstOrDefault(x => x.Id == selectedId) ?? items.FirstOrDefault();
        loading = false;
        if (Selected is null) ClearInputs();
        else ShowSelectedBudget();
    }

    private void BudgetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!loading) ShowSelectedBudget();
    }

    private void ShowSelectedBudget()
    {
        if (Selected is not { } budget) return;
        NameInput.Text = budget.Name;
        DescriptionInput.Text = budget.Description ?? "";
        LimitInput.Text = budget.MonthlyExpenseBudget?.ToString("0.00", CultureInfo.GetCultureInfo("fr-BE")) ?? "";
        StatusText.Text = "";
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        BudgetList.SelectedItem = null;
        ClearInputs();
        NameInput.Focus();
    }

    private void ClearInputs()
    {
        NameInput.Text = "";
        DescriptionInput.Text = "";
        LimitInput.Text = "";
        StatusText.Text = "Nouveau budget";
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        decimal? limit = null;
        if (!string.IsNullOrWhiteSpace(LimitInput.Text))
        {
            if (!decimal.TryParse(LimitInput.Text.Replace('.', ','), NumberStyles.Number,
                    CultureInfo.GetCultureInfo("fr-BE"), out var amount) || amount <= 0)
            {
                StatusText.Text = "Saisissez un plafond supérieur à zéro ou laissez le champ vide.";
                return;
            }
            limit = amount;
        }

        try
        {
            if (Selected is { } budget)
            {
                await service.UpdateAsync(budget.Id, NameInput.Text, DescriptionInput.Text, limit);
                await ReloadAsync(budget.Id);
                StatusText.Text = "Budget modifié.";
            }
            else
            {
                var created = await service.CreateAsync(NameInput.Text, DescriptionInput.Text, limit);
                await ReloadAsync(created.Id);
                StatusText.Text = "Budget créé.";
            }
            DialogResult = true;
        }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } budget) { StatusText.Text = "Sélectionnez un budget à supprimer."; return; }
        if (ModernDialogWindow.Ask("Supprimer le budget", $"Supprimer « {budget.Name} » ? Cette action est définitive.", "Supprimer", "Annuler") != "Supprimer") return;
        try
        {
            await service.DeleteAsync(budget.Id);
            await ReloadAsync();
            StatusText.Text = "Budget supprimé.";
            DialogResult = true;
        }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
