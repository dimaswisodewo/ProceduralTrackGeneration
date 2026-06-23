using UnityEngine;

namespace ProceduralTrack
{
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Rendering/Pastel Camera Effect")]
    public class PastelCameraEffect : MonoBehaviour
    {
        [Header("Pastel Look")]
        [Range(0f, 1f), Tooltip("Strength of the pastel color tint and wash.")]
        public float pastelStrength = 0.5f;

        [ColorUsage(false, false), Tooltip("The base tint color to give a soft pastel look.")]
        public Color pastelTint = new Color(0.98f, 0.92f, 0.94f); // Warm soft pink/peach

        [Range(0f, 1f), Tooltip("Desaturation strength. Lower values make colors softer and more pastel.")]
        public float saturation = 0.75f;

        [Range(0f, 1f), Tooltip("Shadow lift. Lifts dark colors to make the scene look soft and low-contrast.")]
        public float shadowLift = 0.1f;

        [Range(0f, 1f), Tooltip("Warmth/Temperature shift. Higher values add cozy peach/orange tones.")]
        public float warmth = 0.15f;

        [Header("Dreamy Glow (Orton Blur)")]
        [Range(0f, 1f), Tooltip("Amount of soft glow overlay.")]
        public float dreaminess = 0.35f;

        [Range(0.1f, 5f), Tooltip("Sample radius for the screen blur.")]
        public float blurOffset = 1.5f;

        [Header("Cozy Vignette")]
        [Range(0f, 1f), Tooltip("Strength of the colored vignette framing.")]
        public float vignetteStrength = 0.3f;

        [Range(0.1f, 1f), Tooltip("Smoothness and range of the vignette.")]
        public float vignetteRange = 0.45f;

        [ColorUsage(false, false), Tooltip("Color of the framing vignette. A warm dark tone or a soft peach works best.")]
        public Color vignetteColor = new Color(0.35f, 0.28f, 0.32f); // Cozy soft dark mauve

        // Shader property IDs cached for efficiency
        private static readonly int PastelStrengthId = Shader.PropertyToID("_PastelStrength");
        private static readonly int PastelTintId = Shader.PropertyToID("_PastelTint");
        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        private static readonly int ShadowLiftId = Shader.PropertyToID("_ShadowLift");
        private static readonly int WarmthId = Shader.PropertyToID("_Warmth");
        private static readonly int DreaminessId = Shader.PropertyToID("_Dreaminess");
        private static readonly int BlurOffsetId = Shader.PropertyToID("_BlurOffset");
        private static readonly int VignetteStrengthId = Shader.PropertyToID("_VignetteStrength");
        private static readonly int VignetteRangeId = Shader.PropertyToID("_VignetteRange");
        private static readonly int VignetteColorId = Shader.PropertyToID("_VignetteColor");

        /// <summary>
        /// Applies the inspector values to the provided material.
        /// </summary>
        public void UpdateMaterial(Material material)
        {
            if (material == null) return;

            material.SetFloat(PastelStrengthId, pastelStrength);
            material.SetColor(PastelTintId, pastelTint);
            material.SetFloat(SaturationId, saturation);
            material.SetFloat(ShadowLiftId, shadowLift);
            material.SetFloat(WarmthId, warmth);
            material.SetFloat(DreaminessId, dreaminess);
            material.SetFloat(BlurOffsetId, blurOffset);
            material.SetFloat(VignetteStrengthId, vignetteStrength);
            material.SetFloat(VignetteRangeId, vignetteRange);
            material.SetColor(VignetteColorId, vignetteColor);
        }

        private void OnValidate()
        {
            // Keep bounds clean in editor
            blurOffset = Mathf.Max(0.1f, blurOffset);
        }
    }
}
