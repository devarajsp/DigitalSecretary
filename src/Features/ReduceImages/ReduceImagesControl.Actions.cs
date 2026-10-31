using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms;

namespace DigitalSecretary.Features.ReduceImages;

/// <summary>Scan, reduce, undo and report of <see cref="ReduceImagesControl"/>.</summary>
public sealed partial class ReduceImagesControl
{
    private void BrowseFolder()
    {
        using var dlg = new FolderBrowserDialog { Description = "Choose the folder with your pictures" };
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
        var cacheFile = ImageFactsCache.PathFor(_context.DataDirectory, root);
        if (!string.Equals(_cacheRoot, root, StringComparison.OrdinalIgnoreCase))
        {
            _cache = ImageFactsCache.Load(cacheFile);
            _cacheRoot = root;
        }
        var request = new ImageFactsScanRequest
        {
            Root = root,
            Cache = _cache,
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
        var result = await Task.Run(() => ImageFactsScanner.Run(request));
        TrySave(_cache, cacheFile);
        _status.Text = string.Format(CultureInfo.InvariantCulture, "{0}: {1:N0} pictures ({2:N0} read, {3:N0} already known, {4:N0} unreadable).",
            result.Cancelled ? "Stopped" : "Done", result.Items, result.Read, result.Reused, result.Unreadable);
        AppendLog(_status.Text);
        _facts = [.. _cache.Records];
        Readvise();
    }

    private void TrySave(ImageFactsCache cache, string file)
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

    private async void OnReduce()
    {
        var selected = Selected();
        if (selected.Count == 0)
        {
            return;
        }
        if (_tools is null && selected.Any(s => s.Advice.Action == ImageAction.ToModern))
        {
            Inform("WebP / AVIF need ffmpeg. Install it once with:\n\n" + FfmpegLocator.InstallCommand + "\n\nor choose \"Keep JPEG / PNG\".");
            return;
        }
        var where = _settings.Action == RemoveAction.RecycleBin ? "the Recycle Bin" : "the folder \"" + ImageReduceRunner.ReviewFolderName + "\" (Undo puts them back)";
        if (MessageBox.Show(FindForm(), ReduceImagesText.Selection(selected) + ".\n\nEach copy is checked before it replaces its original; originals go to " + where + ".",
            Title, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
        {
            return;
        }
        BeginRun();
        try
        {
            var job = ReduceJob(selected);
            var outcome = await Task.Run(() => ImageReduceRunner.Run(job));
            if (outcome.Batch is { } batch)
            {
                ShrinkSwap.SaveLastBatch(_lastBatchFile, batch);
            }
            _status.Text = ReduceImagesText.RunSummary(outcome, _settings.Action);
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

    private ImageReduceJob ReduceJob(List<(ImageFacts Facts, ImageAdvice Advice)> selected) => new()
    {
        Root = _cacheRoot,
        Candidates = [.. selected.Select(s => new ImageCandidate(s.Facts, s.Advice))],
        Quality = _settings.Quality,
        MinSavingPercent = _settings.MinSavingPercent,
        Tools = _tools,
        Action = _settings.Action,
        Ledger = _ledger,
        Progress = new Progress<ImageProgress>(p =>
        {
            _progress.Value = Math.Clamp(p.Index * 100 / Math.Max(1, p.Total), 0, 100);
            _status.Text = string.Format(CultureInfo.InvariantCulture, "Reducing {0:N0} of {1:N0}: {2}", p.Index, p.Total, Path.GetFileName(p.Path));
        }),
        Log = new Progress<string>(AppendLog),
        Pause = _pause,
        Cancel = _cts!.Token
    };

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
        var folder = ReviewFolder.For(root, ImageReduceRunner.ReviewFolderName);
        if (!Directory.Exists(folder))
        {
            Inform("There is no originals folder yet - nothing has been reduced in this folder.");
            return;
        }
        Shell(folder);
    }

    private void OnSaveReport()
    {
        using var dlg = new SaveFileDialog { Filter = "Text file (*.txt)|*.txt", FileName = "reduce-images-report.txt", Title = "Save the Reduce Size - Images report" };
        if (dlg.ShowDialog() != DialogResult.OK)
        {
            return;
        }
        try
        {
            File.WriteAllText(dlg.FileName, ReduceImagesText.Report(_cacheRoot, _rows, _settings.ToAdviceOptions(), DateTime.Now));
            AppendLog("Report saved: " + dlg.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Fail("Could not save the report", ex);
        }
    }

    private void OpenSelectedPicture()
    {
        if (_list.SelectedItems.Count > 0 && _list.SelectedItems[0].Tag is (ImageFacts facts, ImageAdvice _))
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
