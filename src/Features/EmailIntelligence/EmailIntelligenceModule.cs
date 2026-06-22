using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>Entry point for the Email Intelligence feature.</summary>
public sealed class EmailIntelligenceModule : IFeatureModule
{
    public Control CreateView(IFeatureContext context) => new EmailIntelligenceControl(context);
}
