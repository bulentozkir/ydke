using System.Net;
using YDKE_Windows;

// A separate, explicitly selected mode. It serves the REAL production page and
// headers, not a copy. No coordinator, credential store, Firebase override, fake
// callback, or normal browser launcher is used. The parent owns its test browser.
internal static class BrowserPageProbe
{
    internal static async Task<int> RunAsync()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(110));
        ConsoleCancelEventHandler cancel = (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancel;
        Uri? page = null;
        // Console readers can block synchronously even through ReadLineAsync.
        // A background EOF monitor must not delay listener startup or the hard
        // deadline; this opt-in console process exits when RunAsync returns.
        _ = Task.Run(() => CancelOnInputAsync(cancellation));
        var exitCode = 0;
        try
        {
            var result = await GoogleBrowserSignIn.RunAsync(cancellation.Token, launchedPage =>
            {
                page = launchedPage;
                // The actual, ephemeral, query/fragment-free test-attempt path is
                // the ONLY URL allowed on stdout. State, nonce and OAuth URLs are not.
                Console.WriteLine("PROBE_URL=" + launchedPage.AbsoluteUri);
            }, TimeSpan.FromSeconds(110), "en");

            if (result.IdToken is not null)
            {
                // Never print or exchange an unexpected credential.
                Console.WriteLine("PROBE_OUTCOME=unexpected-token");
                exitCode = 1;
            }
            else if (result.ErrorMessage is not null)
            {
                var code = GoogleBrowserSignIn.BrowserErrorCodes.FirstOrDefault(
                    code => result.ErrorMessage.Contains("(" + code + ")", StringComparison.Ordinal));
                Console.WriteLine("PROBE_OUTCOME=" + (code ?? "native-error-or-timeout"));
                exitCode = 1;
            }
            else
            {
                Console.WriteLine("PROBE_OUTCOME=cancelled-without-authentication");
            }
        }
        catch (Exception ex)
        {
            // Exception messages can contain request data; print the type only.
            Console.WriteLine("PROBE_ERROR_TYPE=" + ex.GetType().Name);
            exitCode = 2;
        }
        finally
        {
            cancellation.Cancel();
            Console.CancelKeyPress -= cancel;
            if (page is not null)
            {
                try
                {
                    // HTTP.sys can retain a TCP port. Re-registering the exact
                    // prefix is a stronger cleanup check than connection refusal.
                    using var check = new HttpListener();
                    check.Prefixes.Add(page.GetLeftPart(UriPartial.Authority) + "/");
                    check.Start();
                    check.Stop();
                    Console.WriteLine("PROBE_LISTENER_RELEASED=true");
                }
                catch (Exception ex) when (ex is HttpListenerException or InvalidOperationException)
                {
                    Console.WriteLine("PROBE_LISTENER_RELEASED=false");
                    exitCode = 2;
                }
            }
        }
        return exitCode;
    }

    private static async Task CancelOnInputAsync(CancellationTokenSource cancellation)
    {
        try
        {
            // One line (or parent EOF) ends the attempt; this is not a user prompt.
            await Console.In.ReadLineAsync(cancellation.Token);
        }
        catch (OperationCanceledException) { return; }
        catch (ObjectDisposedException) { return; }
        catch (IOException) { }
        try { cancellation.Cancel(); }
        catch (ObjectDisposedException) { /* The bounded probe has already ended. */ }
    }
}