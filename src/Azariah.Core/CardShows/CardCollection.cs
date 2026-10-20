namespace Azariah.Core.CardShows;

/// <summary>One entry in the Collectr EVERYTHING export. Value and cost are per copy.</summary>
public sealed record CollectionItem(
    string Name,
    string Set,
    string Condition,
    string Kind,
    int Quantity,
    decimal ValueEach,
    decimal CostEach)
{
    public decimal Value => CardShowResults.Cents(ValueEach * Quantity);

    public decimal Paid => CardShowResults.Cents(CostEach * Quantity);
}

/// <summary>The whole collection from <c>Projects\Card Shows\data\collection.json</c>. Read-only.</summary>
public sealed class CardCollection
{
    private CardCollection(IReadOnlyList<CollectionItem> items, string? problem)
    {
        Items = items;
        Problem = problem;
    }

    /// <summary>Most valuable first (value of one copy).</summary>
    public IReadOnlyList<CollectionItem> Items { get; }

    public string? Problem { get; }

    public bool HasItems => Items.Count > 0;

    public int Entries => Items.Count;

    public int Copies => Items.Sum(i => i.Quantity);

    public decimal Value => CardShowResults.Cents(Items.Sum(i => i.Value));

    public decimal Paid => CardShowResults.Cents(Items.Sum(i => i.Paid));

    public decimal Unrealized => CardShowResults.Cents(Value - Paid);

    public decimal RawValue => ValueOf("raw");

    public decimal GradedValue => ValueOf("graded");

    public decimal SealedValue => ValueOf("sealed");

    /// <summary>Anything that isn't raw, graded or sealed, so the split always adds up to the whole.</summary>
    public decimal OtherValue => CardShowResults.Cents(Value - RawValue - GradedValue - SealedValue);

    public static CardCollection Load(CardShowBook book)
    {
        var rows = CardShowBook.ReadRows(book.Folder, "collection.json", out var problem);
        var items = new List<CollectionItem>();
        foreach (var r in rows ?? [])
        {
            var name = CardShowBook.Text(r, "name").Trim();
            if (name.Length == 0)
            {
                continue;
            }

            var quantity = (int)(CardShowBook.Number(r, "quantity") ?? 1);
            var each = CardShowBook.Number(r, "cost_each")
                ?? (CardShowBook.Number(r, "cost_total") is { } total && quantity > 0 ? total / quantity : 0);
            items.Add(new CollectionItem(
                name,
                CardShowBook.Text(r, "set"),
                CardShowBook.Text(r, "condition"),
                CardShowBook.Text(r, "kind").Trim().ToLowerInvariant(),
                quantity,
                CardShowBook.Number(r, "value") ?? 0,
                each));
        }

        return new CardCollection(
            items.OrderByDescending(i => i.ValueEach).ThenBy(i => i.Name, StringComparer.Ordinal).ToList(),
            problem);
    }

    private decimal ValueOf(string kind) =>
        CardShowResults.Cents(Items.Where(i => i.Kind == kind).Sum(i => i.Value));
}
