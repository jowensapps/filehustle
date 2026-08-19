using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FileHustle.Views;

/// Developer credits — who made the app and how to reach them.
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    private void OnCrossClicked(object? sender, EventArgs e)
    {
        GospelPromptOverlay.IsVisible = true;
    }

    private void OnGospelPromptCloseClicked(object? sender, RoutedEventArgs e)
    {
        GospelPromptOverlay.IsVisible = false;
    }

    private void OnGospelPromptMoreClicked(object? sender, RoutedEventArgs e)
    {
        GospelPromptOverlay.IsVisible = false;
        new GospelWindow().ShowDialog(this);
    }

    private void OnReportBugClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "mailto:j.owens.apps@gmail.com?subject=FileHustle",
                UseShellExecute = true,
            });
        }
        catch
        {
            // No mail client configured — nothing sensible to do here.
        }
    }

    private void OnViewTutorialClicked(object? sender, RoutedEventArgs e) => new TutorialWindow().ShowDialog(this);
}
