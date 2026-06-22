namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>
/// Builds the relationship "map": an undirected co-occurrence graph where two people are linked
/// when they appear together on the same email (To/Cc), weighted by how often. This is the data
/// behind the report's network-graph tab. Computed entirely locally.
/// </summary>
public sealed class NetworkGraphBuilder
{
    public IReadOnlyList<GraphEdge> Build(
        IReadOnlyList<Person> people,
        IReadOnlyList<ParsedEmail> emails,
        string ownerAddress)
    {
        var personIdByAddress = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var person in people)
            foreach (var address in person.Addresses)
                personIdByAddress[address] = person.Id;

        var weights = new Dictionary<(string, string), int>();

        foreach (var email in emails)
        {
            var ids = email.AllParticipants
                .Where(p => !p.Address.Equals(ownerAddress, StringComparison.OrdinalIgnoreCase))
                .Select(p => personIdByAddress.GetValueOrDefault(p.Address))
                .Where(id => !string.IsNullOrEmpty(id))
                .Select(id => id!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (var i = 0; i < ids.Count; i++)
            {
                for (var j = i + 1; j < ids.Count; j++)
                {
                    var key = string.CompareOrdinal(ids[i], ids[j]) <= 0
                        ? (ids[i], ids[j])
                        : (ids[j], ids[i]);
                    weights[key] = weights.GetValueOrDefault(key) + 1;
                }
            }
        }

        return weights
            .Select(kv => new GraphEdge(kv.Key.Item1, kv.Key.Item2, kv.Value))
            .OrderByDescending(e => e.Weight)
            .ToList();
    }
}
