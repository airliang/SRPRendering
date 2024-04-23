using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

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
        //public int m_inputOffsetX = 0;
        //public int m_inputOffsetY = 0;
        public Vector4 m_inputST; //x,y:scale, z,w:offset
        public Vector4 m_validRect;
        public bool m_firstPassTransposeOutput = false;
        public bool m_transposeOutput;
        public int m_groupNumX = 1;


        public TextureHandle GetFinalOutputTexture()
        {
            return m_transposeOutput ? m_transposeSumTexture : m_OutputTexture;
        }
    }

    public class GenerateTestTexturePassData
    {
        public Texture2D m_inputTexture;
        public TextureHandle m_testTexture;
        public Material m_blitMaterial;
    }


    
    public struct SATTexture
    {
        public TextureHandle m_texture;
        public int m_width;
        public int m_height;
        public TextureFormat m_format;
        public Vector4 m_ST;
        
        public SATTexture(TextureHandle texture, int width, int height, TextureFormat textureFormat, Vector4 st)
        {
            m_texture = texture;
            m_width = width;
            m_height = height;
            m_format = textureFormat;
            m_ST = st;
        }
    }

    public class SATRenderer
    {
        public static Vector4 defaultST = new Vector4(1, 1, 0, 0);
        private static class SATConstantBuffer
        {
            public static int _InputTexture;
            public static int _GroupSumTexture;
            public static int _OutputTexture;
            public static int _InputTextureSize;
            public static int _GroupSumTextureSize;
            public static int _ValidRect;
            public static int _IsTranposeOutput;
            public static int _ArrayLengthPerThreadGroup;
            public static int _InputTextureST;
            public static int _OutputTextureST;
        }

        public SATRenderer()
        {
            SATConstantBuffer._InputTexture = Shader.PropertyToID("InputTexture");
            SATConstantBuffer._GroupSumTexture = Shader.PropertyToID("GroupSumTexture");
            SATConstantBuffer._OutputTexture = Shader.PropertyToID("OutputTexture");
            SATConstantBuffer._InputTextureSize = Shader.PropertyToID("InputTextureSize");
            SATConstantBuffer._GroupSumTextureSize = Shader.PropertyToID("GroupSumTextureSize");
            SATConstantBuffer._ValidRect = Shader.PropertyToID("ValidRect");
            SATConstantBuffer._IsTranposeOutput = Shader.PropertyToID("IsTranposeOutput");
            SATConstantBuffer._ArrayLengthPerThreadGroup = Shader.PropertyToID("ArrayLengthPerThreadGroup");
            SATConstantBuffer._InputTextureST = Shader.PropertyToID("InputTextureST");
            SATConstantBuffer._OutputTextureST = Shader.PropertyToID("OutputTextureST");
        }


        private const int MaxGroupThreadsNum = 128;
        public SATPassData RenderSAT(RenderGraph graph, ComputeShader scanCS, ref SATTexture inputTexture, ref SATTexture outputTexture, Vector4 validRect)
        {
            SATPassData passDataRow = ParallelScan(graph, scanCS, ref inputTexture, ref outputTexture, false, validRect);
            SATPassData passData = null;
            if (inputTexture.m_height > 1)
            {
                //testTexture = new Texture2D(passData.m_transposeSumTexture);
                int inputTextureWidth = inputTexture.m_height;
                int inputTextureHeight = inputTexture.m_width;
                inputTexture = new SATTexture(passDataRow.GetFinalOutputTexture(), inputTextureWidth, inputTextureHeight, 
                    outputTexture.m_format, defaultST);
                passData = ParallelScan(graph, scanCS, ref inputTexture, ref outputTexture, true, validRect);
            }
            else
                passData = passDataRow;
            return passData;
        }

        //test codes
        private Texture2D m_testInputTexture;
        const int k_TestInputTextureWidth = 1024;
        const int k_TestInputTextureHeight = 1024;

        Texture2D GetTestInputTexture()
        {
            if (m_testInputTexture == null)
            {
                m_testInputTexture = new Texture2D(k_TestInputTextureWidth, k_TestInputTextureHeight, TextureFormat.RFloat, false, true);
                Color[] colors = new Color[k_TestInputTextureWidth * k_TestInputTextureHeight];
                for (int row = 0; row < k_TestInputTextureHeight; row++)
                {
                    for (int col = 0; col < k_TestInputTextureWidth; col++)
                    {
                        float pixelValue = 0;
                        if (col >= 785 && col <= 787 && row == 192)
                        {
                            pixelValue = 0.99998f;
                            
                        }
                        colors[row * k_TestInputTextureWidth + col] = new Color(pixelValue, pixelValue, pixelValue, pixelValue);//Color.white.linear;
                    }
                }

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

        public SATPassData ParallelScan(RenderGraph graph, ComputeShader scanCS, ref SATTexture inputTexture, ref SATTexture outputTexture,
            bool verticalScan, Vector4 validRect)
        {
            string passName = verticalScan ? "Vertical Parallen Scan Pass" : "Horizontal Parallen Scan Pass";
            using (var builder = graph.AddRenderPass<SATPassData>(passName, out var passData,
                               new ProfilingSampler("SAT Pass Profiler")))
            {
                passData.m_inputTexture = builder.ReadTexture(inputTexture.m_texture);
                passData.m_SATCompute = scanCS;
                GraphicsFormat satFormat = GetSATTextureFormat(inputTexture.m_format);
                int outputTextureWidth = verticalScan ? inputTexture.m_height : inputTexture.m_width;
                int outputTextureHeight = verticalScan ? inputTexture.m_width : inputTexture.m_height;
                passData.m_OutputTexture = builder.ReadWriteTexture(GetOutputTexture(graph, outputTextureWidth,
                    outputTextureHeight, satFormat));
                bool equalRatio = inputTexture.m_width == inputTexture.m_height;
                passData.m_OutputTexture = builder.ReadWriteTexture(outputTexture.m_texture);
                if (passData.kernelPreSum == -1)
                {
                    passData.kernelPreSum = scanCS.FindKernel("PreSum");
                }
                int groupThreadsX = inputTexture.m_width < MaxGroupThreadsNum ? 1 : Mathf.CeilToInt((float)inputTexture.m_width / MaxGroupThreadsNum);
                if (groupThreadsX > 1)
                {
                    int groupSumTextureWidth = groupThreadsX;
                    passData.m_OutputGroupSumTexture = builder.ReadWriteTexture(GetOutputTexture(graph, groupSumTextureWidth, 
                        inputTexture.m_height, satFormat));
                    if (passData.kernelPreSumGroup == -1)
                        passData.kernelPreSumGroup = scanCS.FindKernel("PreSumGroup");
                    if (passData.kernelPostSum == -1)
                        passData.kernelPostSum = scanCS.FindKernel("AddGroupSum");
                }
                else
                    passData.m_OutputGroupSumTexture = TextureHandle.nullHandle;
                passData.m_inputST = inputTexture.m_ST;

                if (!verticalScan)
                {
                    if (inputTexture.m_height > 1)
                    {
                        passData.m_transposeSumTexture = /*equalRatio ? passData.m_OutputTexture : */builder.ReadWriteTexture(GetOutputTexture(graph,
                            inputTexture.m_height, inputTexture.m_width, satFormat));
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
                    passData.m_transposeSumTexture = /*equalRatio ? passData.m_OutputTexture : */builder.ReadWriteTexture(GetOutputTexture(graph,
                        inputTexture.m_height, inputTexture.m_width, satFormat));
                    passData.m_transposeOutput = true;
                }
                passData.m_inputTextureWidth = inputTexture.m_width;
                passData.m_inputTextureHeight = inputTexture.m_height;
                passData.m_validRect = validRect;
                //if verticalScan is true, the input texture height must be greater than 1.
                //so in the verticalScan it do not need transpose output in the first pass.
                passData.m_firstPassTransposeOutput = verticalScan ? false : (inputTexture.m_height > 1 && groupThreadsX == 1);
                passData.m_groupNumX = groupThreadsX;

                builder.SetRenderFunc((SATPassData data, RenderGraphContext context) =>
                {
                    context.cmd.SetComputeIntParam(data.m_SATCompute, SATConstantBuffer._IsTranposeOutput, data.m_firstPassTransposeOutput ? 1 : 0);
                    context.cmd.SetComputeVectorParam(data.m_SATCompute, SATConstantBuffer._ValidRect, data.m_validRect);
                    Vector4 inputSize = new Vector4(data.m_inputTextureWidth, data.m_inputTextureHeight, 0, 0);
                    context.cmd.SetComputeVectorParam(data.m_SATCompute, SATConstantBuffer._InputTextureSize, inputSize);
                    context.cmd.SetComputeVectorParam(data.m_SATCompute, SATConstantBuffer._InputTextureST, data.m_inputST);
                    context.cmd.SetComputeTextureParam(data.m_SATCompute, data.kernelPreSum, SATConstantBuffer._InputTexture, data.m_inputTexture);
                    context.cmd.SetComputeTextureParam(data.m_SATCompute, data.kernelPreSum, SATConstantBuffer._OutputTexture, data.m_OutputTexture);

                    int groupsX = data.m_inputTextureWidth < MaxGroupThreadsNum ? 1 : Mathf.CeilToInt((float)data.m_inputTextureWidth / MaxGroupThreadsNum);
                    int groupsY = data.m_inputTextureHeight;
                    context.cmd.DispatchCompute(data.m_SATCompute, data.kernelPreSum, groupsX, groupsY, 1);

                    if (groupsX > 1)
                    {
                        context.cmd.SetComputeIntParam(data.m_SATCompute, SATConstantBuffer._ArrayLengthPerThreadGroup, groupsX);
                        context.cmd.SetComputeVectorParam(data.m_SATCompute, SATConstantBuffer._GroupSumTextureSize, new Vector2(groupsX, groupsY));

                        context.cmd.SetComputeTextureParam(data.m_SATCompute, data.kernelPreSumGroup, SATConstantBuffer._InputTexture, data.m_OutputTexture);
                        context.cmd.SetComputeTextureParam(data.m_SATCompute, data.kernelPreSumGroup, SATConstantBuffer._OutputTexture, data.m_OutputGroupSumTexture);
                        context.cmd.DispatchCompute(data.m_SATCompute, data.kernelPreSumGroup, 1, groupsY, 1);

                        context.cmd.SetComputeIntParam(data.m_SATCompute, SATConstantBuffer._IsTranposeOutput, data.m_transposeOutput ? 1 : 0);
                        context.cmd.SetComputeTextureParam(data.m_SATCompute, data.kernelPostSum, SATConstantBuffer._InputTexture, data.m_OutputTexture);
                        context.cmd.SetComputeTextureParam(data.m_SATCompute, data.kernelPostSum, SATConstantBuffer._OutputTexture,
                            data.m_transposeOutput ? data.m_transposeSumTexture : data.m_OutputTexture);
                        context.cmd.SetComputeTextureParam(data.m_SATCompute, data.kernelPostSum, SATConstantBuffer._GroupSumTexture, data.m_OutputGroupSumTexture);
                        context.cmd.DispatchCompute(data.m_SATCompute, data.kernelPostSum, groupsX, groupsY, 1);
                    }
                });
                return passData;
            }
        }

        public SATPassData TestParallelScan(RenderGraph graph, ComputeShader scanCS)
        {
            GenerateTestTexturePassData generateTestTexturePassData = GenerateTestTexture(graph);
            int inValidWidth = 4;
            SATTexture inputTexture = new SATTexture(generateTestTexturePassData.m_testTexture, generateTestTexturePassData.m_inputTexture.width,
                generateTestTexturePassData.m_inputTexture.height, generateTestTexturePassData.m_inputTexture.format, defaultST);
            GraphicsFormat graphicsFormat = generateTestTexturePassData.m_inputTexture.graphicsFormat;
            int outputTextureWidth = generateTestTexturePassData.m_inputTexture.width;
            int outputTextureHeight = generateTestTexturePassData.m_inputTexture.height;
            TextureHandle outputTextureHandle =
                GetOutputTestTexture(graph, outputTextureWidth,
                outputTextureHeight, graphicsFormat, "SATTestOutputTexture", true);
            SATTexture outputTexture = new SATTexture(outputTextureHandle, outputTextureWidth,
                               outputTextureHeight, generateTestTexturePassData.m_inputTexture.format, defaultST);

            SATPassData passDataRow = ParallelScan(graph, scanCS, ref inputTexture, ref outputTexture, false, new Vector4(inValidWidth, inValidWidth, 
                generateTestTexturePassData.m_inputTexture.width - 1 - inValidWidth,
                generateTestTexturePassData.m_inputTexture.height - 1 - inValidWidth));
            SATPassData passData = null;
            if (generateTestTexturePassData.m_inputTexture.height > 1)
            {
                int inputTextureWidth = generateTestTexturePassData.m_inputTexture.height;
                int inputTextureHeight = generateTestTexturePassData.m_inputTexture.width;
                TextureHandle inputTextureHandle = passDataRow.GetFinalOutputTexture();
                inputTexture = new SATTexture(inputTextureHandle, inputTextureWidth, inputTextureHeight, generateTestTexturePassData.m_inputTexture.format, defaultST);
                passData = ParallelScan(graph, scanCS, ref inputTexture, ref outputTexture, true, new Vector4(inValidWidth, inValidWidth,
                    inputTextureWidth - 1 - inValidWidth, inputTextureHeight - 1 - inValidWidth));
            }
            else
                passData = passDataRow;
            return passData;
        }

        TextureHandle GetOutputTestTexture(RenderGraph graph, int width, int height, GraphicsFormat graphicsFormat, string textureName, bool writable)
        {
            TextureDesc textureDesc = new TextureDesc(width, height)
            {
                colorFormat = graphicsFormat,
                filterMode = FilterMode.Point,
                depthBufferBits = DepthBits.None,
                autoGenerateMips = false,
                useMipMap = false,
                name = textureName,
                enableRandomWrite = writable
            };

            return graph.CreateTexture(textureDesc);
        }

        GenerateTestTexturePassData GenerateTestTexture(RenderGraph graph)
        {
            using (var builder = graph.AddRenderPass<GenerateTestTexturePassData>("Generate Test Texture", out var passData,
                               new ProfilingSampler("Generate Test Texture Pass Profiler")))
            {
                Texture2D texture = GetTestInputTexture();
                GraphicsFormat graphicsFormat = texture.graphicsFormat;
                passData.m_inputTexture = texture;
                passData.m_testTexture = builder.WriteTexture(GetOutputTestTexture(graph, texture.width, texture.height, graphicsFormat, "TestSATInputTexture", false));

                builder.SetRenderFunc((GenerateTestTexturePassData data, RenderGraphContext context) =>
                {
                    context.cmd.Blit(data.m_inputTexture, data.m_testTexture);
                });
                return passData;
            }
        }

        public void Release()
        {

        }
    }

}
