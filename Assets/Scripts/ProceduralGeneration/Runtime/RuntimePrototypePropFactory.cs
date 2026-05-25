using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Runtime
{
    internal static class RuntimePrototypePropFactory
    {
        private static GameObject[] forestPrefabs;
        private static bool loggedFallback;

        public static GameObject[] GetForestPrefabs()
        {
            if (forestPrefabs != null)
            {
                return forestPrefabs;
            }

            forestPrefabs = new[]
            {
                CreatePinePrototype("Prototype Pine A", 0.9f, 3.1f),
                CreatePinePrototype("Prototype Pine B", 1.15f, 3.8f)
            };

            if (!loggedFallback)
            {
                Debug.Log("No authored forest prefabs were configured for procedural generation; using hidden runtime prototype trees.");
                loggedFallback = true;
            }

            return forestPrefabs;
        }

        private static GameObject CreatePinePrototype(string name, float width, float height)
        {
            var root = new GameObject(name)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            root.SetActive(false);

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.hideFlags = HideFlags.HideAndDontSave;
            trunk.transform.SetParent(root.transform, false);
            trunk.transform.localPosition = new Vector3(0f, height * 0.23f, 0f);
            trunk.transform.localScale = new Vector3(width * 0.18f, height * 0.23f, width * 0.18f);
            trunk.GetComponent<Renderer>().sharedMaterial = CreateMaterial(new Color(0.34f, 0.20f, 0.10f, 1f), "Prototype Trunk");

            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Crown";
            crown.hideFlags = HideFlags.HideAndDontSave;
            crown.transform.SetParent(root.transform, false);
            crown.transform.localPosition = new Vector3(0f, height * 0.68f, 0f);
            crown.transform.localScale = new Vector3(width, height * 0.58f, width);
            crown.GetComponent<Renderer>().sharedMaterial = CreateMaterial(new Color(0.08f, 0.29f, 0.13f, 1f), "Prototype Crown");

            return root;
        }

        private static Material CreateMaterial(Color color, string name)
        {
            var shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader)
            {
                name = name,
                color = color,
                hideFlags = HideFlags.HideAndDontSave
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            return material;
        }
    }
}
