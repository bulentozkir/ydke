using System.Text.RegularExpressions;

namespace YDKE_Windows;

internal static class OfflineArchitectureContracts
{
    public static void Verify(string root, string sourceDirectory, Action<bool, string> check)
    {
        var assertions = 0;
        var failures = 0;
        void Test(bool condition, string message)
        {
            assertions++;
            if (!condition) failures++;
            check(condition, "Offline architecture: " + message);
        }

        var projectPath = Path.Combine(sourceDirectory, "YDKE.Windows.csproj");
        Test(File.Exists(projectPath), "native project file must exist for packaging checks.");
        if (!File.Exists(projectPath)) return;
        var project = File.ReadAllText(projectPath);
        foreach (var fragment in new[]
        {
            "<Content Include=\"..\\..\\..\\data\\*.js\"",
            "Link=\"Data\\%(Filename)%(Extension)\"",
            "CopyToOutputDirectory=\"PreserveNewest\"",
            "CopyToPublishDirectory=\"PreserveNewest\"",
        })
            Test(project.Contains(fragment, StringComparison.Ordinal),
                "packaging must ship bundled vocabulary data for offline study: " + fragment);

        var repositoryPath = Path.Combine(sourceDirectory, "VocabularyRepository.cs");
        Test(File.Exists(repositoryPath), "VocabularyRepository.cs must exist.");
        if (File.Exists(repositoryPath))
        {
            var repository = SourceAudit.WithoutComments(File.ReadAllText(repositoryPath));
            Test(repository.Contains("Path.Combine(AppContext.BaseDirectory, \"Data\", language.Files[effectiveLevel])", StringComparison.Ordinal),
                "word loading must resolve local packaged Data files.");
            Test(repository.Contains("File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken)", StringComparison.Ordinal),
                "word loading must read local files rather than network endpoints.");
            Test(!Regex.IsMatch(repository, @"\b(?:HttpClient|HttpWebRequest|WebClient|ClientWebSocket|Windows\.Web\.Http)\b", RegexOptions.CultureInvariant),
                "VocabularyRepository must not introduce HTTP clients.");
        }

        var sourceFiles = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path) != "MainWindow.xaml.cs")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Test(sourceFiles.Length > 0, "expected C# source files for runtime audit.");

        var forbidden = new Regex(@"\b(?:HttpClient|HttpRequestMessage|HttpResponseMessage|HttpWebRequest|WebClient|SocketsHttpHandler|ClientWebSocket|WebSocket|TcpClient|UdpClient|Socket|Dns|Ping|RestClient|Windows\.Web\.Http|Windows\.Networking\.Connectivity|BeginGoogleSignInAsync|CloudSaveAsync|CloudLoadAsync|CloudDeleteAsync|DeleteCloudProfileAsync)\b",
            RegexOptions.CultureInvariant);
        var offenders = new List<string>();
        foreach (var path in sourceFiles)
        {
            var source = SourceAudit.WithoutComments(File.ReadAllText(path));
            foreach (Match match in forbidden.Matches(source))
            {
                offenders.Add($"{Path.GetFileName(path)}:{match.Value}");
                if (offenders.Count >= 8) break;
            }
            if (offenders.Count >= 8) break;
        }
        Test(offenders.Count == 0,
            "runtime source must remain local-only with no network client APIs. Found: " + string.Join(", ", offenders));

        var shellPath = Path.Combine(sourceDirectory, "MainPage.Shell.cs");
        Test(File.Exists(shellPath), "MainPage.Shell.cs must exist.");
        if (File.Exists(shellPath))
        {
            var shell = SourceAudit.WithoutComments(File.ReadAllText(shellPath));
            Test(shell.Contains("link.Click += (_, _) => OpenAboutLink(address);", StringComparison.Ordinal),
                "external links must stay user-initiated.");
            Test(shell.Contains("Process.Start(new ProcessStartInfo(address) { UseShellExecute = true })", StringComparison.Ordinal),
                "external links must use shell execution, not embedded network clients.");
        }

        var allowedUrls = new HashSet<string>(StringComparer.Ordinal)
        {
            "https://polyformproject.org/licenses/noncommercial/1.0.0",
        };
        var urlViolations = new List<string>();
        foreach (var path in sourceFiles)
        {
            var fileName = Path.GetFileName(path);
            var source = SourceAudit.WithoutComments(File.ReadAllText(path));
            foreach (Match match in Regex.Matches(source, "https?://[^\"\\s]+", RegexOptions.CultureInvariant))
            {
                if (!allowedUrls.Contains(match.Value))
                    urlViolations.Add($"{fileName}:{match.Value}");
            }
        }
        Test(urlViolations.Count == 0, "unexpected HTTP/HTTPS endpoint in runtime source: " + string.Join(", ", urlViolations));

        var readmePath = Path.Combine(root, "README.md");
        Test(File.Exists(readmePath), "README.md must exist for offline guidance audit.");
        if (File.Exists(readmePath))
        {
            var readme = File.ReadAllText(readmePath);
            var activeReadme = readme.Contains("## Archived Documents", StringComparison.Ordinal)
                ? readme[..readme.IndexOf("## Archived Documents", StringComparison.Ordinal)]
                : readme;
            Test(Regex.IsMatch(activeReadme, @"without\s+an\s+internet\s+connection", RegexOptions.IgnoreCase),
                "README must explicitly state that study content works without internet.");
            Test(activeReadme.Contains("local-only", StringComparison.OrdinalIgnoreCase),
                "README must keep the local-only architecture statement.");
        }

        var settingsPath = Path.Combine(sourceDirectory, "MainPage.Settings.cs");
        if (File.Exists(settingsPath))
        {
            var settings = SourceAudit.WithoutComments(File.ReadAllText(settingsPath));
            Test(settings.Contains("SettingsText(T(\"Storage.Notice\"), 18)", StringComparison.Ordinal),
                "settings must keep the local-storage notice in the grown-up section.");
        }

        Console.WriteLine($"OFFLINE_ARCH checks={assertions} errors={failures} files={sourceFiles.Length}");
    }
}