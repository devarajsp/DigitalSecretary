using System.Text.RegularExpressions;

namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>
/// Builds the master list of people from all participants, then merges the multiple email
/// addresses that belong to one person. Merging is deterministic (no AI): addresses are grouped,
/// then identities that share the same normalized full name (first + last) are folded together.
/// </summary>
public sealed class IdentityResolver
{
    private static readonly Regex TitleRe =
        new(@"\b(mr|mrs|ms|miss|dr|prof)\.?\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NonLetterRe = new(@"[^a-z\s]", RegexOptions.Compiled);
    private static readonly Regex SpacesRe = new(@"\s+", RegexOptions.Compiled);

    public IReadOnlyList<Person> Resolve(IEnumerable<ParsedEmail> emails, string ownerAddress)
    {
        var byAddress = new Dictionary<string, Person>(StringComparer.OrdinalIgnoreCase);

        foreach (var email in emails)
        {
            foreach (var p in email.AllParticipants)
            {
                if (string.IsNullOrEmpty(p.Address) ||
                    p.Address.Equals(ownerAddress, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!byAddress.TryGetValue(p.Address, out var person))
                {
                    person = new Person { Id = p.Address, DisplayName = p.Name ?? p.Address };
                    person.Addresses.Add(p.Address);
                    byAddress[p.Address] = person;
                }

                if (!string.IsNullOrWhiteSpace(p.Name))
                {
                    if (!person.NameVariants.Contains(p.Name))
                        person.NameVariants.Add(p.Name);
                    // Prefer a real display name over a bare address.
                    if (person.DisplayName.Equals(person.Id, StringComparison.OrdinalIgnoreCase))
                        person.DisplayName = p.Name;
                }
            }
        }

        return MergeByName(byAddress.Values);
    }

    private static List<Person> MergeByName(IEnumerable<Person> people)
    {
        var byName = new Dictionary<string, Person>(StringComparer.Ordinal);
        var result = new List<Person>();

        foreach (var person in people)
        {
            var key = NormalizeName(person.DisplayName);
            // Only merge on multi-word names (a real first + last); single tokens are too risky.
            if (key.Contains(' ') && byName.TryGetValue(key, out var existing))
            {
                foreach (var a in person.Addresses)
                    if (!existing.Addresses.Contains(a, StringComparer.OrdinalIgnoreCase))
                        existing.Addresses.Add(a);
                foreach (var n in person.NameVariants)
                    if (!existing.NameVariants.Contains(n))
                        existing.NameVariants.Add(n);
            }
            else
            {
                if (key.Contains(' '))
                    byName[key] = person;
                result.Add(person);
            }
        }

        return result;
    }

    /// <summary>Lower-cases a name, drops titles and punctuation, and collapses whitespace.</summary>
    public static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";
        var n = TitleRe.Replace(name.Trim().ToLowerInvariant(), "");
        n = NonLetterRe.Replace(n, " ");
        return SpacesRe.Replace(n, " ").Trim();
    }
}
