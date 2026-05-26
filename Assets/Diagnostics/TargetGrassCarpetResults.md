# Forest Geometry Diagnostics

- Camera: TargetGrassCarpet Camera
- Max distance: 160
- Frustum only: True
- Visible renderer records: 0
- Instanced draw groups: 0
- GPU grass records: 2
- Visible triangles: 8,143,800
- Visible vertices: 8,143,800
- Shadow casting records: 0

## Top Offenders

| Source | Mesh/Prefab | Count | Tris per instance | Total visible tris | Visible verts | LOD | Shadows | Notes |
|---|---|---:|---:|---:|---:|---|---|---|
| GPU Grass | GPU Grass High | 9249 | 540 | 4,994,460 | 4,994,460 | GPU Grass High | Off | Chunked GPU instanced grass, estimated batches 65 |
| GPU Grass | GPU Grass Low | 52489 | 60 | 3,149,340 | 3,149,340 | GPU Grass Low | Off | Chunked GPU instanced grass, estimated batches 65 |

## Instanced LOD Groups

| Prefabs | Mesh | Material | LOD | Visible instances | Tris per instance | Visible tris | Shadows |
|---|---|---|---:|---:|---:|---:|---|

## GPU Grass

| LOD | Visible clumps | Tris per clump | Verts per clump | Visible tris | Visible verts | Estimated batches |
|---|---:|---:|---:|---:|---:|---:|
| GPU Grass High | 9,249 | 540 | 540 | 4,994,460 | 4,994,460 | 65 |
| GPU Grass Low | 52,489 | 60 | 60 | 3,149,340 | 3,149,340 | 65 |
