using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using static TreeEditor.TextureAtlas;

namespace Insanity
{
    public class SATPassData
    {
        //public TextureHandle m_InputTexture;
        public TextureHandle m_OutputTexture;
        public TextureHandle m_OutputGroupSumTexture;
        public TextureHandle m_transposeSumTexture;
        public TextureHandle m_OutputGroupSumTextureVertical;
        public ComputeShader m_SATCompute;
        public int kernelPreSum = -1;
        public int kernelPreSumGroup = -1;
        public int kernelPostSum = -1;
        //public Texture2D m_testTexture;
        public TextureHandle m_inputTexture;
        public int m_inputTextureWidth;
        public int m_inputTextureHeight;
        public bool m_firstPassTransposeOutput = false;
        public bool m_transposeOutput;
        public int m_groupNumX = 1;
    }

    public class GenerateTestTexturePassData
    {
        public Texture2D m_inputTexture;
        public TextureHandle m_testTexture;
        public Material m_blitMaterial;
    }

    public class SATRenderer
    {
        private const int MaxGroupThreadsNum = 128;
        public SATPassData RenderSAT(RenderGraph graph, TextureHandle inputTexture, ComputeShader scanCS)
        {
            using (var builder = graph.AddRenderPass<SATPassData>("SAT Pass", out var passData, 
                               new ProfilingSampler("SAT Pass Profiler")))
            {
                //passData.m_InputTexture = builder.ReadTexture(inputTexture);
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
        const int k_TestInputTextureWidth = 256;
        const int k_TestInputTextureHeight = 256;

        Texture2D GetTestInputTexture()
        {
            if (m_testInputTexture == null)
            {
                m_testInputTexture = new Texture2D(k_TestInputTextureWidth, k_TestInputTextureHeight, TextureFormat.ARGB32, false, true);
                Color[] colors = new Color[k_TestInputTextureWidth * k_TestInputTextureHeight];
                for (int i = 0; i < k_TestInputTextureWidth * k_TestInputTextureHeight; i++)
                {
                    colors[i] = Color.white.linear;
                }
                //for (int i = k_TestInputTextureWidth; i < k_TestInputTextureWidth * k_TestInputTextureHeight; ++i)
                //{
                //    colors[i] = Color.red;
                //}
                m_testInputTexture.SetPixels(colors);
                m_testInputTexture.Apply(false, true);
            }
            return m_testInputTexture;
        }

        GraphicsFormat GetSATTextureFormat(TextureFormat inputTextureFormat)
        {
            switch (inputTextureFormat)
            {
                case TextureFormat.R8:
                case TextureFormat.R16:
                case TextureFormat.RFloat:
                    return GraphicsFormat.R32_SFloat;
                case TextureFormat.RGFloat:
                    return GraphicsFormat.R32G32_SFloat;
                case TextureFormat.RGBAFloat:
                    return GraphicsFormat.R32G32B32A32_SFloat;
                default:
                    return GraphicsFormat.R32G32B32A32_SFloat;
            }
        }

        TextureHandle GetOutputTexture(RenderGraph graph, int width, int height, GraphicsFormat graphicsFormat)
        {
            TextureDesc textureDesc = new TextureDesc(width, height)
            {
                colorFormat = graphicsFormat,
                enableRandomWrite = true,
                filterMode = FilterMode.Point
            };

            return graph.CreateTexture(textureDesc); 
        }

        public SATPassData ParallelScan(RenderGraph graph, ComputeShader scanCS, TextureHandle inputTexture, 
            int inputWidth, int inputHeight, TextureFormat inputFormat, bool verticalScan)
        {
            string passName = verticalScan ? "Vertical Parallen Scan Pass" : "Horizontal Parallen Scan Pass";
            using (var builder = graph.AddRenderPass<SATPassData>(passName, out var passData,
                               new ProfilingSampler("SAT Pass Profiler")))
            {
                passData.m_inputTexture = builder.ReadTexture(inputTexture);
                passData.m_SATCompute = scanCS;
                GraphicsFormat satFormat = GetSATTextureFormat(inputFormat);
                int outputTextureWidth = verticalScan ? inputHeight : inputWidth;
                int outputTextureHeight = verticalScan ? inputWidth : inputHeight;
                passData.m_OutputTexture = builder.ReadWriteTexture(GetOutputTexture(graph, outputTextureWidth,
                    outputTextureHeight, satFormat));
                if (passData.kernelPreSum == -1)
                {
                    passData.kernelPreSum = scanCS.FindKernel("PreSum");
                }
                int groupThreadsX = inputWidth < MaxGroupThreadsNum ? 1 : Mathf.CeilToInt((float)inputWidth / MaxGroupThreadsNum);
                if (groupThreadsX > 1)
                {
                    int groupSumTextureWidth = groupThreadsX;
                    passData.m_OutputGroupSumTexture = builder.ReadWriteTexture(GetOutputTexture(graph, groupSumTextureWidth, 
                        inputHeight, satFormat));
                    if (passData.kernelPreSumGroup == -1)
                        passData.kernelPreSumGroup = scanCS.FindKernel("PreSumGroup");
                    if (passData.kernelPostSum == -1)
                        passData.kernelPostSum = scanCS.FindKernel("AddGroupSum");
                }
                else
                    passData.m_OutputGroupSumTexture = TextureHandle.nullHandle;

                if (!verticalScan)
                {
                    if (inputHeight > 1)
                    {
                        passData.m_transposeSumTexture = builder.ReadWriteTexture(GetOutputTexture(graph,
                            inputHeight, inputWidth, satFormat));
                        passData.m_transposeOutput = true;
                    }
                    else
                    {
                        passData.m_transposeSumTexture = TextureHandle.nullHandle;
                        passData.m_transposeOutput = false;
                    }
                }
                else
                {
                    passData.m_transposeSumTexture = builder.ReadWriteTexture(GetOutputTexture(graph,
                        inputHeight, inputWidth, satFormat));
                    passData.m_transposeOutput = true;
                }
                passData.m_inputTextureWidth = inputWidth;
                passData.m_inputTextureHeight = inputHeight;
                //if verticalScan is true, the input texture height must be greater than 1.
                //so in the verticalScan it do not need transpose output in the first pass.
                passData.m_firstPassTransposeOutput = verticalScan ? false : (inputHeight > 1 && groupThreadsX == 1);
                passData.m_groupNumX = groupThreadsX;

                builder.SetRenderFunc((SATPassData data, RenderGraphContext context) =>
                {
                    context.cmd.SetComputeIntParam(data.m_SATCompute, "IsTranposeOutput", data.m_firstPassTransposeOutput ? 1 : 0);
                    context.cmd.SetComputeTextureParam(data.m_SATCompute, data.kernelPreSum, "InputTexture", data.m_inputTexture);
                    context.cmd.SetComputeTextureParam(data.m_SATCompute, data.kernelPreSum, "OutputTexture", data.m_OutputTexture);
                    context.cmd.SetComputeVectorParam(data.m_SATCompute, "InputTextureSize", new Vector4(data.m_inputTextureWidth, data.m_inputTextureHeight, 0, 0));

                    int groupsX = data.m_inputTextureWidth < MaxGroupThreadsNum ? 1 : Mathf.CeilToInt((float)data.m_inputTextureWidth / MaxGroupThreadsNum);
                    int groupsY = data.m_inputTextureHeight;
                    context.cmd.DispatchCompute(data.m_SATCompute, data.kernelPreSum, groupsX, groupsY, 1);

                    if (groupsX > 1)
                    {
                        context.cmd.SetComputeIntParam(data.m_SATCompute, "arrayLengthPerThreadGroup", groupsX);
                        context.cmd.SetComputeVectorParam(data.m_SATCompute, "GroupSumTextureSize", new Vector2(groupsX, groupsY));
                        context.cmd.SetComputeTextureParam(data.m_SATCompute, data.kernelPreSumGroup, "InputTexture", data.m_OutputTexture);
                        context.cmd.SetComputeTextureParam(data.m_SATCompute, data.kernelPreSumGroup, "OutputTexture", data.m_OutputGroupSumTexture);
                        context.cmd.DispatchCompute(data.m_SATCompute, data.kernelPreSumGroup, 1, groupsY, 1);

                        context.cmd.SetComputeIntParam(data.m_SATCompute, "IsTranposeOutput", data.m_transposeOutput ? 1 : 0);
                        context.cmd.SetComputeTextureParam(data.m_SATCompute, data.kernelPostSum, "InputTexture", data.m_OutputTexture);
                        context.cmd.SetComputeTextureParam(data.m_SATCompute, data.kernelPostSum, "OutputTexture",
                            data.m_transposeOutput ? data.m_transposeSumTexture : data.m_OutputTexture);
                        context.cmd.SetComputeTextureParam(data.m_SATCompute, data.kernelPostSum, "GroupSumTexture", data.m_OutputGroupSumTexture);
                        context.cmd.DispatchCompute(data.m_SATCompute, data.kernelPostSum, groupsX, groupsY, 1);
                    }
                });
                return passData;
            }
        }

        public SATPassData TestParallelScan(RenderGraph graph, ComputeShader scanCS)
        {
            //Texture2D testTexture = GetTestInputTexture();
            GenerateTestTexturePassData generateTestTexturePassData = GenerateTestTexture(graph);

            SATPassData passDataRow = ParallelScan(graph, scanCS, generateTestTexturePassData.m_testTexture,
                generateTestTexturePassData.m_inputTexture.width, generateTestTexturePassData.m_inputTexture.height,
                generateTestTexturePassData.m_inputTexture.format, false);
            SATPassData passData = null;
            if (generateTestTexturePassData.m_inputTexture.height > 1)
            {
                //testTexture = new Texture2D(passData.m_transposeSumTexture);
                int inputTextureWidth = generateTestTexturePassData.m_inputTexture.height;
                int inputTextureHeight = generateTestTexturePassData.m_inputTexture.width;
                TextureHandle inputTexture = passDataRow.m_groupNumX == 1 ? passDataRow.m_OutputTexture : passDataRow.m_transposeSumTexture;
                passData = ParallelScan(graph, scanCS, inputTexture, inputTextureWidth, inputTextureHeight,
                    generateTestTexturePassData.m_inputTexture.format, true);
            }
            else
                passData = passDataRow;
            return passData;
        }

        GenerateTestTexturePassData GenerateTestTexture(RenderGraph graph)
        {
            TextureHandle GetOutputTestTexture(RenderGraph graph, int width, int height, GraphicsFormat graphicsFormat)
            {
                TextureDesc textureDesc = new TextureDesc(width, height)
                {
                    colorFormat = graphicsFormat,
                    filterMode = FilterMode.Point,
                    depthBufferBits = DepthBits.None,
                    autoGenerateMips = false,
                    useMipMap = false,
                    name = "TestSATInputTexture"
                };

                return graph.CreateTexture(textureDesc);
            }

            using (var builder = graph.AddRenderPass<GenerateTestTexturePassData>("Generate Test Texture", out var passData,
                               new ProfilingSampler("Generate Test Texture Pass Profiler")))
            {
                Texture2D texture = GetTestInputTexture();
                GraphicsFormat graphicsFormat = texture.graphicsFormat;
                passData.m_inputTexture = texture;
                passData.m_testTexture = builder.WriteTexture(GetOutputTestTexture(graph, texture.width, texture.height, graphicsFormat));

                builder.SetRenderFunc((GenerateTestTexturePassData data, RenderGraphContext context) =>
                {
                    context.cmd.Blit(data.m_inputTexture, data.m_testTexture);
                });
                return passData;
            }
        }
    }

}
