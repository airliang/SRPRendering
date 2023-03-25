using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public static class SyncViewTool
{
    private static bool syncMode = false; // 0: Follow Scene 1: Follow Game
    private static bool enableSync = false;
    static SyncViewTool()
    {
        EditorApplication.update += UpdateViewSync;

#if UNITY_2019_1_OR_NEWER
        SceneView.duringSceneGui += OnSceneGUI;
#else
        SceneView.onSceneGUIDelegate += OnSceneGUI;
#endif
    }

    static void UpdateViewSync()
    {
        if(Camera.main == null || SceneView.lastActiveSceneView == null)
            return;
        if(!enableSync)
            return;

        if(syncMode)
        {
            SceneView.lastActiveSceneView.AlignViewToObject(Camera.main.transform);
        }
        else
        {
            Camera.main.transform.position = SceneView.lastActiveSceneView.camera.transform.position;
            Camera.main.transform.rotation = SceneView.lastActiveSceneView.camera.transform.rotation;
        }
        enableSync = false;
    }

    static private void OnSceneGUI(SceneView sceneView)
    {
        Handles.BeginGUI();

        var cameraValid = Camera.main != null && Camera.main.enabled;
        GUILayout.BeginHorizontal();
        GUI.color = cameraValid ? (enableSync ? Color.green : Color.gray) : Color.gray * 0.5f;
        if(GUI.Button(new Rect(20, 10, 80, 25), "Sync View", new GUIStyle("LargeButton")) && cameraValid)
        {
            enableSync = true;
            syncMode = false;
        }

        GUI.color = Color.white;
        if(!cameraValid)
        {
            GUIStyle style = EditorStyles.label;
            Color clr = style.normal.textColor;
            style.normal.textColor = Color.red;
            GUI.Label(new Rect(20, 30, 180, 25), "Camera not found or disable!!!", style);
            style.normal.textColor = clr;
        }
        GUILayout.EndHorizontal();

        //if(enableSync)
        {
            GUI.color = Color.cyan;
            if(GUI.Button(new Rect(20, 40, 80, 25), "Sync Game", EditorStyles.miniButtonLeft))
            {
                enableSync = true;
                syncMode = true;
            }
            GUI.color = Color.white;
        }

        Handles.EndGUI();
    }
}
