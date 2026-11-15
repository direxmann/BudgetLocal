using System.Windows;
using BudgetLocal.Core.Services;
using BudgetLocal.Desktop.ViewModels;

namespace BudgetLocal.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(TransactionService transactions, BudgetConfigurationService budget, BudgetService budgets, DataTransferService transfer)
    {
        InitializeComponent();
        DataContext = new MainViewModel(transactions, budget, budgets, transfer);
    }
}
