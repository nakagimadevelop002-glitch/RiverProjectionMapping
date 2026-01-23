using UnityEngine;
using System.Diagnostics;
using System.Collections;

/// <summary>
/// Pythonスクリプトを起動し、川の速度を受け取る
/// </summary>
public class CameraSpeedReceiver : MonoBehaviour
{
    [Header("Python設定")]
    [Tooltip("Pythonの実行ファイルパス")]
    public string pythonExePath = "python";

    [Tooltip("実行するPythonスクリプトのパス")]
    public string scriptPath = "Assets/StreamingAssets/PythonScripts/river_speed_camera.py";

    [Header("速度適用")]
    [Tooltip("速度を適用するRiverFlowSimulation")]
    public RiverFlowSimulation simulation;

    [Tooltip("受信した速度を自動的に適用するか")]
    public bool autoApplySpeed = true;

    [Header("デバッグ")]
    [Tooltip("デバッグログを表示するか")]
    public bool showDebugLog = true;

    [Tooltip("最後に受信した速度")]
    public float lastReceivedSpeed = 0f;

    private Process pythonProcess;
    private bool isRunning = false;

    // 最新値のみ保持（Queue不要）
    private float latestSpeed = 0f;
    private bool hasNewSpeed = false;
    private object lockObject = new object();

    void Start()
    {
        StartPythonProcess();
    }

    /// <summary>
    /// Pythonプロセスを起動
    /// </summary>
    void StartPythonProcess()
    {
        if (string.IsNullOrEmpty(scriptPath))
        {
            UnityEngine.Debug.LogError("[CameraSpeedReceiver] scriptPathが設定されていません");
            return;
        }

        if (simulation == null)
        {
            UnityEngine.Debug.LogError("[CameraSpeedReceiver] simulationが設定されていません");
            return;
        }

        try
        {
            pythonProcess = new Process();
            pythonProcess.StartInfo.FileName = pythonExePath;
            pythonProcess.StartInfo.Arguments = scriptPath;
            pythonProcess.StartInfo.UseShellExecute = false;
            pythonProcess.StartInfo.RedirectStandardOutput = true;
            pythonProcess.StartInfo.RedirectStandardError = true;
            pythonProcess.StartInfo.CreateNoWindow = true;

            // 非同期でデータ受信（別スレッドで実行）
            pythonProcess.OutputDataReceived += OnOutputDataReceived;
            pythonProcess.ErrorDataReceived += OnErrorDataReceived;

            pythonProcess.Start();
            isRunning = true;

            // 非同期読み取り開始
            pythonProcess.BeginOutputReadLine();
            pythonProcess.BeginErrorReadLine();

            if (showDebugLog)
                UnityEngine.Debug.Log($"[CameraSpeedReceiver] Pythonプロセス起動: {scriptPath}");

            // メインスレッドで速度適用
            StartCoroutine(ApplyLatestSpeed());
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"[CameraSpeedReceiver] Pythonプロセス起動失敗: {e.Message}");
        }
    }

    /// <summary>
    /// Pythonからの標準出力を受信（別スレッド）
    /// </summary>
    void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            if (float.TryParse(e.Data, out float speed))
            {
                // 最新値のみ保持（上書き）
                lock (lockObject)
                {
                    latestSpeed = speed;
                    hasNewSpeed = true;
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[CameraSpeedReceiver] 速度変換失敗: {e.Data}");
            }
        }
    }

    /// <summary>
    /// Pythonからのエラー出力を受信（別スレッド）
    /// </summary>
    void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            UnityEngine.Debug.LogError($"[CameraSpeedReceiver] Pythonエラー: {e.Data}");
        }
    }

    /// <summary>
    /// 最新の速度を適用（メインスレッド）
    /// </summary>
    IEnumerator ApplyLatestSpeed()
    {
        while (isRunning)
        {
            bool shouldApply = false;
            float speed = 0f;

            // 最新値を取得
            lock (lockObject)
            {
                if (hasNewSpeed)
                {
                    speed = latestSpeed;
                    hasNewSpeed = false;
                    shouldApply = true;
                }
            }

            // 速度を適用（メインスレッドで実行）
            if (shouldApply)
            {
                lastReceivedSpeed = speed;

                if (showDebugLog)
                    UnityEngine.Debug.Log($"[CameraSpeedReceiver] 速度受信: {speed}");

                if (autoApplySpeed && simulation != null)
                {
                    simulation.SetSpeedMultiplier(speed);
                }
            }

            yield return null;  // 次のフレームまで待機（パーティクルはフリーズしない）
        }

        if (showDebugLog)
            UnityEngine.Debug.Log("[CameraSpeedReceiver] 速度適用終了");
    }

    /// <summary>
    /// Pythonプロセスを終了
    /// </summary>
    void StopPythonProcess()
    {
        isRunning = false;

        if (pythonProcess != null)
        {
            // イベントハンドラを解除
            pythonProcess.OutputDataReceived -= OnOutputDataReceived;
            pythonProcess.ErrorDataReceived -= OnErrorDataReceived;

            if (!pythonProcess.HasExited)
            {
                pythonProcess.Kill();
            }

            pythonProcess.Dispose();

            if (showDebugLog)
                UnityEngine.Debug.Log("[CameraSpeedReceiver] Pythonプロセス終了");
        }

        // 最新値をリセット
        lock (lockObject)
        {
            hasNewSpeed = false;
        }
    }

    void OnDestroy()
    {
        StopPythonProcess();
    }

    void OnApplicationQuit()
    {
        StopPythonProcess();
    }
}
