# Graph Report - SCT_Updater  (2026-11-09)

## Corpus Check
- 31 files · ~13,837 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 12 file(s) not represented in the graph (top: (none) 2, .config 2, .resx 2)

## Summary
- 286 nodes · 567 edges · 16 communities (12 shown, 4 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 54 edges (avg confidence: 0.87)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `c8b6eec8`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Log.Debug
- Form1.cs
- Form1
- Product
- CLAUDE.md
- release_manager.py
- Root Cause Tracing
- Systematic Debugging
- Condition-Based Waiting
- Defense-in-Depth Validation
- Karpathy Guidelines
- Resources
- find-polluter.sh

## God Nodes (most connected - your core abstractions)
1. `Form1` - 36 edges
2. `UpdateService` - 24 edges
3. `Product` - 17 edges
4. `NextcloudClient` - 15 edges
5. `FileManifest` - 14 edges
6. `Systematic Debugging` - 11 edges
7. `SCT_Updater` - 10 edges
8. `AppConfig` - 10 edges
9. `Root Cause Tracing` - 10 edges
10. `Condition-Based Waiting` - 9 edges

## Surprising Connections (you probably didn't know these)
- `Architecture` --references--> `AppConfig`  [INFERRED]
  CLAUDE.md → SCT_Updater/AppConfig.cs
- `Architecture` --references--> `Form1`  [INFERRED]
  CLAUDE.md → SCT_Updater/Form1.Designer.cs
- `Architecture` --references--> `NextcloudClient`  [INFERRED]
  CLAUDE.md → SCT_Updater/NextcloudClient.cs
- `Architecture` --references--> `LocalStateService`  [INFERRED]
  CLAUDE.md → SCT_Updater/LocalStateService.cs
- `Architecture` --references--> `UpdateService`  [INFERRED]
  CLAUDE.md → SCT_Updater/UpdateService.cs

## Import Cycles
- None detected.

## Communities (16 total, 4 thin omitted)

### Community 0 - "Log.Debug"
Cohesion: 0.13
Nodes (5): Architecture, LocalStateService, Log, UpdateService, Utility

### Community 1 - "Form1.cs"
Cohesion: 0.11
Nodes (4): SCT_Updater, SCT_Updater.Properties, Settings, Default

### Community 2 - "Form1"
Cohesion: 0.07
Nodes (11): AppConfig, NC_DEVICE_CONFIGS_URL, NC_DRIVERS_URL, NC_PASSWORD, NC_SERVER_URL, NC_USER, NC_WEBDAV_BASE_URL, SUITE_MANIFEST_URL (+3 more)

### Community 3 - "Product"
Cohesion: 0.08
Nodes (26): FileManifest, BaseUrl, Files, PackageMode, ProductId, Version, InstalledProduct, Id (+18 more)

### Community 4 - "CLAUDE.md"
Cohesion: 0.08
Nodes (22): [1.0.0] - 2026-01-23, [1.0.1] - 2026-05-07, [1.1.0] - 2026-08-11, Added, Added, Changelog, Features, Fixed (+14 more)

### Community 5 - "release_manager.py"
Cohesion: 0.13
Nodes (6): ensure_remote_dir(), get_nc_client(), get_sha256(), main(), prepare_package(), upload_chunked()

### Community 6 - "Root Cause Tracing"
Cohesion: 0.12
Nodes (15): 1. Observe the Symptom, 2. Find Immediate Cause, 3. Ask: What Called This?, 4. Keep Tracing Up, 5. Find Original Trigger, Adding Stack Traces, Finding Which Test Causes Pollution, Key Principle (+7 more)

### Community 7 - "Systematic Debugging"
Cohesion: 0.12
Nodes (15): Common Rationalizations, Overview, Phase 1: Root Cause Investigation, Phase 2: Pattern Analysis, Phase 3: Hypothesis and Testing, Phase 4: Implementation, Quick Reference, Red Flags - STOP and Follow Process (+7 more)

### Community 8 - "Condition-Based Waiting"
Cohesion: 0.15
Nodes (12): Common Mistakes, Condition-Based Waiting, Core Pattern, waitForEvent(), waitForEventCount(), waitForEventMatch(), Implementation, Overview (+4 more)

### Community 9 - "Defense-in-Depth Validation"
Cohesion: 0.17
Nodes (11): Applying the Pattern, Defense-in-Depth Validation, Example from Session, Key Insight, Layer 1: Entry Point Validation, Layer 2: Business Logic Validation, Layer 3: Environment Guards, Layer 4: Debug Instrumentation (+3 more)

### Community 10 - "Karpathy Guidelines"
Cohesion: 0.33
Nodes (5): 1. Think Before Coding, 2. Simplicity First, 3. Surgical Changes, 4. Goal-Driven Execution, Karpathy Guidelines

### Community 11 - "Resources"
Cohesion: 0.33
Nodes (3): Resources, Culture, ResourceManager

## Knowledge Gaps
- **29 isolated node(s):** `NC_USER`, `NC_PASSWORD`, `NC_SERVER_URL`, `NC_WEBDAV_BASE_URL`, `SUITE_MANIFEST_URL` (+24 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 142 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **4 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Form1` connect `Form1` to `Log.Debug`, `Form1.cs`, `Product`?**
  _High betweenness centrality (0.127) - this node is a cross-community bridge._
- **Are the 2 inferred relationships involving `Form1` (e.g. with `Architecture` and `Program.Main()`) actually correct?**
  _`Form1` has 2 INFERRED edges - model-reasoned connections that need verification._
- **What connects `NC_USER`, `NC_PASSWORD`, `NC_SERVER_URL` to the rest of the system?**
  _29 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Log.Debug` be split into smaller, more focused modules?**
  _Cohesion score 0.13032581453634084 - nodes in this community are weakly interconnected._
- **Why does `Architecture` connect `Log.Debug` to `Form1`, `CLAUDE.md`?**
  _High betweenness centrality (0.124) - this node is a cross-community bridge._
- **Are the 2 inferred relationships involving `UpdateService` (e.g. with `Architecture` and `Form1.Form1()`) actually correct?**
  _`UpdateService` has 2 INFERRED edges - model-reasoned connections that need verification._
- **Should `Form1.cs` be split into smaller, more focused modules?**
  _Cohesion score 0.10510510510510511 - nodes in this community are weakly interconnected._