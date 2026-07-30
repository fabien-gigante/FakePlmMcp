# Fake PLM MCP
A local MCP Server for a fake PLM Service as a learning exercise and experiment

## Typical conversation from Claude Desktop

> Use my Fake PLM tools. I'd like to identify the maturity state discrepancies between the complete EBOM and related MBOM of product Root. Summarize in a nice table.

I pulled the full EBOM and MBOM structures for Root and matched each engineering item to its scoped manufacturing counterpart. Here's the comparison:

| Level | EBOM Item (Eng) | EBOM Rev | EBOM Maturity | MBOM Item (Manuf) | MBOM Rev | MBOM Maturity | Discrepancy? |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Root | Prd Root | A | In Work | Mfg Prd Root | A | In Work | ✅ Aligned |
| Child 1 | Prd 1 | A | Frozen | Mfg Prd 1 | B | Draft | ⚠️ Yes --- MBOM lags behind (Frozen vs Draft) |
| Child 2 | Prd 2 | B | Obsolete | Mfg Prd 2 | A | In Work | ⚠️ Yes --- MBOM still active while EBOM is obsolete |

Summary of findings:

-   Root itself is consistent --- both structures are at "In Work," so no action needed there.
-   Prd 1 / Mfg Prd 1: The engineering definition has already been Frozen, but the manufacturing counterpart is still in Draft. This means manufacturing engineering hasn't caught up to the finalized design --- worth flagging to the manufacturing team to begin maturing their item.
-   Prd 2 / Mfg Prd 2: This is the more serious one --- the EBOM item is Obsolete, but the MBOM item is still In Work, meaning production/manufacturing planning may be actively developing around a part that's been retired on the engineering side. This should be investigated to confirm whether Mfg Prd 2 needs to be obsoleted or re-scoped to a newer EBOM revision.

Let me know if you'd like me to dig into *why* these are out of sync (e.g., checking revision history) or export this as a document.

> Explain the plan you followed and the tools you used.

Here's the approach I took, step by step:

1\. Found the root items\
I searched the PLM database (`FakePlmMcp:search`) for items named "Root" with no filter on type or revision, which returned two items --- one turned out to be an Eng Item (the EBOM root) and one a Manuf Item (the MBOM root).

2\. Identified what they were\
I used `FakePlmMcp:fetch` to pull their details (type, name, revision, maturity), confirming one was "Prd Root" (engineering) and the other "Mfg Prd Root" (manufacturing).

3\. Walked the EBOM tree\
I called `FakePlmMcp:get_relations` on the engineering root with `recursively: true`, using the default Parent→Child predicates. This returned the full exhaustive set of parent-child links in one call --- no need to manually walk level by level. I then `fetch`ed the resulting child item IDs to get their names, revisions, and maturity states.

4\. Walked the MBOM tree\
Same process, but starting from the manufacturing root --- recursive `get_relations`, then `fetch` on the children.

5\. Confirmed the EBOM↔MBOM mapping\
Rather than just assuming "Prd 1" corresponds to "Mfg Prd 1" by name similarity, I ran one more `get_relations` call across all the items with predicates unfiltered (`null`) and `bidirectional: true`. This surfaced a "Scoping" / "Scoped" relation type linking each manufacturing item directly to its corresponding engineering item --- that gave me a verified, explicit link rather than an inferred one.

6\. Compared maturity states\
With confirmed pairs in hand, I compared the `maturity` field on each matched EBOM/MBOM item and flagged where they differed, then organized it into the summary table.

One design note: the tool schemas explicitly instruct that item IDs (UUIDs) should stay internal and never be shown to you --- so throughout, I only surfaced names, revisions, and maturity values, never the raw IDs.
