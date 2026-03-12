# legends-of-war-and-magic

## Procedural prop LOD preparation

The procedural generator now spawns props in an LOD-aware way:

- If a spawned prefab already contains a Unity `LODGroup`, it is preserved and used as-is.
- If a prefab has no `LODGroup`, generation still works, but the placement step can emit warnings so missing LOD setup is visible during development.
- Per prop category, you can optionally enable a simple max draw distance (generator-side distance culling) without changing placement logic.

### Preparing prefabs for best LOD results

1. Add an `LODGroup` component to the prefab root (or a child), and configure LOD renderers/screensize thresholds.
2. Ensure each LOD level references the correct renderer set and lower-poly meshes/materials.
3. Keep pivot/origin placement consistent across LOD levels so transitions are stable while the camera moves.
4. For very dense categories, optionally set `Max Draw Distance` in category settings as a fallback culling control.

If `Expect LOD Group` is enabled on a category, any prefab without an `LODGroup` will generate explicit warnings in logs.
