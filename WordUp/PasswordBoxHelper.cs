using System.Windows;
using System.Windows.Controls;

namespace WordUp;

public static class PasswordBoxHelper
{
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword",
            typeof(string),
            typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnBoundPasswordChanged));

    private static readonly DependencyProperty IsUpdatingProperty =
        DependencyProperty.RegisterAttached(
            "IsUpdating",
            typeof(bool),
            typeof(PasswordBoxHelper),
            new PropertyMetadata(false));

    public static string GetBoundPassword(DependencyObject dependencyObject)
    {
        return dependencyObject.GetValue(BoundPasswordProperty) as string ?? string.Empty;
    }

    public static void SetBoundPassword(DependencyObject dependencyObject, string? value)
    {
        dependencyObject.SetValue(BoundPasswordProperty, value ?? string.Empty);
    }

    private static bool GetIsUpdating(DependencyObject dependencyObject)
    {
        return (bool)dependencyObject.GetValue(IsUpdatingProperty);
    }

    private static void SetIsUpdating(DependencyObject dependencyObject, bool value)
    {
        dependencyObject.SetValue(IsUpdatingProperty, value);
    }

    private static void OnBoundPasswordChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not PasswordBox passwordBox)
        {
            return;
        }

        passwordBox.PasswordChanged -= OnPasswordChanged;

        if (!GetIsUpdating(passwordBox))
        {
            passwordBox.Password = e.NewValue?.ToString() ?? string.Empty;
        }

        passwordBox.PasswordChanged += OnPasswordChanged;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox)
        {
            return;
        }

        SetIsUpdating(passwordBox, true);
        SetBoundPassword(passwordBox, passwordBox.Password);
        SetIsUpdating(passwordBox, false);
    }
}
