namespace Tarpg.Items;

// Procedurally-rolled stat on a Magic / Rare item.
// e.g. "+12% physical damage", "+18 strength".
public sealed class Affix
{
    public required string Id { get; init; }
    public required string Template { get; init; }   // "+{0}% phys damage"
    public required int MinValue { get; init; }
    public required int MaxValue { get; init; }
    public int Value { get; init; }                   // rolled value

    public string Render() => string.Format(Template, Value);
}
