using System.Net.Http.Headers;

namespace PCWaechter.LiveInstaller;

public static class Download
{
    public static async Task DownloadFileAsync(HttpClient http, string url, string targetPath, IProgress<long>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        // Basic download (no resume). Add resume later if needed.
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.Add(new ProductInfoHeaderValue("PCWaechterLiveInstaller", "1.0"));

        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        await using var input = await resp.Content.ReadAsStreamAsync(ct);
        await using var output = File.Create(targetPath);

        var buffer = new byte[1024 * 256];
        long total = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, 0, buffer.Length, ct);
            if (read <= 0) break;
            await output.WriteAsync(buffer, 0, read, ct);
            total += read;
            progress?.Report(total);
        }
    }
}
