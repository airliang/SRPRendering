using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{
    public class SATPassData
    {
        public TextureHandle m_InputTexture;
        public TextureHandle m_OutputTexture;
        public TextureHandle m_OutputGroupSumTexture;
        public ComputeShader m_SATCompute;
        public int kernelPreSum = -1;
        public int kernelPreSumGroup = -1;
        public int kernelPostSum = -1;
        public Texture2D m_testTexture;
    }

    public class SATRenderer
    {
        private const int MaxGroupThreadsNum = 8;
        public SATPassData RenderSAT(RenderGraph graph, TextureHandle inputTexture, ComputeShader scanCS)
        {
            using (var builder = graph.AddRenderPass<SATPassData>("SAT Pass", out var passData, 
                               new ProfilingSampler("SAT Pass Profiler")))
            {
                passData.m_InputTexture = builder.ReadTexture(inputTexture);
                passData.m_OutputTexture = builder.UseColorBuffer(inputTexture, 0);
                builder.SetRenderFunc((SATPassData data, RenderGraphContext context) =>
                {
                    //context.cmd.SetGlobalTexture("_InputTexture", data.m_InputTexture);
                    //context.cmd.SetGlobalTexture("_OutputTexture", data.m_OutputTexture);
                    //context.cmd.SetGlobalTexture("_ShadowMap", data.m_ShadowMap);
                    //CoreUtils.DrawRendererList(context.renderContext, context.cmd, data.m_renderList_opaque);
                });
                return passData;
            }
        }

        //test codes
        private Texture2D m_testInputTexture;
        const int k_TestInputTextureSize = 32;

        Texture2D GetTestInputTexture()
        {
            if (m_testInputTexture == null)
            {
                m_testInputTexture = new Texture2D(k_TestInputTextureSize, 1, TextureFormat.ARGB32, false, true);
                Color[] colors = new Color[k_TestInputTextureSize];
                for (int i = 0; i < k_TestInputTextureSize; i++)
                {
                    colors[i] = Color.white.linear;
                }
                m_testInputTexture.SetPixels(colors);
                m_testInputTexture.Apply(false, true);
            }
            return m_testInputTexture;
        }

        TextureHandle GetOutputTexture(RenderGraph graph, int width, int height)
        {
            TextureDesc textureDesc = new TextureDesc(width, height)
            {
                colorFormat = GraphicsFormat.R16G16B16A16_SFloat,
                enableRandomWrite = true,
                filterMode = FilterMode.Point
            };

            return graph.CreateTexture(textureDesc); 
        }

        public SATPassData TestParallelScan(RenderGraph graph, ComputeShader scanCS)
        {

            using (var builder = graph.AddRenderPass<SATPassData>("SAT Pass", out var passData,
                               new ProfilingSampler("SAT Pass Profiler")))
            {
                passData.m_testTexture = GetTestInputTexture();
                passData.m_SATCompute = scanCS;
                passData.m_OutputTexture = builder.ReadWriteTexture(GetOutputTexture(graph, passData.m_testTexture.width, passData.m_testTexture.height));
                if (passData.kernelPreSum == -1)
                {
                    passData.kernelPreSum = scanCS.FindKernel("PreSum");
                }
                int groupThreadsX = k_TestInputTextureSize < MaxGroupThreadsNum ? 1 : Mathf.CeilToInt((float)k_TestInputTextureSize / MaxGroupThreadsNum);
                if (groupThreadsX > 1)
                {
                    int groupSumTextureWidth = groupThreadsX;
                    passData.m_OutputGroupSumTexture = builder.ReadWriteTexture(GetOutputTexture(graph, groupSumTextureWidth, 1));
                    if (passData.kernelPreSumGroup == -1)
                        passData.kernelPreSumGroup = scanCS.FindKernel("PreSumGroup");
                    if (passData.kernelPostSum == -1)
                        passData.kernelPostSum = scanCS.FindKernel("AddGroupSum");
                }
                else
                    passData.m_OutputGroupSumTexture = TextureHandle.nullHandle;

                builder.SetRenderFunc((SATPassData data, RenderGraphContext context) =>
                {
                    data.m_SATCompute.SetTexture(data.kernelPreSum, "InputTexture", data.m_testTexture);
                    data.m_SATCompute.SetTexture(data.kernelPreSum, "OutputTexture", data.m_OutputTexture);
                    data.m_SATCompute.SetVector("InputTextureSize", new Vector4(data.m_testTexture.width, data.m_testTexture.height, 0, 0));
                    int groupsX = k_TestInputTextureSize < MaxGroupThreadsNum ? 1 : Mathf.CeilToInt((float)k_TestInputTextureSize / MaxGroupThreadsNum);
                    context.cmd.DispatchCompute(data.m_SATCompute, data.kernelPreSum, groupsX, 1, 1);

                    if (groupsX > 1)
                    {
                        data.m_SATCompute.SetVector("GroupSumTextureSize", new Vector2(groupsX, 1));
                        data.m_SATCompute.SetTexture(data.kernelPreSumGroup, "InputTexture", data.m_OutputTexture);
                        data.m_SATCompute.SetTexture(data.kernelPreSumGroup, "OutputTexture", data.m_OutputGroupSumTexture);
                        //data.m_SATCompute.SetVector("InputTextureSize", new Vector4(data.m_testTexture.width, data.m_testTexture.height, 0, 0));
                        context.cmd.DispatchCompute(data.m_SATCompute, data.kernelPreSumGroup, 1, 1, 1);

                        data.m_SATCompute.SetTexture(data.kernelPostSum, "OutputTexture", data.m_OutputTexture);
                        data.m_SATCompute.SetTexture(data.kernelPostSum, "GroupSumTexture", data.m_OutputGroupSumTexture);
                        context.cmd.DispatchCompute(data.m_SATCompute, data.kernelPostSum, groupsX, 1, 1);
                    }
                });
                return passData;
            }
        }
    }

}
