using System.Globalization;
using System.Windows.Data;
using BudgetLocal.Core.Models;

namespace BudgetLocal.Desktop;

public sealed class TransactionTypeLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is TransactionType.Income ? "Revenu" : "Dépense";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() == "Revenu" ? TransactionType.Income : TransactionType.Expense;
}
