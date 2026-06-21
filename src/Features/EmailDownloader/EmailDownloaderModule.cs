using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.EmailDownloader;

/// <summary>Entry point for the Download Emails feature.</summary>
public sealed class EmailDownloaderModule : IFeatureModule
{
    public Control CreateView(IFeatureContext context) => new DownloadEmailsControl(context);
}
