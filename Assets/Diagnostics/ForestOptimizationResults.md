# Forest Geometry Diagnostics

- Camera: Diagnostics_ForestCamera_TEMP
- Max distance: 280
- Frustum only: True
- Visible renderer records: 1
- Instanced draw groups: 23
- Visible triangles: 4,272,278
- Visible vertices: 8,404,595
- Shadow casting records: 17

## Top Offenders

| Source | Mesh/Prefab | Count | Tris per instance | Total visible tris | Visible verts | LOD | Shadows | Notes |
|---|---|---:|---:|---:|---:|---|---|---|
| Instanced | Fristy_Tree_03_01 / Tree_3_1_LOD1 / 2_Tree_Leaves | 392 | 1,728 | 677,376 | 878,080 | LOD1 | Off | Generated instanced LOD |
| Instanced | Fristy_Tree_03_02 / Tree_3_2_LOD1 / 2_Tree_Leaves | 420 | 1,576 | 661,920 | 907,200 | LOD1 | Off | Generated instanced LOD |
| Instanced | Fristy_Tree_03_03 / Tree_3_3_LOD0 / 2_Tree_Leaves | 70 | 8,025 | 561,750 | 816,200 | LOD0 | On | Generated instanced LOD |
| Instanced | Fristy_Tree_Prefab_04 / Tree_3_4_LOD1 / 2_Tree_Leaves | 367 | 1,121 | 411,407 | 526,278 | LOD1 | Off | Generated instanced LOD |
| Instanced | Fristy_Tree_03_03 / Tree_3_3_LOD0 / Tree_3_Group | 70 | 4,987 | 349,090 | 816,200 | LOD0 | On | Generated instanced LOD |
| Instanced | Fristy_Tree_03_02 / Tree_3_2_LOD0 / Tree_3_Group | 64 | 4,748 | 303,872 | 477,888 | LOD0 | On | Generated instanced LOD |
| Instanced | Fristy_Tree_03_02 / Tree_3_2_LOD0 / 2_Tree_Leaves | 64 | 4,300 | 275,200 | 477,888 | LOD0 | On | Generated instanced LOD |
| Instanced | Fristy_Tree_03_01 / Tree_3_1_LOD0 / Tree_3_Group | 73 | 3,716 | 271,268 | 394,638 | LOD0 | On | Generated instanced LOD |
| Instanced | Fristy_Tree_03_01 / Tree_3_1_LOD0 / 2_Tree_Leaves | 73 | 2,900 | 211,700 | 394,638 | LOD0 | On | Generated instanced LOD |
| Instanced | Fristy_Tree_Prefab_04 / Tree_3_4_LOD0 / Tree_3_Group | 54 | 2,412 | 130,248 | 159,408 | LOD0 | On | Generated instanced LOD |
| Instanced | Fristy_Tree_03_01 / Tree_3_1_LOD1 / Tree_3_Group | 392 | 259 | 101,528 | 878,080 | LOD1 | On | Generated instanced LOD |
| Instanced | Fristy_Tree_03_02 / Tree_3_2_LOD1 / Tree_3_Group | 420 | 215 | 90,300 | 907,200 | LOD1 | On | Generated instanced LOD |
| Instanced | Fristy_Tree_Prefab_04 / Tree_3_4_LOD1 / Tree_3_Group | 367 | 201 | 73,767 | 526,278 | LOD1 | On | Generated instanced LOD |
| Instanced | Fristy_Tree_Prefab_04 / Tree_3_4_LOD0 / 2_Tree_Leaves | 54 | 1,350 | 72,900 | 159,408 | LOD0 | On | Generated instanced LOD |
| Instanced | Fristy_Rock_02 / 2_Rock / Fristy_Rock_02 | 30 | 1,198 | 35,940 | 23,850 | none | On | Generated instanced |
| Instanced | Fristy_Bush_Vegetation_02 / 1_Grass  / 1_Grass_ | 800 | 16 | 12,800 | 19,200 | none | On | Generated instanced |
| Instanced | Fristy_Bush_Vegetation_02 / Weed 1.001 / 2_Tree_Leaves | 240 | 50 | 12,000 | 12,960 | none | On | Generated instanced |
| Instanced | Fristy_Plant_01 / Plant_1 / Fristy_Plant_Stylized_Art_03 | 20 | 204 | 4,080 | 4,480 | none | On | Generated instanced |
| Instanced | Fristy_Tree_03_03 / Tree_3_3_LOD1 / Billboard | 1009 | 4 | 4,036 | 8,072 | LOD1 | Off | Generated instanced LOD |
| Instanced | Fristy_Rock_04 / 4_Rock / Fristy_Rock_04 | 14 | 276 | 3,864 | 2,464 | none | On | Generated instanced |

## Instanced LOD Groups

| Prefabs | Mesh | Material | LOD | Visible instances | Tris per instance | Visible tris | Shadows |
|---|---|---|---:|---:|---:|---:|---|
| Fristy_Tree_03_01 | Tree_3_1_LOD1 | 2_Tree_Leaves | 1 | 392 | 1,728 | 677,376 | Off |
| Fristy_Tree_03_02 | Tree_3_2_LOD1 | 2_Tree_Leaves | 1 | 420 | 1,576 | 661,920 | Off |
| Fristy_Tree_03_03 | Tree_3_3_LOD0 | 2_Tree_Leaves | 0 | 70 | 8,025 | 561,750 | On |
| Fristy_Tree_Prefab_04 | Tree_3_4_LOD1 | 2_Tree_Leaves | 1 | 367 | 1,121 | 411,407 | Off |
| Fristy_Tree_03_03 | Tree_3_3_LOD0 | Tree_3_Group | 0 | 70 | 4,987 | 349,090 | On |
| Fristy_Tree_03_02 | Tree_3_2_LOD0 | Tree_3_Group | 0 | 64 | 4,748 | 303,872 | On |
| Fristy_Tree_03_02 | Tree_3_2_LOD0 | 2_Tree_Leaves | 0 | 64 | 4,300 | 275,200 | On |
| Fristy_Tree_03_01 | Tree_3_1_LOD0 | Tree_3_Group | 0 | 73 | 3,716 | 271,268 | On |
| Fristy_Tree_03_01 | Tree_3_1_LOD0 | 2_Tree_Leaves | 0 | 73 | 2,900 | 211,700 | On |
| Fristy_Tree_Prefab_04 | Tree_3_4_LOD0 | Tree_3_Group | 0 | 54 | 2,412 | 130,248 | On |
| Fristy_Tree_03_01 | Tree_3_1_LOD1 | Tree_3_Group | 1 | 392 | 259 | 101,528 | On |
| Fristy_Tree_03_02 | Tree_3_2_LOD1 | Tree_3_Group | 1 | 420 | 215 | 90,300 | On |
| Fristy_Tree_Prefab_04 | Tree_3_4_LOD1 | Tree_3_Group | 1 | 367 | 201 | 73,767 | On |
| Fristy_Tree_Prefab_04 | Tree_3_4_LOD0 | 2_Tree_Leaves | 0 | 54 | 1,350 | 72,900 | On |
| Fristy_Rock_02 | 2_Rock | Fristy_Rock_02 | 0 | 30 | 1,198 | 35,940 | On |
| Fristy_Bush_Vegetation_02 | 1_Grass  | 1_Grass_ | 0 | 800 | 16 | 12,800 | On |
| Fristy_Bush_Vegetation_02 | Weed 1.001 | 2_Tree_Leaves | 0 | 240 | 50 | 12,000 | On |
| Fristy_Plant_01 | Plant_1 | Fristy_Plant_Stylized_Art_03 | 0 | 20 | 204 | 4,080 | On |
| Fristy_Tree_03_03 | Tree_3_3_LOD1 | Billboard | 1 | 1009 | 4 | 4,036 | Off |
| Fristy_Rock_04 | 4_Rock | Fristy_Rock_04 | 0 | 14 | 276 | 3,864 | On |
| Fristy_Tree_03_01 | Tree_3_1_LOD2 | Billboard | 2 | 594 | 4 | 2,376 | Off |
| Fristy_Tree_03_02 | Tree_3_2_LOD2 | Billboard | 2 | 582 | 4 | 2,328 | Off |
| Fristy_Tree_Prefab_04 | Tree_3_4_LOD2 | Billboard | 2 | 582 | 4 | 2,328 | Off |
