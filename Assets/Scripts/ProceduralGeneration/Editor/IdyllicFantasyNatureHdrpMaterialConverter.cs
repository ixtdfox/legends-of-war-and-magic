using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Editor
{
    public static class IdyllicFantasyNatureHdrpMaterialConverter
    {
        private const string IdyllicMaterialRoot = "Assets/Idyllic Fantasy Nature/Materials";
        private const string IdyllicTextureRoot = "Assets/Idyllic Fantasy Nature/Textures";

        [MenuItem("Tools/Legends of War and Magic/Procedural Generation/Convert Idyllic Materials To HDRP Lit")]
        public static void ConvertIdyllicMaterialsToHdrpLit()
        {
            var converted = ConvertAll();
            Debug.Log($"Converted {converted} Idyllic Fantasy Nature materials to HDRP/Lit.");
        }

        public static int ConvertAll()
        {
            var hdrpLit = Shader.Find("HDRP/Lit");
            if (hdrpLit == null)
            {
                Debug.LogError("Cannot convert Idyllic materials: HDRP/Lit shader was not found.");
                return 0;
            }

            var materialGuids = AssetDatabase.FindAssets("t:Material", new[] { IdyllicMaterialRoot });
            var converted = 0;
            for (var i = 0; i < materialGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(materialGuids[i]);
                if (path.Contains("/Skybox/"))
                {
                    continue;
                }

                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    continue;
                }

                ConvertMaterial(material, path, hdrpLit);
                EditorUtility.SetDirty(material);
                converted++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return converted;
        }

        private static void ConvertMaterial(Material material, string path, Shader hdrpLit)
        {
            var baseTexture = FindTexture(material, "_BaseColorMap", "_BaseMap", "_Base_Map", "_MainTex", "_Texture", "_RockBaseMap", "_MossBaseMap");
            var normalTexture = FindTexture(material, "_NormalMap", "_Normal_Map", "_BumpMap", "_RockNormal", "_MossNormal", "_Coverage_Normal_Map");
            baseTexture ??= ResolveKnownBaseTexture(path);
            normalTexture ??= ResolveKnownNormalTexture(path);
            var baseColor = FindColor(material, "_BaseColor", "_Color", "_RockColor", "_TopColor", "_Top_Color");
            var alphaCutout = ShouldUseAlphaCutout(material, path);
            baseColor.a = 1f;

            material.shader = hdrpLit;

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", baseColor);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", baseColor);
            }

            if (baseTexture != null)
            {
                SetTextureIfPresent(material, "_BaseColorMap", baseTexture);
                SetTextureIfPresent(material, "_MainTex", baseTexture);
            }

            if (normalTexture != null)
            {
                SetTextureIfPresent(material, "_NormalMap", normalTexture);
                SetFloatIfPresent(material, "_NormalScale", 1f);
            }

            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_Smoothness", IsRockLike(path) ? 0.28f : 0.36f);
            ConfigureAlphaAndSidedness(material, alphaCutout, ResolveAlphaCutoff(path));
        }

        private static Texture ResolveKnownBaseTexture(string path)
        {
            var key = path.ToLowerInvariant();
            if (ContainsAny(key, "broadleaf"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Trees/BroadleafTree_Leaves.png");
            }

            if (ContainsAny(key, "fir_branch"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Trees/Fir_Branch.png");
            }

            if (ContainsAny(key, "willow"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Trees/WillowTree_Branch.png");
            }

            if (ContainsAny(key, "blossom"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Trees/Tree_Blossoms.png");
            }

            if (ContainsAny(key, "bark"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Trees/Bark_Albedo.png");
            }

            if (ContainsAny(key, "grass_01"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Grass/Grass_01.png");
            }

            if (ContainsAny(key, "grass_02"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Grass/Grass_02.png");
            }

            if (ContainsAny(key, "grass_03"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Grass/Grass_03.png");
            }

            if (ContainsAny(key, "plant"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Plants/Plants_Albedo.png");
            }

            if (ContainsAny(key, "flowermeadow"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Flowers/FlowerMeadow.png");
            }

            if (ContainsAny(key, "flower"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Flowers/Flower.png");
            }

            if (ContainsAny(key, "bush_01"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Bushes/Bush_01/Bush_Branch.png");
            }

            if (ContainsAny(key, "bush_02"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Bushes/Bush_02/Bush_Branches.png");
            }

            if (ContainsAny(key, "bush_03"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Bushes/Bush_03/Bush_Branches.png");
            }

            if (ContainsAny(key, "cliff"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Cliff/Cliff_Albedo.png");
            }

            if (ContainsAny(key, "rock_big_01"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Rocks/Rock_Big_01_Albedo.png");
            }

            if (ContainsAny(key, "rock_big_02"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Rocks/Rock_Big_02_Albedo.png");
            }

            if (ContainsAny(key, "rock_big_03"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Rocks/Rock_Big_03_Albedo.png");
            }

            if (ContainsAny(key, "rock_medium_01"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Rocks/Rock_Medium_01_Albedo.png");
            }

            if (ContainsAny(key, "rock_medium_02"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Rocks/Rock_Medium_02_Albedo.png");
            }

            if (ContainsAny(key, "rock_medium_03"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Rocks/Rock_Medium_03_Albedo.png");
            }

            if (ContainsAny(key, "rock_small_01"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Rocks/Rock_Small_01_Albedo.png");
            }

            if (ContainsAny(key, "rock_small_02"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Rocks/Rock_Small_02_Albedo.png");
            }

            if (ContainsAny(key, "rock_small_03"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Rocks/Rock_Small_03_Albedo.png");
            }

            if (ContainsAny(key, "stone_big_01"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Stones/Stone_Big_01_Albedo.png");
            }

            if (ContainsAny(key, "stone_big_02"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Stones/Stone_Big_02_Albedo.png");
            }

            if (ContainsAny(key, "stone_big_03"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Stones/Stone_Big_03_Albedo.png");
            }

            if (ContainsAny(key, "stone_medium_01"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Stones/Stone_Medium_01_Albedo.png");
            }

            if (ContainsAny(key, "stone_medium_02"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Stones/Stone_Medium_02_Albedo.png");
            }

            if (ContainsAny(key, "stone_medium_03"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Stones/Stone_Medium_03_Albedo.png");
            }

            if (ContainsAny(key, "reeds"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Waterplants/Cattail_Albedo.png");
            }

            if (ContainsAny(key, "cattail"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Waterplants/Cattail_Albedo.png");
            }

            if (ContainsAny(key, "lilypad"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Waterplants/LilyPad_Albedo.png");
            }

            if (ContainsAny(key, "waterlily_bottom"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Waterplants/Waterlily_Bottom_Albedo.png");
            }

            if (ContainsAny(key, "waterlily"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Waterplants/Waterlily_Leaf.png");
            }

            return null;
        }

        private static Texture ResolveKnownNormalTexture(string path)
        {
            var key = path.ToLowerInvariant();
            if (ContainsAny(key, "bark", "tree_bark", "fir_bark"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Trees/Bark_Normal.png");
            }

            if (ContainsAny(key, "cliff"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Cliff/Cliff_Normal.png");
            }

            if (ContainsAny(key, "rock_big_01"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Rocks/Rock_Big_01_Normal.png");
            }

            if (ContainsAny(key, "rock_big_02"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Rocks/Rock_Big_02_Normal.png");
            }

            if (ContainsAny(key, "rock_big_03"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Rocks/Rock_Big_03_Normal.png");
            }

            if (ContainsAny(key, "rock_medium_01"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Rocks/Rock_Medium_01_Normal.png");
            }

            if (ContainsAny(key, "rock_medium_02"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Rocks/Rock_Medium_02_Normal.png");
            }

            if (ContainsAny(key, "rock_medium_03"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Rocks/Rock_Medium_03_Normal.png");
            }

            if (ContainsAny(key, "rock_small_01"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Rocks/Rock_Small_01_Normal.png");
            }

            if (ContainsAny(key, "rock_small_02"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Rocks/Rock_Small_02_Normal.png");
            }

            if (ContainsAny(key, "rock_small_03"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Rocks/Rock_Small_03_Normal.png");
            }

            if (ContainsAny(key, "stone_big_01"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Stones/Stone_Big_01_Normal.png");
            }

            if (ContainsAny(key, "stone_big_02"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Stones/Stone_Big_02_Normal.png");
            }

            if (ContainsAny(key, "stone_big_03"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Stones/Stone_Big_03_Normal.png");
            }

            if (ContainsAny(key, "stone_medium_01"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Stones/Stone_Medium_01_Normal.png");
            }

            if (ContainsAny(key, "stone_medium_02"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Stones/Stone_Medium_02_Normal.png");
            }

            if (ContainsAny(key, "stone_medium_03"))
            {
                return LoadTexture($"{IdyllicTextureRoot}/Stones/Stone_Medium_03_Normal.png");
            }

            return null;
        }

        private static Texture LoadTexture(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static bool ShouldUseAlphaCutout(Material material, string path)
        {
            if (material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > 0.5f)
            {
                return true;
            }

            var key = path.ToLowerInvariant();
            return ContainsAny(
                key,
                "grass",
                "flower",
                "plant",
                "bush",
                "branch",
                "leaf",
                "leaves",
                "willow",
                "broadleaf",
                "fir_branch",
                "waterlily",
                "lilypad",
                "reeds",
                "cattail");
        }

        private static float ResolveAlphaCutoff(string path)
        {
            var key = path.ToLowerInvariant();
            if (ContainsAny(key, "grass", "flower", "plant"))
            {
                return 0.24f;
            }

            if (ContainsAny(key, "branch", "leaf", "leaves", "willow", "broadleaf", "fir_branch", "bush", "reeds", "cattail"))
            {
                return 0.28f;
            }

            return 0.36f;
        }

        private static void ConfigureAlphaAndSidedness(Material material, bool alphaCutout, float alphaCutoff)
        {
            SetFloatIfPresent(material, "_SurfaceType", 0f);
            SetFloatIfPresent(material, "_BlendMode", 0f);
            SetFloatIfPresent(material, "_SrcBlend", 1f);
            SetFloatIfPresent(material, "_DstBlend", 0f);
            SetFloatIfPresent(material, "_ZWrite", 1f);

            if (alphaCutout)
            {
                SetFloatIfPresent(material, "_AlphaCutoffEnable", 1f);
                SetFloatIfPresent(material, "_AlphaCutoff", alphaCutoff);
                SetFloatIfPresent(material, "_AlphaCutoffPrepass", alphaCutoff);
                SetFloatIfPresent(material, "_AlphaCutoffPostpass", alphaCutoff);
                SetFloatIfPresent(material, "_AlphaCutoffShadow", alphaCutoff);
                SetFloatIfPresent(material, "_Cutoff", alphaCutoff);
                SetFloatIfPresent(material, "_CullMode", 0f);
                SetFloatIfPresent(material, "_CullModeForward", 0f);
                SetFloatIfPresent(material, "_OpaqueCullMode", 0f);
                SetFloatIfPresent(material, "_DoubleSidedEnable", 1f);
                SetFloatIfPresent(material, "_DoubleSidedNormalMode", 1f);
                material.doubleSidedGI = true;
                material.renderQueue = 2450;
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.EnableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_DOUBLESIDED_ON");
            }
            else
            {
                SetFloatIfPresent(material, "_AlphaCutoffEnable", 0f);
                SetFloatIfPresent(material, "_CullMode", 2f);
                SetFloatIfPresent(material, "_CullModeForward", 2f);
                SetFloatIfPresent(material, "_OpaqueCullMode", 2f);
                SetFloatIfPresent(material, "_DoubleSidedEnable", 0f);
                material.doubleSidedGI = false;
                material.renderQueue = -1;
                material.SetOverrideTag("RenderType", "Opaque");
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_DOUBLESIDED_ON");
            }
        }

        private static bool IsRockLike(string path)
        {
            var key = path.ToLowerInvariant();
            return ContainsAny(key, "rock", "stone", "cliff");
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
    }
}
