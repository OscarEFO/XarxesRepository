using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class LogUI : MonoBehaviour
{
    public TMP_Text logText;

    private static readonly List<string> logs = new List<string>();
    private const int maxLogs = 60;

    void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
        Refresh();
    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        logs.Add(logString);
        if (logs.Count > maxLogs)
            logs.RemoveAt(0);
        Refresh();
    }

    void Refresh()
    {
        if (logText != null)
        {
            logText.text = string.Join("\n", logs);
        }
    }
}
