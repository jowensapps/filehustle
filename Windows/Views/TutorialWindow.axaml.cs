using Avalonia.Controls;
using Avalonia.Interactivity;
using FileHustle.Data;

namespace FileHustle.Views;

/// Explains the two things that trip people up: how device names work, and
/// how to actually get a received file out of the app. Shown automatically
/// on first launch (see MainWindow) and reachable anytime from About.
public partial class TutorialWindow : Window
{
    public TutorialWindow()
    {
        InitializeComponent();
        Closed += (_, _) => TutorialState.HasSeenTutorial = true;
    }

    private void OnGotItClicked(object? sender, RoutedEventArgs e) => Close();
}
