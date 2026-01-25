using UnityEngine;
using UnityEngine.UI;
using System.Diagnostics;
using System.Collections;

/// <summary>
/// Pythonスクリプトを定期的に起動し、川の速度を受け取る
/// </summary>
public class CameraSpeedReceiver : MonoBehaviour
{
    [Header("Python設定")]
    [Tooltip("Pythonの実行ファイルパス")]
    public string pythonExePath = "python";

    [Tooltip("実行するPythonスクリプトのパス")]
    public string scriptPath = "Assets/StreamingAssets/PythonScripts/baseline_stiv.py";

    [Header("カメラ設定")]
    [Tooltip("カメラID（0: ASUS IR camera、1: ASUS FHD webcam、2以降: カメラ名で指定）")]
    [Range(0, 9)]
    public int cameraId = 1;

    [Tooltip("カメラ名（ID 2以降の場合に使用。例: HD Webcam eMeet C960）")]
    public string cameraName = "";

    [Header("計測モード")]
    [Tooltip("テストモード（PCカメラで手の動きを計測）")]
    public bool testMode = false;

    [Tooltip("テストモード時の空間分解能 [m/pixel]（50cm距離の手の動き用）")]
    public float testSpatialResolution = 0.05f;

    [Tooltip("本番モード時の空間分解能 [m/pixel]（川の計測用）")]
    public float normalSpatialResolution = 0.01f;

    [Header("計測間隔設定")]
    [Tooltip("計測実行間隔（秒）")]
    [Range(15f, 300f)]
    public float measurementInterval = 15f;

    [Header("速度適用")]
    [Tooltip("速度を適用するRiverFlowSimulation")]
    public RiverFlowSimulation simulation;

    [Tooltip("受信した速度を自動的に適用するか")]
    public bool autoApplySpeed = true;

    [Header("計測設定")]
    [Tooltip("有効な速度の最小値（これ以下は計測失敗とみなす）[m/s]")]
    [Range(0.001f, 0.1f)]
    public float minimumValidSpeed = 0.01f;

    [Header("STIV検出パラメータ")]
    [Tooltip("事前平滑化（小さいほど敏感、テストモード推奨: 0.5）")]
    [Range(0.1f, 3.0f)]
    public float sigmaPre = 1.0f;

    [Tooltip("構造テンソル平滑化（小さいほど敏感、テストモード推奨: 1.0）")]
    [Range(0.1f, 5.0f)]
    public float sigmaTensor = 2.0f;

    [Tooltip("使用フレーム数（少ないほど短時間の動きを検出、テストモード推奨: 150）")]
    [Range(50, 600)]
    public int maxFrames = 300;

    [Header("UI")]
    [Tooltip("即時計測ボタン（計測中は無効化）")]
    public Button measureButton;

    [Header("デバッグ")]
    [Tooltip("デバッグログを表示するか")]
    public bool showDebugLog = true;

    [Tooltip("最後に受信した速度")]
    public float lastReceivedSpeed = 0f;

    [Tooltip("現在計測中かどうか")]
    public bool isProcessing = false;

    private Process pythonProcess;
    private bool lastIsProcessing = false;

    void Start()
    {
        if (simulation == null)
        {
            UnityEngine.Debug.LogError("[CameraSpeedReceiver] simulationが設定されていません");
            return;
        }

        // ボタン初期化
        UpdateButtonState();

        // 定期計測開始（最初は即座に、その後はmeasurementInterval秒ごと）
        InvokeRepeating(nameof(StartMeasurement), 0f, measurementInterval);
    }

    void Update()
    {
        // isProcessing状態が変更された場合、ボタン状態を更新
        if (isProcessing != lastIsProcessing)
        {
            UpdateButtonState();
            lastIsProcessing = isProcessing;
        }
    }

    /// <summary>
    /// ボタンの有効/無効状態を更新
    /// </summary>
    void UpdateButtonState()
    {
        if (measureButton != null)
        {
            measureButton.interactable = !isProcessing;
        }
    }

    /// <summary>
    /// 計測を開始（定期起動用）
    /// </summary>
    void StartMeasurement()
    {
        if (isProcessing)
        {
            if (showDebugLog)
                UnityEngine.Debug.LogWarning("[CameraSpeedReceiver] 前回の計測中のためスキップ");
            return;
        }

        StartPythonProcess();
    }

    /// <summary>
    /// 今すぐ計測を開始（UI Button用）
    /// </summary>
    public void StartMeasurementNow()
    {
        if (isProcessing)
        {
            if (showDebugLog)
                UnityEngine.Debug.LogWarning("[CameraSpeedReceiver] 計測中のため開始できません");
            return;
        }

        StartPythonProcess();
    }

    /// <summary>
    /// 計測間隔を設定（UI Slider用）
    /// </summary>
    public void SetMeasurementInterval(float interval)
    {
        measurementInterval = interval;

        // InvokeRepeatingを再設定
        CancelInvoke(nameof(StartMeasurement));
        InvokeRepeating(nameof(StartMeasurement), interval, interval);

        if (showDebugLog)
            UnityEngine.Debug.Log($"[CameraSpeedReceiver] 計測間隔を{interval}秒に設定");
    }

    /// <summary>
    /// カメラIDを設定（UI Slider用）
    /// </summary>
    public void SetCameraId(float id)
    {
        cameraId = Mathf.RoundToInt(id);

        if (showDebugLog)
            UnityEngine.Debug.Log($"[CameraSpeedReceiver] カメラIDを{cameraId}に設定");
    }

    /// <summary>
    /// テストモードを設定（UI Toggle用）
    /// </summary>
    public void SetTestMode(bool enabled)
    {
        testMode = enabled;

        if (showDebugLog)
        {
            string modeText = enabled ? "テストモード" : "本番モード";
            UnityEngine.Debug.Log($"[CameraSpeedReceiver] {modeText}に切り替え");
        }
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

        isProcessing = true;

        // モードに応じた空間分解能を選択
        float spatialRes = testMode ? testSpatialResolution : normalSpatialResolution;

        // モードに応じたSTIV検出パラメータを選択
        float useSigmaPre = testMode ? sigmaPre : 1.0f;
        float useSigmaTensor = testMode ? sigmaTensor : 2.0f;
        int useMaxFrames = testMode ? maxFrames : 300;

        try
        {
            // カメラID 2以降はカメラ名が必要
            string videoArg;
            if (cameraId >= 2)
            {
                if (string.IsNullOrEmpty(cameraName))
                {
                    UnityEngine.Debug.LogWarning($"[CameraSpeedReceiver] カメラID {cameraId} はカメラ名の指定が必要です。Camera Nameフィールドに入力してください");
                    isProcessing = false;
                    return;
                }
                videoArg = $"\"{cameraName}\"";
            }
            else
            {
                videoArg = cameraId.ToString();
            }

            pythonProcess = new Process();
            pythonProcess.StartInfo.FileName = pythonExePath;
            pythonProcess.StartInfo.Arguments = $"\"{scriptPath}\" --video {videoArg} --spatial-res {spatialRes} --sigma-pre {useSigmaPre} --sigma-tensor {useSigmaTensor} --max-frames {useMaxFrames}";
            pythonProcess.StartInfo.UseShellExecute = false;
            pythonProcess.StartInfo.RedirectStandardOutput = true;
            pythonProcess.StartInfo.RedirectStandardError = true;
            pythonProcess.StartInfo.CreateNoWindow = true;

            // 非同期でデータ受信（別スレッドで実行）
            pythonProcess.OutputDataReceived += OnOutputDataReceived;
            pythonProcess.ErrorDataReceived += OnErrorDataReceived;

            // プロセス終了イベント
            pythonProcess.EnableRaisingEvents = true;
            pythonProcess.Exited += OnProcessExited;

            pythonProcess.Start();

            // 非同期読み取り開始
            pythonProcess.BeginOutputReadLine();
            pythonProcess.BeginErrorReadLine();

            if (showDebugLog)
            {
                string modeText = testMode ? "テストモード" : "本番モード";
                UnityEngine.Debug.Log($"[CameraSpeedReceiver] Pythonプロセス起動: {scriptPath} ({modeText}, 空間分解能={spatialRes} m/pixel, sigma_pre={useSigmaPre}, sigma_tensor={useSigmaTensor}, max_frames={useMaxFrames})");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"[CameraSpeedReceiver] Pythonプロセス起動失敗: {e.Message}");
            isProcessing = false;
        }
    }

    /// <summary>
    /// Pythonからの標準出力を受信（別スレッド）
    /// </summary>
    void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            // 全ての標準出力をログ表示（カメラ接続状態等を確認できるように）
            if (showDebugLog)
                UnityEngine.Debug.Log($"[Python] {e.Data}");

            // "Estimated Surface Velocity: X.XXXXXX [m/s]" の行を探す
            if (e.Data.Contains("Estimated Surface Velocity:"))
            {
                // "Estimated Surface Velocity: 0.123456 [m/s]" から数値を抽出
                string[] parts = e.Data.Split(':');
                if (parts.Length >= 2)
                {
                    string valuePart = parts[1].Trim().Split(' ')[0]; // "0.123456"
                    if (float.TryParse(valuePart, out float speed))
                    {
                        // 速度が有効範囲かチェック（マイナスまたは閾値以下は計測失敗）
                        if (speed < minimumValidSpeed)
                        {
                            if (showDebugLog)
                                UnityEngine.Debug.LogWarning($"[CameraSpeedReceiver] 計測失敗: 速度が閾値以下 ({speed:F6} m/s < {minimumValidSpeed} m/s) - 速度を更新しません");
                            return;
                        }

                        lastReceivedSpeed = speed;

                        if (showDebugLog)
                            UnityEngine.Debug.Log($"[CameraSpeedReceiver] 速度受信: {speed} m/s");

                        if (autoApplySpeed && simulation != null)
                        {
                            simulation.SetSpeedMultiplier(speed);
                        }
                    }
                }
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
    /// Pythonプロセス終了時
    /// </summary>
    void OnProcessExited(object sender, System.EventArgs e)
    {
        if (showDebugLog)
            UnityEngine.Debug.Log("[CameraSpeedReceiver] Pythonプロセス終了");

        isProcessing = false;

        // イベントハンドラを解除
        if (pythonProcess != null)
        {
            pythonProcess.OutputDataReceived -= OnOutputDataReceived;
            pythonProcess.ErrorDataReceived -= OnErrorDataReceived;
            pythonProcess.Exited -= OnProcessExited;
            pythonProcess.Dispose();
            pythonProcess = null;
        }
    }

    /// <summary>
    /// Pythonプロセスを強制終了
    /// </summary>
    void StopPythonProcess()
    {
        CancelInvoke(nameof(StartMeasurement));

        if (pythonProcess != null)
        {
            // イベントハンドラを解除
            pythonProcess.OutputDataReceived -= OnOutputDataReceived;
            pythonProcess.ErrorDataReceived -= OnErrorDataReceived;
            pythonProcess.Exited -= OnProcessExited;

            if (!pythonProcess.HasExited)
            {
                pythonProcess.Kill();
            }

            pythonProcess.Dispose();
            pythonProcess = null;

            if (showDebugLog)
                UnityEngine.Debug.Log("[CameraSpeedReceiver] Pythonプロセス強制終了");
        }

        isProcessing = false;
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
