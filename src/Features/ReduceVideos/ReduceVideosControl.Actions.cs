using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms;

namespace DigitalSecretary.Features.ReduceVideos;

/// <summary>Scan, reduce, undo, report and the tool buttons of <see cref="ReduceVideosControl"/>.</summary>
public sealed partial class ReduceVideosControl
{
    // ----------------------------------------------------------------- scan

    private void BrowseFolder()
    {
        using var dlg = new FolderBrowserDialog { Description = "Choose the folder with your videos" };
        if (Directory.Exists(_folder.Text))
        {
            dlg.SelectedPath = _folder.Text;
        }
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _folder.Text = dlg.SelectedPath;
        }
    }

    private async void OnScan()
    {
        var root = _folder.Text.Trim();
        if (root.Length == 0 || !Directory.Exists(root))
        {
            Inform("Please choose a folder that exists.");
            return;
        }
        if (_toolPaths is null && !MfVideoProbe.IsAvailable)
        {
            Inform("Neither ffmpeg nor Windows Media Foundation is available, so videos cannot be read. Install ffmpeg: " + FfmpegLocator.InstallCommand);
            return;
        }
        _settings.Root = root;
        SaveSettings();
        BeginRun();
        try
        {
            await ScanAsync(root);
        }
        catch (Exception ex)
        {
            Fail("Scan failed", ex);
        }
        finally
        {
            EndRun();
        }
    }

    private async Task ScanAsync(string root)
    {
        _isSample = false;
        var cacheFile = VideoFactsCache.PathFor(_context.DataDirectory, root);
        if (!string.Equals(_cacheRoot, root, StringComparison.OrdinalIgnoreCase))
        {
            _cache = VideoFactsCache.Load(cacheFile);
            _cacheRoot = root;
        }
        var tools = _toolPaths;
        Func<FileInfo, VideoFacts?> read = tools is null ? MfVideoProbe.Read : f => FfprobeReader.Read(tools.Ffprobe, f);
        var request = new VideoFactsScanRequest
        {
            Root = root,
            Cache = _cache,
            Read = read,
            Progress = new Progress<FileScanProgress>(p =>
            {
                _progress.Value = Math.Clamp(p.Percent, 0, 100);
                _status.Text = string.Format(CultureInfo.InvariantCulture, "Reading... {0:N0} of {1:N0} ({2:N0} read, {3:N0} already known)  {4}", p.Processed, p.Total, p.Read, p.Reused, p.Current);
            }),
            Log = new Progress<string>(AppendLog),
            Pause = _pause,
            Cancel = _cts!.Token,
            AutoSave = c => TrySave(c, cacheFile)
        };
        var result = await Task.Run(() => VideoFactsScanner.Run(request));
        TrySave(_cache, cacheFile);
        _status.Text = string.Format(CultureInfo.InvariantCulture, "{0}: {1:N0} videos ({2:N0} read, {3:N0} already known, {4:N0} unreadable).",
            result.Cancelled ? "Stopped" : "Done", result.Items, result.Read, result.Reused, result.Unreadable);
        AppendLog(_status.Text);
        _facts = [.. _cache.Records];
        Readvise();
    }

    private void TrySave(VideoFactsCache cache, string file)
    {
        try
        {
            cache.Save(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            BeginInvoke(() => AppendLog("Could not save the scan results: " + ex.Message));
        }
    }

    // ----------------------------------------------------------------- reduce

    private async void OnReduce()
    {
        var selected = Selected();
        if (selected.Count == 0 || ConfirmReduce(selected) != DialogResult.OK)
        {
            return;
        }
        BeginRun();
        try
        {
            var engine = await MakeEngineAsync();
            if (engine is null)
            {
                return;
            }
            AppendLog("Reducing " + selected.Count + " video(s) with " + engine.Name + ", quality " + _settings.Quality + ".");
            var job = ReduceJob(selected, engine);
            var outcome = await Task.Run(() => VideoReduceRunner.Run(job));
            if (outcome.Batch is { } batch)
            {
                ShrinkSwap.SaveLastBatch(_lastBatchFile, batch);
            }
            _status.Text = ReduceVideosText.RunSummary(outcome, _settings.Action);
            AppendLog(_status.Text);
            ShowSaved();
            await ScanAsync(_cacheRoot);
        }
        catch (Exception ex)
        {
            Fail("Reduce failed", ex);
        }
        finally
        {
            EndRun();
        }
    }

    private DialogResult ConfirmReduce(List<(VideoFacts Facts, VideoAdvice Advice)> selected)
    {
        var where = _settings.Action == RemoveAction.RecycleBin
            ? "the Recycle Bin"
            : "the folder \"" + VideoReduceRunner.ReviewFolderName + "\" (Undo puts them back)";
        var hours = selected.Sum(s => s.Facts.DurationMs) / 3_600_000.0;
        var text = ReduceVideosText.Selection(selected) + ".\n\nEach copy is checked before it replaces its original; originals go to " + where
            + ".\n\nTotal length " + hours.ToString("0.0", CultureInfo.InvariantCulture) + " h. With the recommended engine this takes roughly as long to run - you can pause or stop at any time.";
        return MessageBox.Show(FindForm(), text, Title, MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
    }

    /// <summary>The engine for the current choice, or null (after telling the user) when ffmpeg is needed but missing.</summary>
    private async Task<IVideoEngine?> MakeEngineAsync()
    {
        if (_settings.Engine == VideoEngineKind.Windows)
        {
            return new WindowsVideoEngine();
        }
        if (_toolPaths is not { } tools)
        {
            Inform("ffmpeg is not installed. Install it once with:\n\n" + FfmpegLocator.InstallCommand + "\n\nthen click \"Find ffmpeg\" - or choose the Windows built-in engine.");
            return null;
        }
        if (_settings.Engine == VideoEngineKind.FfmpegGpu && _gpu is null)
        {
            _status.Text = "Testing the graphics card's encoder...";
            _gpu = await Task.Run(() => FfmpegVideoEngine.DetectGpu(tools));
            AppendLog(_gpu == GpuEncoder.None ? "No working graphics-card encoder found - using x265." : "Graphics-card encoder: " + FfmpegVideoArgs.EncoderName(_gpu.Value));
            UpdateHelp();
        }
        return new FfmpegVideoEngine(tools, _settings.Engine, _gpu ?? GpuEncoder.None);
    }

    private VideoReduceJob ReduceJob(List<(VideoFacts Facts, VideoAdvice Advice)> selected, IVideoEngine engine) => new()
    {
        Root = _cacheRoot,
        Candidates = [.. selected.Select(s => new VideoCandidate(s.Facts, s.Advice))],
        Engine = engine,
        Quality = _settings.Quality,
        MinSavingPercent = _settings.MinSavingPercent,
        CheckQuality = _settings.CheckQuality,
        Action = _settings.Action,
        Ledger = _ledger,
        Progress = new Progress<ReduceProgress>(p =>
        {
            _progress.Value = Math.Clamp(((p.Index - 1) * 100 + p.FilePercent) / Math.Max(1, p.Total), 0, 100);
            _status.Text = string.Format(CultureInfo.InvariantCulture, "Reducing {0} of {1} ({2}%): {3}", p.Index, p.Total, p.FilePercent, Path.GetFileName(p.Path));
        }),
        Log = new Progress<string>(AppendLog),
        Pause = _pause,
        Cancel = _cts!.Token
    };

    // ----------------------------------------------------------------- undo / originals / report / tools

    private async void OnUndo()
    {
        if (ShrinkSwap.LoadLastBatch(_lastBatchFile) is not { } batch)
        {
            return;
        }
        var text = string.Format(CultureInfo.InvariantCulture, "Put back the {0:N0} original(s) of the run on {1}? Their smaller copies are deleted.",
            batch.Entries.Count, batch.WhenUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        if (MessageBox.Show(FindForm(), text, Title, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
        {
            return;
        }
        var (restored, skipped) = ShrinkSwap.Undo(batch, new Progress<string>(AppendLog));
        foreach (var e in batch.Entries.Where(e => File.Exists(e.Original) && !File.Exists(e.StoredAt)))
        {
            _ledger.Append(new SavingRecord(DateTime.UtcNow, e.Original, e.ReducedSize, e.OriginalSize, "undo"));
        }
        File.Delete(_lastBatchFile);
        _undo.Enabled = false;
        _status.Text = string.Format(CultureInfo.InvariantCulture, "Put back {0:N0} original(s); {1:N0} could not be.", restored, skipped);
        AppendLog(_status.Text);
        ShowSaved();
        if (Directory.Exists(batch.Root))
        {
            BeginRun();
            try
            {
                await ScanAsync(batch.Root);
            }
            finally
            {
                EndRun();
            }
        }
    }

    private void OpenOriginals()
    {
        var root = _cacheRoot.Length > 0 ? _cacheRoot : _folder.Text.Trim();
        var folder = ReviewFolder.For(root, VideoReduceRunner.ReviewFolderName);
        if (!Directory.Exists(folder))
        {
            Inform("There is no originals folder yet - nothing has been reduced in this folder.");
            return;
        }
        Shell(folder);
    }

    private void OnSaveReport()
    {
        using var dlg = new SaveFileDialog { Filter = "Text file (*.txt)|*.txt", FileName = "reduce-videos-report.txt", Title = "Save the Reduce Size - Videos report" };
        if (dlg.ShowDialog() != DialogResult.OK)
        {
            return;
        }
        try
        {
            File.WriteAllText(dlg.FileName, ReduceVideosText.Report(_cacheRoot, _rows, Options(), DateTime.Now));
            AppendLog("Report saved: " + dlg.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Fail("Could not save the report", ex);
        }
    }

    private void LocateFfmpeg()
    {
        using var dlg = new FolderBrowserDialog { Description = "Choose the folder that holds ffmpeg.exe and ffprobe.exe (leave empty to search the usual places)" };
        if (dlg.ShowDialog() != DialogResult.OK)
        {
            RefreshTools();
            return;
        }
        _settings.FfmpegFolder = dlg.SelectedPath;
        SaveSettings();
        RefreshTools();
        if (_toolPaths is null)
        {
            Inform("ffmpeg.exe and ffprobe.exe were not found in that folder.");
        }
    }

    private void CopyInstallCommand()
    {
        Clipboard.SetText(FfmpegLocator.InstallCommand);
        Inform("Copied to the clipboard:\n\n" + FfmpegLocator.InstallCommand + "\n\nPaste it into a Terminal / PowerShell window and press Enter. When it finishes, click \"Find ffmpeg\".");
    }

    private void OpenSelectedVideo()
    {
        if (_list.SelectedItems.Count > 0 && _list.SelectedItems[0].Tag is (VideoFacts facts, VideoAdvice _))
        {
            Shell(facts.Path);
        }
    }

    private void Shell(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppendLog("Could not open " + path + ": " + ex.Message);
        }
    }
}
