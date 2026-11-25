using UnityEngine;

/// <summary>
/// Oyuncunun kaybetme/yeniden doğma sebebini belirtir.
/// Ölüm tepkileri (ses, parçacık, kamera ve mesaj) bu sebebe göre ayrışır.
/// </summary>
public enum DeathCause
{
    Fall,
    SpikeTrap,
    Hazard,
    KillZone
}

/// <summary>
/// Sarsıntı, flaş azaltma, mobil titreşim ve SFX ses seviyesi için
/// PlayerPrefs tabanlı merkezi erişilebilirlik ve his ayarları.
/// </summary>
public static class GameFeelSettings
{
    private const string KeyShakeStrength = "Bazde_ShakeStrength";
    private const string KeyReduceFlash = "Bazde_ReduceFlash";
    private const string KeyHapticsEnabled = "Bazde_HapticsEnabled";
    private const string KeySfxVolume = "Bazde_SfxVolume";

    private static bool loaded;
    private static float shakeStrength = 1f;
    private static bool reduceFlash;
    private static bool hapticsEnabled = true;
    private static float sfxVolume = 0.85f;

    public static float ShakeStrength
    {
        get { EnsureLoaded(); return shakeStrength; }
        set
        {
            EnsureLoaded();
            shakeStrength = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(KeyShakeStrength, shakeStrength);
        }
    }

    public static bool ReduceFlash
    {
        get { EnsureLoaded(); return reduceFlash; }
        set
        {
            EnsureLoaded();
            reduceFlash = value;
            PlayerPrefs.SetInt(KeyReduceFlash, reduceFlash ? 1 : 0);
        }
    }

    public static bool HapticsEnabled
    {
        get { EnsureLoaded(); return hapticsEnabled; }
        set
        {
            EnsureLoaded();
            hapticsEnabled = value;
            PlayerPrefs.SetInt(KeyHapticsEnabled, hapticsEnabled ? 1 : 0);
        }
    }

    public static float SfxVolume
    {
        get { EnsureLoaded(); return sfxVolume; }
        set
        {
            EnsureLoaded();
            sfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(KeySfxVolume, sfxVolume);
        }
    }

    /// <summary>
    /// Doğrusal 0..1 kaydırıcıyı algısal karesel kazanca dönüştürür (audio-design kuralı).
    /// </summary>
    public static float EffectiveSfxGain
    {
        get
        {
            float v = SfxVolume;
            return v <= 0.0001f ? 0f : v * v;
        }
    }

    public static void Save()
    {
        EnsureLoaded();
        PlayerPrefs.Save();
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        shakeStrength = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyShakeStrength, 1f));
        reduceFlash = PlayerPrefs.GetInt(KeyReduceFlash, 0) != 0;
        hapticsEnabled = PlayerPrefs.GetInt(KeyHapticsEnabled, 1) != 0;
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfxVolume, 0.85f));
    }
}
