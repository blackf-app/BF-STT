using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;

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
                // GitHub API requires User-Agent
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("BF-STT", "1.0"));

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var release = JsonSerializer.Deserialize<GitHubRelease>(json, options);

                if (release == null || string.IsNullOrEmpty(release.TagName))
                    return null;

                // Normalize version strings (remove 'v' prefix if present)
                var latestVersionStr = release.TagName.TrimStart('v');
                var currentVersionStr = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

                if (IsNewerVersion(latestVersionStr, currentVersionStr))
                {
                    // Find the asset that is an .exe file
                    var asset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
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

        private bool IsNewerVersion(string latest, string current)
        {
            if (Version.TryParse(latest, out var latestV) && Version.TryParse(current, out var currentV))
            {
                return latestV > currentV;
            }
            return false;
        }

        public async Task DownloadAndInstallUpdateAsync(string downloadUrl)
        {
            try
            {
                var tempFilePath = Path.Combine(Path.GetTempPath(), "BF-STT-Update.exe");
                
                // 1. Download the new version
                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    using (var fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }

                // 2. Prepare the updater script
                var currentExePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(currentExePath))
                {
                    throw new Exception("Could not determine current executable path.");
                }

                var updaterScriptPath = Path.Combine(Path.GetTempPath(), "BF_STT_Updater.ps1");
                
                // The powershell script will:
                // - Wait for the main app to close
                // - Copy the new exe over the old one
                // - Start the new exe
                // - Delete itself
                var scriptContent = $@"
$currentExe = '{currentExePath}'
$tempExe = '{tempFilePath}'
$processName = [System.IO.Path]::GetFileNameWithoutExtension($currentExe)

# 1. Wait for the main process to exit
Write-Output 'Waiting for $processName to exit...'
while (Get-Process -Name $processName -ErrorAction SilentlyContinue) {{
    Stop-Process -Name $processName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
}}

# 2. Replace the executable
Write-Output 'Replacing executable...'
try {{
    Move-Item -Path $tempExe -Destination $currentExe -Force
}} catch {{
    Write-Error 'Failed to replace executable: $_'
    exit
}}

# 3. Restart the application
Write-Output 'Restarting application...'
Start-Process -FilePath $currentExe

# 4. Cleanup script
Remove-Item -Path $MyInvocation.MyCommand.Path -Force
";
                await File.WriteAllTextAsync(updaterScriptPath, scriptContent);

                // 3. Launch the updater script
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{updaterScriptPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(startInfo);

                // 4. Shutdown the current application
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Lỗi trong quá trình cập nhật: {ex.Message}", "Lỗi Cập Nhật", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
