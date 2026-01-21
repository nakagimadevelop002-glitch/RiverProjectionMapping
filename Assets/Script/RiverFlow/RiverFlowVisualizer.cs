using UnityEngine;

/// <summary>
/// パーティクルを直接Gameビューに描画
/// </summary>
public class RiverFlowVisualizer : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("RiverFlowSimulation")]
    public RiverFlowSimulation simulation;

    [Header("描画設定")]
    [Tooltip("パーティクルマテリアル")]
    public Material particleMaterial;

    [Tooltip("パーティクルの色")]
    public Color particleColor = Color.white;

    [Tooltip("表示するパーティクル数")]
    [Range(100, 14000)]
    public int displayCount = 14000;

    [Header("表示範囲")]
    [Tooltip("表示スケール（幅）")]
    public float displayScaleX = 10f;

    [Tooltip("表示スケール（高さ）")]
    public float displayScaleY = 10f;

    private ParticleSystem ps;
    private ParticleSystem.Particle[] renderParticles;

    // ========================================
    // UnityEvent用UI制御関数（RGBスライダー）
    // ========================================

    /// <summary>
    /// パーティクルの赤色成分を設定（Slider用）
    /// </summary>
    public void SetParticleColorR(float r)
    {
        particleColor.r = r;
    }

    /// <summary>
    /// パーティクルの緑色成分を設定（Slider用）
    /// </summary>
    public void SetParticleColorG(float g)
    {
        particleColor.g = g;
    }

    /// <summary>
    /// パーティクルの青色成分を設定（Slider用）
    /// </summary>
    public void SetParticleColorB(float b)
    {
        particleColor.b = b;
    }

    void Start()
    {
        InitializeParticleSystem();
    }

    /// <summary>
    /// ParticleSystem初期化
    /// </summary>
    void InitializeParticleSystem()
    {
        // ParticleSystem追加
        ps = gameObject.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.maxParticles = 200000;  // WaveModeの最大値（maxWaves=20 × particlesPerWave=10000）に対応
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = float.MaxValue;
        main.startSize = simulation.particleSize;
        main.startColor = particleColor;

        var emission = ps.emission;
        emission.enabled = false;

        var shape = ps.shape;
        shape.enabled = false;

        // レンダラー設定
        var renderer = GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        // マテリアル設定
        if (particleMaterial != null)
        {
            renderer.material = particleMaterial;
        }

        renderParticles = new ParticleSystem.Particle[200000];  // 最大パーティクル数に対応
    }

    void LateUpdate()
    {
        if (simulation == null) return;

        // マテリアルの色を更新
        if (particleMaterial != null)
        {
            if (particleMaterial.HasProperty("_BaseColor"))
            {
                particleMaterial.SetColor("_BaseColor", particleColor);
            }

            // Litマテリアルの場合、エミッション色も同期
            if (particleMaterial.HasProperty("_EmissionColor"))
            {
                particleMaterial.SetColor("_EmissionColor", particleColor);
                particleMaterial.EnableKeyword("_EMISSION");
            }
        }

        RiverParticleData[] particles = simulation.GetParticles();

        // WaveModeの場合は全パーティクル表示、NormalModeの場合はdisplayCountで制限
        int count = (simulation.mode == SimulationMode.WaveMode)
            ? simulation.GetParticleCount()
            : Mathf.Min(displayCount, simulation.GetParticleCount());

        // 配列サイズが不足している場合は拡張
        if (renderParticles.Length < count)
        {
            int newSize = Mathf.NextPowerOfTwo(count);  // 2のべき乗に切り上げ
            Debug.LogWarning($"RenderParticles配列を拡張: {renderParticles.Length} → {newSize}");
            renderParticles = new ParticleSystem.Particle[newSize];
        }

        // ParticleSystemのパーティクル更新
        for (int i = 0; i < count; i++)
        {
            var p = particles[i];

            // 正規化座標をワールド座標に変換
            Vector3 worldPos = new Vector3(
                (p.position.x - 0.5f) * displayScaleX,
                (p.position.y - 0.5f) * displayScaleY,
                0
            );

            renderParticles[i].position = worldPos;
            renderParticles[i].startSize = simulation.particleSize;
            renderParticles[i].startColor = particleColor;
            renderParticles[i].remainingLifetime = 1f;
        }

        ps.SetParticles(renderParticles, count);
    }
}
