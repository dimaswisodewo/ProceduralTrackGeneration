using UnityEngine;
using UnityEditor;
using System.IO;

[InitializeOnLoad]
public static class MaterialGenerator {
    static MaterialGenerator() {
        // Run generation when Editor loads or compiles scripts
        GenerateMaterials();
    }

    [MenuItem("Tools/Generate Materials")]
    public static void GenerateMaterials() {
        string dirPath = "Assets/Resources/Materials";
        if (!Directory.Exists(dirPath)) {
            Directory.CreateDirectory(dirPath);
        }

        CreateMaterialIfMissing(dirPath + "/FireMaterial.mat", "Universal Render Pipeline/Particles/Unlit", true);
        CreateMaterialIfMissing(dirPath + "/SmokeMaterial.mat", "Universal Render Pipeline/Particles/Unlit", false);
        CreateMaterialIfMissing(dirPath + "/SpotMaterial.mat", "Universal Render Pipeline/Lit", false, new Color(1f, 1f, 1f, 0.4f));

        AssetDatabase.Refresh();
    }

    private static void CreateMaterialIfMissing(string path, string shaderName, bool isAdditive, Color? defaultColor = null) {
        if (File.Exists(path)) {
            // Material already exists, no need to overwrite it unless it's invalid.
            return;
        }

        Shader shader = Shader.Find(shaderName);
        if (shader == null) {
            // Fallback shaders for non-URP setups just in case, but URP is expected here.
            if (shaderName.Contains("Particles")) {
                shader = Shader.Find(isAdditive ? "Particles/Additive" : "Particles/Alpha Blended");
            } else {
                shader = Shader.Find("Standard");
            }
        }

        if (shader == null) {
            Debug.LogError($"[MaterialGenerator] Shader not found and fallback failed: {shaderName}");
            return;
        }

        Material mat = new Material(shader);
        
        // Configure transparency properties for URP Particle/Lit shaders
        if (mat.HasProperty("_Surface")) {
            mat.SetFloat("_Surface", 1f); // 1 = Transparent
            mat.SetFloat("_Blend", isAdditive ? 1f : 0f); // 1 = Additive, 0 = Alpha Blend
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", isAdditive ? (int)UnityEngine.Rendering.BlendMode.One : (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        // Set default particle texture if applicable
        if (shaderName.Contains("Particles")) {
            Texture2D defaultParticleTex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            if (defaultParticleTex == null) {
                defaultParticleTex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.png");
            }

            if (defaultParticleTex != null) {
                if (mat.HasProperty("_BaseMap")) {
                    mat.SetTexture("_BaseMap", defaultParticleTex);
                } else if (mat.HasProperty("_MainTex")) {
                    mat.SetTexture("_MainTex", defaultParticleTex);
                }
            } else {
                Debug.LogWarning("[MaterialGenerator] Built-in soft particle texture not found.");
            }
        }

        if (defaultColor.HasValue) {
            mat.color = defaultColor.Value;
            if (mat.HasProperty("_BaseColor")) {
                mat.SetColor("_BaseColor", defaultColor.Value);
            }
        }

        AssetDatabase.CreateAsset(mat, path);
        Debug.Log($"[MaterialGenerator] Created material asset at {path} with shader {shader.name}");
    }
}
