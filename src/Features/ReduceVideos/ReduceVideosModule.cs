using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.ReduceVideos;

/// <summary>Entry point for the Reduce Size - Videos feature.</summary>
public sealed class ReduceVideosModule : IFeatureModule
{
    public Control CreateView(IFeatureContext context) => new ReduceVideosControl(context);
}
