using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

/// <summary>
/// カメラ計測結果をUI Textに表示
/// </summary>
public class MeasurementLogger : MonoBehaviour
{
    [Header("UI設定")]
    [Tooltip("ログを表示するText")]
    public Text logText;

    [Header("表示設定")]
    [Tooltip("表示する最大行数")]
    [Range(1, 20)]
    public int maxLines = 5;

    private Queue<string> logQueue = new Queue<string>();
    private Queue<string> pendingLogs = new Queue<string>();
    private object lockObject = new object();

    void Update()
    {
        // メインスレッドで保留中のログを処理
        lock (lockObject)
        {
            while (pendingLogs.Count > 0)
            {
                string log = pendingLogs.Dequeue();
                logQueue.Enqueue(log);

                UnityEngine.Debug.Log($"[MeasurementLogger] メインスレッドでログ処理: {log}, logQueue.Count={logQueue.Count}");

                // 最大行数を超えたら古いログを削除
                while (logQueue.Count > maxLines)
                {
                    logQueue.Dequeue();
                }
            }
        }

        // Textを更新
        UpdateText();
    }

    /// <summary>
    /// 計測成功ログを追加
    /// </summary>
    public void LogSuccess(float speed)
    {
        UnityEngine.Debug.Log($"[MeasurementLogger] LogSuccess呼び出し: {speed}");
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string log = $"[{timestamp}] 成功: {speed:F6} m/s";
        AddLog(log);
    }

    /// <summary>
    /// 計測失敗ログを追加
    /// </summary>
    public void LogFailure(float speed, float threshold)
    {
        UnityEngine.Debug.Log($"[MeasurementLogger] LogFailure呼び出し: speed={speed}, threshold={threshold}");
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string log = $"[{timestamp}] 失敗: {speed:F6} m/s (閾値: {threshold:F3} m/s)";
        AddLog(log);
    }

    /// <summary>
    /// 計測開始ログを追加
    /// </summary>
    public void LogStart()
    {
        UnityEngine.Debug.Log("[MeasurementLogger] LogStart呼び出し");
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string log = $"[{timestamp}] 計測開始...";
        AddLog(log);
    }

    /// <summary>
    /// エラーログを追加
    /// </summary>
    public void LogError(string message)
    {
        UnityEngine.Debug.Log($"[MeasurementLogger] LogError呼び出し: {message}");
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string log = $"[{timestamp}] エラー: {message}";
        AddLog(log);
    }

    /// <summary>
    /// ログを追加（別スレッド対応）
    /// </summary>
    private void AddLog(string log)
    {
        lock (lockObject)
        {
            pendingLogs.Enqueue(log);
        }

        UnityEngine.Debug.Log($"[MeasurementLogger] ログ追加: {log}");
    }

    /// <summary>
    /// Textコンポーネントの内容を更新
    /// </summary>
    private void UpdateText()
    {
        if (logText != null)
        {
            if (logQueue.Count > 0)
            {
                // Queue<string>を配列に変換してからJoin
                string[] logs = logQueue.ToArray();
                logText.text = string.Join("\n", logs);
            }
            else
            {
                logText.text = "";
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("[MeasurementLogger] logTextが設定されていません");
        }
    }

    /// <summary>
    /// ログをクリア
    /// </summary>
    public void ClearLog()
    {
        lock (lockObject)
        {
            logQueue.Clear();
            pendingLogs.Clear();
        }

        if (logText != null)
        {
            logText.text = "";
        }
    }
}
