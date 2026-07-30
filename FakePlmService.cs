using System.Text.Json;
using System.Text.Json.Serialization;

public class Item(string type, string name, string revision) {
  public Guid Id { get; init; } = Guid.NewGuid();
  public string Type { get; init; } = type;
  public string Name { get; init; } = name;
  public string Revision { get; init; } = revision;
  public string Maturity { get; set; } = "In Work";
  [JsonIgnore]
  public List<Relation> Relations { get; init; } = [];
  public static Item Load(Dictionary<Guid, Item> database, JsonElement element) {
    Guid id = Guid.Parse(element.GetProperty("id").GetString()!);
    string type = element.GetProperty("type").GetString()!;
    string name = element.GetProperty("name").GetString()!;
    string revision = element.TryGetProperty("revision", out var rev) ? rev.GetString()! : "A";
    string maturity = element.TryGetProperty("maturity", out var mat) ? mat.GetString()! : "In Work";
    return database[id] = new Item(type, name, revision) { Id = id, Maturity = maturity };
  }
}
public class Relation {
  public string FromPredicate { get; init; }
  public string ToPredicate { get; init; }
  [JsonIgnore]
  public Item FromItem { get; init; }
  [JsonIgnore]
  public Item ToItem { get; init; }
  public Guid FromId => FromItem.Id;
  public Guid ToId => ToItem.Id;
  public Relation(string fromPredicate, Item fromItem, string toPredicate, Item toItem) {
    FromPredicate = fromPredicate; FromItem = fromItem; ToPredicate = toPredicate; ToItem = toItem;
    FromItem.Relations.Add(this); ToItem.Relations.Add(this);
  }
  public static Relation Load(Dictionary<Guid, Item> database, JsonElement element) {
    Guid fromId = Guid.Parse(element.GetProperty("fromId").GetString()!);
    Guid toId = Guid.Parse(element.GetProperty("toId").GetString()!);
    Item fromItem = database.GetValueOrDefault(fromId) ?? throw new InvalidDataException($"Unknown item id '{fromId}'.");
    Item toItem = database.GetValueOrDefault(toId) ?? throw new InvalidDataException($"Unknown item id '{toId}'.");
    string fromPredicate = element.GetProperty("fromPredicate").GetString()!;
    string toPredicate = element.GetProperty("toPredicate").GetString()!;
    return new Relation(fromPredicate, fromItem, toPredicate, toItem);
  }
}

public class FakePlmService {
  private static readonly string _datasetFilename = "PlmDataset.json";
  private readonly Dictionary<Guid, Item> _database = [];
  public FakePlmService() {
    using var dataset = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, _datasetFilename)));
    foreach (var element in dataset.RootElement.GetProperty("items").EnumerateArray())
      Item.Load(_database, element);
    foreach (var element in dataset.RootElement.GetProperty("relations").EnumerateArray())
      Relation.Load(_database, element);
  }
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
  public IEnumerable<Relation> GetRelations(IEnumerable<Item> items, string[]? fromPredicate, string[]? toPredicate, bool bidirectional = false, bool recursively = false) {
    HashSet<Item> visitedItems = [.. items];
    HashSet<Relation> visitedRelations = [];
    Queue<Item> queue = new(visitedItems);
    while (queue.TryDequeue(out var item))
      foreach (var relation in GetRelations(item, fromPredicate, toPredicate, bidirectional).Where(visitedRelations.Add))
        if (recursively && visitedItems.Add(relation.ToItem)) queue.Enqueue(relation.ToItem);
    return visitedRelations;
  }
}