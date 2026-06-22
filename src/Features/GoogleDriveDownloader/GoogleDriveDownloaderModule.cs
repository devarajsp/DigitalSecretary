using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.GoogleDriveDownloader;

/// <summary>Entry point for the Download Google Drive feature.</summary>
public sealed class GoogleDriveDownloaderModule : IFeatureModule
{
    public Control CreateView(IFeatureContext context) => new DownloadDriveControl(context);
}
