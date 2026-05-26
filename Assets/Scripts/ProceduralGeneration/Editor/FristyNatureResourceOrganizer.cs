using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Editor
{
    public static class FristyNatureResourceOrganizer
    {
        private const string SourceRoot = "Assets/Fristy stylize Modular Assets 2";
        private const string ResourceRoot = "Assets/Resources";
        private const string PrefabRoot = ResourceRoot + "/Prefabs";
        private const string ModelRoot = ResourceRoot + "/Models";
        private const string MaterialRoot = ResourceRoot + "/Materials";
        private const string TextureRoot = ResourceRoot + "/Textures";
        private const string ShaderRoot = ResourceRoot + "/Shaders";
        private const string TerrainLayerRoot = ResourceRoot + "/TerrainLayers";

        private static readonly string[] ResourceTypeRoots =
        {
            PrefabRoot,
            ModelRoot,
            MaterialRoot,
            TextureRoot,
            ShaderRoot,
            TerrainLayerRoot
        };

        private static readonly string[] Categories =
        {
            "Trees",
            "Bushes",
            "Rocks",
            "Grass",
            "Plants",
            "Vines",
            "ShorePlants",
            "Terrain",
            "Misc"
        };

        [MenuItem("Tools/Legends of War and Magic/Procedural Generation/Organize Fristy Nature Resources")]
        public static void OrganizeAndRebuildCatalog()
        {
            var result = OrganizeResources();
            var catalog = ProceduralEnvironmentAssetCatalogBuilder.RebuildDefaultCatalogFromImportedAssets();

            Debug.Log(
                "Fristy nature resources organized and catalog rebuilt. " +
                $"Copied={result.CopiedAssets}, ConvertedMaterials={result.ConvertedMaterials}, " +
                $"RemappedFiles={result.RemappedFiles}, RepairedReferences={result.RepairedMissingReferences}, " +
                $"PropCategories={catalog.PropCategories.Count}, " +
                $"TerrainDetails={catalog.TerrainDetails.Count}.");
        }

        public static OrganizationResult OrganizeResources()
        {
            if (!AssetDatabase.IsValidFolder(SourceRoot))
            {
                Debug.LogError($"Cannot organize Fristy nature resources: source folder was not found at '{SourceRoot}'.");
                return new OrganizationResult();
            }

            EnsureResourceFolders();

            var guidRemap = new Dictionary<string, string>();
            var copiedPaths = new List<string>();
            var result = new OrganizationResult();

            AssetDatabase.StartAssetEditing();
            try
            {
                result.CopiedAssets += CopyAssets("t:Texture2D", TextureRoot, guidRemap, copiedPaths);
                result.CopiedAssets += CopyAssets("t:Material", MaterialRoot, guidRemap, copiedPaths);
                result.CopiedAssets += CopyAssets("t:Model", ModelRoot, guidRemap, copiedPaths);
                result.CopiedAssets += CopyAssets("t:Prefab", PrefabRoot, guidRemap, copiedPaths);
                result.CopiedAssets += CopyAssets("t:TerrainLayer", TerrainLayerRoot, guidRemap, copiedPaths);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();

            result.RemappedFiles = RemapCopiedAssetGuids(copiedPaths, guidRemap);
            ConfigureTextureImporters(copiedPaths);
            AssetDatabase.Refresh();

            result.ConvertedMaterials = ConvertResourceMaterials();
            result.RepairedMissingReferences = RepairMissingResourceReferences();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return result;
        }

        private static int CopyAssets(
            string filter,
            string typeRoot,
            Dictionary<string, string> guidRemap,
            List<string> copiedPaths)
        {
            var copied = 0;
            var guids = AssetDatabase.FindAssets(filter, new[] { SourceRoot });
            Array.Sort(guids, StringComparer.Ordinal);

            for (var i = 0; i < guids.Length; i++)
            {
                var sourcePath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!ShouldCopy(sourcePath))
                {
                    continue;
                }

                var category = ClassifyCategory(sourcePath);
                var destinationPath = BuildDestinationPath(sourcePath, typeRoot, category);
                EnsureFolder(Path.GetDirectoryName(destinationPath)?.Replace("\\", "/"));

                if (AssetDatabase.LoadMainAssetAtPath(destinationPath) != null)
                {
                    AssetDatabase.DeleteAsset(destinationPath);
                }

                if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
                {
                    Debug.LogWarning($"Failed to copy Fristy asset '{sourcePath}' to '{destinationPath}'.");
                    continue;
                }

                var destinationGuid = AssetDatabase.AssetPathToGUID(destinationPath);
                if (!string.IsNullOrWhiteSpace(destinationGuid))
                {
                    guidRemap[guids[i]] = destinationGuid;
                }

                copiedPaths.Add(destinationPath);
                copied++;
            }

            return copied;
        }

        private static int RemapCopiedAssetGuids(IReadOnlyList<string> copiedPaths, IReadOnlyDictionary<string, string> guidRemap)
        {
            var remappedFiles = 0;
            for (var i = 0; i < copiedPaths.Count; i++)
            {
                var path = copiedPaths[i];
                remappedFiles += RemapGuidsInTextFile(path, guidRemap) ? 1 : 0;
                remappedFiles += RemapGuidsInTextFile(path + ".meta", guidRemap) ? 1 : 0;
            }

            for (var i = 0; i < copiedPaths.Count; i++)
            {
                AssetDatabase.ImportAsset(copiedPaths[i], ImportAssetOptions.ForceUpdate);
            }

            return remappedFiles;
        }

        private static bool RemapGuidsInTextFile(string path, IReadOnlyDictionary<string, string> guidRemap)
        {
            if (!File.Exists(path) || IsBinaryAsset(path))
            {
                return false;
            }

            var text = File.ReadAllText(path);
            var updated = text;
            foreach (var pair in guidRemap)
            {
                updated = updated.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
            }

            if (updated == text)
            {
                return false;
            }

            File.WriteAllText(path, updated);
            return true;
        }

        private static void ConfigureTextureImporters(IReadOnlyList<string> copiedPaths)
        {
            for (var i = 0; i < copiedPaths.Count; i++)
            {
                var path = copiedPaths[i];
                if (!IsTexturePath(path))
                {
                    continue;
                }

                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                {
                    continue;
                }

                var lower = path.ToLowerInvariant();
                importer.textureType = ContainsAny(lower, "normal", "_nrm", "_nm", "normals")
                    ? TextureImporterType.NormalMap
                    : TextureImporterType.Default;
                importer.alphaIsTransparency = ContainsAny(lower, "leaf", "leaves", "branch", "grass", "plant", "weed", "ivy");
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
        }

        private static int ConvertResourceMaterials()
        {
            var hdrpLit = Shader.Find("HDRP/Lit");
            if (hdrpLit == null)
            {
                Debug.LogError("Cannot convert Fristy materials: HDRP/Lit shader was not found.");
                return 0;
            }

            var converted = 0;
            var materialGuids = AssetDatabase.FindAssets("t:Material", new[] { ResourceRoot });
            for (var i = 0; i < materialGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(materialGuids[i]);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    continue;
                }

                ConvertMaterialToHdrpLit(material, path, hdrpLit);
                EditorUtility.SetDirty(material);
                converted++;
            }

            return converted;
        }

        private static int RepairMissingResourceReferences()
        {
            var repaired = RepairMissingModelMaterialReferences();
            repaired += ClearMissingTextureReferencesInMaterials();
            repaired += RepairGeneratedModelMaterials();
            if (repaired > 0)
            {
                AssetDatabase.Refresh();
            }

            return repaired;
        }

        private static int RepairMissingModelMaterialReferences()
        {
            if (!Directory.Exists(ModelRoot))
            {
                return 0;
            }

            var repairedFiles = 0;
            var materialReferencePattern = new Regex(
                "name: (?<name>.+?)\\n\\s*second: \\{fileID: 2100000, guid: (?<guid>[0-9a-f]{32}), type: 2\\}",
                RegexOptions.Multiline);
            var metaPaths = Directory.GetFiles(ModelRoot, "*.fbx.meta", SearchOption.AllDirectories);
            for (var i = 0; i < metaPaths.Length; i++)
            {
                var text = File.ReadAllText(metaPaths[i]);
                var repaired = false;
                var updated = materialReferencePattern.Replace(
                    text,
                    match =>
                    {
                        var guid = match.Groups["guid"].Value;
                        if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
                        {
                            return match.Value;
                        }

                        var replacementGuid = ResolveKnownMaterialGuid(match.Groups["name"].Value);
                        if (string.IsNullOrEmpty(replacementGuid))
                        {
                            return match.Value;
                        }

                        repaired = true;
                        return match.Value.Replace(guid, replacementGuid, StringComparison.Ordinal);
                    });

                if (!repaired)
                {
                    continue;
                }

                File.WriteAllText(metaPaths[i], updated);
                repairedFiles++;
            }

            return repairedFiles;
        }

        private static int ClearMissingTextureReferencesInMaterials()
        {
            if (!Directory.Exists(ResourceRoot))
            {
                return 0;
            }

            var repairedFiles = 0;
            var textureReferencePattern = new Regex(
                "m_Texture: \\{fileID: [0-9-]+, guid: (?<guid>[0-9a-f]{32}), type: [0-9]+\\}");
            var materialPaths = Directory.GetFiles(ResourceRoot, "*.mat", SearchOption.AllDirectories);
            for (var i = 0; i < materialPaths.Length; i++)
            {
                var text = File.ReadAllText(materialPaths[i]);
                var repaired = false;
                var updated = textureReferencePattern.Replace(
                    text,
                    match =>
                    {
                        var guid = match.Groups["guid"].Value;
                        if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
                        {
                            return match.Value;
                        }

                        repaired = true;
                        return "m_Texture: {fileID: 0}";
                    });

                if (!repaired)
                {
                    continue;
                }

                File.WriteAllText(materialPaths[i], updated);
                repairedFiles++;
            }

            return repairedFiles;
        }

        private static int RepairGeneratedModelMaterials()
        {
            var repaired = 0;
            repaired += ApplyMaterialTemplate($"{ModelRoot}/Trees/Materials/Tree_3_Group.mat", $"{MaterialRoot}/Trees/Fristy_Tree_03_Group.mat");
            repaired += ApplyMaterialTemplate($"{ModelRoot}/Trees/Materials/2_Tree_Leaves.mat", $"{MaterialRoot}/Trees/Fristy_Tree_02_Leaves.mat");
            repaired += ApplyMaterialTemplate($"{ModelRoot}/Trees/Materials/Billboard.mat", $"{MaterialRoot}/Trees/Fristy_Tree_Billboard.mat");
            repaired += ApplyMaterialTemplate($"{ModelRoot}/Plants/Materials/Plants.mat", $"{MaterialRoot}/Plants/Fristy_Plant_Common.mat");
            repaired += ApplyMaterialTemplate($"{ModelRoot}/Plants/Materials/stylized art plants 3.mat", $"{MaterialRoot}/Plants/Fristy_Plant_Stylized_Art_03.mat");
            repaired += ApplyMaterialTemplate($"{ModelRoot}/Plants/Materials/2_Tree_Leaves.mat", $"{MaterialRoot}/Trees/Fristy_Tree_02_Leaves.mat");
            repaired += ApplyMaterialTemplate($"{ModelRoot}/Rocks/Materials/2_Rock.mat", $"{MaterialRoot}/Rocks/Fristy_Rock_02.mat");
            repaired += ApplyMaterialTemplate($"{ModelRoot}/Rocks/Materials/3_Rock.mat", $"{MaterialRoot}/Rocks/Fristy_Rock_03.mat");
            repaired += ApplyMaterialTemplate($"{ModelRoot}/Rocks/Materials/4_Rocks.mat", $"{MaterialRoot}/Rocks/Fristy_Rock_04.mat");
            return repaired;
        }

        private static int ApplyMaterialTemplate(string targetPath, string templatePath)
        {
            var target = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
            var template = AssetDatabase.LoadAssetAtPath<Material>(templatePath);
            if (target == null || template == null)
            {
                return 0;
            }

            var changed = false;
            if (target.shader != template.shader)
            {
                target.shader = template.shader;
                changed = true;
            }

            changed |= CopyMaterialColor(template, target, "_BaseColor");
            changed |= CopyMaterialColor(template, target, "_Color");
            changed |= CopyMaterialTexture(template, target, "_BaseColorMap");
            changed |= CopyMaterialTexture(template, target, "_MainTex");
            changed |= CopyMaterialTexture(template, target, "_NormalMap");
            changed |= CopyMaterialTexture(template, target, "_BumpMap");
            changed |= CopyMaterialTexture(template, target, "_MaskMap");
            changed |= CopyMaterialFloat(template, target, "_Metallic");
            changed |= CopyMaterialFloat(template, target, "_Smoothness");
            changed |= CopyMaterialFloat(template, target, "_AlphaClip");
            changed |= CopyMaterialFloat(template, target, "_AlphaCutoffEnable");
            changed |= CopyMaterialFloat(template, target, "_AlphaCutoff");
            changed |= CopyMaterialFloat(template, target, "_Cutoff");
            changed |= CopyMaterialFloat(template, target, "_DoubleSidedEnable");
            changed |= CopyMaterialFloat(template, target, "_CullMode");
            changed |= CopyMaterialFloat(template, target, "_CullModeForward");

            if (target.renderQueue != template.renderQueue)
            {
                target.renderQueue = template.renderQueue;
                changed = true;
            }

            if (target.doubleSidedGI != template.doubleSidedGI)
            {
                target.doubleSidedGI = template.doubleSidedGI;
                changed = true;
            }

            if (!changed)
            {
                return 0;
            }

            EditorUtility.SetDirty(target);
            return 1;
        }

        private static bool CopyMaterialTexture(Material source, Material target, string propertyName)
        {
            if (!source.HasProperty(propertyName) || !target.HasProperty(propertyName))
            {
                return false;
            }

            var sourceValue = source.GetTexture(propertyName);
            if (sourceValue == null || target.GetTexture(propertyName) == sourceValue)
            {
                return false;
            }

            target.SetTexture(propertyName, sourceValue);
            return true;
        }

        private static bool CopyMaterialColor(Material source, Material target, string propertyName)
        {
            if (!source.HasProperty(propertyName) || !target.HasProperty(propertyName))
            {
                return false;
            }

            var sourceValue = source.GetColor(propertyName);
            if (target.GetColor(propertyName) == sourceValue)
            {
                return false;
            }

            target.SetColor(propertyName, sourceValue);
            return true;
        }

        private static bool CopyMaterialFloat(Material source, Material target, string propertyName)
        {
            if (!source.HasProperty(propertyName) || !target.HasProperty(propertyName))
            {
                return false;
            }

            var sourceValue = source.GetFloat(propertyName);
            if (Mathf.Approximately(target.GetFloat(propertyName), sourceValue))
            {
                return false;
            }

            target.SetFloat(propertyName, sourceValue);
            return true;
        }

        private static string ResolveKnownMaterialGuid(string materialName)
        {
            var key = NormalizeLookupKey(materialName);
            var path = key switch
            {
                _ when key.Contains("stylized_art_plants_3") => $"{MaterialRoot}/Plants/Fristy_Plant_Stylized_Art_03.mat",
                "plants" => $"{MaterialRoot}/Plants/Fristy_Plant_Common.mat",
                _ when key.Contains("1_grass") || key.Contains("grass_1") => $"{MaterialRoot}/Grass/Fristy_Grass_01.mat",
                _ when key.Contains("2_tree_leaves") => $"{MaterialRoot}/Trees/Fristy_Tree_02_Leaves.mat",
                _ when key.Contains("tree_3_group") => $"{MaterialRoot}/Trees/Fristy_Tree_03_Group.mat",
                _ when key.Contains("billboard") => $"{MaterialRoot}/Trees/Fristy_Tree_Billboard.mat",
                _ when key.Contains("rock") && key.Contains("2") => $"{MaterialRoot}/Rocks/Fristy_Rock_02.mat",
                _ when key.Contains("rock") && key.Contains("3") => $"{MaterialRoot}/Rocks/Fristy_Rock_03.mat",
                _ when key.Contains("rock") && key.Contains("4") => $"{MaterialRoot}/Rocks/Fristy_Rock_04.mat",
                _ => null
            };

            return string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.AssetPathToGUID(path);
        }

        private static string NormalizeLookupKey(string value)
        {
            var key = value.Trim().Trim('\'').Trim('"').ToLowerInvariant();
            key = Regex.Replace(key, "\\.\\d+$", string.Empty);
            return key.Replace(' ', '_');
        }

        private static void ConvertMaterialToHdrpLit(Material material, string path, Shader hdrpLit)
        {
            var baseTexture = FindTexture(
                material,
                "_BaseColorMap",
                "_BaseMap",
                "_MainTex",
                "_MainTex1",
                "_Texture",
                "_MainTexture",
                "_Albedo",
                "_AlbedoMap");
            var normalTexture = FindTexture(
                material,
                "_NormalMap",
                "_BumpMap",
                "_MainNorm",
                "_MainNormal",
                "_Normal",
                "_Normal_Map");
            var color = FindColor(material, "_BaseColor", "_Color", "_Tint", "_TopColor");
            color.a = 1f;

            material.shader = hdrpLit;

            SetColorIfPresent(material, "_BaseColor", color);
            SetColorIfPresent(material, "_Color", color);

            if (baseTexture != null)
            {
                SetTextureIfPresent(material, "_BaseColorMap", baseTexture);
                SetTextureIfPresent(material, "_MainTex", baseTexture);
            }

            if (normalTexture != null)
            {
                SetTextureIfPresent(material, "_NormalMap", normalTexture);
                SetTextureIfPresent(material, "_BumpMap", normalTexture);
                SetFloatIfPresent(material, "_NormalScale", 1f);
                SetFloatIfPresent(material, "_BumpScale", 1f);
            }

            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_Smoothness", IsRockLike(path) ? 0.28f : 0.36f);
            ConfigureAlphaAndSidedness(material, ShouldUseAlphaCutout(path));
        }

        private static void ConfigureAlphaAndSidedness(Material material, bool alphaCutout)
        {
            SetFloatIfPresent(material, "_SurfaceType", 0f);
            SetFloatIfPresent(material, "_BlendMode", 0f);
            SetFloatIfPresent(material, "_AlphaClip", alphaCutout ? 1f : 0f);
            SetFloatIfPresent(material, "_AlphaCutoffEnable", alphaCutout ? 1f : 0f);
            SetFloatIfPresent(material, "_AlphaCutoff", alphaCutout ? 0.35f : 0.5f);
            SetFloatIfPresent(material, "_Cutoff", alphaCutout ? 0.35f : 0.5f);

            if (alphaCutout)
            {
                SetFloatIfPresent(material, "_DoubleSidedEnable", 1f);
                SetFloatIfPresent(material, "_CullMode", 0f);
                SetFloatIfPresent(material, "_CullModeForward", 0f);
                material.doubleSidedGI = true;
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else
            {
                material.renderQueue = -1;
            }
        }

        private static string BuildDestinationPath(string sourcePath, string typeRoot, string category)
        {
            var extension = Path.GetExtension(sourcePath);
            var standardName = BuildStandardName(sourcePath, category);
            return $"{typeRoot}/{category}/{standardName}{extension}";
        }

        private static string BuildStandardName(string sourcePath, string category)
        {
            var sourceName = Path.GetFileNameWithoutExtension(sourcePath);
            var normalized = NormalizeTerms(sourceName);
            var tokens = Regex.Split(normalized, "[^A-Za-z0-9]+");
            var categoryToken = Singularize(category);
            var cleanedTokens = new List<string>();

            for (var i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i];
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                token = NormalizeToken(token);
                if (ShouldDropToken(token, categoryToken))
                {
                    continue;
                }

                cleanedTokens.Add(token);
            }

            if (cleanedTokens.Count == 0)
            {
                cleanedTokens.Add("Common");
            }

            return $"Fristy_{categoryToken}_{string.Join("_", cleanedTokens)}";
        }

        private static string NormalizeTerms(string value)
        {
            var result = value
                .Replace("&", " And ", StringComparison.Ordinal)
                .Replace(".", "_", StringComparison.Ordinal)
                .Replace("Gound", "Ground", StringComparison.OrdinalIgnoreCase)
                .Replace("Difuse", "Albedo", StringComparison.OrdinalIgnoreCase)
                .Replace("Diffuse", "Albedo", StringComparison.OrdinalIgnoreCase)
                .Replace("Defuse", "Albedo", StringComparison.OrdinalIgnoreCase)
                .Replace("Deffuse", "Albedo", StringComparison.OrdinalIgnoreCase)
                .Replace("Normals", "Normal", StringComparison.OrdinalIgnoreCase)
                .Replace("Heights", "Height", StringComparison.OrdinalIgnoreCase);

            result = ReplaceStandaloneTerm(result, "NRM", "Normal");
            result = ReplaceStandaloneTerm(result, "NM", "Normal");
            result = ReplaceStandaloneTerm(result, "Disp", "Height");
            result = ReplaceStandaloneTerm(result, "OCC", "Occlusion");
            result = ReplaceStandaloneTerm(result, "Veg", "Vegetation");

            result = Regex.Replace(result, "\\bVariant\\b", string.Empty, RegexOptions.IgnoreCase);
            result = Regex.Replace(result, "\\bPrefab\\b", string.Empty, RegexOptions.IgnoreCase);
            result = Regex.Replace(result, "\\bGrouped\\b", "Group", RegexOptions.IgnoreCase);
            return result;
        }

        private static string ReplaceStandaloneTerm(string value, string term, string replacement)
        {
            return Regex.Replace(
                value,
                $"(?<![A-Za-z0-9]){Regex.Escape(term)}(?![A-Za-z0-9])",
                replacement,
                RegexOptions.IgnoreCase);
        }

        private static string NormalizeToken(string token)
        {
            if (int.TryParse(token, out var numeric))
            {
                return numeric.ToString("00");
            }

            if (token.Length == 1)
            {
                return token.ToUpperInvariant();
            }

            return char.ToUpperInvariant(token[0]) + token.Substring(1);
        }

        private static bool ShouldDropToken(string token, string categoryToken)
        {
            return string.Equals(token, categoryToken, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, categoryToken + "s", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "Fristy", StringComparison.OrdinalIgnoreCase);
        }

        private static string Singularize(string category)
        {
            return category switch
            {
                "Trees" => "Tree",
                "Bushes" => "Bush",
                "Rocks" => "Rock",
                "Grass" => "Grass",
                "Plants" => "Plant",
                "Vines" => "Vine",
                "ShorePlants" => "ShorePlant",
                "Misc" => "Misc",
                _ => category.TrimEnd('s')
            };
        }

        private static string ClassifyCategory(string sourcePath)
        {
            var key = sourcePath.ToLowerInvariant();
            if (ContainsAny(key, "/terrain/", "soil", "mud", "sand", "dark sand", "ground"))
            {
                return "Terrain";
            }

            if (ContainsAny(key, "/trees/", "tree", "bark", "branch", "billboard"))
            {
                return "Trees";
            }

            if (ContainsAny(key, "boulder", "rock", "stone"))
            {
                return "Rocks";
            }

            if (ContainsAny(key, "/foliage/grass/", "grass"))
            {
                return "Grass";
            }

            if (ContainsAny(key, "river", "water plant", "shore"))
            {
                return "ShorePlants";
            }

            if (ContainsAny(key, "ivy"))
            {
                return "Vines";
            }

            if (ContainsAny(key, "vegetation", "veg prefab"))
            {
                return "Bushes";
            }

            if (ContainsAny(key, "weed", "plant", "flower"))
            {
                return "Plants";
            }

            return "Misc";
        }

        private static bool ShouldCopy(string sourcePath)
        {
            var key = sourcePath.ToLowerInvariant();
            if (key.Contains("/scenes/") ||
                key.Contains("/setup/") ||
                key.Contains("/postprocessing profiles/") ||
                key.Contains("/read me"))
            {
                return false;
            }

            if (key.Contains("/shaders/"))
            {
                return false;
            }

            return IsNaturePath(key);
        }

        private static bool IsNaturePath(string key)
        {
            return ContainsAny(
                key,
                "/prefabs/",
                "/trees/",
                "/rocks/",
                "/foliage/",
                "/materials/",
                "/textures/",
                "/terrain/");
        }

        private static bool IsBinaryAsset(string path)
        {
            return path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".psd", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".tif", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTexturePath(string path)
        {
            return path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".psd", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".tif", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldUseAlphaCutout(string path)
        {
            var key = path.ToLowerInvariant();
            return ContainsAny(key, "grass", "plant", "weed", "leaf", "leaves", "branch", "ivy", "vine", "billboard");
        }

        private static bool IsRockLike(string path)
        {
            var key = path.ToLowerInvariant();
            return ContainsAny(key, "rock", "boulder", "stone", "soil", "terrain");
        }

        private static Texture FindTexture(Material material, params string[] propertyNames)
        {
            for (var i = 0; i < propertyNames.Length; i++)
            {
                if (!material.HasProperty(propertyNames[i]))
                {
                    continue;
                }

                var texture = material.GetTexture(propertyNames[i]);
                if (texture != null)
                {
                    return texture;
                }
            }

            return null;
        }

        private static Color FindColor(Material material, params string[] propertyNames)
        {
            for (var i = 0; i < propertyNames.Length; i++)
            {
                if (material.HasProperty(propertyNames[i]))
                {
                    return material.GetColor(propertyNames[i]);
                }
            }

            return Color.white;
        }

        private static void SetTextureIfPresent(Material material, string propertyName, Texture texture)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetTexture(propertyName, texture);
            }
        }

        private static void SetColorIfPresent(Material material, string propertyName, Color color)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, color);
            }
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static void EnsureResourceFolders()
        {
            EnsureFolder(ResourceRoot);
            for (var i = 0; i < ResourceTypeRoots.Length; i++)
            {
                EnsureFolder(ResourceTypeRoots[i]);
                for (var j = 0; j < Categories.Length; j++)
                {
                    EnsureFolder($"{ResourceTypeRoots[i]}/{Categories[j]}");
                }
            }
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            var folderName = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            for (var i = 0; i < needles.Length; i++)
            {
                if (value.Contains(needles[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public sealed class OrganizationResult
        {
            public int CopiedAssets { get; set; }
            public int ConvertedMaterials { get; set; }
            public int RemappedFiles { get; set; }
            public int RepairedMissingReferences { get; set; }
        }
    }
}
