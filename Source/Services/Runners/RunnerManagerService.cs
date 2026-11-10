using System.Diagnostics;
using File = System.IO.File;

namespace TreasureChest.Source.Services.Runners;

public class RunnerManagerService
{
    private readonly HttpClient _httpClient;
    private static readonly string[] CompoundExtensions = [
        ".tar.xz", 
        ".tar.gz", 
        ".tar.bz2", 
        ".tar.zst"
    ];

    private RunnerManagerService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TreasureChest-App/1.0");
    }

    public static RunnerInfo GetRunnerInformation(ERunnerType runnerType)
    {
        Console.Out.WriteLine($"[INFO] - Checking runner {runnerType.ToString()}...");

        var binaryName = GetBinaryName(runnerType);
        
        if (SettingsService.Instance.PreferSystemRunners)
        {
            Console.Out.WriteLine($"[INFO] - Trying to find system-installed '{binaryName}' first...");
            
            var systemPath = FindBinaryInSystemPath(binaryName);
        
            if (!string.IsNullOrWhiteSpace(systemPath))
            {
                Console.Out.WriteLine($"[INFO] - Runner {runnerType.ToString()} found in system path (native): {systemPath}");
            
                return new RunnerInfo
                {
                    Type = runnerType,
                    Name = runnerType.ToString(),
                    ExecutablePath = systemPath,
                    IsSystemInstalled = true
                };
            }

            Console.Out.WriteLine("[WARN] - Could not find the requested runner in system PATH. Falling back to downloaded version...");
        }

        var baseRuntimeDir = Path.Combine(SettingsService.Instance.RuntimesFolder, runnerType.ToString().ToLower());
        var binaryPath = FindBinaryInDownloadedPath(binaryName, baseRuntimeDir) ?? string.Empty;

        Console.Out.WriteLine(!string.IsNullOrWhiteSpace(binaryPath)
            ? $"[INFO] - Runner {runnerType.ToString()} found in local path (local): {binaryPath}"
            : $"[INFO] - Runner {runnerType.ToString()} not present in system or local paths. Download may be necessary.");

        return new RunnerInfo
        {
            Type = runnerType,
            Name = runnerType.ToString(),
            ExecutablePath = binaryPath,
            IsSystemInstalled = false
        };
    }

    public async Task<bool> InstallRunnerAsync(ERunnerType runnerType, string downloadUrl, IProgress<double>? progress = null)
    {
        await Console.Out.WriteLineAsync($"[INFO] - Installing runner {runnerType.ToString()}...");
        
        var temporaryPath = Path.Join(Path.GetTempPath(), SettingsService.ApplicationId);
        
        // First, we download the runner archive:
        var downloadedFile = await DownloadRunnerAsync(downloadUrl, temporaryPath);

        if (string.IsNullOrWhiteSpace(downloadedFile) || !File.Exists(downloadedFile))
            return false;
        
        // Now, we extract and install it on the target folder:
        var targetDir = Path.Combine(SettingsService.Instance.RuntimesFolder, runnerType.ToString().ToLower());

        try
        {
            return await ExtractDownloadedRunnerAsync(downloadedFile, targetDir);
        }
        finally
        {
            if (File.Exists(downloadedFile))
            {
                File.Delete(downloadedFile);
            }   
        }
    }

    private async Task<string?> DownloadRunnerAsync(string downloadUrl, string targetDir)
    {
        await Console.Out.WriteLineAsync($"[INFO] - Downloading runner from URL: {downloadUrl}");
        await Console.Out.WriteLineAsync($"[INFO] - Download will be saved into: {targetDir}");
        
        Directory.CreateDirectory(targetDir);
        
        // Checking if we can find the extension from the download URL:
        var targetFileExtension = ExtractExtensionFromDownloadUrl(downloadUrl);
        var targetFile = Path.Combine(targetDir, $"{Guid.NewGuid()}{targetFileExtension}");

        try
        {
            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write);
            await stream.CopyToAsync(fileStream);
            
            await Console.Out.WriteLineAsync($"[INFO] - Download complete! Final file: {targetFile}");

            return targetFile;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ERROR] - Failed to download the requested runner: {e.Message}]");
            return null;
        }
    }

    private async Task<bool> ExtractDownloadedRunnerAsync(string filePath, string targetDir)
    {
        await Console.Out.WriteLineAsync($"[INFO] - Extracting and installing runner from file: {filePath}");
        await Console.Out.WriteLineAsync($"[INFO] - Install path: {targetDir}");
        
        Directory.CreateDirectory(targetDir);
        
        if (!File.Exists(filePath))
            return false;
        
        try
        {
            var fileDirectory = Path.GetDirectoryName(filePath);
            var fileName = Path.GetFileName(filePath);
            
            if (string.IsNullOrWhiteSpace(fileDirectory) || string.IsNullOrWhiteSpace(fileName))
                return false;
            
            string programName;
            string arguments;
            
            // If we downloaded a .deb file, we need to extract the binary from it. Otherwise, we can simply
            if (Path.GetExtension(filePath) == ".deb")
            {
                programName = "bash";
                arguments = $"-c \"cd '{targetDir}' && ar x {filePath} && tar -xf data.tar.* --strip-components=2 && rm -f debian-binary control.tar.* data.tar.*\"";
            }
            else
            {
                programName = "tar";
                arguments = $"-xf \"{filePath}\" -C \"{targetDir}\" --strip-components=1";
            }

            if (string.IsNullOrWhiteSpace(programName) || string.IsNullOrWhiteSpace(arguments))
                return false;
            
            var psi = new ProcessStartInfo
            {
                FileName = programName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            
            using var process = Process.Start(psi);

            if (process is null)
                return false;

            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                await Console.Out.WriteLineAsync("[INFO] - Runner installed successfully!");
            }
            
            return process.ExitCode == 0;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ERROR] - Failed to extract the downloaded runner: {e.Message}]");
            return false;
        }
    }

    private static string GetBinaryName(ERunnerType runnerType)
    {
        return runnerType switch
        {
            ERunnerType.DosBox => "dosbox",
            ERunnerType.ScummVm => "scummvm",
            ERunnerType.Wine => "wine",
            ERunnerType.Proton => "proton",
            ERunnerType.InnoExtract => "innoextract",
            _ => string.Empty
        };
    }

    private static string? FindBinaryInSystemPath(string binaryName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrWhiteSpace(pathEnv))
            return null;

        var searchDirectories = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        
        return searchDirectories.Select(directory => Path.Combine(directory, binaryName)).FirstOrDefault(File.Exists);
    }

    private static string? FindBinaryInDownloadedPath(string binaryName, string targetDir)
    {
        if (!Directory.Exists(targetDir) || string.IsNullOrWhiteSpace(binaryName))
            return null;
        
        var files = Directory.GetFiles(targetDir, $"{binaryName}", SearchOption.AllDirectories);
        return files.FirstOrDefault(File.Exists);
    }

    private static string ExtractExtensionFromDownloadUrl(string downloadUrl)
    {
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var convertedUri))
            return string.Empty;

        var fileName = Path.GetFileName(convertedUri.AbsolutePath);

        foreach (var extension in CompoundExtensions)
        {
            if (fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                return extension;
        }
        
        return Path.GetExtension(fileName);
    }
    
    public static string GetDownloadUrlForRunner(ERunnerType runnerType)
    {
        const string baseUrl = "https://github.com/lorencinibrdev/TreasureChest/releases/download/runtimes-v1";
        
        return runnerType switch
        {
            ERunnerType.DosBox => $"{baseUrl}/dosbox-staging-linux-x86_64-v0.83.0.tar.xz",
            ERunnerType.ScummVm => $"{baseUrl}/scummvm-2026.4.0-x86_64-linux.tar.xz",
            ERunnerType.InnoExtract => "https://github.com/dscharrer/innoextract/releases/download/1.9/innoextract-1.9-linux.tar.xz",
            ERunnerType.Wine => "https://github.com/Kron4ek/Wine-Builds/releases/download/11.21/wine-11.21-staging-amd64-wow64.tar.xz",
            _ => throw new NotImplementedException($"No direct download URL configured for {runnerType}")
        };
    }
    
    // Properties:
    public static RunnerManagerService Instance { get; } = new();
}