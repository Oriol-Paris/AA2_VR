using UnityEngine;
using TMPro;
using System.Collections.Generic;

namespace FlickGrab
{
    public class FlickGrabDebugger : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI logText;
        [SerializeField] private int maxLines = 15;
        
        private Queue<string> logQueue = new Queue<string>();

        private void OnEnable()
        {
            Application.logMessageReceived += HandleLog;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= HandleLog;
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (!logString.Contains("[FlickGrab]") && type != LogType.Error && type != LogType.Exception)
                return;

            string color = "white";
            if (type == LogType.Error || type == LogType.Exception) color = "red";
            if (type == LogType.Warning) color = "yellow";
            
            logQueue.Enqueue($"<color={color}>{logString}</color>");
            if (logQueue.Count > maxLines) logQueue.Dequeue();

            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (logText != null)
            {
                logText.text = string.Join("\n", logQueue.ToArray());
            }
        }
    }
}
