using System.Net;
using System.Net.Sockets;
using System.Text;

namespace YDKE_Windows;

/// <summary>Code is null for a genuine user cancel (no ErrorMessage); a real failure (Google
/// error, CSRF state mismatch, timeout) also has Code null but a non-null ErrorMessage.
/// RedirectUri is the exact value used for THIS attempt -- the token exchange must reuse it.</summary>
internal sealed record GoogleAuthorizationResult(string? Code, string RedirectUri, string? ErrorMessage);

// OAuth stays in an external browser because Google rejects embedded user agents.
internal static class GoogleLoopbackListener
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromMinutes(5);

    public static async Task<GoogleAuthorizationResult> RequestAuthorizationCodeAsync(
        string clientId, string codeChallenge, string state, CancellationToken cancellationToken,
        GoogleSignInBrowser browser = GoogleSignInBrowser.Default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!GoogleAuthService.IsValidClientId(clientId)) throw new InvalidOperationException(GoogleAuthService.InvalidClientIdMessage);
        clientId = clientId.Trim();
        ArgumentException.ThrowIfNullOrWhiteSpace(codeChallenge);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        var redirectUri = $"http://127.0.0.1:{GetFreeLoopbackPort()}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(redirectUri);
        try
        {
            listener.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Could not start a local listener for Google sign-in. Another program may be using the port.", ex);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var authorizationUrl = GoogleAuthService.BuildAuthorizationUrl(clientId, codeChallenge, state, redirectUri);
            GoogleBrowserLauncher.Launch(authorizationUrl, browser);

            using var timeoutCts = new CancellationTokenSource(WaitTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            HttpListenerContext context;
            try
            {
                context = await GetContextAsync(listener, linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return new GoogleAuthorizationResult(null, redirectUri,
                    "Sign-in timed out waiting for your browser. Try again.");
            }
            catch (OperationCanceledException)
            {
                return new GoogleAuthorizationResult(null, redirectUri, null); // genuine user cancel
            }

            var result = await RespondAndParseAsync(context, state, redirectUri).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task<GoogleAuthorizationResult> RespondAndParseAsync(
        HttpListenerContext context, string expectedState, string redirectUri)
    {
        var query = context.Request.QueryString;
        var code = query["code"];
        var returnedState = query["state"];
        var error = query["error"];
        var errorDescription = query["error_description"];

        string? errorMessage;
        string page;
        if (!string.IsNullOrEmpty(error))
        {
            errorMessage = string.IsNullOrEmpty(errorDescription)
                ? $"Google reported an error: {error}"
                : $"Google reported an error: {error} ({errorDescription})";
            page = ClosePageHtml("Sign-in failed. You can close this tab and return to YDKE.");
        }
        else if (!string.Equals(returnedState, expectedState, StringComparison.Ordinal))
        {
            errorMessage = "Sign-in response failed a security check and was rejected.";
            page = ClosePageHtml("Sign-in failed a security check. You can close this tab and return to YDKE.");
        }
        else if (string.IsNullOrEmpty(code))
        {
            errorMessage = "Google did not return an authorization code.";
            page = ClosePageHtml("Sign-in did not complete. You can close this tab and return to YDKE.");
        }
        else
        {
            errorMessage = null;
            page = ClosePageHtml("Sign-in complete. You can close this tab and return to YDKE.");
        }

        await WriteResponseAsync(context.Response, page).ConfigureAwait(false);
        return new GoogleAuthorizationResult(errorMessage is null ? code : null, redirectUri, errorMessage);
    }

    // HttpListener has no CancellationToken overload; Stop() aborts a pending GetContextAsync,
    // which surfaces as ObjectDisposedException/HttpListenerException, not OperationCanceledException.
    private static async Task<HttpListenerContext> GetContextAsync(HttpListener listener, CancellationToken cancellationToken)
    {
        var contextTask = listener.GetContextAsync();
        using var registration = cancellationToken.Register(() =>
        {
            try { listener.Stop(); } catch (ObjectDisposedException) { }
        });
        try
        {
            return await contextTask.ConfigureAwait(false);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private static int GetFreeLoopbackPort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        try { return ((IPEndPoint)probe.LocalEndpoint).Port; }
        finally { probe.Stop(); }
    }

    private static string ClosePageHtml(string message) =>
        "<!doctype html><html><head><meta charset='utf-8'><title>YDKE</title></head>" +
        "<body style='font-family:sans-serif;text-align:center;margin-top:15vh;'><p>" +
        WebUtility.HtmlEncode(message) + "</p></body></html>";

    private static async Task WriteResponseAsync(HttpListenerResponse response, string html)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        response.OutputStream.Close();
    }
}
