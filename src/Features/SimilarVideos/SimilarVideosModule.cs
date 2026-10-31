using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.SimilarVideos;

/// <summary>Entry point for the Similar Videos feature.</summary>
public sealed class SimilarVideosModule : IFeatureModule
{
    public Control CreateView(IFeatureContext context) => new SimilarVideosControl(context);
}
