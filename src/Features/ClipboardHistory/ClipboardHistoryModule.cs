using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.ClipboardHistory;

/// <summary>Entry point for the Clipboard History feature.</summary>
public sealed class ClipboardHistoryModule : IFeatureModule
{
    public Control CreateView(IFeatureContext context) => new ClipboardHistoryControl();
}
