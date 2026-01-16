using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Webカメラ
public class CameraConnecter : MonoBehaviour
{
    private static int INPUT_SIZE = 256;
    private static int FPS = 30;

    // UI
    RawImage rawImage;
    WebCamTexture webCamTexture;

    // カメラの選択
    int selectCamera = 0;

    // スタート時に呼ばれる
    void Start()
    {
        // Webカメラの開始
        this.rawImage = GetComponent<RawImage>();
        this.webCamTexture = new WebCamTexture(INPUT_SIZE, INPUT_SIZE, FPS);
        this.rawImage.texture = this.webCamTexture;
        this.webCamTexture.Play();
    }

    // カメラの変更
    public void ChangeCamera()
    {
        // カメラの取得
        WebCamDevice[] webCamDevice = WebCamTexture.devices;

        // カメラが1個の時は無処理
        if (webCamDevice.Length <= 1) return;

        // カメラの切り替え
        selectCamera++;
        if (selectCamera >= webCamDevice.Length) selectCamera = 0;
        this.webCamTexture.Stop();
        this.webCamTexture = new WebCamTexture(webCamDevice[selectCamera].name,
            INPUT_SIZE, INPUT_SIZE, FPS);
        this.rawImage.texture = this.webCamTexture;
        this.webCamTexture.Play();
    }
}
