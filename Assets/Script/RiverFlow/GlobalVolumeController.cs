using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Global VolumeのBloom Intensityをコントロール
/// </summary>
public class GlobalVolumeController : MonoBehaviour
{
    [Header("Volume設定")]
    [Tooltip("制御するGlobal Volume")]
    public Volume globalVolume;

    /// <summary>
    /// BloomのIntensityを設定（UI Slider用）
    /// </summary>
    public void SetIntensity(float intensity)
    {
        UnityEngine.Debug.Log($"[GlobalVolumeController] SetIntensity called: {intensity}");

        if (globalVolume == null)
        {
            UnityEngine.Debug.LogError("[GlobalVolumeController] globalVolume is null!");
            return;
        }

        if (globalVolume.profile == null)
        {
            UnityEngine.Debug.LogError("[GlobalVolumeController] globalVolume.profile is null!");
            return;
        }

        if (globalVolume.profile.TryGet<Bloom>(out var bloom))
        {
            bloom.intensity.overrideState = true;
            bloom.intensity.value = intensity;
            UnityEngine.Debug.Log($"[GlobalVolumeController] Bloom Intensity set to: {bloom.intensity.value}");
        }
        else
        {
            UnityEngine.Debug.LogWarning("[GlobalVolumeController] Bloom overrideが見つかりません。Volume ProfileにBloomを追加してください。");
        }
    }
}
