using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BF_STT.Services.Infrastructure
{
    public class UpdateService
    {
        private readonly HttpClient _httpClient;
        private const string RepoOwner = "blackf-app";
        private const string RepoName = "BF-STT";
        private const string GitHubApiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

        public UpdateService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ReleaseInfo?> CheckForUpdateAsync()
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, GitHubApiUrl);
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("BF-STT", "1.0"));

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var release = JsonSerializer.Deserialize<GitHubRelease>(json, options);

                if (release == null || string.IsNullOrEmpty(release.TagName))
                    return null;

                var latestVersionStr = release.TagName.TrimStart('v');
                var currentVersionStr = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

                if (IsNewerVersion(latestVersionStr, currentVersionStr))
                {
                    // Pick the asset that matches the running platform.
                    // Windows installer = .exe, macOS distribution = .dmg or .zip
                    string[] preferredExtensions = OperatingSystem.IsWindows()
                        ? new[] { ".exe" }
                        : new[] { ".dmg", ".zip" };

                    var asset = release.Assets.FirstOrDefault(a =>
                        preferredExtensions.Any(ext => a.Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));

                    if (asset != null)
                    {
                        return new ReleaseInfo
                        {
                            Version = latestVersionStr,
                            DownloadUrl = asset.BrowserDownloadUrl,
                            ReleaseNotes = release.Body
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update check failed: {ex.Message}");
            }

            return null;
        }

        private static bool IsNewerVersion(string latest, string current)
        {
            if (Version.TryParse(latest, out var latestV) && Version.TryParse(current, out var currentV))
            {
                return latestV > currentV;
            }
            return false;
        }

        public async Task DownloadAndInstallUpdateAsync(string downloadUrl)
        {
            if (OperatingSystem.IsWindows())
            {
                await DownloadAndInstallWindowsAsync(downloadUrl);
            }
            else if (OperatingSystem.IsMacOS())
            {
                await DownloadAndOpenMacAsync(downloadUrl);
            }
        }

        private async Task DownloadAndInstallWindowsAsync(string downloadUrl)
        {
            try
            {
                var tempFilePath = Path.Combine(Path.GetTempPath(), "BF-STT-Update.exe");

                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    using var fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await response.Content.CopyToAsync(fs);
                }

                var currentExePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(currentExePath))
                {
                    throw new Exception("Could not determine current executable path.");
                }

                var updaterScriptPath = Path.Combine(Path.GetTempPath(), "BF_STT_Updater.ps1");
                var scriptContent = $@"
$currentExe = '{currentExePath}'
$tempExe = '{tempFilePath}'
$processName = [System.IO.Path]::GetFileNameWithoutExtension($currentExe)

Write-Output 'Waiting for $processName to exit...'
while (Get-Process -Name $processName -ErrorAction SilentlyContinue) {{
    Stop-Process -Name $processName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
}}

Write-Output 'Replacing executable...'
try {{
    Move-Item -Path $tempExe -Destination $currentExe -Force
}} catch {{
    Write-Error 'Failed to replace executable: $_'
    exit
}}

Write-Output 'Restarting application...'
Start-Process -FilePath $currentExe

Remove-Item -Path $MyInvocation.MyCommand.Path -Force
";
                await File.WriteAllTextAsync(updaterScriptPath, scriptContent);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{updaterScriptPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(startInfo);

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (Avalonia.Application.Current?.ApplicationLifetime
                        is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        desktop.Shutdown();
                    }
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Update install failed");
            }
        }

        private async Task DownloadAndOpenMacAsync(string downloadUrl)
        {
            try
            {
                var ext = Path.GetExtension(new Uri(downloadUrl).AbsolutePath);
                if (string.IsNullOrEmpty(ext)) ext = ".dmg";

                var tempFilePath = Path.Combine(Path.GetTempPath(), $"BF-STT-Update{ext}");

                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    using var fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await response.Content.CopyToAsync(fs);
                }

                // Open the downloaded file (DMG mounts; ZIP opens in Finder) so the user can install manually.
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = $"\"{tempFilePath}\"",
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Mac update download failed");
            }
        }
    }

    public class ReleaseInfo
    {
        public string Version { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
    }

    public class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("assets")]
        public GitHubAsset[] Assets { get; set; } = Array.Empty<GitHubAsset>();
    }

    public class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}
