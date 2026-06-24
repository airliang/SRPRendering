using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{
    public class TAAHistoryData
    {
        public const int MaxHistoryCount = 2;

        public RTHandle[] historyRT = new RTHandle[MaxHistoryCount];
        public bool isFirstFrame = true;

        int m_Width = -1;
        int m_Height = -1;
        GraphicsFormat m_ColorFormat = GraphicsFormat.None;

        static readonly System.Collections.Generic.Dictionary<Camera, TAAHistoryData> s_HistoryDatas =
            new System.Collections.Generic.Dictionary<Camera, TAAHistoryData>();

        public static TAAHistoryData GetOrCreate(Camera camera)
        {
            if (!s_HistoryDatas.TryGetValue(camera, out var historyData))
            {
                historyData = new TAAHistoryData();
                s_HistoryDatas.Add(camera, historyData);
            }

            return historyData;
        }

        public void SwapHistoryRTs()
        {
            var nextFirst = historyRT[historyRT.Length - 1];
            for (int i = 0, count = historyRT.Length - 1; i < count; ++i)
                historyRT[i + 1] = historyRT[i];
            historyRT[0] = nextFirst;
        }

        public void AllocateHistoryRT(int width, int height, GraphicsFormat colorFormat)
        {
            if (m_Width == width && m_Height == height && m_ColorFormat == colorFormat && historyRT[0] != null)
                return;

            ReleaseHistoryRT();

            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            var descriptor = new RenderTextureDescriptor(width, height, colorFormat, 0, 0)
            {
                enableRandomWrite = false,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false
            };

            for (int i = 0; i < historyRT.Length; ++i)
            {
                RTHandleUtils.ReAllocateIfNeeded(ref historyRT[i], descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp,
                    false, 1, 0, "TAAHistory" + i);
            }

            m_Width = width;
            m_Height = height;
            m_ColorFormat = colorFormat;
            isFirstFrame = true;
        }

        public void ReleaseHistoryRT()
        {
            for (int i = 0; i < historyRT.Length; ++i)
            {
                historyRT[i]?.Release();
                historyRT[i] = null;
            }

            m_Width = -1;
            m_Height = -1;
            m_ColorFormat = GraphicsFormat.None;
        }

        public void Invalidate()
        {
            isFirstFrame = true;
        }
    }

    public class TemporalAAResolvePassData
    {
        public TextureHandle currentFrame;
        public TextureHandle depth;
        public TextureHandle historyRead;
        public TextureHandle output;
        public TextureHandle motionVector;
        public Material material;
        public bool useMotionVectors;
        public bool isFirstFrame;
        public float feedback;
        public float depthRejectScale;
        public float varianceGamma;
        public float farAdaptStrength;
        public Vector4 texelSize;
    }

    public partial class RenderPasses
    {
        // Linear eye-depth tolerance: ~0.25m difference reaches full reject before far-factor attenuation.
        const float k_DepthRejectScale = 4.0f;
        const float k_VarianceGamma = 1.25f;
        const float k_FarAdaptStrength = 1.0f;
        static bool s_taaWasActiveLastFrame;

        static readonly int s_SourceTex = Shader.PropertyToID("_SourceTex");
        static readonly int s_HistoryTex = Shader.PropertyToID("_HistoryTex");
        static readonly int s_CameraDepthTex = Shader.PropertyToID("_CameraDepthTex");
        static readonly int s_MotionVectorTex = Shader.PropertyToID("_MotionVectorTex");
        static readonly int s_MainTexTexelSize = Shader.PropertyToID("_MainTex_TexelSize");
        static readonly int s_TAAParams = Shader.PropertyToID("_TAAParams");
        static readonly int s_FirstFrame = Shader.PropertyToID("_FirstFrame");

        public static TextureHandle TAAResolvePass(
            RenderingData renderingData,
            TextureHandle currentFrameColor,
            TextureHandle depth,
            TextureHandle motionVector,
            Material taaMaterial)
        {
            var historyData = TAAHistoryData.GetOrCreate(renderingData.cameraData.camera);
            if (!s_taaWasActiveLastFrame)
                historyData.Invalidate();
            s_taaWasActiveLastFrame = true;

            historyData.SwapHistoryRTs();

            var colorDescriptor = renderingData.cameraData.GetCameraTargetDescriptor(
                InsanityPipeline.asset.ResolutionRate,
                InsanityPipeline.asset.HDREnable,
                1);
            historyData.AllocateHistoryRT(colorDescriptor.width, colorDescriptor.height, colorDescriptor.graphicsFormat);

            TextureHandle historyRead = renderingData.renderGraph.ImportTexture(historyData.historyRT[0]);
            TextureHandle historyWrite = renderingData.renderGraph.ImportTexture(historyData.historyRT[1]);

            bool useMotionVectors = motionVector.IsValid();
            bool isFirstFrame = historyData.isFirstFrame || renderingData.cameraData.isFirstFrame;

            using (var builder = renderingData.renderGraph.AddRenderPass<TemporalAAResolvePassData>(
                       "TAA Resolve Pass", out var passData, new ProfilingSampler("TAA Resolve Pass Profiler")))
            {
                passData.currentFrame = builder.ReadTexture(currentFrameColor);
                passData.depth = builder.ReadTexture(depth);
                passData.historyRead = builder.ReadTexture(historyRead);
                passData.output = builder.UseColorBuffer(historyWrite, 0);
                passData.motionVector = useMotionVectors ? builder.ReadTexture(motionVector) : TextureHandle.nullHandle;
                passData.material = taaMaterial;
                passData.useMotionVectors = useMotionVectors;
                passData.isFirstFrame = isFirstFrame;
                passData.feedback = InsanityPipeline.asset.TAAFeedback;
                passData.depthRejectScale = k_DepthRejectScale;
                passData.varianceGamma = k_VarianceGamma;
                passData.farAdaptStrength = k_FarAdaptStrength;
                passData.texelSize = new Vector4(
                    1.0f / colorDescriptor.width,
                    1.0f / colorDescriptor.height,
                    colorDescriptor.width,
                    colorDescriptor.height);

                builder.AllowPassCulling(false);
                builder.SetRenderFunc((TemporalAAResolvePassData data, RenderGraphContext context) =>
                {
                    CoreUtils.SetKeyword(context.cmd, "_USE_MOTION_VECTORS", data.useMotionVectors);

                    var material = data.material;
                    material.SetTexture(s_SourceTex, data.currentFrame);
                    material.SetTexture(s_HistoryTex, data.historyRead);
                    material.SetTexture(s_CameraDepthTex, data.depth);
                    if (data.useMotionVectors)
                        material.SetTexture(s_MotionVectorTex, data.motionVector);
                    material.SetVector(s_MainTexTexelSize, data.texelSize);
                    material.SetVector(s_TAAParams, new Vector4(data.feedback, data.depthRejectScale, data.varianceGamma, data.farAdaptStrength));
                    material.SetFloat(s_FirstFrame, data.isFirstFrame ? 1.0f : 0.0f);

                    context.cmd.SetRenderTarget(data.output, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                    context.cmd.SetViewport(new Rect(0, 0, data.texelSize.z, data.texelSize.w));
                    context.cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1);
                });
            }

            historyData.isFirstFrame = false;
            return historyWrite;
        }

        public static void NotifyTAAInactive()
        {
            s_taaWasActiveLastFrame = false;
        }
    }
}
