using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;

public class FPSDisplay : MonoBehaviour
{
    private float updateInterval = 0.3f;
    private float updateTime = 0.0f;

    public int averageFPSFrame = 10;
    private Queue<float> deltaTimes = new Queue<float>();
    private float displayDeltaTime;
    private float displayFPS = 0.0f;
    // Start is called before the first frame update
    void Start()
    {
        updateTime = updateInterval;
    }

    // Update is called once per frame
    void Update()
    {
        deltaTimes.Enqueue(Time.unscaledDeltaTime);
        if (deltaTimes.Count >= averageFPSFrame)
            deltaTimes.Dequeue();

        float deltaTime = deltaTimes.Average();

        updateTime += Time.deltaTime;
        if (updateTime >= updateInterval)
        {
            displayDeltaTime = deltaTime;
            displayFPS = 1.0f / deltaTime;
            updateTime = 0.0f;
        }
    }

    private void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label);
        style.fontSize = 24;
        GUI.Label(new Rect(20, 20, 100, style.fontSize * 2), "FPS:" + displayFPS.ToString("0.0"));
    }
}
