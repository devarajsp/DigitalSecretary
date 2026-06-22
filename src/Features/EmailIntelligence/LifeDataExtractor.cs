using System.Globalization;
using System.Text.RegularExpressions;

namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>
/// Extracts structured "life data" — purchases, subscriptions, travel and account sign-ups — from the
/// archive using local keyword/amount heuristics (no AI, no network). Each matching message yields one
/// <see cref="LifeDataItem"/> under its strongest category, with any amount it can find.
/// </summary>
public sealed class LifeDataExtractor
{
    private static readonly Regex AmountRe = new(
        @"(?<cur>US\$|\$|₹|€|£|INR|USD|EUR|GBP|Rs\.?)\s?(?<amt>\d{1,3}(?:[,\d]{0,12})(?:\.\d{1,2})?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] TravelWords =
    {
        "flight", "boarding pass", "itinerary", "pnr", "hotel reservation", "hotel booking",
        "check-in", "your trip", "booking confirmation", "e-ticket", "departure",
    };
    private static readonly string[] PurchaseWords =
    {
        "receipt", "your order", "order confirmation", "invoice", "payment received", "purchase",
        "order #", "thanks for your order", "shipped", "delivered",
    };
    private static readonly string[] SubscriptionWords =
    {
        "subscription", "auto-renew", "auto renew", "renewal", "recurring", "your plan",
        "membership", "billing cycle", "free trial",
    };
    private static readonly string[] AccountWords =
    {
        "welcome to", "verify your email", "confirm your email", "activate your account",
        "password reset", "reset your password", "account created", "sign in to",
    };

    public IReadOnlyList<LifeDataItem> Extract(IReadOnlyList<ParsedEmail> emails)
    {
        var items = new List<LifeDataItem>();
        foreach (var email in emails)
        {
            var category = Categorize(email);
            if (category is null)
                continue;
            var (amount, currency) = FindAmount(email.Subject + " " + email.Body);
            var detail = email.From?.Name ?? Domain(email.From?.Address);
            items.Add(new LifeDataItem(category, email.Date, email.From?.Address ?? "", email.Subject, amount, currency, detail));
        }
        return items;
    }

    /// <summary>Strongest single life-data category for a message, or null if none applies.</summary>
    public static string? Categorize(ParsedEmail email)
    {
        var text = (email.Subject + " " + email.Body).ToLowerInvariant();
        if (TravelWords.Any(w => text.Contains(w, StringComparison.Ordinal)))
            return "Travel";
        if (PurchaseWords.Any(w => text.Contains(w, StringComparison.Ordinal)))
            return "Purchase";
        if (SubscriptionWords.Any(w => text.Contains(w, StringComparison.Ordinal)))
            return "Subscription";
        if (AccountWords.Any(w => text.Contains(w, StringComparison.Ordinal)))
            return "Account";
        return null;
    }

    /// <summary>Finds the first monetary amount + normalized currency code in the text.</summary>
    public static (decimal? Amount, string? Currency) FindAmount(string text)
    {
        var m = AmountRe.Match(text);
        if (!m.Success)
            return (null, null);
        var raw = m.Groups["amt"].Value.Replace(",", "");
        if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            return (null, NormalizeCurrency(m.Groups["cur"].Value));
        return (amount, NormalizeCurrency(m.Groups["cur"].Value));
    }

    private static string NormalizeCurrency(string token) => token.Trim().ToUpperInvariant() switch
    {
        "$" or "US$" or "USD" => "USD",
        "₹" or "INR" or "RS" or "RS." => "INR",
        "€" or "EUR" => "EUR",
        "£" or "GBP" => "GBP",
        _ => token.Trim(),
    };

    private static string Domain(string? address)
    {
        if (string.IsNullOrEmpty(address))
            return "";
        var at = address.LastIndexOf('@');
        return at >= 0 && at < address.Length - 1 ? address[(at + 1)..] : address;
    }
}
