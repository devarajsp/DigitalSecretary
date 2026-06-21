using System.Data;
using System.Globalization;

namespace DigitalSecretary.Features.Calculator;

/// <summary>
/// Pure expression evaluation, separated from the UI so it can be unit-tested.
/// Accepts the display operators (× ÷ −) as well as ASCII (* / -).
/// </summary>
public static class CalculatorEngine
{
    /// <summary>
    /// Evaluates an arithmetic expression. Returns the result as an invariant-culture string,
    /// an empty string for blank input, or "Error" if the expression is invalid.
    /// </summary>
    public static string Evaluate(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return "";

        var normalized = expression
            .Replace("×", "*")
            .Replace("÷", "/")
            .Replace("−", "-");

        try
        {
            var result = new DataTable().Compute(normalized, null);

            // Division by zero yields ±Infinity / NaN rather than throwing — treat as an error.
            if (result is double d && (double.IsNaN(d) || double.IsInfinity(d)))
                return "Error";

            return Convert.ToString(result, CultureInfo.InvariantCulture) ?? "";
        }
        catch
        {
            return "Error";
        }
    }
}
