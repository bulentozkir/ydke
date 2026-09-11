using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace YDKE_Windows;

// A transport result, NOT an authenticated native session. The caller must exchange
// IdToken with FirebaseAuthService.SignInWithGoogleIdTokenAsync before trusting it.
internal sealed record GoogleBrowserSignInResult(string? IdToken, string? ErrorMessage)
{
    public override string ToString() =>
        $"GoogleBrowserSignInResult {{ IdToken = {(IdToken is null ? "null" : "[redacted]")}, ErrorMessage = {ErrorMessage} }}";
}

internal static class GoogleBrowserSignIn
{
    internal const int MaximumBodyBytes = 64 * 1024;
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private const int BindAttempts = 8;

    // Only these codes can cross the browser/native boundary. Never echo SDK error
    // messages, customData, accounts, HTML, request URLs or exception messages.
    internal static IReadOnlyList<string> BrowserErrorCodes { get; } = Array.AsReadOnly(new[]
    {
        "auth/popup-closed-by-user", "auth/cancelled-popup-request", "auth/page-closed",
        "auth/network-request-failed", "auth/unauthorized-domain", "auth/operation-not-allowed",
        "auth/invalid-api-key", "auth/account-exists-with-different-credential",
        "auth/web-storage-unsupported", "auth/too-many-requests", "auth/internal-error",
        "auth/missing-id-token", "auth/sdk-load-failed", "auth/sign-in-failed",
    });

    public static Task<GoogleBrowserSignInResult> RequestIdTokenAsync(
        CancellationToken cancellationToken, GoogleSignInBrowser browser = GoogleSignInBrowser.Default, string language = "en")
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(new GoogleBrowserSignInResult(null, null));
        if (!Enum.IsDefined(browser))
            return Task.FromResult(new GoogleBrowserSignInResult(null,
                "Choose System default, Microsoft Edge or Google Chrome for Google sign-in."));

        return RunAsync(cancellationToken, page => GoogleBrowserLauncher.LaunchSignInPage(page, browser), DefaultTimeout, language);
    }

    // Per-call injection only: no global browser/state override and no authentication
    // service, credential store or external HTTP client in this transport.
    internal static async Task<GoogleBrowserSignInResult> RunAsync(
        CancellationToken cancellationToken, Action<Uri> launch, TimeSpan timeout, string language = "en")
    {
        ArgumentNullException.ThrowIfNull(launch);
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > uint.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        if (cancellationToken.IsCancellationRequested) return new(null, null);

        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lifetime.CancelAfter(timeout);
        HttpListener? listener = null;
        Task<GoogleBrowserSignInResult>? receive = null;
        byte[]? stateBytes = null;
        // Stopping the listener cancels the actual pending GetContextAsync / body IO,
        // rather than abandoning them behind Task.WaitAsync.
        using var stopRegistration = lifetime.Token.Register(() => StopListener(listener));
        try
        {
            (listener, var origin) = StartListener(lifetime.Token);
            var page = new Uri(origin, $"auth/{RandomValue()}/");
            var state = RandomValue();
            var nonce = RandomValue();
            stateBytes = Encoding.ASCII.GetBytes(state);
            var html = GoogleSignInPage.CreateHtml(state, nonce, language);
            var policy = GoogleSignInPage.CreateContentSecurityPolicy(nonce);

            // Start accepting BEFORE launching, including for synchronous injected
            // launchers. All listener continuations are independent of the WinUI thread.
            receive = ReceiveAsync(listener, page, stateBytes, html, policy, lifetime.Token);
            lifetime.Token.ThrowIfCancellationRequested();
            try
            {
                launch(page);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException
                or IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                lifetime.Token.ThrowIfCancellationRequested();
                return new(null, "Could not open the browser for Google sign-in. Check your browser settings or choose another available browser.");
            }

            var result = await receive.ConfigureAwait(false);
            lifetime.Token.ThrowIfCancellationRequested();
            return result;
        }
        catch (Exception ex) when (lifetime.IsCancellationRequested
            && ex is OperationCanceledException or HttpListenerException or ObjectDisposedException or IOException or InvalidOperationException)
        {
            return cancellationToken.IsCancellationRequested
                ? new(null, null)
                : new(null, "Google sign-in timed out. Please try again.");
        }
        catch (Exception ex) when (ex is HttpListenerException or SocketException or IOException or InvalidOperationException
            or UnauthorizedAccessException or System.Security.SecurityException or NotSupportedException or OperationCanceledException)
        {
            return new(null, "Could not run the local Google sign-in page. Check local network permissions and try again.");
        }
        finally
        {
            lifetime.Cancel();
            StopListener(listener);
            if (receive is not null)
            {
                try { await receive.ConfigureAwait(false); }
                catch (Exception ex) when (ex is OperationCanceledException or HttpListenerException or ObjectDisposedException
                    or IOException or InvalidOperationException) { }
            }
            listener?.Close();
            if (stateBytes is not null) CryptographicOperations.ZeroMemory(stateBytes);
        }
    }

    private static (HttpListener Listener, Uri Origin) StartListener(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < BindAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // The OS chooses a free port on loopback, never a wildcard interface.
            using var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var origin = new Uri($"http://localhost:{port.ToString(CultureInfo.InvariantCulture)}/");
            var listener = new HttpListener { AuthenticationSchemes = AuthenticationSchemes.Anonymous };
            try
            {
                listener.Prefixes.Add(origin.AbsoluteUri);
                listener.Start();
                return (listener, origin);
            }
            catch (HttpListenerException ex) when (ex.ErrorCode is 32 or 183 or 10048)
            {
                // The probe cannot reserve an HTTP.sys prefix; retry the bounded race.
                listener.Close();
            }
            catch
            {
                listener.Close();
                throw;
            }
        }
        throw new InvalidOperationException("No local sign-in port was available.");
    }

    private static async Task<GoogleBrowserSignInResult> ReceiveAsync(
        HttpListener listener, Uri page, byte[] state, string html, string policy, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var context = await listener.GetContextAsync().ConfigureAwait(false);
            using var requestLifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            requestLifetime.CancelAfter(RequestTimeout);
            // Also abort a slow/disconnected request: one partial body must not occupy
            // the entire five-minute attempt, and cancellation must release its IO.
            using var abortRegistration = requestLifetime.Token.Register(() => AbortResponse(context.Response));
            try
            {
                var result = await HandleRequestAsync(context, page, state, html, policy, requestLifetime.Token).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (result is not null) return result; // Exactly one accepted, fully validated payload.
            }
            catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException or OperationCanceledException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Bad/disconnected requests are not an authorization outcome.
            }
            finally
            {
                try { context.Response.Close(); }
                catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or IOException) { }
            }
        }
    }

    private static async Task<GoogleBrowserSignInResult?> HandleRequestAsync(
        HttpListenerContext context, Uri page, byte[] state, string html, string policy, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var response = context.Response;
        SetSecurityHeaders(response, policy);
        var origin = page.GetLeftPart(UriPartial.Authority);

        // HTTP.sys host routing is not a substitute for validating both the peer and
        // the literal Host header. No 127.0.0.1 alias, trailing dot or rebinding origin.
        if (!IsLoopbackPeer(request.RemoteEndPoint?.Address)
            || !IsLoopbackPeer(request.LocalEndPoint?.Address) || request.LocalEndPoint?.Port != page.Port
            || !IsExpectedHost(request.Headers["Host"], page.Port)
            || request.Url is not { } url || url.Scheme != Uri.UriSchemeHttp
            || url.Host != "localhost" || url.Port != page.Port || url.UserInfo.Length != 0)
            return await RejectAsync(context, 403, cancellationToken).ConfigureAwait(false);

        if (request.HttpMethod is not ("GET" or "POST"))
        {
            response.Headers["Allow"] = request.RawUrl == page.AbsolutePath + "result" ? "POST" : "GET";
            return await RejectAsync(context, 405, cancellationToken).ConfigureAwait(false);
        }

        // RawUrl, not the normalized path, rejects query strings, dot segments,
        // escaped slashes and alternate spellings of this attempt's path.
        if (string.Equals(request.RawUrl, page.AbsolutePath, StringComparison.Ordinal))
        {
            if (request.HttpMethod != "GET")
            {
                response.Headers["Allow"] = "GET";
                return await RejectAsync(context, 405, cancellationToken).ConfigureAwait(false);
            }
            await WriteResponseAsync(response, 200, "text/html; charset=utf-8", html, cancellationToken).ConfigureAwait(false);
            return null;
        }
        if (!string.Equals(request.RawUrl, page.AbsolutePath + "result", StringComparison.Ordinal))
            return await RejectAsync(context, 404, cancellationToken).ConfigureAwait(false);
        if (request.HttpMethod != "POST")
        {
            response.Headers["Allow"] = "POST";
            return await RejectAsync(context, 405, cancellationToken).ConfigureAwait(false);
        }

        // Check CSRF before reading any body. No OPTIONS handling and no CORS headers.
        if (!string.Equals(request.Headers["Origin"], origin, StringComparison.Ordinal)
            || !MatchesState(request.Headers["X-YDKE-State"], state))
            return await RejectAsync(context, 403, cancellationToken).ConfigureAwait(false);
        if (!IsJsonContentType(request.ContentType) || request.Headers["Content-Encoding"] is not null)
            return await RejectAsync(context, 415, cancellationToken).ConfigureAwait(false);
        if (request.ContentLength64 > MaximumBodyBytes)
            return await RejectAsync(context, 413, cancellationToken).ConfigureAwait(false);
        if (request.ContentLength64 == 0)
            return await RejectAsync(context, 400, cancellationToken).ConfigureAwait(false);

        var buffer = new byte[MaximumBodyBytes + 1];
        try
        {
            var length = 0;
            while (length < buffer.Length)
            {
                var read = await request.InputStream.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false);
                if (read == 0) break;
                length += read;
            }
            if (length > MaximumBodyBytes)
                return await RejectAsync(context, 413, cancellationToken).ConfigureAwait(false);
            if (!TryReadResult(buffer.AsMemory(0, length), out var result))
                return await RejectAsync(context, 400, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            // Acknowledge transport delivery only, never claim Firebase/native sign-in.
            try
            {
                await WriteResponseAsync(response, 200, "application/json; charset=utf-8", "{\"received\":true}", cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // A browser disconnect after a fully validated payload must not let
                // a second callback replace the first accepted transport result.
            }
            return result;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    internal static bool IsLoopbackPeer(IPAddress? address) => address is not null
        && IPAddress.IsLoopback(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address);

    internal static bool IsExpectedHost(string? host, int port) =>
        string.Equals(host, "localhost:" + port.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static bool MatchesState(string? supplied, byte[] expected)
    {
        if (supplied is null || supplied.Length != expected.Length
            || supplied.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_'))) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(supplied), expected);
    }

    private static bool IsJsonContentType(string? value) =>
        MediaTypeHeaderValue.TryParse(value, out var parsed)
        && string.Equals(parsed.MediaType, "application/json", StringComparison.OrdinalIgnoreCase)
        && parsed.Parameters.Count <= 1
        && parsed.Parameters.All(p => string.Equals(p.Name, "charset", StringComparison.OrdinalIgnoreCase)
            && string.Equals(p.Value?.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase));

    private static bool TryReadResult(ReadOnlyMemory<byte> payload, out GoogleBrowserSignInResult? result)
    {
        result = null;
        try
        {
            using var json = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 4 });
            if (json.RootElement.ValueKind != JsonValueKind.Object) return false;
            var properties = json.RootElement.EnumerateObject();
            if (!properties.MoveNext()) return false;
            var property = properties.Current;
            if (properties.MoveNext() || property.Value.ValueKind != JsonValueKind.String) return false;
            var value = property.Value.GetString();
            if (string.IsNullOrEmpty(value)) return false;
            if (property.NameEquals("idToken"))
            {
                // Lexical sanity only, NOT JWT validation. Firebase is the authority.
                if (value.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or '.'))) return false;
                result = new(value, null);
                return true;
            }
            if (property.NameEquals("error") && BrowserErrorCodes.Contains(value, StringComparer.Ordinal))
            {
                result = new(null, $"Google sign-in did not finish ({value}). Please try again from YDKE.");
                return true;
            }
        }
        catch (Exception ex) when (ex is JsonException or DecoderFallbackException or InvalidOperationException) { }
        return false;
    }

    private static async Task<GoogleBrowserSignInResult?> RejectAsync(HttpListenerContext context, int status, CancellationToken token)
    {
        if (context.Request.HttpMethod == "HEAD")
        {
            // Body-free for EVERY rejected HEAD, including bad Host/peer requests.
            // HttpListener otherwise throws a protocol error and ends the attempt.
            context.Response.StatusCode = status;
            context.Response.ContentLength64 = 0;
        }
        else
        {
            await WriteResponseAsync(context.Response, status, "application/json; charset=utf-8", "{\"received\":false}", token).ConfigureAwait(false);
        }
        return null;
    }

    private static async Task WriteResponseAsync(
        HttpListenerResponse response, int status, string contentType, string content, CancellationToken token)
    {
        response.StatusCode = status;
        response.ContentType = contentType;
        var bytes = Encoding.UTF8.GetBytes(content);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, token).ConfigureAwait(false);
    }

    private static void SetSecurityHeaders(HttpListenerResponse response, string policy)
    {
        response.KeepAlive = false;
        response.Headers["Cache-Control"] = "no-store, max-age=0";
        response.Headers["Pragma"] = "no-cache";
        response.Headers["Referrer-Policy"] = "no-referrer";
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["Content-Security-Policy"] = policy;
        response.Headers["X-Frame-Options"] = "DENY";
        response.Headers["Cross-Origin-Opener-Policy"] = "same-origin-allow-popups";
        response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
        response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    }

    private static string RandomValue() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static void StopListener(HttpListener? listener)
    {
        try { listener?.Stop(); }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException) { }
    }

    private static void AbortResponse(HttpListenerResponse response)
    {
        try { response.Abort(); }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException) { }
    }
}