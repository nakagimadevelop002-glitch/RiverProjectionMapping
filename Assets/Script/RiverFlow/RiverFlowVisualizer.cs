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
        main.maxParticles = 50000;
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

        renderParticles = new ParticleSystem.Particle[50000];
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
