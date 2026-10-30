using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.SimilarPictures;

/// <summary>Entry point for the Similar Pictures feature.</summary>
public sealed class SimilarPicturesModule : IFeatureModule
{
    public Control CreateView(IFeatureContext context) => new SimilarPicturesControl(context);
}
