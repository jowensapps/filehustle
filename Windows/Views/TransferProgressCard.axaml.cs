using Avalonia.Controls;
using FileHustle.Data;

namespace FileHustle.Views;

/// The floating card shown while a transfer is in flight — used both by
/// the sender ("Sending to …") and by the receiver once it has accepted
/// ("Receiving from …"), so both ends see the same ring fill up.
public partial class TransferProgressCard : UserControl
{
    public TransferProgressCard()
    {
        InitializeComponent();
    }

    /// No byte count yet (zipping, waiting on Accept) — spin instead.
    public void ShowIndeterminate(string title, string subtitle)
    {
        Ring.IsIndeterminate = true;
        CenterText.Text = "";
        TitleText.Text = title;
        SubtitleText.Text = subtitle;
    }

    public void ShowProgress(string title, long done, long total)
    {
        var fraction = total > 0 ? (double)done / total : 1;
        Ring.IsIndeterminate = false;
        Ring.Value = fraction;
        CenterText.Text = $"{(int)Math.Floor(fraction * 100)}%";
        TitleText.Text = title;
        SubtitleText.Text = $"{ByteFormat.Format(done)} of {ByteFormat.Format(total)}";
    }

    public void ShowDone(string title, string subtitle)
    {
        Ring.IsIndeterminate = false;
        Ring.Value = 1;
        CenterText.Text = "✓";
        TitleText.Text = title;
        SubtitleText.Text = subtitle;
    }

    /// Stops the spin timer — call when the card's overlay is hidden, since
    /// hiding a parent doesn't change the ring's own IsVisible.
    public void Reset() => Ring.IsIndeterminate = false;
}
