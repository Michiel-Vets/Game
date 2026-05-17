using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class VolumetricFogFeature : ScriptableRendererFeature
{
    public Material fogMaterial;
    public RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingPostProcessing;

    private FogPass _pass;

    public override void Create()
    {
        _pass = new FogPass(fogMaterial, passEvent);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        _pass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (fogMaterial == null) return;
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing) => _pass?.Dispose();

    class PassData
    {
        public Material material;
        public TextureHandle source;
        public TextureHandle destination;
    }

    class FogPass : ScriptableRenderPass
    {
        private Material _mat;

        public FogPass(Material mat, RenderPassEvent evt)
        {
            _mat = mat;
            renderPassEvent = evt;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_mat == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            var source = resourceData.activeColorTexture;
            var desc = renderGraph.GetTextureDesc(source);
            desc.name = "_VolumetricFogTemp";
            desc.clearBuffer = false;

            TextureHandle temp = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddUnsafePass<PassData>("VolumetricFog_Apply", out var passData))
            {
                passData.material = _mat;
                passData.source = source;
                passData.destination = temp;

                builder.UseTexture(source);
                builder.UseTexture(temp, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext ctx) =>
                {
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                    Blitter.BlitCameraTexture(cmd, data.source, data.destination, data.material, 0);
                });
            }

            using (var builder = renderGraph.AddUnsafePass<PassData>("VolumetricFog_CopyBack", out var passData))
            {
                passData.material = null;
                passData.source = temp;
                passData.destination = source;

                builder.UseTexture(temp);
                builder.UseTexture(source, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext ctx) =>
                {
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                    Blitter.BlitCameraTexture(cmd, data.source, data.destination);
                });
            }
        }

        public void Dispose() { }
    }
}