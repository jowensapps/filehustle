using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace FileHustle.Views;

public partial class GospelWindow : Window
{
    private int _page;

    public GospelWindow()
    {
        InitializeComponent();
        BuildDots();
        RenderPage();
    }

    private void BuildDots()
    {
        DotsPanel.Children.Clear();
        for (var i = 0; i < GospelData.Sheets.Count; i++)
        {
            DotsPanel.Children.Add(new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = i == _page ? Brushes.DodgerBlue : Brushes.LightGray,
            });
        }
    }

    private void RenderPage()
    {
        var sheet = GospelData.Sheets[_page];
        PageContent.Children.Clear();

        PageContent.Children.Add(new TextBlock
        {
            Text = sheet.Title,
            FontSize = 22,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap,
        });

        PageContent.Children.Add(new TextBlock
        {
            Text = sheet.Body,
            FontSize = 15,
            TextWrapping = TextWrapping.Wrap,
        });

        foreach (var verse in sheet.Verses)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
                CornerRadius = new Avalonia.CornerRadius(8),
                Padding = new Avalonia.Thickness(12),
            };
            var stack = new StackPanel { Spacing = 6 };
            stack.Children.Add(new TextBlock
            {
                Text = $"“{verse.Text}”",
                FontStyle = FontStyle.Italic,
                TextWrapping = TextWrapping.Wrap,
            });
            stack.Children.Add(new TextBlock
            {
                Text = $"— {verse.Reference}",
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.DodgerBlue,
                FontSize = 12,
            });
            card.Child = stack;
            PageContent.Children.Add(card);
        }

        if (_page == GospelData.Sheets.Count - 1)
        {
            PageContent.Children.Add(new TextBlock
            {
                Text = "Scripture quotations are from the World English Bible.",
                FontSize = 11,
                Opacity = 0.6,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        BackButton.IsVisible = _page > 0;
        var isLast = _page == GospelData.Sheets.Count - 1;
        NextButton.Content = isLast ? "Close" : "Next";
        BuildDots();
    }

    private void OnBackClicked(object? sender, RoutedEventArgs e)
    {
        if (_page > 0) { _page--; RenderPage(); }
    }

    private void OnNextClicked(object? sender, RoutedEventArgs e)
    {
        if (_page < GospelData.Sheets.Count - 1) { _page++; RenderPage(); }
        else Close();
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
