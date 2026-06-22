using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.GmailDownloader;

/// <summary>Entry point for the Download Gmail feature.</summary>
public sealed class GmailDownloaderModule : IFeatureModule
{
    public Control CreateView(IFeatureContext context) => new DownloadGmailControl(context);
}
