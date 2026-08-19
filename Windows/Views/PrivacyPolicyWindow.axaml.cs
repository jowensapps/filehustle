using Avalonia.Controls;
using Avalonia.Interactivity;
using FileHustle.Data;

namespace FileHustle.Views;

/// FileHustle's privacy policy — shown automatically on first launch, before
/// the tutorial (see MainWindow), and reachable anytime from About.
public partial class PrivacyPolicyWindow : Window
{
    public PrivacyPolicyWindow()
    {
        InitializeComponent();
        Closed += (_, _) => PrivacyPolicyState.HasSeenPrivacyPolicy = true;
    }

    private void OnUnderstandClicked(object? sender, RoutedEventArgs e) => Close();
}
