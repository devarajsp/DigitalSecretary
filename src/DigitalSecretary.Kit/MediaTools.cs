using System.Diagnostics;
using System.Text;

namespace DigitalSecretary.Kit;

/// <summary>Where ffmpeg and ffprobe were found.</summary>
public sealed record MediaToolPaths(string Ffmpeg, string Ffprobe)
{
    /// <summary>The folder holding both tools.</summary>
    public string Folder => Path.GetDirectoryName(Ffmpeg) ?? "";
}

/// <summary>
/// Finds an installed ffmpeg (the optional, best-quality engine of the Reduce Size features). Nothing is
/// downloaded or installed by the app: the user installs ffmpeg once (<see cref="InstallCommand"/>) or points
/// at a folder that holds it. Search order: the user's folder, then PATH, then the usual install places
/// (winget links and packages, Program Files, C:\ffmpeg, Chocolatey, Scoop).
/// </summary>
public static class FfmpegLocator
{
    /// <summary>The one-line install the app suggests (Windows Package Manager, the Gyan build).</summary>
    public const string InstallCommand = "winget install --id Gyan.FFmpeg -e";

    /// <summary>Finds ffmpeg + ffprobe on this PC, or null.</summary>
    public static MediaToolPaths? Find(string? preferredFolder = null)
        => Find(Candidates(preferredFolder), File.Exists);

    /// <summary>The first folder that holds both tools. Testable with any folder list and existence check.</summary>
    public static MediaToolPaths? Find(IEnumerable<string> folders, Func<string, bool> exists)
    {
        foreach (var folder in folders.Where(f => !string.IsNullOrWhiteSpace(f)))
        {
            var ffmpeg = Path.Combine(folder, "ffmpeg.exe");
            var ffprobe = Path.Combine(folder, "ffprobe.exe");
            if (exists(ffmpeg) && exists(ffprobe))
            {
                return new MediaToolPaths(ffmpeg, ffprobe);
            }
        }
        return null;
    }

    /// <summary>Folders to look in, most specific first. Missing folders are harmless.</summary>
    public static IEnumerable<string> Candidates(string? preferredFolder)
    {
        if (!string.IsNullOrWhiteSpace(preferredFolder))
        {
            yield return preferredFolder;
        }
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            yield return dir.Trim().Trim('"');
        }
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(local, "Microsoft", "WinGet", "Links");
        foreach (var bin in WingetPackageBins(Path.Combine(local, "Microsoft", "WinGet", "Packages")))
        {
            yield return bin;
        }
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin");
        yield return @"C:\ffmpeg\bin";
        yield return Path.Combine(Environment.GetEnvironmentVariable("ChocolateyInstall") ?? @"C:\ProgramData\chocolatey", "bin");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "shims");
    }

    private static List<string> WingetPackageBins(string packages)
    {
        try
        {
            return Directory.Exists(packages)
                ? Directory.EnumerateDirectories(packages, "*FFmpeg*")
                    .SelectMany(p => Directory.EnumerateDirectories(p))
                    .Select(p => Path.Combine(p, "bin"))
                    .ToList()
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}

/// <summary>How an external tool run ended. <see cref="ErrorTail"/> holds the last lines it wrote to stderr.</summary>
public sealed record ToolResult(int ExitCode, string Output, string ErrorTail, bool Cancelled)
{
    public bool Succeeded => ExitCode == 0 && !Cancelled;
}

/// <summary>
/// Runs a command-line tool (ffmpeg / ffprobe) without a window, at below-normal priority so the PC stays
/// usable, with arguments passed as a list (never one concatenated string, so file names need no quoting).
/// A stop kills the whole process tree. Standard output is either collected or streamed line by line.
/// </summary>
public static class ToolRunner
{
    private const int TailLines = 30;

    /// <summary>
    /// Runs <paramref name="exe"/> to the end. Each stdout line goes to <paramref name="onOutputLine"/> when
    /// given; otherwise stdout is collected into <see cref="ToolResult.Output"/>.
    /// </summary>
    public static ToolResult Run(string exe, IReadOnlyList<string> args, Action<string>? onOutputLine, CancellationToken cancel)
    {
        using var process = new Process { StartInfo = StartInfo(exe, args) };
        var output = new StringBuilder();
        var tail = new Queue<string>();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }
            if (onOutputLine is null)
            {
                lock (output)
                {
                    output.AppendLine(e.Data);
                }
            }
            else
            {
                onOutputLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) => Remember(tail, e.Data);

        process.Start();
        TryLowerPriority(process);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var cancelled = WaitOrKill(process, cancel);
        process.WaitForExit();

        lock (output)
        {
            lock (tail)
            {
                return new ToolResult(cancelled ? -1 : process.ExitCode, output.ToString(), string.Join(Environment.NewLine, tail), cancelled);
            }
        }
    }

    /// <summary>The process start settings: no window, no shell, redirected UTF-8 streams.</summary>
    public static ProcessStartInfo StartInfo(string exe, IReadOnlyList<string> args)
    {
        var info = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }
        return info;
    }

    private static void Remember(Queue<string> tail, string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }
        lock (tail)
        {
            tail.Enqueue(line);
            while (tail.Count > TailLines)
            {
                tail.Dequeue();
            }
        }
    }

    private static bool WaitOrKill(Process process, CancellationToken cancel)
    {
        while (!process.WaitForExit(200))
        {
            if (cancel.IsCancellationRequested)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // Already gone.
                }
                return true;
            }
        }
        return false;
    }

    private static void TryLowerPriority(Process process)
    {
        try
        {
            process.PriorityClass = ProcessPriorityClass.BelowNormal;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // It may already have finished; priority is only a courtesy.
        }
    }
}
