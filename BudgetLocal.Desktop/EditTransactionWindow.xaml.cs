using System.Globalization;
using System.Windows;
using BudgetLocal.Core.Models;
using BudgetLocal.Core.Services;
using BudgetLocal.Desktop.ViewModels;

namespace BudgetLocal.Desktop;

public partial class EditTransactionWindow : Window
{
    private readonly TransactionEditSession session;

    public IReadOnlyList<TransactionTypeChoice> TransactionTypes { get; } =
        [new(TransactionType.Expense, "Dépense"), new(TransactionType.Income, "Revenu")];
    public IReadOnlyList<string> Categories { get; }

    public EditTransactionWindow(TransactionService service, int budgetId, Transaction original, IReadOnlyList<string> categories)
    {
        Categories = categories;
        InitializeComponent();
        session = new TransactionEditSession(service, budgetId, original);
        DataContext = session.Draft;
        TransactionDatePicker.SelectedDate = original.Date.ToDateTime(TimeOnly.MinValue);
        AmountInput.Text = original.Amount.ToString("0.00", CultureInfo.GetCultureInfo("fr-FR"));
        Owner = Application.Current.MainWindow;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (TransactionDatePicker.SelectedDate is null)
        {
            ErrorText.Text = "Sélectionnez une date valide.";
            return;
        }

        if (!decimal.TryParse(AmountInput.Text.Replace('.', ','), NumberStyles.Number,
                CultureInfo.GetCultureInfo("fr-FR"), out var amount) || amount <= 0)
        {
            ErrorText.Text = "Saisissez un montant supérieur à zéro (ex. : 45,90).";
            return;
        }

        session.Draft.Amount = amount;
        session.Draft.Date = DateOnly.FromDateTime(TransactionDatePicker.SelectedDate.Value);
        try
        {
            await session.SaveAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        session.Cancel();
        DialogResult = false;
    }
}

