using System.Text.Json;
using System.Text.Json.Serialization;

public class Item(string type, string name, string revision = "A") {
  public Guid Id { get; init; } = Guid.NewGuid();
  public string Type { get; init; } = type;
  public string Name { get; init; } = name;
  public string Revision { get; init; } = revision;
  public string Maturity { get; set; } = "In Work";
  public Dictionary<string,string> Attributes { get; set; } = [];
  [JsonIgnore]
  public List<Relation> OutRelations { get; init; } = [];
  [JsonIgnore]
  public List<Relation> InRelations { get; init; } = [];

  private static readonly JsonSerializerOptions _serializerOptions = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
  public static Item Load(Ontology ontology, Dictionary<Guid, Item> database, JsonElement element) {
    Item item = element.Deserialize<Item>(_serializerOptions) ?? throw new JsonException("Item element deserialized to null");
    TypeDefinition? typeDefinition = ontology.Types.FirstOrDefault(t => t.Type == item.Type);
    if (typeDefinition is null) ontology.Types.Add(typeDefinition = new TypeDefinition(item.Type, []));
    foreach (var attribute in item.Attributes.Keys) typeDefinition.Attributes.Add(attribute);
    ontology.MaturityStates.Add(item.Maturity);
    return database[item.Id] = item;
  }
}
public class Relation {
  public RelationDefinition Definition { get; init; }
  [JsonIgnore]
  public Item FromItem { get; init; }
  [JsonIgnore]
  public Item ToItem { get; init; }
  public Guid FromId => FromItem.Id;
  public Guid ToId => ToItem.Id;
  public Dictionary<string, string> Attributes { get; set; } = [];
  public Relation(RelationDefinition definition, Item fromItem, Item toItem) {
    Definition = definition; FromItem = fromItem; ToItem = toItem;
    FromItem.OutRelations.Add(this); ToItem.InRelations.Add(this);
  }

  public static Relation Load(Ontology ontology, Dictionary<Guid, Item> database, JsonElement element)  {
    Guid fromId = element.GetProperty("fromId").GetGuid(), toId = element.GetProperty("toId").GetGuid();
    Item fromItem = database.GetValueOrDefault(fromId) ?? throw new InvalidDataException($"Unknown item id '{fromId}'.");
    Item toItem = database.GetValueOrDefault(toId) ?? throw new InvalidDataException($"Unknown item id '{toId}'.");
    string type = element.GetProperty("type").GetString()!;
    string fromPredicate = element.GetProperty("fromPredicate").GetString()!, toPredicate = element.GetProperty("toPredicate").GetString()!;
    var relationDefinition = new RelationDefinition(type, fromPredicate, toPredicate);
    ontology.Relations.Add(relationDefinition);
    Dictionary<string, string> attributes = [];
    if (element.TryGetProperty("attributes", out var attributesElement))
      attributes = attributesElement.Deserialize<Dictionary<string, string>>() ?? [];
    return new Relation(relationDefinition, fromItem, toItem) { Attributes = attributes };
  }
}

public record RelationDefinition(string Type, string From, string To);
public record TypeDefinition(string Type, HashSet<string> Attributes);
public class Ontology {
  public List<TypeDefinition> Types { get; } = [];
  public HashSet<RelationDefinition> Relations { get; } = [];
  public HashSet<string> MaturityStates { get; } = [];
}

public class FakePlmService {
  private static readonly string _datasetFilename = "PlmDataset.json";
  private readonly Dictionary<Guid, Item> _database = [];
  private readonly Ontology _ontology = new();
  public FakePlmService() {
    using var dataset = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, _datasetFilename)));
    foreach (var element in dataset.RootElement.GetProperty("items").EnumerateArray()) Item.Load(_ontology, _database, element);
    foreach (var element in dataset.RootElement.GetProperty("relations").EnumerateArray()) Relation.Load(_ontology, _database, element);
  }
  public IEnumerable<Item> Search(string[]? types, string? name, string? revision, string? text)
    => _database.Values
      .Where(item => types?.Contains(item.Type) ?? true)
      .Where(item => name is null || item.Name.Contains(name, StringComparison.InvariantCultureIgnoreCase))
      .Where(item => revision is null || item.Revision == revision)
      .Where(item => text is null || item.Attributes.Values.Any(value => value.Contains(text, StringComparison.InvariantCultureIgnoreCase)));
  public Item? Fetch(Guid id) => _database.GetValueOrDefault(id);
  public Ontology GetOntology() => _ontology;
  public IEnumerable<Relation> GetRelations(Item item, string[]? relTypes,string[]? fromPredicate, string[]? toPredicate, bool bidirectional = false)
    => (bidirectional ? item.OutRelations.Concat(item.InRelations) : item.OutRelations)
      .Where(rel => relTypes?.Contains(rel.Definition.Type) ?? true)
      .Where(rel => fromPredicate?.Contains(rel.Definition.From) ?? true)
      .Where(rel => toPredicate?.Contains(rel.Definition.To) ?? true);
  public IEnumerable<Relation> GetRelations(IEnumerable<Item> items, string[]? relTypes, string[]? fromPredicate, string[]? toPredicate, bool bidirectional = false, bool recursively = false) {
    HashSet<Item> visitedItems = [.. items];
    HashSet<Relation> visitedRelations = [];
    Queue<Item> queue = new(visitedItems);
    while (queue.TryDequeue(out var item))
      foreach (var relation in GetRelations(item, relTypes, fromPredicate, toPredicate, bidirectional).Where(visitedRelations.Add))
        if (recursively && visitedItems.Add(relation.ToItem)) queue.Enqueue(relation.ToItem);
    return visitedRelations;
  }
}