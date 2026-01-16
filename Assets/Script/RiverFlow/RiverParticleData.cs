using UnityEngine;

/// <summary>
/// パーティクルの位置データを保持する構造体
/// Pythonコードの(x, y)配列に対応
/// </summary>
public struct RiverParticleData
{
    /// <summary>
    /// パーティクルの正規化座標（0-1）
    /// x: 0=左端、1=右端
    /// y: 0=下端、1=上端
    /// </summary>
    public Vector2 position;

    /// <summary>
    /// 所属する壁ID（WaveMode用）
    /// -1: 未使用、0以上: 壁ID
    /// </summary>
    public int waveId;

    /// <summary>
    /// コンストラクタ（NormalMode互換）
    /// </summary>
    /// <param name="x">X座標（0-1）</param>
    /// <param name="y">Y座標（0-1）</param>
    public RiverParticleData(float x, float y)
    {
        position = new Vector2(x, y);
        waveId = -1;
    }

    /// <summary>
    /// コンストラクタ（WaveMode用）
    /// </summary>
    /// <param name="x">X座標（0-1）</param>
    /// <param name="y">Y座標（0-1）</param>
    /// <param name="waveId">壁ID</param>
    public RiverParticleData(float x, float y, int waveId)
    {
        position = new Vector2(x, y);
        this.waveId = waveId;
    }
}
