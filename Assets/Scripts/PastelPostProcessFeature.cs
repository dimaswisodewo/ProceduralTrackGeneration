using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ProceduralTrack
{
    public class PastelPostProcessFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class FeatureSettings
        {
            public Shader customShader;
        }

        public FeatureSettings settings = new FeatureSettings();
        private Material m_Material;
        private PastelPass m_PastelPass;

        /// <summary>
        /// Initializes the renderer feature, loads the shader, and creates the material.
        /// </summary>
        public override void Create()
        {
            Shader shader = settings.customShader;
            if (shader == null)
            {
                shader = Shader.Find("Hidden/PastelPostProcess");
            }

            if (shader != null)
            {
                m_Material = CoreUtils.CreateEngineMaterial(shader);
            }

            m_PastelPass = new PastelPass(m_Material)
            {
                // Execute after transparent drawing and post processing, before final screen blit
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
        }

        /// <summary>
        /// Registers the render pass if the camera has the PastelCameraEffect component active.
        /// </summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (m_Material == null) return;

            Camera camera = renderingData.cameraData.camera;
            if (camera == null) return;

            // Only render if the camera has the effect script attached and enabled
            var effect = camera.GetComponent<PastelCameraEffect>();
            if (effect == null || !effect.enabled) return;

            // Push inspector settings from the camera component into the material shader properties
            effect.UpdateMaterial(m_Material);

            renderer.EnqueuePass(m_PastelPass);
        }

        /// <summary>
        /// Cleans up resources when the renderer feature is destroyed or disabled.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(m_Material);
        }

        class PastelPass : ScriptableRenderPass
        {
            private readonly Material m_Material;

            public PastelPass(Material material)
            {
                m_Material = material;
            }

            private class PassData
            {
                public TextureHandle source;
                public Material material;
                public int passIndex;
            }

            /// <summary>
            /// Modern Render Graph execution (Unity 6 / URP 17)
            /// </summary>
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (m_Material == null) return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData == null) return;

                TextureHandle source = resourceData.activeColorTexture;
                if (!source.IsValid()) return;

                // Create a temporary texture using the same format and dimension as the camera color target
                TextureDesc desc = renderGraph.GetTextureDesc(source);
                desc.name = "_PastelTempTexture";
                desc.clearBuffer = false;
                
                TextureHandle tempTexture = renderGraph.CreateTexture(desc);

                // Pass 1: Render the screen to the temp texture applying the custom pastel & dream glow shader (Pass 0)
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Pastel Post Process", out var passData))
                {
                    passData.source = source;
                    passData.material = m_Material;
                    passData.passIndex = 0; // Pass 0: PastelDream

                    builder.UseTexture(source, AccessFlags.Read);
                    builder.SetRenderAttachment(tempTexture, 0, AccessFlags.Write);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.passIndex);
                    });
                }

                // Pass 2: Blit the processed temp texture back to the active color texture using the copy shader (Pass 1)
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Pastel Copy Back", out var passData))
                {
                    passData.source = tempTexture;
                    passData.material = m_Material;
                    passData.passIndex = 1; // Pass 1: Copy

                    builder.UseTexture(tempTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(source, 0, AccessFlags.Write);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.passIndex);
                    });
                }
            }
        }
    }
}
