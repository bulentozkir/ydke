[CmdletBinding()]
param([switch]$CheckGooglePage)
$ErrorActionPreference = 'Stop'
$config = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\src\YDKE.Windows\CloudConfig.cs') -Raw -Encoding utf8
$key = [regex]::Match($config, 'FirebaseApiKey\s*=\s*"([^"]+)"').Groups[1].Value
if (-not $key) { throw 'Firebase public application configuration is missing.' }
Add-Type -AssemblyName System.Net.Http
$client = [Net.Http.HttpClient]::new()
try {
    $project = $client.GetAsync("https://identitytoolkit.googleapis.com/v1/projects?key=$key").GetAwaiter().GetResult()
    try {
        if ($project.IsSuccessStatusCode) {
            $projectConfig = $project.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
            Write-Output "DOMAINS localhostAllowed=$($projectConfig.authorizedDomains -contains 'localhost') loopbackIpAllowed=$($projectConfig.authorizedDomains -contains '127.0.0.1') firebaseHostAllowed=$($projectConfig.authorizedDomains -contains 'udsp-9fedc.firebaseapp.com')"
        }
        else { Write-Output "DOMAINS status=$([int]$project.StatusCode)" }
    }
    finally { $project.Dispose() }
    foreach ($continueUri in @('http://127.0.0.1:54682/', 'http://localhost:54682/')) {
        $body = @{ providerId='google.com'; continueUri=$continueUri; authFlowType='CODE_FLOW' } | ConvertTo-Json -Compress
        $content = [Net.Http.StringContent]::new($body, [Text.Encoding]::UTF8, 'application/json')
        $response = $null
        try {
            $response = $client.PostAsync("https://identitytoolkit.googleapis.com/v1/accounts:createAuthUri?key=$key", $content).GetAwaiter().GetResult()
            $json = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
            if (-not $response.IsSuccessStatusCode) {
                Write-Output "PROVIDER continue=$continueUri status=$([int]$response.StatusCode) error=$($json.error.message)"
                continue
            }
            $authorization = [Uri]$json.authUri
            $query = @{}
            foreach ($part in $authorization.Query.TrimStart('?').Split('&')) {
                $pair = $part.Split('=', 2)
                $query[[Uri]::UnescapeDataString($pair[0])] = if ($pair.Length -eq 2) { [Uri]::UnescapeDataString($pair[1]) } else { '' }
            }
            $redirect = [Uri]$query['redirect_uri']
            Write-Output "PROVIDER continue=$continueUri status=200 authorizationHost=$($authorization.Host) authorizationPath=$($authorization.AbsolutePath) responseType=$($query['response_type']) callback=$redirect statePresent=$(-not [string]::IsNullOrEmpty($query['state'])) sessionPresent=$(-not [string]::IsNullOrEmpty($json.sessionId)) clientIdPresent=$(-not [string]::IsNullOrEmpty($query['client_id']))"
            if ($CheckGooglePage) {
                $page = $client.GetAsync($authorization).GetAwaiter().GetResult()
                try {
                    $html = $page.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                    $errors = @('redirect_uri_mismatch', 'invalid_client', 'unauthorized_client', 'disallowed_useragent') | Where-Object { $html.Contains($_) }
                    Write-Output "GOOGLE_PAGE status=$([int]$page.StatusCode) host=$($page.RequestMessage.RequestUri.Host) path=$($page.RequestMessage.RequestUri.AbsolutePath) detectedErrors=$($errors -join ',')"
                }
                finally { $page.Dispose() }
            }
        }
        finally { if ($response) { $response.Dispose() }; $content.Dispose() }
    }
}
finally { $client.Dispose() }