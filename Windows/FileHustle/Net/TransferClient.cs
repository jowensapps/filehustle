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

    public static async Task SendAsync(List<SendableItem> items, Peer peer)
    {
        var tempZips = new List<string>();
        try
        {
            var prepared = new List<(TransferItem Item, string Path)>();
            foreach (var item in items)
            {
                if (item.IsFolder)
                {
                    var zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
                    var folderName = new DirectoryInfo(item.Path).Name;
                    ZipFile.CreateFromDirectory(item.Path, zipPath);
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

                foreach (var (_, path) in prepared)
                {
                    await using var fileStream = File.OpenRead(path);
                    await fileStream.CopyToAsync(stream);
                }

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
