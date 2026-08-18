using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FileHustle.Data;

namespace FileHustle.Net;

/// Mirrors iOS/Android's TransferServer: owns the TCP listener, the
/// accept/decline handshake, and writing received files to disk. See
/// docs/protocol.md.
public static class TransferServer
{
    /// Fires with the parsed header when a transfer request arrives, and
    /// with null once it's been resolved (accepted/declined) — mirrors
    /// iOS/Android's nullable `incomingRequest` state.
    public static event Action<TransferHeader?>? IncomingRequestChanged;

    private static TcpListener? _listener;
    private static TaskCompletionSource<bool>? _pendingDecision;
    private static readonly object DecisionLock = new();

    private static readonly string ReceivedFilesDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "FileHustle", "Received");

    static TransferServer()
    {
        Directory.CreateDirectory(ReceivedFilesDir);
    }

    /// Starts listening on an OS-assigned port and returns it.
    public static int Start()
    {
        var listener = new TcpListener(IPAddress.Any, 0);
        listener.Start();
        _listener = listener;
        _ = AcceptLoopAsync(listener);
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    public static void Stop()
    {
        _listener?.Stop();
        _listener = null;
    }

    /// Called by the UI once the user acts on the incoming request.
    public static void Respond(bool accept)
    {
        lock (DecisionLock)
        {
            _pendingDecision?.TrySetResult(accept);
            _pendingDecision = null;
        }
    }

    private static async Task AcceptLoopAsync(TcpListener listener)
    {
        while (true)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync();
            }
            catch (Exception)
            {
                break;
            }
            _ = HandleConnectionAsync(client);
        }
    }

    private static async Task HandleConnectionAsync(TcpClient client)
    {
        using var _ = client;
        // Hoisted so the catch block can still record a history entry for a
        // transfer that started (header parsed, sender known) but failed
        // partway through, instead of vanishing without a trace.
        TransferHeader? header = null;
        try
        {
            var stream = client.GetStream();
            var headerLine = await ReadLineAsync(stream);
            header = JsonSerializer.Deserialize<TransferHeader>(headerLine)
                     ?? throw new InvalidDataException("invalid header");

            TaskCompletionSource<bool> decision;
            lock (DecisionLock)
            {
                decision = new TaskCompletionSource<bool>();
                _pendingDecision = decision;
            }
            IncomingRequestChanged?.Invoke(header);
            var accepted = await decision.Task;
            IncomingRequestChanged?.Invoke(null);

            await stream.WriteAsync(new[] { (byte)(accepted ? 1 : 0) });
            await stream.FlushAsync();

            if (accepted)
            {
                var itemPaths = await ReceiveBodyAsync(header, stream);
                RecordHistory(header, TransferStatus.Completed, itemPaths);
            }
            else
            {
                RecordHistory(header, TransferStatus.Declined);
            }
        }
        catch
        {
            IncomingRequestChanged?.Invoke(null);
            if (header != null) RecordHistory(header, TransferStatus.Failed);
        }
    }

    private static async Task<List<string>> ReceiveBodyAsync(TransferHeader header, NetworkStream stream)
    {
        var itemPaths = new List<string>();
        foreach (var item in header.Items)
        {
            var dest = Path.Combine(ReceivedFilesDir, Sanitize(item.RelativePath));
            if (item.IsFolder)
            {
                await ReceiveFolderItemAsync(item, dest, stream);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                await using var fileStream = File.Create(dest);
                await ReadExactlyAsync(stream, fileStream, item.Size);
            }
            itemPaths.Add(dest);
        }
        return itemPaths;
    }

    /// Folder items arrive as a single zip stream (see TransferClient's
    /// folder-zipping) — buffer to a temp file, unzip into a directory
    /// named after the item, then discard the zip. `ZipFile.ExtractToDirectory`
    /// already rejects entries that would escape the destination directory
    /// (zip-slip), matching the manual sanitization Android does by hand.
    private static async Task ReceiveFolderItemAsync(TransferItem item, string dest, NetworkStream stream)
    {
        var zipPath = Path.GetTempFileName();
        try
        {
            await using (var zipFileStream = File.Create(zipPath))
            {
                await ReadExactlyAsync(stream, zipFileStream, item.Size);
            }

            if (Directory.Exists(dest)) Directory.Delete(dest, recursive: true);
            Directory.CreateDirectory(dest);
            ZipFile.ExtractToDirectory(zipPath, dest);
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    private static async Task<string> ReadLineAsync(NetworkStream stream)
    {
        var bytes = new List<byte>();
        var buffer = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0) throw new IOException("connection closed before header");
            if (buffer[0] == (byte)'\n') break;
            bytes.Add(buffer[0]);
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static async Task ReadExactlyAsync(Stream input, Stream output, long count)
    {
        var buffer = new byte[64 * 1024];
        var remaining = count;
        while (remaining > 0)
        {
            var toRead = (int)Math.Min(buffer.Length, remaining);
            var read = await input.ReadAsync(buffer.AsMemory(0, toRead));
            if (read == 0) throw new IOException("connection closed early");
            await output.WriteAsync(buffer.AsMemory(0, read));
            remaining -= read;
        }
    }

    private static string Sanitize(string relativePath) =>
        string.Join('/', relativePath.Split('/').Where(p => p.Length > 0 && p != ".."));

    private static void RecordHistory(TransferHeader header, TransferStatus status, List<string>? itemPaths = null)
    {
        TransferHistoryStore.Record(
            header.SenderName,
            header.Items.Select(i => i.Name).ToList(),
            header.TotalBytes,
            TransferDirection.Received,
            status,
            itemPaths);
    }
}
