using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddSingleton<FakePlmService>();
builder.Services.AddSingleton<FakePlmTools>();
builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();
await builder.Build().RunAsync();

[McpServerToolType]
public class FakePlmTools
{
  private readonly FakePlmService _plm;
  public FakePlmTools(FakePlmService plm) => _plm = plm;

  [McpServerTool(ReadOnly = true),
   Description("Returns the PLM ontology: distinct relation item types, relation types, predicate names, and maturity states. Use this to learn valid filter values before calling search or get_relations, or valid state names when doing maturity analysis.. Once call once during discovery, subsequent calls should use the values returned here rather than re-running the discovery.")]
  public Ontology GetOntology() => _plm.GetOntology(); 

  [McpServerTool(ReadOnly = true),
   Description("Searches the PLM database for Ids of Items matching the given criteria. All filters are combined with AND; leave a filter 'null' to not restrict on it. Item Ids should be kept internal and must never appear in any user-facing text. At least one filter must be specified, narrow the searches as much as possible to avoid comprehensive scans.")]
  public IEnumerable<Guid> Search(
    [Description("The exact Types of Item to look for. Any types if 'null' is given.")]
    string[]? types,
    [Description("A case-insensitive substring to look for in the Item's Name. All are returned if 'null' is given.")]
    string? name,
    [Description("The exact Revision to look for. All are returned if 'null' is given.")]
    string? revision,
    [Description("A case-insensitive substring to look for in any of the Item's attribute values. All are returned if 'null' is given.")]
    string? text
  ) => _plm.Search(types, name, revision, text).Select(item => item.Id);

  [McpServerTool(ReadOnly = true),
   Description("Returns the Items with the given Ids, in the same order as the input Ids. If an Id is unknown, null is returned in its place. Except from Id, all Item attributes can be shown to the user. Attributes values, such as Name, shouldn't be used to infer connectivity or pairings, use get_relations instead. Don't use fetch for discovery purposes, only to retrieve specific items after discovery.")]
  public IEnumerable<Item?> Fetch(
    [Description("The exact Item Ids to fetch. Ids should be kept internal and must never appear in any user-facing text.")]
    Guid[] ids
  ) => ids.Select(id => _plm.Fetch(id));

  [McpServerTool(ReadOnly = true),
   Description("Returns all Relations of Items with the given Ids. If an Id is unknown, it is ignored. Always batch similar lookups together rather than calling once per Id. If you already made one call with sufficient depth covering a set, you can safely assume that the result is exhaustive for the given predicates. Always consider the from and to predicates in the results to interpret the relation semantic and direction, not the relation type alone.")]
  public IEnumerable<Relation> GetRelations(
    [Description("The exact Item Ids. (Ids should be kept internal and must never appear in any user-facing text    .)")]
    Guid[] ids,
    [Description("Criteria for Relation types to consider for the related Items. Strongly recommended over leaving 'null'. Leave 'null' only on your first discovery call for a given set of items/domains. If used soley for discovery, batching multiple ids is still ok, but using depth > 1 should generally be avoided. Once a call has revealed the relation types relevant to your task, all subsequent calls for that same purpose must pass those types explicitly rather than re-running unfiltered.")]
    [DefaultValue(new[] { "Instance" })]
    string[]? relTypes = null,
    [Description("Criteria for Relation predicates to consider FROM the related Items. Strongly recommended over leaving 'null'. Leave 'null' only on your first discovery call for a given set of items/domains. If used soley for discovery, batching multiple ids is still ok, but using depth > 1 should generally be avoided. Once a call has revealed the predicate names relevant to your task, all subsequent calls for that same purpose must pass those predicates explicitly rather than re-running unfiltered.")]
    [DefaultValue(new[] { "Parent" })]
    string[]? fromPredicate = null,
    [Description("Criteria for Relation predicates to consider TO the related Items. Strongly recommended over leaving 'null'. Leave 'null' only on your first discovery call for a given set of items/domains. If used soley for discovery, batching multiple ids is still ok, but using depth > 1 should generally be avoided. Once a call has revealed the predicate names relevant to your task, all subsequent calls for that same purpose must pass those predicates explicitly rather than re-running unfiltered.")]
    [DefaultValue(new[] { "Child" })]
    string[]? toPredicate = null,
    [Description("If bidirectional is 'true' both Relations from and to the Item (in either direction) are returned. If bidirectional is 'false', only the from → to relations are returned. If used for discovery, on an item whose role in the schema is yet unknown, use 'true' to include incoming relations.")]
    bool bidirectional = false,
    [Description("How many hops of traversal to perform. 'depth' = 1 (default) returns only the direct relations of the given Items. 'depth' = 2 or more also follows relations from those results, that many hops deep, and the returned set is exhaustive for the given predicates up to that depth — items with no further outgoing relations in the result can be treated as confirmed leaves within that depth, not as unexplored. 'depth' = -1 means unlimited traversal until the relation graph is fully explored; this is strongly discouraged except on small, well-understood, tightly filtered relation sets, since it can traverse very large graphs and consume excessive time and tokens. Prefer an explicit, small positive depth over -1 whenever possible. Values below -1 are invalid.")]
    [DefaultValue(1)]
    int depth = 1
  ) => _plm.GetRelations(Fetch(ids).Where(item => item is not null)!, relTypes, fromPredicate, toPredicate, bidirectional, depth);

  [McpServerTool(ReadOnly = true),
   Description("Returns the revision graphs for the given Items: prefered tool to use than the generic get_relations for the \"Revision\" relation type. Defaults to bidirectional traversal. Always consider the from and to predicates in the results to interpret the revision graph semantic and chaining order, not the relation type alone.")]
  public IEnumerable<Relation> GetRevisionGraphs(
      [Description("The exact Item Ids. (Ids should be kept internal and must never appear in any user-facing text.)")]
    Guid[] ids,
      [Description("If 'true', both descendant and ancestor revisions are included.")]
    bool bidirectional = true,
      [Description("How many hops of the revision graph to traverse. depth = 1 returns only the direct revision neighbors. Increase incrementally to explore further. depth = -1 (default) fully explores the connected revision chain and is strongly discouraged except on small, well-understood, tightly filtered item sets, since revision chains can be very long (100+ hops) and this can consume excessive time and tokens. Prefer an explicit, small positive depth over -1 whenever possible.")]
    [DefaultValue(-1)]
    int depth = -1
  ) => GetRelations(ids, ["Revision"], null, null, bidirectional, depth);
}