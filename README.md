# legends-of-war-and-magic

## Procedural generation settings presets

The location generator now uses a **ScriptableObject preset** (`ProceduralLocationSettings`) so designers can tweak generation values in a reusable asset instead of editing many MonoBehaviour fields.

### What changed

- `ProceduralLocationGenerator` references a `ProceduralLocationSettings` asset.
- Settings are grouped in one inspector-friendly preset:
  - **Global** (world size)
  - **Seed** (random/fixed seed)
  - **Terrain** (noise + edge falloff)
  - **Props** (enable + category list)
- A default preset is included at:
  - `Assets/Resources/ProceduralGeneration/DefaultProceduralLocationSettings.asset`
- If no preset is assigned on the generator component, it auto-loads the default preset from `Resources` so generation still happens automatically on play.

### How to create and assign a preset in Unity

1. In the Project window, create a preset:
   - `Create -> Legends of War and Magic -> Procedural Generation -> Location Settings`
2. Open the new asset and edit groups:
   - **Global**: `World Width`, `World Length`
   - **Seed**: `Seed Mode` (`Random` or `Fixed`), `Fixed Seed`
   - **Terrain**: resolution, height, fractal noise, and edge falloff values
   - **Props**: toggle placement and edit prop categories
3. Select your scene object with `ProceduralLocationGenerator`.
4. Drag your preset into the **Settings** field.
5. Press Play or use the component context menu **Generate Location**.

### Presets workflow

Create multiple `ProceduralLocationSettings` assets (e.g., forest, rocky, sparse) and swap them on the generator to quickly test different location styles.

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
