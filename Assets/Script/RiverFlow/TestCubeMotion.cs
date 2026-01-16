using UnityEngine;

/// <summary>
/// Motion Blur 検証用：Cubeを横方向に往復移動させる
/// </summary>
public class TestCubeMotion : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 5f;        // 移動速度（大きいほどブラーが出やすい）
    public float range = 5f;        // 往復距離

    private Vector3 startPos;
    private int direction = 1;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        // 横方向に移動
        transform.Translate(Vector3.right * direction * speed * Time.deltaTime, Space.World);

        // 範囲を超えたら反転
        float offset = transform.position.x - startPos.x;
        if (Mathf.Abs(offset) > range)
        {
            direction *= -1;
        }
    }
}
