using UnityEngine;

/// <summary>
/// VectorFieldのテストクラス
/// Gizmosでベクトル場を可視化
/// </summary>
public class VectorFieldTest : MonoBehaviour
{
    [Header("可視化設定")]
    [Tooltip("ベクトル場を表示するか")]
    public bool showVectorField = true;

    [Tooltip("グリッド解像度")]
    [Range(5, 30)]
    public int gridResolution = 15;

    [Tooltip("矢印の長さスケール")]
    [Range(0.1f, 5f)]
    public float arrowScale = 2f;

    private VectorField vectorField;

    void Start()
    {
        vectorField = new VectorField();
    }

    void OnDrawGizmos()
    {
        if (!showVectorField) return;
        if (vectorField == null) vectorField = new VectorField();

        float time = Application.isPlaying ? Time.time : 0f;
        float step = 1f / gridResolution;

        for (float x = 0; x <= 1; x += step)
        {
            for (float y = 0; y <= 1; y += step)
            {
                Vector2 vel = vectorField.GetVelocity(x, y, time);

                // ワールド座標に変換（中心を原点に）
                Vector3 pos = new Vector3((x - 0.5f) * 10f, (y - 0.5f) * 10f, 0);
                Vector3 dir = new Vector3(vel.x, vel.y, 0) * arrowScale;

                // 速度の大きさで色を変更
                float speed = vel.magnitude;
                Gizmos.color = Color.Lerp(Color.blue, Color.red, speed / 1.5f);

                // 矢印を描画
                Gizmos.DrawLine(pos, pos + dir);

                // 矢印の先端に小さな球
                Gizmos.DrawSphere(pos + dir, 0.05f);
            }
        }
    }
}
