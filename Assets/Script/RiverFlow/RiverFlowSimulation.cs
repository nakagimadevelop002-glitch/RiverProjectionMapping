using UnityEngine;

/// <summary>
/// シミュレーションモード
/// </summary>
public enum SimulationMode
{
    NormalMode,
    WaveMode
}

/// <summary>
/// パーティクルシミュレーションクラス
/// 14,000個のパーティクルを更新・管理
/// </summary>
public class RiverFlowSimulation : MonoBehaviour
{
    [Header("モード設定")]
    [Tooltip("シミュレーションモード")]
    public SimulationMode mode = SimulationMode.NormalMode;

    [Header("パーティクル設定")]
    [Tooltip("パーティクル数")]
    public int particleCount = 14000;

    [Tooltip("ランダムシード")]
    public int randomSeed = 7;

    [Header("速度設定")]
    [Tooltip("速度倍率（NormalMode用）")]
    [Range(0.01f, 5.0f)]
    public float speedMultiplier = 1.0f;

    [Header("WaveMode設定")]
    [Tooltip("壁の生成間隔（秒）")]
    [Min(0.01f)]
    public float waveInterval = 2f;

    [Tooltip("壁の移動速度")]
    [Min(0.01f)]
    public float waveSpeed = 0.5f;

    [Tooltip("プール内の壁の最大数")]
    [Range(1, 20)]
    public int maxWaves = 10;

    [Tooltip("壁1本あたりのパーティクル数")]
    [Range(1000, 10000)]
    public int particlesPerWave = 3000;

    [Tooltip("波の揺らぎ強度（0.0=壁状態、1.0=NormalMode相当の揺れ）")]
    [Range(0f, 1f)]
    public float waveUndulationStrength = 0f;

    [Tooltip("波の幅をランダム化（壁ごとに0.5～最大倍率の幅）")]
    public bool randomizeWaveWidth = false;

    [Tooltip("波の幅ランダム化の最大倍率")]
    [Min(0.5f)]
    public float waveWidthMaxMultiplier = 2.0f;

    [Header("デバッグ表示")]
    [Tooltip("パーティクルを表示するか")]
    public bool showParticles = true;

    [Tooltip("表示するパーティクル数（負荷軽減用）")]
    [Range(100, 14000)]
    public int displayParticleCount = 1000;

    [Tooltip("パーティクルのサイズ")]
    public float particleSize = 0.05f;

    private System.Collections.Generic.List<RiverParticleData> particles;
    private VectorField vectorField;
    private System.Random rng;
    private int currentWaveId = 0;  // 次に発生させる壁ID
    private int lastMaxWaves = -1;  // 前回のmaxWaves値
    private int lastParticlesPerWave = -1;  // 前回のparticlesPerWave値
    private float[] waveWidthMultipliers;  // 各壁の幅倍率（0.5～waveWidthMaxMultiplier）
    private bool lastRandomizeWaveWidth = false;  // 前回のrandomizeWaveWidth値

    void Start()
    {
        Initialize();
    }

    void OnEnable()
    {
        if (mode == SimulationMode.WaveMode && Application.isPlaying)
        {
            StartWaving();
        }
    }

    void OnDisable()
    {
        if (mode == SimulationMode.WaveMode)
        {
            CancelInvoke(nameof(SpawnWave));
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying) return;
        if (!isActiveAndEnabled) return;
        if (mode != SimulationMode.WaveMode) return;
        if (particles == null) return;

        // maxWavesまたはparticlesPerWaveが変更されたら再初期化
        if (maxWaves != lastMaxWaves || particlesPerWave != lastParticlesPerWave)
        {
            lastMaxWaves = maxWaves;
            lastParticlesPerWave = particlesPerWave;

            particles.Clear();
            InitializeWave();
        }

        // ランダム化Boolが変更されたら配列を再初期化
        if (randomizeWaveWidth != lastRandomizeWaveWidth)
        {
            lastRandomizeWaveWidth = randomizeWaveWidth;
            ReinitializeRandomMultipliers();
        }

        // Inspector変更を即反映
        StartWaving();
    }
#endif

    /// <summary>
    /// ランダム倍率配列を再初期化
    /// </summary>
    void ReinitializeRandomMultipliers()
    {
        if (waveWidthMultipliers == null || waveWidthMultipliers.Length != maxWaves)
        {
            waveWidthMultipliers = new float[maxWaves];
        }

        // 幅倍率
        for (int waveId = 0; waveId < maxWaves; waveId++)
        {
            waveWidthMultipliers[waveId] = randomizeWaveWidth
                ? (0.5f + (float)rng.NextDouble() * (waveWidthMaxMultiplier - 0.5f))
                : 1.0f;
        }
    }

    /// <summary>
    /// システム初期化（モード分岐）
    /// </summary>
    void Initialize()
    {
        rng = new System.Random(randomSeed);
        vectorField = new VectorField();
        particles = new System.Collections.Generic.List<RiverParticleData>(particleCount);

        switch (mode)
        {
            case SimulationMode.NormalMode:
                InitializeNormal();
                break;
            case SimulationMode.WaveMode:
                InitializeWave();
                break;
        }
    }

    /// <summary>
    /// NormalMode初期化
    /// </summary>
    void InitializeNormal()
    {
        // 初期配置（左端少し外から流入）
        for (int i = 0; i < particleCount; i++)
        {
            particles.Add(new RiverParticleData(
                (float)rng.NextDouble() * 0.05f - 0.02f,  // x: -0.02 ~ 0.03
                (float)rng.NextDouble()                    // y: 0 ~ 1
            ));
        }
    }

    /// <summary>
    /// WaveMode初期化（オブジェクトプール方式）
    /// </summary>
    void InitializeWave()
    {
        currentWaveId = 0;
        lastMaxWaves = maxWaves;
        lastParticlesPerWave = particlesPerWave;

        // 各壁の幅倍率を初期化
        waveWidthMultipliers = new float[maxWaves];
        for (int waveId = 0; waveId < maxWaves; waveId++)
        {
            waveWidthMultipliers[waveId] = randomizeWaveWidth
                ? (0.5f + (float)rng.NextDouble() * (waveWidthMaxMultiplier - 0.5f))
                : 1.0f;
        }

        // maxWaves本分のパーティクルを作成（各壁particlesPerWave個）
        for (int waveId = 0; waveId < maxWaves; waveId++)
        {
            for (int i = 0; i < particlesPerWave; i++)
            {
                particles.Add(new RiverParticleData(-10f, 0f, waveId));
            }
        }

    }

    /// <summary>
    /// 壁の定期生成開始
    /// </summary>
    void StartWaving()
    {
        CancelInvoke(nameof(SpawnWave));
        InvokeRepeating(nameof(SpawnWave), 0f, waveInterval);
    }

    /// <summary>
    /// パーティクル壁を生成（オブジェクトプール使い回し）
    /// </summary>
    void SpawnWave()
    {
        // 使用可能なwaveIdを探す（全パーティクルが非表示位置にあるか確認）
        int availableWaveId = -1;
        for (int checkId = 0; checkId < maxWaves; checkId++)
        {
            int waveId = (currentWaveId + checkId) % maxWaves;

            // このwaveIdのパーティクルが全て非表示位置にあるか確認
            bool isAvailable = true;
            for (int i = 0; i < particles.Count; i++)
            {
                if (particles[i].waveId == waveId && particles[i].position.x >= -5f)
                {
                    isAvailable = false;
                    break;
                }
            }

            if (isAvailable)
            {
                availableWaveId = waveId;
                break;
            }
        }

        // 使用可能なwaveIdが見つかった場合のみ生成
        if (availableWaveId >= 0)
        {
            // 壁の幅倍率取得
            float widthMultiplier = (randomizeWaveWidth && waveWidthMultipliers != null) ? waveWidthMultipliers[availableWaveId] : 1.0f;
            float waveWidth = 0.05f * widthMultiplier;  // 基準幅0.05を倍率で調整

            for (int i = 0; i < particles.Count; i++)
            {
                var p = particles[i];
                if (p.waveId == availableWaveId && p.position.x < -5f)
                {
                    // 横幅を考慮して完全に画面外に配置（-0.1 - waveWidth ～ -0.1）
                    p.position.x = -0.1f - waveWidth + (float)rng.NextDouble() * waveWidth;
                    p.position.y = (float)rng.NextDouble();
                    particles[i] = p;
                }
            }
            // 次のwaveIdへ進める
            currentWaveId = (availableWaveId + 1) % maxWaves;
        }
    }

    void Update()
    {
        UpdateParticles();
    }

    /// <summary>
    /// 全パーティクル更新（モード分岐）
    /// </summary>
    void UpdateParticles()
    {
        switch (mode)
        {
            case SimulationMode.NormalMode:
                UpdateParticlesNormal();
                break;
            case SimulationMode.WaveMode:
                UpdateParticlesWave();
                break;
        }
    }

    /// <summary>
    /// NormalMode: 全パーティクル更新
    /// Pythonコードのadvect処理を完全移植
    /// </summary>
    void UpdateParticlesNormal()
    {
        float dt = 0.7f / 24f;  // Pythonと同じ時間ステップ
        float time = Time.time;

        for (int i = 0; i < particles.Count; i++)
        {
            var p = particles[i];

            // ベクトル場から速度取得
            Vector2 velocity = vectorField.GetVelocity(p.position.x, p.position.y, time);

            // Y座標による速度倍率（上側ほど速い: y=0で1.0倍、y=1で2.0倍）
            float ySpeedMultiplier = 1.0f + p.position.y;

            // 位置更新（速度倍率 × Y座標倍率を適用）
            p.position += velocity * dt * speedMultiplier * ySpeedMultiplier;

            // 右端から出た → 左端で再投入
            if (p.position.x > 1.02f)
            {
                p.position.x = (float)rng.NextDouble() * 0.05f - 0.02f;
                p.position.y = (float)rng.NextDouble();
            }

            // 上下は反射（バウンド）
            if (p.position.y < -0.02f)
            {
                p.position.y = -p.position.y;
            }
            if (p.position.y > 1.02f)
            {
                p.position.y = 2.0f - p.position.y;
            }

            particles[i] = p;
        }
    }

    /// <summary>
    /// WaveMode: パーティクル更新（揺れ強度を0.0～1.0で制御）
    /// </summary>
    void UpdateParticlesWave()
    {
        float dt = 0.7f / 24f;  // NormalModeと同じ時間ステップ
        float time = Time.time;

        for (int i = 0; i < particles.Count; i++)
        {
            var p = particles[i];

            // 非表示位置にあるパーティクルはスキップ
            if (p.position.x < -5f) continue;

            // 黄金比による時間オフセット（壁ごとに異なる位相）
            float timeOffset = p.waveId * 0.618f;

            // 黄金比の補数による空間オフセット（壁ごとに異なる開始位置）
            float spaceOffset = p.waveId * 0.382f;

            // ベクトル場から速度取得（X座標にオフセットで各壁が異なる波形開始位置から蛇行）
            Vector2 velocity = vectorField.GetVelocity(p.position.x + spaceOffset, p.position.y, time + timeOffset);

            // X方向: 一律waveSpeed + VectorFieldの影響を揺れ強度で制御
            p.position.x += waveSpeed * Time.deltaTime + velocity.x * dt * waveUndulationStrength;

            // Y方向: VectorFieldの影響を揺れ強度で制御
            p.position.y += velocity.y * dt * waveUndulationStrength;

            // 右端を超えたら非表示位置に戻す
            if (p.position.x > 1.02f)
            {
                p.position.x = -10f;
                p.position.y = 0f;
            }

            // 上下は反射（バウンド）- 揺れ強度が0より大きい場合のみ
            if (waveUndulationStrength > 0f)
            {
                if (p.position.y < -0.02f)
                {
                    p.position.y = -p.position.y;
                }
                if (p.position.y > 1.02f)
                {
                    p.position.y = 2.0f - p.position.y;
                }
            }

            particles[i] = p;
        }
    }

    /// <summary>
    /// Gizmosでパーティクル可視化
    /// </summary>
    void OnDrawGizmos()
    {
        if (!showParticles || particles == null || particles.Count == 0) return;

        Gizmos.color = Color.cyan;

        // 表示数を制限（負荷軽減）
        int displayCount = Mathf.Min(displayParticleCount, particles.Count);

        for (int i = 0; i < displayCount; i++)
        {
            var p = particles[i];

            // 正規化座標をワールド座標に変換（中心を原点に）
            Vector3 worldPos = new Vector3(
                (p.position.x - 0.5f) * 10f,
                (p.position.y - 0.5f) * 10f,
                0
            );

            Gizmos.DrawSphere(worldPos, particleSize);
        }
    }

    /// <summary>
    /// パーティクルデータ取得（将来のレンダラーで使用）
    /// </summary>
    public RiverParticleData[] GetParticles() => particles.ToArray();

    /// <summary>
    /// パーティクル数取得
    /// </summary>
    public int GetParticleCount() => particles.Count;

    // ========================================
    // UnityEvent用UI制御関数
    // ========================================

    /// <summary>
    /// 速度を設定（Slider用 - 両モード対応）
    /// </summary>
    public void SetSpeedMultiplier(float speed)
    {
        if (mode == SimulationMode.NormalMode)
        {
            speedMultiplier = speed;
        }
        else if (mode == SimulationMode.WaveMode)
        {
            waveSpeed = speed;
        }
    }

    /// <summary>
    /// 壁の生成間隔を設定（Slider用 - WaveMode）
    /// </summary>
    public void SetWaveInterval(float interval)
    {
        waveInterval = interval;

        if (Application.isPlaying && mode == SimulationMode.WaveMode)
        {
            StartWaving();  // InvokeRepeatingを再設定
        }
    }

    /// <summary>
    /// 波の揺れ強度を設定（Slider用 - WaveMode）
    /// </summary>
    public void SetWaveUndulationStrength(float strength)
    {
        waveUndulationStrength = strength;
    }

    /// <summary>
    /// 波の幅最大倍率を設定（Slider用 - WaveMode）
    /// </summary>
    public void SetWaveWidthMaxMultiplier(float multiplier)
    {
        waveWidthMaxMultiplier = multiplier;
    }

    /// <summary>
    /// WaveModeのON/OFF切り替え（UnityEvent用）
    /// </summary>
    /// <param name="enabled">true: WaveMode、false: NormalMode</param>
    public void SetWaveMode(bool enabled)
    {
        if (enabled)
        {
            // WaveModeに切り替え
            if (mode == SimulationMode.WaveMode) return;
            mode = SimulationMode.WaveMode;

            if (Application.isPlaying && isActiveAndEnabled)
            {
                particles.Clear();
                InitializeWave();
                StartWaving();
            }
        }
        else
        {
            // NormalModeに切り替え
            if (mode == SimulationMode.NormalMode) return;
            mode = SimulationMode.NormalMode;

            if (Application.isPlaying)
            {
                CancelInvoke(nameof(SpawnWave));
                particles.Clear();
                InitializeNormal();
            }
        }
    }

    /// <summary>
    /// 波の幅ランダム化を設定（UnityEvent用）
    /// </summary>
    public void SetRandomizeWaveWidth(bool enabled)
    {
        if (randomizeWaveWidth == enabled) return;

        randomizeWaveWidth = enabled;
        lastRandomizeWaveWidth = enabled;

        if (Application.isPlaying && isActiveAndEnabled && mode == SimulationMode.WaveMode)
        {
            ReinitializeRandomMultipliers();
        }
    }
}
