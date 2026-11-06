using System.IO.Compression;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FileHustle.Data;

namespace FileHustle.Net;

/// Mirrors iOS/Android's TransferClient. See docs/protocol.md.
///
/// Unlike iOS, .NET's Stream/TcpClient async APIs accept a CancellationToken
/// that actually interrupts the underlying I/O — no equivalent of the
/// withTaskCancellationHandler workaround needed here for the connect/ack
/// timeout (see filehustle-app memory for that iOS bug's history).
public static class TransferClient
{
    public class DeclinedException() : Exception("declined");
    public class TimedOutException() : Exception("timed out");

    private const int AckTimeoutMs = 60_000;

    public enum SendPhase { Preparing, WaitingForAccept, Sending }

    public readonly record struct SendProgress(SendPhase Phase, long BytesSent, long TotalBytes);

    // How often byte-level progress is pushed to the UI — a report per
    // 64KB chunk would flood the dispatcher on a fast LAN.
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(50);

    public static async Task SendAsync(List<SendableItem> items, Peer peer, IProgress<SendProgress>? progress = null)
    {
        var tempZips = new List<string>();
        try
        {
            progress?.Report(new SendProgress(SendPhase.Preparing, 0, 0));
            var prepared = new List<(TransferItem Item, string Path)>();
            foreach (var item in items)
            {
                if (item.IsFolder)
                {
                    var zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
                    var folderName = new DirectoryInfo(item.Path).Name;
                    // Off the UI thread — a big folder can take a while to zip.
                    await Task.Run(() => ZipFile.CreateFromDirectory(item.Path, zipPath));
                    tempZips.Add(zipPath);
                    prepared.Add((
                        new TransferItem { Name = folderName, RelativePath = folderName, Size = new FileInfo(zipPath).Length, IsFolder = true },
                        zipPath));
                }
                else
                {
                    var name = Path.GetFileName(item.Path);
                    prepared.Add((
                        new TransferItem { Name = name, RelativePath = name, Size = new FileInfo(item.Path).Length, IsFolder = false },
                        item.Path));
                }
            }

            var header = new TransferHeader
            {
                SenderId = DeviceIdentity.Id,
                SenderName = DeviceIdentity.Name,
                TotalBytes = prepared.Sum(p => p.Item.Size),
                Items = prepared.Select(p => p.Item).ToList(),
            };
            var itemNames = header.Items.Select(i => i.Name).ToList();

            try
            {
                using var client = new TcpClient();
                using var timeoutCts = new CancellationTokenSource(AckTimeoutMs);

                try
                {
                    await client.ConnectAsync(peer.Host, peer.Port, timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    throw new TimedOutException();
                }

                progress?.Report(new SendProgress(SendPhase.WaitingForAccept, 0, header.TotalBytes));
                await using var stream = client.GetStream();
                var headerBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(header) + "\n");
                await stream.WriteAsync(headerBytes, timeoutCts.Token);

                var ackBuffer = new byte[1];
                int ackRead;
                try
                {
                    ackRead = await stream.ReadAsync(ackBuffer, timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    throw new TimedOutException();
                }
                if (ackRead == 0) throw new IOException("connection closed before ack");
                if (ackBuffer[0] != 1) throw new DeclinedException();

                progress?.Report(new SendProgress(SendPhase.Sending, 0, header.TotalBytes));
                long sent = 0;
                var lastReport = DateTime.UtcNow;
                var buffer = new byte[64 * 1024];
                foreach (var (_, path) in prepared)
                {
                    await using var fileStream = File.OpenRead(path);
                    int read;
                    while ((read = await fileStream.ReadAsync(buffer)) > 0)
                    {
                        await stream.WriteAsync(buffer.AsMemory(0, read));
                        sent += read;
                        if (DateTime.UtcNow - lastReport >= ProgressInterval)
                        {
                            lastReport = DateTime.UtcNow;
                            progress?.Report(new SendProgress(SendPhase.Sending, sent, header.TotalBytes));
                        }
                    }
                }
                progress?.Report(new SendProgress(SendPhase.Sending, sent, header.TotalBytes));

                TransferHistoryStore.Record(peer.Name, itemNames, header.TotalBytes, TransferDirection.Sent, TransferStatus.Completed);
            }
            catch (TimedOutException)
            {
                TransferHistoryStore.Record(peer.Name, itemNames, header.TotalBytes, TransferDirection.Sent, TransferStatus.TimedOut);
                throw;
            }
            catch (DeclinedException)
            {
                TransferHistoryStore.Record(peer.Name, itemNames, header.TotalBytes, TransferDirection.Sent, TransferStatus.Declined);
                throw;
            }
            catch (Exception)
            {
                TransferHistoryStore.Record(peer.Name, itemNames, header.TotalBytes, TransferDirection.Sent, TransferStatus.Failed);
                throw;
            }
        }
        finally
        {
            foreach (var zip in tempZips)
            {
                try { File.Delete(zip); } catch { /* best effort cleanup */ }
            }
        }
    }
}
