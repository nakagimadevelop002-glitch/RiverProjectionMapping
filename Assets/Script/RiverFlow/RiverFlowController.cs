using UnityEngine;

/// <summary>
/// RiverFlowシステム統合制御クラス
/// パーティクルシミュレーションとTrailレンダリングを統合
/// </summary>
public class RiverFlowController : MonoBehaviour
{
    [Header("パーティクル設定")]
    [Tooltip("パーティクル数")]
    public int particleCount = 14000;

    [Tooltip("ランダムシード")]
    public int randomSeed = 7;

    [Header("レンダリング設定")]
    [Tooltip("出力解像度（幅）")]
    public int outputWidth = 1920;

    [Tooltip("出力解像度（高さ）")]
    public int outputHeight = 1080;

    [Header("表示設定")]
    [Tooltip("結果を表示するQuad")]
    public GameObject displayQuad;

    private RiverFlowSimulation simulation;
    private TrailRenderer trailRenderer;

    void Start()
    {
        Initialize();
    }

    /// <summary>
    /// システム初期化
    /// </summary>
    void Initialize()
    {
        // シミュレーション初期化
        simulation = gameObject.AddComponent<RiverFlowSimulation>();
        simulation.particleCount = particleCount;
        simulation.randomSeed = randomSeed;
        simulation.showParticles = false;  // Gizmos表示は無効化

        // TrailRenderer初期化
        trailRenderer = new TrailRenderer();
        trailRenderer.Initialize(outputWidth, outputHeight);

        // 表示用Quadに設定
        if (displayQuad != null)
        {
            Renderer renderer = displayQuad.GetComponent<Renderer>();
            if (renderer != null)
            {
                RenderTexture rt = trailRenderer.GetOutputTexture();
                renderer.material.mainTexture = rt;
            }
            else
            {
                Debug.LogError("[RiverFlowController] displayQuadにRendererがありません！");
            }
        }
        else
        {
            
        }
    }

    void Update()
    {
        // パーティクルは自動更新される（RiverFlowSimulation.Update）

        // Trail描画
        trailRenderer.RenderFrame(
            simulation.GetParticles(),
            simulation.GetParticleCount(),
            Time.time
        );
    }

    void OnDestroy()
    {
        // クリーンアップ
        if (trailRenderer != null)
        {
            trailRenderer.Cleanup();
        }
    }
}
