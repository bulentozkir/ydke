using System.IO;
using Microsoft.Web.WebView2.Core;

namespace TopWords.Windows;

internal static class PackagedContent
{
    private static readonly IReadOnlyDictionary<string, string> ContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".css"] = "text/css; charset=utf-8",
            [".html"] = "text/html; charset=utf-8",
            [".ico"] = "image/x-icon",
            [".js"] = "text/javascript; charset=utf-8",
            [".json"] = "application/json; charset=utf-8",
            [".map"] = "application/json; charset=utf-8",
            [".png"] = "image/png",
            [".svg"] = "image/svg+xml",
            [".txt"] = "text/plain; charset=utf-8",
            [".webmanifest"] = "application/manifest+json; charset=utf-8",
            [".woff"] = "font/woff",
            [".woff2"] = "font/woff2",
            [".xml"] = "application/xml; charset=utf-8",
        };

    public static bool IsRequest(string requestUri) =>
        Uri.TryCreate(requestUri, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        string.Equals(uri.Host, AppConfig.VirtualHostName, StringComparison.OrdinalIgnoreCase);

    public static async Task<CoreWebView2WebResourceResponse> CreateResponseAsync(
        CoreWebView2Environment environment,
        string requestUri)
    {
        var path = ResolvePath(requestUri, AppConfig.WebAppFolder);
        if (path is null)
        {
            return CreateTextResponse(environment, 404, "Not Found", "Not found");
        }

        try
        {
            var content = await File.ReadAllBytesAsync(path);
            var stream = new MemoryStream(content, writable: false);
            var contentType = ContentTypes.GetValueOrDefault(
                Path.GetExtension(path),
                "application/octet-stream");

            return environment.CreateWebResourceResponse(
                stream,
                200,
                "OK",
                $"Content-Type: {contentType}\r\n" +
                "Cache-Control: no-store\r\n" +
                "X-Content-Type-Options: nosniff");
        }
        catch (IOException)
        {
            return CreateTextResponse(environment, 500, "Internal Server Error", "Unable to read packaged content");
        }
        catch (UnauthorizedAccessException)
        {
            return CreateTextResponse(environment, 403, "Forbidden", "Access denied");
        }
    }

    internal static string? ResolvePath(string requestUri, string rootFolder)
    {
        if (!IsRequest(requestUri) ||
            !Uri.TryCreate(requestUri, UriKind.Absolute, out var uri))
        {
            return null;
        }

        try
        {
            var relativePath = Uri.UnescapeDataString(uri.AbsolutePath)
                .TrimStart('/')
                .Replace('/', Path.DirectorySeparatorChar);

            if (string.IsNullOrEmpty(relativePath) ||
                string.Equals(relativePath, "review", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(relativePath, "review.html", StringComparison.OrdinalIgnoreCase))
            {
                relativePath = "index.html";
            }

            var root = Path.GetFullPath(rootFolder)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(Path.Combine(root, relativePath));

            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (Directory.Exists(candidate))
            {
                candidate = Path.Combine(candidate, "index.html");
            }

            if (!File.Exists(candidate) && string.IsNullOrEmpty(Path.GetExtension(candidate)))
            {
                candidate += ".html";
            }

            return File.Exists(candidate) ? candidate : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static CoreWebView2WebResourceResponse CreateTextResponse(
        CoreWebView2Environment environment,
        int statusCode,
        string reasonPhrase,
        string body)
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body), writable: false);
        return environment.CreateWebResourceResponse(
            stream,
            statusCode,
            reasonPhrase,
            "Content-Type: text/plain; charset=utf-8\r\nCache-Control: no-store");
    }
}