using UnityEngine;

/// <summary>
/// Trail蓄積レンダリングクラス
/// RenderTextureへの描画、Decay、Blur、正規化・ガンマ補正を統合
/// </summary>
public class TrailRenderer
{
    private RenderTexture accumulationRT;  // 蓄積用RenderTexture
    private RenderTexture tempRT;          // Blur作業用

    private Material decayMaterial;
    private Material blurMaterial;
    private Material normalizeGammaMaterial;

    private VectorField vectorField;

    private const float DECAY = 0.92f;
    private const int BLUR_PASSES = 1;

    private float p99Cached = 1.0f;
    private int frameCount = 0;

    private int width;
    private int height;

    /// <summary>
    /// 初期化
    /// </summary>
    /// <param name="width">RenderTexture幅</param>
    /// <param name="height">RenderTexture高さ</param>
    public void Initialize(int width, int height)
    {
        this.width = width;
        this.height = height;

        // RenderTexture作成（ARGBHalf: URP対応、RGBAチャンネル全て使用）
        accumulationRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf);
        accumulationRT.filterMode = FilterMode.Bilinear;
        tempRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf);
        tempRT.filterMode = FilterMode.Bilinear;

        // シェーダーからMaterial作成
        Shader decayShader = Shader.Find("RiverFlow/Decay");
        Shader blurShader = Shader.Find("RiverFlow/Blur");
        Shader normalizeShader = Shader.Find("RiverFlow/NormalizeGamma");

        // シェーダー読み込み確認
        if (decayShader == null)
            Debug.LogError("[TrailRenderer] DecayShader が見つかりません！");

        if (blurShader == null)
            Debug.LogError("[TrailRenderer] BlurShader が見つかりません！");

        if (normalizeShader == null)
            Debug.LogError("[TrailRenderer] NormalizeGammaShader が見つかりません！");

        decayMaterial = new Material(decayShader);
        blurMaterial = new Material(blurShader);
        normalizeGammaMaterial = new Material(normalizeShader);

        vectorField = new VectorField();

        // RenderTexture初期化（テスト：白塗り）
        // GL.Clearの代わりにGraphics.Blitを使用（URP対応）
        Texture2D whiteTex = new Texture2D(1, 1);
        whiteTex.SetPixel(0, 0, Color.white);
        whiteTex.Apply();
        Graphics.Blit(whiteTex, accumulationRT);
        Object.Destroy(whiteTex);
    }

    /// <summary>
    /// フレーム描画
    /// </summary>
    public void RenderFrame(RiverParticleData[] particles, int count, float time)
    {
        // 1. Decay処理
        ApplyDecay();

        // 2. パーティクル蓄積描画
        AccumulateParticles(particles, count, time);

        // 3. Blur処理
        for (int i = 0; i < BLUR_PASSES; i++)
        {
            ApplyBlur();
        }

        // 4. 正規化・ガンマ補正（6フレームごとにパーセンタイル更新）
        if (frameCount % 6 == 0)
        {
            UpdatePercentile();
        }
        ApplyNormalizeGamma();

        frameCount++;
    }

    /// <summary>
    /// Decay処理
    /// </summary>
    private void ApplyDecay()
    {
        decayMaterial.SetFloat("_Decay", DECAY);
        Graphics.Blit(accumulationRT, tempRT, decayMaterial);
        Graphics.Blit(tempRT, accumulationRT);
    }

    /// <summary>
    /// パーティクル蓄積描画
    /// </summary>
    private void AccumulateParticles(RiverParticleData[] particles, int count, float time)
    {
        RenderTexture.active = accumulationRT;

        GL.PushMatrix();
        GL.LoadPixelMatrix(0, width, height, 0);

        // 加算ブレンド設定
        GL.sRGBWrite = false;
        GL.Begin(GL.QUADS);

        float maxSpeed = vectorField.GetMaxSpeed(time);
        int drawnCount = 0;

        for (int i = 0; i < count; i++)
        {
            var p = particles[i];

            // 画面範囲内チェック
            if (p.position.x < 0 || p.position.x > 1 ||
                p.position.y < 0 || p.position.y > 1)
                continue;

            // 速度計算（強度決定用）
            Vector2 velocity = vectorField.GetVelocity(p.position.x, p.position.y, time);
            float speed = velocity.magnitude;
            float speedNorm = speed / (maxSpeed + 1e-6f);
            float intensity = 0.5f + 1.5f * speedNorm;

            // ピクセル座標に変換
            float px = p.position.x * width;
            float py = p.position.y * height;

            // 小さな矩形を描画（1ピクセル）
            GL.Color(new Color(intensity, intensity, intensity, 1f));
            GL.Vertex3(px, py, 0);
            GL.Vertex3(px + 1, py, 0);
            GL.Vertex3(px + 1, py + 1, 0);
            GL.Vertex3(px, py + 1, 0);

            drawnCount++;
        }

        GL.End();
        GL.PopMatrix();

        RenderTexture.active = null;
    }

    /// <summary>
    /// Blur処理
    /// </summary>
    private void ApplyBlur()
    {
        // 水平Blur
        blurMaterial.SetVector("_Direction", new Vector2(1, 0));
        Graphics.Blit(accumulationRT, tempRT, blurMaterial);

        // 垂直Blur
        blurMaterial.SetVector("_Direction", new Vector2(0, 1));
        Graphics.Blit(tempRT, accumulationRT, blurMaterial);
    }

    /// <summary>
    /// パーセンタイル更新（簡易版）
    /// </summary>
    private void UpdatePercentile()
    {
        // 簡易版：徐々に減衰
        p99Cached = Mathf.Max(p99Cached * 0.99f, 1e-6f);
    }

    /// <summary>
    /// 正規化・ガンマ補正
    /// </summary>
    private void ApplyNormalizeGamma()
    {
        normalizeGammaMaterial.SetFloat("_P99", p99Cached);
        normalizeGammaMaterial.SetFloat("_Gamma", 0.72f);
        Graphics.Blit(accumulationRT, tempRT, normalizeGammaMaterial);
        Graphics.Blit(tempRT, accumulationRT);
    }

    /// <summary>
    /// 出力RenderTexture取得
    /// </summary>
    public RenderTexture GetOutputTexture() => accumulationRT;

    /// <summary>
    /// クリーンアップ
    /// </summary>
    public void Cleanup()
    {
        if (accumulationRT != null)
        {
            accumulationRT.Release();
            Object.Destroy(accumulationRT);
        }
        if (tempRT != null)
        {
            tempRT.Release();
            Object.Destroy(tempRT);
        }
        if (decayMaterial != null) Object.Destroy(decayMaterial);
        if (blurMaterial != null) Object.Destroy(blurMaterial);
        if (normalizeGammaMaterial != null) Object.Destroy(normalizeGammaMaterial);
    }
}
