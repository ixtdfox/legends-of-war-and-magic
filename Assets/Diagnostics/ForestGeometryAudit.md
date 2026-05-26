# Forest Geometry Audit

## Current bottlenecks

- The main geometry issue was not draw calls. Generated props already use `Graphics.RenderMeshInstanced`, which explains the good batch count.
- The procedural tree path was extracting only LOD0 from tree prefabs. Existing tree prefab LOD1/LOD2 meshes, including 4-triangle billboards, were not being used by the instanced renderer.
- Tree leaves and billboards inherited realtime shadow casting from the source prefabs. That made distant foliage and impostors eligible for shadow rendering.
- Tree source prefabs are the dominant geometry source. Rocks and bushes are secondary, but most tree prefabs are 5.7k-14.3k triangles at source and 3.7k-13k triangles in LOD0.

## Top expensive meshes

| Mesh/Prefab | Count | Tris per instance | Total tris | Has LOD | Notes |
|---|---:|---:|---:|---|---|
| Fristy_Tree_Prefab_03 / Tree_3_3_LOD0 | 1 prefab | 13,012 | 13,012 | Yes, 2 LODs | LOD1 is a 4-triangle billboard. Previously unused by instancing. |
| Fristy_Tree_Prefab_02 / Tree_3_2_LOD0 | 1 prefab | 9,048 | 9,048 | Yes, 3 LODs | LOD1 is 1,791 tris, LOD2 is 4 tris. |
| Fristy_Tree_03_03 / Tree_3_3_LOD0 | 1 prefab | 13,012 | 13,012 | Yes, 2 LODs | Runtime catalog currently uses this direct tree prefab. |
| Fristy_Tree_03_02 / Tree_3_2_LOD0 | 1 prefab | 9,048 | 9,048 | Yes, 3 LODs | Good LOD chain exists in source asset. |
| Fristy_Tree_Prefab_01 / Tree_3_1_LOD0 | 1 prefab | 6,616 | 6,616 | Yes, 3 LODs | LOD1 is 1,987 tris, LOD2 is 4 tris. |
| Fristy_Tree_Prefab_04 / Tree_3_4_LOD0 | 1 prefab | 3,762 | 3,762 | Yes, 3 LODs | Cheapest tree LOD0; still benefits from LOD1 and billboard. |
| Fristy_Rock_Boulder_And_Vegetation_01 | 1 prefab | 6,532 | 6,532 | No | Contains many vegetation renderers; rock-only instancing filter avoids most plant pieces for rock roles. |
| Fristy_Rock_Boulder_02 | 1 prefab | 4,792 | 4,792 | No | Candidate for future manual LOD if large rocks become visible at long distances. |
| Fristy_Bush_Vegetation_01 | 1 prefab | 2,516 | 2,516 | No | No LODGroup; safe future target for low-poly/billboard variants. |

## Recommendations implemented

- Added `ForestLodSettings`, `ForestRenderingSettings`, and `ForestQualityLevel` presets: Low, Medium, High, Ultra.
- Wired forest rendering settings into `ProceduralLocationSettings` and `MapGenerationPresetMapper`.
- Updated `GeneratedInstancedPropRenderer` to register all LODGroup renderers for tree roles instead of only LOD0.
- Added distance-based instanced LOD selection:
  - High preset LOD0: 0-48m.
  - High preset LOD1: 48-105m.
  - High preset LOD2/billboard: 105m-cull.
  - Tree cull distance: capped by category draw distance and preset cull distance.
- Added shadow rules in the instanced renderer:
  - LOD0 keeps source shadows.
  - LOD1 leaves and billboards cast no shadows.
  - LOD2/billboards cast no shadows.
  - Trunk/large branch material can still cast shadows in LOD1.
- Preserved batching/instancing:
  - Materials are still cloned once per source material with `enableInstancing = true`.
  - LODs create additional instanced draw groups, but each group remains batched up to 1023 instances.
  - No `.material` per-instance cloning was introduced.
- Added editor diagnostics:
  - `Tools/Legends of War and Magic/Diagnostics/Forest Geometry Diagnostics`
  - Captures visible scene renderers and generated instanced draw groups.
  - Groups top offenders by prefab/mesh/material/LOD.
  - Writes markdown and CSV reports to `Assets/Diagnostics`.

## Forest Optimization Results

Test capture:

- Generated small mainland/high-density forest via `MapGenerationPresetMapper`.
- Request: Small, Mainland, Hills, High prop density, TreeDensity 1.0, seed text `forest-geometry-diagnostics`.
- Spawned 10,871 `ForestCoreTrees`.
- Camera: `(0, 70, -115)`, FOV 55, far clip 280.
- Diagnostic output: `Assets/Diagnostics/ForestOptimizationResults.md`.

| Metric | Before | After | Notes |
|---|---:|---:|---|
| FPS | 229 | Not captured | Unity Stats after value needs a Game View capture on the target scene/camera. |
| CPU main | 4.4 ms | Not captured | Runtime code path remains instanced; no per-tree GameObject renderers were added. |
| Batches | 248 | Not captured | Diagnostic capture produced 23 instanced draw groups for generated props; Unity Stats should remain well under the target budget. |
| SetPass Calls | 68 | Not captured | Materials remain shared/instanced; no unique per-instance materials were added. |
| Tris | 24.1M | 4.27M diagnostic estimate | Estimate covers visible generated instanced props plus visible scene renderers in the test capture. |
| Verts | 20.3M | 8.40M diagnostic estimate | Vertex estimate is conservative because submesh groups can count the same mesh vertices more than once. |
| Shadow casters | 112 | 17 diagnostic shadow-casting records | Leaves/billboards at LOD1+ are now shadow-off. |

## Remaining risks

- The after table uses the new diagnostics capture, not Unity Game View Stats. Re-run the same camera angle in Game View with Stats visible to fill exact FPS/Batches/SetPass/Tris/Verts.
- Bushes and some rocks still have no LODGroup. They are not the top issue in the dense forest test, but they are future manual LOD candidates.
- `maxHighDetailTrees` and `maxShadowCastingTrees` are exposed in presets as budget knobs. Current enforcement is primarily through distance bands and LOD shadow rules.
- The two-LOD `Fristy_Tree_03_03` jumps from LOD0 directly to billboard. If visual popping is noticeable, it needs a manual mid LOD mesh.

## Tunable parameters

- `Assets/Scripts/ProceduralGeneration/Config/ForestRenderSettings.cs`
  - `lod0Distance`, `lod1Distance`, `lod2Distance`, `cullDistance`
  - `shadowDistance`
  - `disableLeafShadowsAfterLod0`
  - `disableAllShadowsAfterLod1`
  - `foliageDensityScale`
- Runtime/editor switching:
  - Use the diagnostics window to apply Low/Medium/High/Ultra to active `GeneratedInstancedPropRenderer` components.
- Map generation:
  - `MapGenerationPresetMapper` applies the forest preset to runtime settings and caps tree draw distance through the forest cull distance.
