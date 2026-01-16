using UnityEngine;

/// <summary>
/// ベクトル場計算クラス
/// Pythonのvector_field関数を完全移植
/// 蛇行する中心流 + 微小渦を生成
/// </summary>
public class VectorField
{
    private const float TAU = Mathf.PI * 2f;

    /// <summary>
    /// 周期時間（秒）
    /// </summary>
    public float duration = 6f;

    /// <summary>
    /// 基本速度
    /// </summary>
    public float baseSpeed = 0.50f;

    /// <summary>
    /// 指定位置・時刻での速度ベクトルを取得
    /// Pythonコードのvector_field関数と完全一致
    /// </summary>
    /// <param name="x">正規化X座標（0-1）</param>
    /// <param name="y">正規化Y座標（0-1）</param>
    /// <param name="time">経過時間（秒）</param>
    /// <returns>速度ベクトル</returns>
    public Vector2 GetVelocity(float x, float y, float time)
    {
        // 位相計算（周期的変化）
        float phase = TAU * (time / duration);

        // 蛇行する中心流の計算
        float y_center = 0.5f + 0.12f * Mathf.Sin(TAU * (0.65f * x) + 1.0f * phase);
        float width = 0.18f;
        float center_profile = Mathf.Exp(-0.5f * Mathf.Pow((y - y_center) / width, 2f));

        // X方向速度（右→左への基本流）
        float vx = baseSpeed * (0.65f + 1.7f * center_profile);

        // 微小渦の追加（X方向）
        vx += 0.10f * Mathf.Sin(TAU * (3.0f * y) + 1.0f * phase)
            + 0.05f * Mathf.Sin(TAU * (1.2f * x + 0.4f * y) + 2.0f * phase);

        // Y方向速度（中心への収束 + 微小渦）
        float vy = 0.25f * (y_center - y) * center_profile
                 + 0.10f * Mathf.Sin(TAU * (1.4f * x) + 1.5f * phase)
                 + 0.05f * Mathf.Sin(TAU * (2.2f * y + 0.3f * x) + 0.8f * phase);

        return new Vector2(vx, vy);
    }

    /// <summary>
    /// デバッグ用：指定時刻での最大速度を計算
    /// </summary>
    public float GetMaxSpeed(float time)
    {
        float maxSpeed = 0f;

        // グリッドサンプリングで最大速度を推定
        for (float x = 0; x <= 1; x += 0.1f)
        {
            for (float y = 0; y <= 1; y += 0.1f)
            {
                Vector2 vel = GetVelocity(x, y, time);
                float speed = vel.magnitude;
                if (speed > maxSpeed)
                    maxSpeed = speed;
            }
        }

        return maxSpeed;
    }
}
