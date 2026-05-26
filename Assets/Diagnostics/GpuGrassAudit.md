# GPU Grass Audit

## Target look

Reference: `https://www.reddit.com/r/Unity3D/comments/1cn8yq4/free_procedural_gpu_grass_package/`

The implemented project version focuses on the same practical direction: procedural GPU grass, distance culling, LOD, and a bounded triangle budget. It is not an imported copy of that package.

## What changed

- Added `GeneratedGpuGrassRenderer`.
- Added `GpuGrassGenerationStep` to the procedural generation pipeline.
- Added `Legends/Procedural GPU Grass` as a fallback shader, but the normal path now clones the original Fristy grass material to preserve prefab color.
- Switched GPU grass geometry to `Assets/Resources/Prefabs/Grass/Fristy_Grass_02_Ver_00.prefab`.
- High LOD uses the full 9-renderer Fristy grass cluster: 540 triangles per clump.
- Low LOD uses one renderer from the same Fristy grass prefab: 60 triangles per clump.
- Generated meshes are pivot-normalized so the Fristy clump bottom sits on the terrain instead of sinking below it.
- GPU grass clones the original `Fristy_Plant_Stylized_Art_03` material, so grass keeps the prefab's original color and material response.
- Added `GpuGrassSettings` presets through the existing forest quality system.
- Terrain grass detail layers are skipped when GPU grass is enabled.
- Non-grass terrain details remain, but their density and draw distance are reduced so flowers/low plants do not dominate the frame.
- Diagnostics now report GPU grass as its own geometry source, including triangles and vertices.

## Runtime architecture

- Grass is divided into terrain chunks.
- Chunks generate matrices lazily only when a camera can see them.
- Visible chunks are sorted near-to-far and capped by `maxVisibleClumps`.
- Draw submission is grouped by high/low grass LOD mesh and cloned source-material slots.
- Grass casts no realtime shadows.
- Grass can receive shadows where the active shader/pipeline supports it.
- No per-instance material cloning or per-frame GameObject spawning is used.

## High preset defaults

| Setting | Value |
|---|---:|
| drawDistance | 120m |
| highDetailDistance | 34m |
| chunkSize | 16m |
| placementSpacing | 0.46m |
| maxVisibleClumps | 80,000 |
| terrainDetailFallbackDistance | 60m |
| windStrength | 0.23 |
| windSpeed | 1.25 |
| windScale | 0.13 |

## Diagnostic capture

Target "solid carpet" capture:

- Uses `ForestQualityLevel.High`.
- Uses `Fristy_Grass_02_Ver_00` for both high and low GPU grass LOD meshes.
- Report: `Assets/Diagnostics/TargetGrassCarpetResults.md`.
- Preview: `Assets/Diagnostics/TargetGrassCarpetPreview.png`.

| Metric | Result |
|---|---:|
| GPU grass terrain chunks | 81 |
| Visible clumps | 61,738 |
| High LOD clumps | 9,249 |
| Low LOD clumps | 52,489 |
| Estimated GPU grass batches | 65 |
| GPU grass triangles | 8.14M |
| GPU grass vertices | 8.14M |
| Shadow casters | 0 |

## Remaining tuning

- Current High preset is intentionally dense enough to hide terrain in meadow camera angles.
- For even denser meadow shots, increase `GpuGrassSettings.densityScale` or reduce `placementSpacing`.
- For lower-end targets, reduce `drawDistance`, `highDetailDistance`, or `maxVisibleClumps`.
- Player/brush interaction is not implemented yet. The renderer is structured so an interaction mask can be added later without changing generation.
- Grass shadows are intentionally disabled for performance; visual depth should come from terrain shadows, tree shadows, and the original Fristy material.
