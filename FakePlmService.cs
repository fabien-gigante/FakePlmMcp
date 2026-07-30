using System.Text.Json.Serialization;

public class Item(string type, string name, string revision = "A")
{
  public Guid Id { get; init; } = Guid.NewGuid();
  public string Type { get; init; } = type;
  public string Name { get; init; } = name;
  public string Revision { get; init; } = revision;
  public string Maturity { get; set; } = "In Work";
  [JsonIgnore]
  public List<Relation> Relations { get; init; } = [];
}
public class Relation
{
  public string FromPredicate { get; init; }
  public string ToPredicate { get; init; }
  [JsonIgnore]
  public Item FromItem { get; init; }
  [JsonIgnore]
  public Item ToItem { get; init; }
  public Guid FromId => FromItem.Id;
  public Guid ToId => ToItem.Id;
  public Relation(string fromPredicate, Item fromItem, string toPredicate, Item toItem)
  {
    FromPredicate = fromPredicate; FromItem = fromItem; ToPredicate = toPredicate; ToItem = toItem;
    FromItem.Relations.Add(this); ToItem.Relations.Add(this);
  }
}

public class FakePlmService
{
  private readonly Dictionary<Guid, Item> _database = [];
  public FakePlmService()
  {
    Item a = new("Eng Item", "Prd Root");
    Item b = new("Eng Item", "Prd 1") { Maturity = "Frozen" };
    Item c = new("Eng Item", "Prd 2", "B") { Maturity = "Obsolete" };
    Item d = new("Manuf Item", "Mfg Prd Root");
    Item e = new("Manuf Item", "Mfg Prd 1" ,"B") { Maturity = "Draft" };
    Item f = new("Manuf Item", "Mfg Prd 2");
    new Relation("Parent", a, "Child", b); new Relation("Parent", a, "Child", c);
    new Relation("Parent", d, "Child", e); new Relation("Parent", d, "Child", f);
    new Relation("Scoping", d, "Scoped", a); new Relation("Scoping", e, "Scoped", b); new Relation("Scoping", f, "Scoped", c);
    Save(a, b, c, d, e, f);
  }
  private void Save(params Item[] items) => Array.ForEach(items, item => _database.Add(item.Id, item));

  public IEnumerable<Item> Search(string? type, string? name, string? revision)
    => _database.Values
      .Where(item => type is null || item.Type == type)
      .Where(item => name is null || item.Name.Contains(name, StringComparison.InvariantCultureIgnoreCase))
      .Where(item => revision is null || item.Revision == revision);
  public Item? Fetch(Guid id) => _database.GetValueOrDefault(id);
  public IEnumerable<Relation> GetRelations(Item item, string[]? fromPredicate, string[]? toPredicate, bool bidirectional = false)
    => item.Relations
      .Where(rel => bidirectional || rel.FromItem == item)
      .Where(rel => fromPredicate?.Contains(rel.FromPredicate) ?? true)
      .Where(rel => toPredicate?.Contains(rel.ToPredicate) ?? true);
  public IEnumerable<Relation> GetRelations(IEnumerable<Item> items, string[]? fromPredicate, string[]? toPredicate, bool bidirectional = false, bool recursively = false)
  {
    HashSet<Item> visitedItems = [.. items];
    HashSet<Relation> visitedRelations = [];
    Queue<Item> queue = new(visitedItems);
    while (queue.TryDequeue(out var item))
      foreach (var relation in GetRelations(item, fromPredicate, toPredicate, bidirectional).Where(visitedRelations.Add))
        if (recursively && visitedItems.Add(relation.ToItem)) queue.Enqueue(relation.ToItem);
    return visitedRelations;
  }
}