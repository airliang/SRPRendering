using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using System.Diagnostics.CodeAnalysis;
using Insanity;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor.Build;
using UnityEditor.Rendering;
namespace UnityEditor.Insanity
{
    public static class ShaderFilterUtil
    {
        public static List<string> s_ShaderIncludes = new List<string>() { "Hidden/Internal-GUITextureClipText" };

        public static bool TryGetRenderPipelineTag([DisallowNull] this Shader shader, ShaderSnippetData snippetData, [NotNullWhen(true)] out string renderPipelineTag)
        {
            renderPipelineTag = string.Empty;

            var shaderData = ShaderUtil.GetShaderData(shader);
            if (shaderData == null)
                return false;

            int subshaderIndex = (int)snippetData.pass.SubshaderIndex;
            if (subshaderIndex < 0 || subshaderIndex >= shader.subshaderCount)
                return false;

            var subShader = shaderData.GetSerializedSubshader(subshaderIndex);
            if (subShader == null)
                return false;

            ShaderTagId renderPipelineShaderTagId = new ShaderTagId("RenderPipeline");
            var shaderTag = subShader.FindTagValue(renderPipelineShaderTagId);
            if (string.IsNullOrEmpty(shaderTag.name))
                return false;

            renderPipelineTag = shaderTag.name;
            return true;
        }

        public static bool IsShaderExclude(string shaderName)
        {
            return !s_ShaderIncludes.Exists(a => a == shaderName);
        }
    }

    public class ShaderPreprocessor : IPreprocessShaders
    {
        public int callbackOrder => throw new System.NotImplementedException();

        

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            Debug.Log("OnProcessShader:" + shader.name + ", pass: " + snippet.passName);

            string renderPipelineTag;
            if (!ShaderFilterUtil.TryGetRenderPipelineTag(shader, snippet, out renderPipelineTag))
            {
                if (renderPipelineTag != InsanityPipeline.k_ShaderTagName && ShaderFilterUtil.IsShaderExclude(shader.name))
                {
                    Debug.Log(shader.name + " is not for current render pipeline, it will be removed.");
                    data.Clear();
                }
            }
        }
    }

    class ComputeShaderPreprocessorr : IPreprocessComputeShaders
    {
        public int callbackOrder => 0;

        public void OnProcessComputeShader(ComputeShader shader, string kernelName, IList<ShaderCompilerData> inputData)
        {

        }
    }
}

#endif
