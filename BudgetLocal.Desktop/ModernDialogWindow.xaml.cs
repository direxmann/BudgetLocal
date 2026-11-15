using System.Windows;
using System.Windows.Controls;

namespace BudgetLocal.Desktop;

public partial class ModernDialogWindow : Window
{
    public string? Result { get; private set; }

    private ModernDialogWindow(string title, string message, IReadOnlyList<string> options)
    {
        InitializeComponent();
        Title = title;
        DialogTitle.Text = title;
        DialogMessage.Text = message;
        foreach (var option in options)
        {
            var button = new Button { Content = option, MinWidth = 92, Tag = option };
            button.Click += (_, _) => { Result = option; Close(); };
            DialogButtons.Children.Add(button);
        }
        Loaded += (_, _) =>
        {
            if (Owner is not null) WindowStartupLocation = WindowStartupLocation.CenterOwner;
        };
    }

    public static string? Ask(string title, string message, params string[] options)
    {
        var dialog = new ModernDialogWindow(title, message, options);
        if (Application.Current?.MainWindow is { IsVisible: true } owner)
            dialog.Owner = owner;
        dialog.ShowDialog();
        return dialog.Result;
    }

    public static void ShowInformation(string title, string message) =>
        Ask(title, message, "Fermer");
}

