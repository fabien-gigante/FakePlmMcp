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
   Description("Searches the PLM database for Ids of Items matching the given criteria. All filters are combined with AND; leave a filter 'null' to not restrict on it. Item Ids should be kept internal and not shown to the user.")]
  public IEnumerable<Guid> Search(
    [Description("The exact Type of Item to look for. Any type if 'null' is given.")]
    string? type,
    [Description("A case-insensitive substring to look for in the Item's Name.")]
    string name,
    [Description("The exact Revision to look for. All are returned if 'null' is given.")]
    string? revision
  ) => _plm.Search(type, name, revision).Select(item => item.Id);

  [McpServerTool(ReadOnly = true),
   Description("Returns the Items with the given Ids, in the same order as the input Ids. If an Id is unknown, null is returned in its place. Except from Id, all Item attributes can be shown to the user.")]
  public IEnumerable<Item?> Fetch(
    [Description("The exact Item Ids to fetch. Ids should be kept internal and not shown to the user.")]
    Guid[] ids
  ) => ids.Select(id => _plm.Fetch(id));

  [McpServerTool(ReadOnly = true),
   Description("Returns all Relations of Items with the given Ids. If an Id is unknown, it is ignored.")]
  public IEnumerable<Relation> GetRelations(
    [Description("The exact Item Ids. (Ids should be kept internal and not shown to the user.)")]
    Guid[] ids,
    [Description("An optional criteria for Relation predicates to consider FROM the related Items. Unfiltered if 'null' is given.")]
    [DefaultValue(new[] { "Parent" })]
    string[]? fromPredicate = null,
    [Description("An optional criteria for Relation predicates to consider TO the related Items. Unfiltered if 'null' is given.")]
    [DefaultValue(new[] { "Child" })]
    string[]? toPredicate = null,
    [Description("If bidirectional is 'true' both Relations from and to the Item (in either direction) are returned. If bidirectional is 'false', only the from → to relations are returned.")]
    bool bidirectional = false,
    [Description("When recursively is 'true', the returned relation set is exhaustive for the given predicates — every descendant reachable via those predicates is included. When recursively is 'true', callers should treat items with no outgoing relations in the result as confirmed leaf nodes, not as unexplored.")]
    bool recursively = false
  ) => _plm.GetRelations(Fetch(ids).Where(item => item is not null)!, fromPredicate, toPredicate, bidirectional, recursively);
}