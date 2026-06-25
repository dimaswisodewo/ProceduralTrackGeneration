using UnityEngine;

public static class VisualEffectUtility {
    
    // --- Shaders & Materials Loading ---

    public static Material GetSpotMaterial() {
        Material ringMaterial = Resources.Load<Material>("Materials/SpotMaterial");
        if (ringMaterial != null) {
            ringMaterial = Object.Instantiate(ringMaterial);
        } else {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            if (shader == null) {
                shader = Shader.Find("Standard");
            }
            ringMaterial = new Material(shader);
        }
        return ringMaterial;
    }

    public static Material GetFireMaterial() {
        Material psMat = Resources.Load<Material>("Materials/FireMaterial");
        if (psMat != null) {
            psMat = Object.Instantiate(psMat);
        } else {
            Shader shader = FindFireShader();
            psMat = new Material(shader);
            ConfigureURPMaterial(psMat, true);
        }
        return psMat;
    }

    public static Material GetSmokeMaterial() {
        Material psMat = Resources.Load<Material>("Materials/SmokeMaterial");
        if (psMat != null) {
            psMat = Object.Instantiate(psMat);
        } else {
            Shader shader = FindSmokeShader();
            psMat = new Material(shader);
            ConfigureURPMaterial(psMat, false);
        }
        return psMat;
    }

    public static Material GetSparkMaterial() {
        Shader shader = FindSparkShader();
        Material psMat = new Material(shader);
        psMat.color = Color.white;
        ApplyMaterialColor(psMat, Color.white);
        ConfigureSparkURPMaterial(psMat);
        return psMat;
    }

    // --- Particle System Instantiation & Setup ---

    public static ParticleSystem CreateFireParticles(Transform parent, Vector3 localOffset, ParticleSystem prefab = null) {
        if (prefab != null) {
            ParticleSystem activeInstance = Object.Instantiate(prefab, parent);
            activeInstance.transform.localPosition = localOffset;
            activeInstance.transform.localRotation = Quaternion.identity;
            return activeInstance;
        }

        GameObject go = new GameObject("ProceduralFirePS");
        go.transform.parent = parent;
        go.transform.localPosition = localOffset;
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.gravityModifier = -0.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.maxParticles = 1000;

        var emission = ps.emission;
        emission.rateOverTime = 40f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = 0.2f;
        shape.angle = 15f;

        var colorModule = ps.colorOverLifetime;
        colorModule.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1.0f, 0.9f, 0.2f), 0.0f),
                new GradientColorKey(new Color(1.0f, 0.3f, 0.0f), 0.4f),
                new GradientColorKey(new Color(0.3f, 0.0f, 0.0f), 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(0.8f, 0.5f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        colorModule.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeModule = ps.sizeOverLifetime;
        sizeModule.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1.0f);
        sizeCurve.AddKey(0.7f, 0.8f);
        sizeCurve.AddKey(1f, 0.2f);
        sizeModule.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.sharedMaterial = GetFireMaterial();

        return ps;
    }

    public static ParticleSystem CreateSmokeParticles(Transform parent, Vector3 localOffset, ParticleSystem prefab = null) {
        if (prefab != null) {
            ParticleSystem activeInstance = Object.Instantiate(prefab, parent);
            activeInstance.transform.localPosition = localOffset;
            activeInstance.transform.localRotation = Quaternion.identity;
            return activeInstance;
        }

        GameObject go = new GameObject("ProceduralSmokePS");
        go.transform.parent = parent;
        go.transform.localPosition = localOffset;
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.maxParticles = 1000;

        var emission = ps.emission;
        emission.rateOverTime = 25f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = 0.25f;
        shape.angle = 20f;

        var colorModule = ps.colorOverLifetime;
        colorModule.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.2f, 0.2f, 0.2f), 0.0f),
                new GradientColorKey(new Color(0.4f, 0.4f, 0.4f), 0.5f),
                new GradientColorKey(new Color(0.6f, 0.6f, 0.6f), 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.0f, 0.0f),
                new GradientAlphaKey(0.6f, 0.2f),
                new GradientAlphaKey(0.3f, 0.6f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        colorModule.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeModule = ps.sizeOverLifetime;
        sizeModule.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.5f);
        sizeCurve.AddKey(1f, 2.5f);
        sizeModule.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.sharedMaterial = GetSmokeMaterial();

        return ps;
    }

    public static ParticleSystem CreateSparkParticles(Transform parent, Vector3 localOffset) {
        GameObject psObj = new GameObject("CarSparksPS");
        psObj.transform.parent = parent;
        psObj.transform.localPosition = localOffset;
        psObj.transform.localRotation = Quaternion.identity;

        ParticleSystem ps = psObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.startLifetime = 0.5f;
        main.startSpeed = 5f;
        main.startSize = 0.12f;
        main.gravityModifier = 1.0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.maxParticles = 5000;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = false;

        var sizeModule = ps.sizeOverLifetime;
        sizeModule.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeModule.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorModule = ps.colorOverLifetime;
        colorModule.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        colorModule.color = new ParticleSystem.MinMaxGradient(gradient);

        var psr = psObj.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Stretch;
        psr.velocityScale = 0.08f;
        psr.lengthScale = 1.3f;
        psr.sharedMaterial = GetSparkMaterial();

        return ps;
    }

    // --- Color Application Helpers ---

    public static void ApplyMaterialColor(Material mat, Color color) {
        if (mat == null) return;
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) {
            mat.SetColor("_BaseColor", color);
        }
        if (mat.HasProperty("_Color")) {
            mat.SetColor("_Color", color);
        }
    }

    // --- Internal Helpers ---

    private static Shader FindFireShader() {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null) return shader;
        shader = Shader.Find("Particles/Additive");
        if (shader != null) return shader;
        shader = Shader.Find("Mobile/Particles/Additive");
        if (shader != null) return shader;
        return FindSparkShader();
    }

    private static Shader FindSmokeShader() {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null) return shader;
        shader = Shader.Find("Particles/Standard Unlit");
        if (shader != null) return shader;
        shader = Shader.Find("Particles/Alpha Blended");
        if (shader != null) return shader;
        shader = Shader.Find("Mobile/Particles/Alpha Blended");
        if (shader != null) return shader;
        return FindSparkShader();
    }

    private static Shader FindSparkShader() {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Mobile/Diffuse");
        if (shader == null) shader = Shader.Find("Standard");
        return shader;
    }

    private static void ConfigureURPMaterial(Material mat, bool isAdditive) {
        if (mat.HasProperty("_Surface")) {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", isAdditive ? 1f : 0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", isAdditive ? (int)UnityEngine.Rendering.BlendMode.One : (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }

    private static void ConfigureSparkURPMaterial(Material mat) {
        if (mat.HasProperty("_Surface")) {
            mat.SetFloat("_Surface", 1f); // 1 = Transparent
            mat.SetFloat("_Blend", 0f); // 0 = Alpha blend
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
