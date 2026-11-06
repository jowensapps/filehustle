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

    public enum ReceivePhase { Receiving, Completed, Failed }

    public readonly record struct ReceiveProgress(string SenderName, ReceivePhase Phase, long BytesReceived, long TotalBytes);

    /// Fires (from a background thread) once a request has been accepted
    /// and bytes start arriving, periodically while they do, and once more
    /// with Completed/Failed at the end — drives the receiving-side
    /// progress ring so an accepted transfer is visibly incoming.
    public static event Action<ReceiveProgress>? ReceiveProgressChanged;

    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(50);

    private static TcpListener? _listener;
    private static TaskCompletionSource<bool>? _pendingDecision;
    private static readonly object DecisionLock = new();

    private static readonly string ReceivedFilesDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "FileHustle", "Received");

    static TransferServer()
    {
        Directory.CreateDirectory(ReceivedFilesDir);
        // Also on launch, not just on close — catches a previous session
        // that crashed or was killed before its Closing handler ran.
        ClearReceivedFiles();
    }

    /// Every top-level path this app has written into ReceivedFilesDir and
    /// not yet cleaned up, one per line. Cleanup deletes ONLY what's listed
    /// here, never "everything in the folder" — that folder can hold files
    /// from older versions (which kept everything) that the user still
    /// relies on, and a blanket sweep would silently destroy them.
    private static readonly string ManifestPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FileHustle", "received-this-session.txt");
    private static readonly object ManifestLock = new();

    /// Recorded BEFORE the bytes are written, so a receive cut short by a
    /// crash still gets cleaned up on the next launch.
    private static void TrackReceivedPath(string path)
    {
        lock (ManifestLock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)!);
            File.AppendAllLines(ManifestPath, new[] { path });
        }
    }

    /// Received files are only meant to live as long as the session's
    /// Recent Transfers list (which is in-memory only) — "Save as…" is how
    /// you keep one. Best effort: anything still locked (e.g. a receive
    /// interrupted by closing the app) stays in the manifest for the next
    /// launch to retry.
    public static void ClearReceivedFiles()
    {
        lock (ManifestLock)
        {
            if (!File.Exists(ManifestPath)) return;
            var root = Path.GetFullPath(ReceivedFilesDir) + Path.DirectorySeparatorChar;
            var remaining = new List<string>();
            foreach (var line in File.ReadAllLines(ManifestPath).Distinct())
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var full = Path.GetFullPath(line);
                // Never delete outside the Received folder, whatever the manifest says.
                if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
                    else if (File.Exists(full)) File.Delete(full);
                }
                catch
                {
                    remaining.Add(full);
                }
            }
            if (remaining.Count == 0) File.Delete(ManifestPath);
            else File.WriteAllLines(ManifestPath, remaining);
        }
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
                List<string> itemPaths;
                try
                {
                    itemPaths = await ReceiveBodyAsync(header, stream);
                }
                catch
                {
                    ReceiveProgressChanged?.Invoke(new ReceiveProgress(header.SenderName, ReceivePhase.Failed, 0, header.TotalBytes));
                    throw;
                }
                RecordHistory(header, TransferStatus.Completed, itemPaths);
                ReceiveProgressChanged?.Invoke(new ReceiveProgress(header.SenderName, ReceivePhase.Completed, header.TotalBytes, header.TotalBytes));
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
        long received = 0;
        var lastReport = DateTime.UtcNow;
        ReceiveProgressChanged?.Invoke(new ReceiveProgress(header.SenderName, ReceivePhase.Receiving, 0, header.TotalBytes));
        void OnBytes(int count)
        {
            received += count;
            if (DateTime.UtcNow - lastReport < ProgressInterval) return;
            lastReport = DateTime.UtcNow;
            ReceiveProgressChanged?.Invoke(new ReceiveProgress(header.SenderName, ReceivePhase.Receiving, received, header.TotalBytes));
        }

        foreach (var item in header.Items)
        {
            var dest = UniquePath(Path.Combine(ReceivedFilesDir, Sanitize(item.RelativePath)));
            TrackReceivedPath(dest);
            if (item.IsFolder)
            {
                await ReceiveFolderItemAsync(item, dest, stream, OnBytes);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                await using var fileStream = File.Create(dest);
                await ReadExactlyAsync(stream, fileStream, item.Size, OnBytes);
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
    private static async Task ReceiveFolderItemAsync(TransferItem item, string dest, NetworkStream stream, Action<int> onBytes)
    {
        var zipPath = Path.GetTempFileName();
        try
        {
            await using (var zipFileStream = File.Create(zipPath))
            {
                await ReadExactlyAsync(stream, zipFileStream, item.Size, onBytes);
            }

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

    private static async Task ReadExactlyAsync(Stream input, Stream output, long count, Action<int>? onBytes = null)
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
            onBytes?.Invoke(read);
        }
    }

    /// "photo.jpg" → "photo (1).jpg" if taken, so a new transfer never
    /// overwrites (or, for folders, deletes) something already there.
    private static string UniquePath(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var stem = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var n = 1; ; n++)
        {
            var candidate = Path.Combine(dir, $"{stem} ({n}){ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
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
