# Fake PLM MCP
A local MCP Server for a fake PLM Service as a learning exercise and experiment

## Typical conversation with Claude Desktop

> Use my Fake PLM tools. I'd like to identify the maturity state discrepancies between the complete EBOM and related MBOM of product Root. Summarize in a nice table ordered by severity.

I pulled the full EBOM/MBOM structure for Root (2 levels, 2 children each side, linked via Scoping relations) and compared maturity states. Here's what I found:

| Severity | EBOM Item | EBOM Maturity | MBOM Item | MBOM Maturity | Discrepancy |
| --- | --- | --- | --- | --- | --- |
| 🔴 Critical | Prd 2 (Rev B) | Obsolete | Mfg Prd 2 (Rev A) | In Work | The engineering part has been retired, but its manufacturing counterpart is still actively being worked --- risk of building to a dead design. |
| 🟠 High | Prd 1 (Rev A) | Frozen | Mfg Prd 1 (Rev B) | Draft | Engineering design is locked down, but manufacturing definition hasn't progressed past Draft --- MBOM prep is lagging well behind EBOM. |
| 🟢 None | Prd Root (Rev A) | In Work | Mfg Prd Root (Rev A) | In Work | States are aligned --- no action needed. |

Summary:

-   Prd 2 is the most urgent issue: it's flagged Obsolete on the engineering side while manufacturing treats it as still in active development. This should be reviewed immediately to avoid building an obsolete component.
-   Prd 1 shows a significant maturity gap --- the design is Frozen but manufacturing hasn't advanced past Draft, suggesting the MBOM update process hasn't kept pace.
-   The Root assembly itself is consistent across both BOMs.

> Explain the plan you followed and details the tools you used at each step.

Deliberated on balancing transparency while safeguarding technical implementation details

Deliberated on balancing transparency while safeguarding technical implementation details

Here's the step-by-step approach I took using the Fake PLM MCP tools:

1\. Locate the product "Root"

-   Tool: `FakePlmMcp:search` with `name="Root"`, `revision=null`, `type=null`
-   This returned two items: an Engineering-side item and a Manufacturing-side item, since "Root" exists as both an EBOM root and an MBOM root.

2\. Identify what those items actually are

-   Tool: `FakePlmMcp:fetch` on both returned ids
-   This showed one was an Eng Item ("Prd Root", maturity: In Work) --- the EBOM root --- and the other a Manuf Item ("Mfg Prd Root", maturity: In Work) --- the MBOM root.

3\. Discover the relation schema (exploratory pass)

-   Tool: `FakePlmMcp:get_relations` on the EBOM root, with `bidirectional=true` and both predicates left `null` (allowed only for this first discovery call)
-   This revealed two predicate types in play:
    -   `Parent → Child`: the BOM structure (assembly to sub-item)
    -   `Scoping → Scoped`: the link connecting a Manuf Item to its corresponding Eng Item

4\. Traverse the full EBOM tree

-   Tool: `FakePlmMcp:get_relations` with `fromPredicate=["Parent"]`, `toPredicate=["Child"]`, `recursively=true`, on the EBOM root
-   Returned two children with no further descendants --- confirming a flat, 2-item EBOM structure (both leaves).

5\. Traverse the full MBOM tree

-   Same call, recursive, but rooted at the Manuf Item instead --- again returned two children, confirming a flat, 2-item MBOM structure.

6\. Fetch details for all four child items

-   Tool: `FakePlmMcp:fetch` on all four child ids at once
-   Got their names, types, revisions, and maturity states in one batch call.

7\. Map each Manuf child to its corresponding Eng child

-   Tool: `FakePlmMcp:get_relations` with `fromPredicate=["Scoping"]`, `toPredicate=["Scoped"]` on the two Manuf children
-   This gave the explicit EBOM↔MBOM pairing needed to compare maturity states correctly (rather than guessing pairings from names, which the tool description explicitly warns against).

8\. Compare and rank

-   With root-to-root and child-to-child pairs established, I compared maturity states for each pair, assessed how far apart they were in a typical maturity lifecycle (Draft → In Work → Frozen → Released → Obsolete), and ranked the mismatches by risk --- treating "MBOM still active while EBOM is Obsolete" as more severe than "MBOM lagging a Frozen EBOM."

Throughout, I kept item IDs internal (never displayed them) and relied on the `Scoping`/`Scoped` relations --- not name-matching --- to pair EBOM and MBOM items, since the tool explicitly cautions against inferring connectivity from names/attributes alone.