using System.Buffers.Binary;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RoxyzsPortal;

internal sealed record Entry(string InternalName, string Name, string Author, Version AssemblyVersion,
    string? Description, string? Punchline, string? IconUrl, string RepoUrl, bool IsHide, long? CreatedAt, long? LastUpdate);

internal sealed record Snapshot(string? ETag, DateTimeOffset Checked, Entry[] Entries);
internal sealed record CatalogResult(Snapshot Data, bool Offline);

internal sealed class Catalog(string directory, HttpClient http)
{
    public const string Index = "https://raw.githubusercontent.com/Roxyz0501/DalamudPluginRepo/main/repo.json";
    private const int MaxBody = 1024 * 1024;
    private string CachePath => Path.Combine(directory, "catalog.json");
    private readonly SemaphoreSlim iconGate = new(2);
    public static bool ValidRawUrl(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.IsDefaultPort && uri.Host == "raw.githubusercontent.com"
        && uri.AbsolutePath.StartsWith("/Roxyz0501/", StringComparison.OrdinalIgnoreCase);

    internal static Entry[] Parse(byte[] body)
    {
        var options = new JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString };
        var entries = JsonSerializer.Deserialize<Entry[]>(body, options) ?? throw new InvalidDataException();
        if (entries.Length > 256) throw new InvalidDataException("Catalog exceeds supported size");
        return entries.Where(x => x is { IsHide: false, Author: "Roxyz0501", AssemblyVersion: not null }
            && !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.InternalName))
            .DistinctBy(x => x.InternalName, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.LastUpdate).ThenBy(x => x.Name, StringComparer.Ordinal).ToArray();
    }

    public async Task<CatalogResult> Load(Snapshot? previous, bool force, CancellationToken token)
    {
        if (previous is null)
        {
            try
            {
                if (new FileInfo(CachePath).Length <= MaxBody * 2)
                {
                    var cached = JsonSerializer.Deserialize<Snapshot>(await File.ReadAllBytesAsync(CachePath, token));
                    if (cached?.Entries is { Length: <= 256 } rows && rows.All(x => x?.Name != null && x.InternalName != null && x.AssemblyVersion != null))
                        previous = cached;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        }
        if (!force && previous != null && DateTimeOffset.UtcNow - previous.Checked < TimeSpan.FromMinutes(10))
            return new(previous, false);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Index);
            if (EntityTagHeaderValue.TryParse(previous?.ETag, out var tag)) request.Headers.IfNoneMatch.Add(tag);
            using var response = await http.SendAsync(request, token);
            if (response.StatusCode == HttpStatusCode.NotModified && previous != null)
                return new(previous with { Checked = DateTimeOffset.UtcNow }, false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsByteArrayAsync(token);
            var next = new Snapshot(response.Headers.ETag?.ToString(), DateTimeOffset.UtcNow, Parse(body));
            try
            {
                Directory.CreateDirectory(directory);
                await File.WriteAllBytesAsync(CachePath + ".tmp", JsonSerializer.SerializeToUtf8Bytes(next), token);
                File.Move(CachePath + ".tmp", CachePath, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            return new(next, false);
        }
        catch (Exception) when (!token.IsCancellationRequested && previous != null) { return new(previous, true); }
    }

    public async Task<string?> Icon(Entry entry, CancellationToken token)
    {
        if (!ValidRawUrl(entry.IconUrl)) return null;
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(entry.IconUrl + "|" + entry.AssemblyVersion)));
        var folder = Path.Combine(directory, "icons");
        var path = Path.Combine(folder, key + ".png");
        await iconGate.WaitAsync(token);
        try
        {
            if (File.Exists(path)) return path;
            Directory.CreateDirectory(folder);
            byte[] bytes = await http.GetByteArrayAsync(entry.IconUrl, token);
            if (bytes.Length < 33 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})
                || BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16)) is 0 or > 1024
                || BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20)) is 0 or > 1024) return null;
            await File.WriteAllBytesAsync(path + ".tmp", bytes, token);
            File.Move(path + ".tmp", path, true);
            foreach (var stale in new DirectoryInfo(folder).GetFiles("*.png").OrderByDescending(x=>x.LastWriteTimeUtc).Skip(64))
                stale.Delete();
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or OperationCanceledException) { return null; }
        finally { iconGate.Release(); }
    }
}
