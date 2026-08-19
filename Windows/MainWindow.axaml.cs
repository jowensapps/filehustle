using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FileHustle.Data;
using FileHustle.Net;
using FileHustle.Views;

namespace FileHustle;

public partial class MainWindow : Window
{
    private Peer? _nicknameTargetPeer;

    public MainWindow()
    {
        InitializeComponent();

        DeviceNameText.Text = DeviceIdentity.Name;

        PeersList.ItemsSource = PeerDiscovery.Peers;
        HistoryList.ItemsSource = TransferHistoryStore.Entries;

        PeerDiscovery.Peers.CollectionChanged += (_, _) => NoPeersText.IsVisible = PeerDiscovery.Peers.Count == 0;
        TransferHistoryStore.Entries.CollectionChanged += (_, _) => NoHistoryText.IsVisible = TransferHistoryStore.Entries.Count == 0;

        TransferServer.IncomingRequestChanged += OnIncomingRequestChanged;

        Opened += async (_, _) =>
        {
            var port = TransferServer.Start();
            PeerDiscovery.StartAdvertising(port);
            PeerDiscovery.StartBrowsing();
            PeerDiscovery.Start();

            if (!PrivacyPolicyState.HasSeenPrivacyPolicy)
            {
                await new PrivacyPolicyWindow().ShowDialog(this);
            }

            if (!TutorialState.HasSeenTutorial)
            {
                await new TutorialWindow().ShowDialog(this);
            }
        };
        Closing += (_, _) =>
        {
            TransferServer.Stop();
            PeerDiscovery.Stop();
        };
    }

    private void OnIncomingRequestChanged(TransferHeader? header)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (header == null)
            {
                IncomingOverlay.IsVisible = false;
                return;
            }

            IncomingTitleText.Text = $"{header.SenderName} wants to send you";
            IncomingItemsList.ItemsSource = header.Items.Select(i => (i.IsFolder ? "📁 " : "") + i.Name).ToList();
            IncomingSizeText.Text = ByteFormat.Format(header.TotalBytes);
            IncomingOverlay.IsVisible = true;
        });
    }

    private void OnAcceptClicked(object? sender, RoutedEventArgs e) => TransferServer.Respond(true);

    private void OnDeclineClicked(object? sender, RoutedEventArgs e) => TransferServer.Respond(false);

    private void OnErrorOkClicked(object? sender, RoutedEventArgs e) => ErrorOverlay.IsVisible = false;

    private void OnAboutClicked(object? sender, RoutedEventArgs e) => new AboutWindow().ShowDialog(this);

    /// A peer's broadcast Name regenerates every launch (see DeviceIdentity),
    /// so this lets you attach a stable nickname to its Id instead — the
    /// only way to actually recognize the same device across sessions.
    private void OnSetNicknameClicked(object? sender, RoutedEventArgs e)
    {
        var peer = (Peer)((Button)sender!).Tag!;
        _nicknameTargetPeer = peer;
        NicknamePromptText.Text = $"Give {peer.Name} a name you'll recognize next time — its broadcast name changes every launch.";
        NicknameTextBox.Text = PeerNicknames.Nickname(peer.Id) ?? "";
        NicknameOverlay.IsVisible = true;
    }

    private void OnNicknameSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (_nicknameTargetPeer != null)
        {
            PeerNicknames.SetNickname(_nicknameTargetPeer.Id, NicknameTextBox.Text);
            RefreshPeersList();
        }
        NicknameOverlay.IsVisible = false;
    }

    private void OnNicknameClearClicked(object? sender, RoutedEventArgs e)
    {
        if (_nicknameTargetPeer != null)
        {
            PeerNicknames.SetNickname(_nicknameTargetPeer.Id, null);
            RefreshPeersList();
        }
        NicknameOverlay.IsVisible = false;
    }

    private void OnNicknameCancelClicked(object? sender, RoutedEventArgs e) => NicknameOverlay.IsVisible = false;

    /// Peer is a plain record with no change notification, so DisplayName/
    /// HasNickname bindings won't pick up a nickname edit on their own —
    /// force a rebind by reassigning ItemsSource.
    private void RefreshPeersList()
    {
        PeersList.ItemsSource = null;
        PeersList.ItemsSource = PeerDiscovery.Peers;
    }

    /// Received files land in FileHustle's own Documents subfolder with no
    /// obvious way to actually use them — this opens the first item with
    /// its default associated app (or Explorer, for a folder), which is
    /// the natural "get it out of the app" action on Windows.
    private void OnOpenReceivedClicked(object? sender, RoutedEventArgs e)
    {
        var entry = (TransferHistoryEntry)((Button)sender!).Tag!;
        var path = entry.ItemPaths.FirstOrDefault();
        if (path == null) return;

        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowError($"Couldn't open that file: {ex.Message}");
        }
    }

    private async void OnSendFilesClicked(object? sender, RoutedEventArgs e)
    {
        var peer = (Peer)((Button)sender!).Tag!;
        var topLevel = GetTopLevel(this)!;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Send Files",
            AllowMultiple = true,
        });
        if (files.Count == 0) return;
        await SendFilesAsync(files.Select(f => f.Path.LocalPath).ToList(), peer);
    }

    private async void OnSendFolderClicked(object? sender, RoutedEventArgs e)
    {
        var peer = (Peer)((Button)sender!).Tag!;
        var topLevel = GetTopLevel(this)!;
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Send Folder",
            AllowMultiple = false,
        });
        if (folders.Count == 0) return;
        await SendFolderAsync(folders[0].Path.LocalPath, peer);
    }

    /// Bypasses the system file picker (matches iOS/Android's debug hook) so
    /// discovery + transfer can be exercised end-to-end without manual
    /// interaction — this app has no headless UI test harness on Windows
    /// either, so the same pattern applies here.
    private void OnSendTestFileClicked(object? sender, RoutedEventArgs e)
    {
        var peer = (Peer)((Button)sender!).Tag!;
        var path = Path.Combine(Path.GetTempPath(), $"filehustle-test-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.txt");
        File.WriteAllText(path, $"Hello from FileHustle debug test, sent at {DateTime.Now}.");
        _ = SendFilesAsync(new List<string> { path }, peer);
    }

    private void OnSendTestFolderClicked(object? sender, RoutedEventArgs e)
    {
        var peer = (Peer)((Button)sender!).Tag!;
        var folder = Path.Combine(Path.GetTempPath(), $"filehustle-test-folder-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "one.txt"), "First file");
        File.WriteAllText(Path.Combine(folder, "two.txt"), "Second file");
        var nested = Path.Combine(folder, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "three.txt"), "Nested file");
        _ = SendFolderAsync(folder, peer);
    }

    private async Task SendFilesAsync(List<string> paths, Peer peer)
    {
        SendingOverlay.IsVisible = true;
        try
        {
            await TransferClient.SendAsync(paths.Select(p => new SendableItem { Path = p, IsFolder = false }).ToList(), peer);
        }
        catch (Exception ex)
        {
            ShowError(DescribeSendError(ex, peer.Name));
        }
        finally
        {
            SendingOverlay.IsVisible = false;
        }
    }

    private async Task SendFolderAsync(string path, Peer peer)
    {
        SendingOverlay.IsVisible = true;
        try
        {
            await TransferClient.SendAsync(new List<SendableItem> { new() { Path = path, IsFolder = true } }, peer);
        }
        catch (Exception ex)
        {
            ShowError(DescribeSendError(ex, peer.Name));
        }
        finally
        {
            SendingOverlay.IsVisible = false;
        }
    }

    private static string DescribeSendError(Exception ex, string peerName) => ex switch
    {
        TransferClient.DeclinedException => $"{peerName} declined the transfer.",
        TransferClient.TimedOutException => $"{peerName} didn't respond in time.",
        _ => ex.Message,
    };

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorOverlay.IsVisible = true;
    }
}
