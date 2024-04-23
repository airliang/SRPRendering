using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{
    public enum RenderingEvents : byte
    {
        DepthPassEvent,
        //ShadowCasterPassEvent,
        OpaqueForwardPassEvent,
        PostProcessEvent,
        MaxEvent,
    }


    public delegate void RenderingEventDelegate(ScriptableRenderContext context, CommandBuffer cmd);
    public delegate void ShadowCasterPassEventDelegate(ScriptableRenderContext context, CommandBuffer cmd, ShadowSettings shadowSettings, 
        ref ShadowDrawingSettings shadowDrawSettings, int cascadeIndex);
    public delegate void RenderGraphPassDelegate(RenderGraph graph, Camera camera);

    public class RenderingEventManager
    {
        public static event RenderingEventDelegate DepthPassRenderDelegate;
        public static event ShadowCasterPassEventDelegate ShadowCasterRenderDelegate;
        public static event RenderingEventDelegate OpaqueForwardRenderDelegate;
        public static event RenderingEventDelegate PostProcessRenderDelegate;
        public static event RenderGraphPassDelegate BeforeExecuteRenderGraphDelegate;
        static RenderingEventDelegate[] m_Events = new RenderingEventDelegate[(int)RenderingEvents.MaxEvent]
        {
            DepthPassRenderDelegate,  OpaqueForwardRenderDelegate, PostProcessRenderDelegate
        };

        static RenderGraphPassDelegate[] m_RenderGraphPassEvents = new RenderGraphPassDelegate[(int)RenderingEvents.MaxEvent]
        {
            null, null, null
        };

        public static void AddEvent(RenderingEvents evt, RenderingEventDelegate del)
        {
            m_Events[(int)evt] += del;
        }

        public static void RemoveEvent(RenderingEvents evt, RenderingEventDelegate del)
        {
            m_Events[(int)evt] -= del;
        }

        public static void InvokeEvent(RenderingEvents evt, ScriptableRenderContext contex, CommandBuffer cmd)
        {
            if (m_Events[(int)evt] != null)
                m_Events[(int)evt].Invoke(contex, cmd);
        }

        public static void AddShadowCasterEvent(ShadowCasterPassEventDelegate del)
        {
            ShadowCasterRenderDelegate += del;
        }

        public static void RemoveShadowCasterEvent(ShadowCasterPassEventDelegate del)
        {
            ShadowCasterRenderDelegate -= del;
        }

        public static void InvokeShadowCasterEvent(ScriptableRenderContext contex, CommandBuffer cmd, ShadowSettings shadowSettings,
            ref ShadowDrawingSettings shadowDrawSettings, int cascadeIndex)
        {
            ShadowCasterRenderDelegate?.Invoke(contex, cmd, shadowSettings, ref shadowDrawSettings, cascadeIndex);
        }

        public static void BeforeExecuteRenderGraph(RenderGraph renderGraph, Camera camera)
        {
            if (BeforeExecuteRenderGraphDelegate != null)
                BeforeExecuteRenderGraphDelegate.Invoke(renderGraph, camera);
        }
    }
}

