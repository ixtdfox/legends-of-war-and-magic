# GPU Grass Optimization Results

Validation target: dense Fristy grass carpet using `Fristy_Grass_02_Ver_00` with the High forest quality preset.

| Metric | Result | Notes |
|---|---:|---|
| Visible clumps | 61,738 | capped and chunk-culled |
| High LOD clumps | 9,249 | full 9-renderer Fristy cluster |
| Low LOD clumps | 52,489 | one-renderer Fristy proxy |
| GPU grass batches | 65 | grouped by LOD mesh and cloned source material |
| Tris | 8,143,800 | includes high + low GPU grass |
| Verts | 8,143,800 | includes high + low GPU grass |
| Shadow casters | 0 | grass realtime shadows disabled |

Preview: `Assets/Diagnostics/TargetGrassCarpetPreview.png`

Detailed capture: `Assets/Diagnostics/TargetGrassCarpetResults.md`
