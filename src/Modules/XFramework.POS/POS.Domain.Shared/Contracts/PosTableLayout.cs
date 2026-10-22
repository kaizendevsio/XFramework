namespace POS.Domain.Shared.Contracts;

[MemoryPackable]
public partial record PosTableLayout
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Dining room";
    public List<PosDiningTable> Tables { get; set; } = [];

    public bool IsValid() => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name) && Name.Length <= 80 &&
        Tables is { Count: <= 96 } && Tables.All(table => table is not null && table.IsValid()) &&
        Tables.Select(table => table.Id).Distinct().Count() == Tables.Count &&
        Tables.Select(table => table.Label.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == Tables.Count &&
        !Tables.Any(table => Tables.Any(other => other.Id != table.Id && table.Overlaps(other)));

    public PosTableLayout DeepClone() => this with { Tables = Tables.Select(table => table with { }).ToList() };
}

[MemoryPackable]
public partial record PosDiningTable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = "1";
    public int Column { get; set; } = 1;
    public int Row { get; set; } = 1;
    public int Width { get; set; } = 2;
    public int Height { get; set; } = 2;
    public int Seats { get; set; } = 4;
    public bool Round { get; set; }

    public bool IsValid() => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Label) && Label.Length <= 64 &&
        Width is >= 1 and <= 4 && Height is >= 1 and <= 4 && Seats is >= 1 and <= 24 &&
        Column is >= 1 and <= 12 && Row is >= 1 and <= 8 && Column + Width <= 13 && Row + Height <= 9;

    public bool Overlaps(PosDiningTable other) => Column < other.Column + other.Width && Column + Width > other.Column &&
        Row < other.Row + other.Height && Row + Height > other.Row;
}
