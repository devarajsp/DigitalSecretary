using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.Calculator;

/// <summary>Entry point for the Calculator feature.</summary>
public sealed class CalculatorModule : IFeatureModule
{
    public Control CreateView(IFeatureContext context) => new CalculatorControl();
}
