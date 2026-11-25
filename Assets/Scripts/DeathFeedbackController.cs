using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Kaybetme, tuzak çarpması, savrulma ve yeniden doğma anlarının tüm duyusal geri bildirimini
/// (hit-stop, kamera sarsıntısı, biyoma duyarlı parçacıklar, UGUI vignette/kararma, ses ve mobil titreşim)
/// gerçek zamanlı (unscaledDeltaTime) olarak yönetir.
/// </summary>
public class DeathFeedbackController : MonoBehaviour
{
    public static DeathFeedbackController Instance { get; private set; }

    [Header("Zamanlama (Gerçek Zaman - Saniye)")]
    public float fallFadeDelay = 0.22f;
    public float trapTumbleDuration = 0.28f;
    public float trapHitStopDuration = 0.09f;
    public float trapHitStopScale = 0.06f;
    public float knockbackHitStopDuration = 0.04f;
    public float knockbackHitStopScale = 0.12f;
    public float fadeOutDuration = 0.24f;
    public float blackHoldDuration = 0.06f;
    public float fadeInDuration = 0.22f;

    [Header("Doğuş Koruması ve Pop")]
    public float spawnGraceDuration = 0.60f;
    public float spawnPopDuration = 0.20f;
    public float spawnControlLock = 0.14f;

    [Header("Özel Ses Klipleri (Opsiyonel - Boşsa Prosedürel Çalar)")]
    public AudioClip fallWhooshClip;
    public AudioClip waterSplashClip;
    public AudioClip mistPoofClip;
    public AudioClip trapImpactClip;
    public AudioClip knockbackClip;
    public AudioClip respawnChimeClip;

    private int hitStopId;
    private bool ownsTimeScale;
    private float lastHapticTime = -10f;

    private GameObject overlayCanvasGO;
    private Image vignetteImage;
    private Image fadeImage;
    private Coroutine vignetteRoutine;

    private static Sprite cachedVignetteSprite;
    private static Texture2D cachedParticleTexture;
    private static Material cachedParticleMaterial;

    private enum BiomeFeedbackKind
    {
        Lake,
        River,
        SunsetMist,
        CloudSea
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        EnsureOverlayUI();
    }

    void OnDisable()
    {
        RestoreTimeScaleIfOwned();
    }

    void OnDestroy()
    {
        RestoreTimeScaleIfOwned();
        if (Instance == this)
            Instance = null;
    }

    public void NotifyFallingTowardAbyss()
    {
        ProceduralGameAudio.Play(fallWhooshClip, ProceduralGameAudio.FallWhooshClip, 0.72f, 0.05f);
    }

    public void PlayNonLethalHitFeedback(Vector3 hitPos)
    {
        TriggerHitStop(knockbackHitStopDuration, knockbackHitStopScale);

        var cam = ResolveMainCameraController();
        if (cam != null)
            cam.AddTrauma(0.42f);

        PulseVignette(new Color(0.85f, 0.14f, 0.14f, 1f), 0.32f, 0.24f);
        SpawnKnockbackBurst(hitPos);
        ProceduralGameAudio.Play(knockbackClip, ProceduralGameAudio.KnockbackHitClip, 0.85f, 0.08f);
        TriggerHaptic();
    }

    public IEnumerator PlayDeathAndRespawnSequence(
        PlayerController target,
        DeathCause cause,
        Vector3 deathPos,
        Vector3 impactOrigin,
        float surfaceY,
        AudioClip extraDeathSound,
        GameObject extraDeathVFX,
        Action executeTeleport)
    {
        EnsureOverlayUI();
        var cam = ResolveMainCameraController();
        Vector3 lookPoint = cause == DeathCause.Fall
            ? new Vector3(deathPos.x, surfaceY + 0.1f, deathPos.z)
            : deathPos + Vector3.up * 0.5f;

        if (cam != null)
        {
            cam.LockDeathWatch(cause == DeathCause.Fall ? null : target.transform, lookPoint);
            cam.AddTrauma(cause == DeathCause.Fall ? 0.75f : 0.90f);
        }

        if (cause != DeathCause.Fall)
            TriggerHitStop(trapHitStopDuration, trapHitStopScale);

        SpawnDeathBurst(deathPos, cause, surfaceY);
        if (extraDeathVFX != null)
            Instantiate(extraDeathVFX, deathPos, Quaternion.identity);

        PlayDeathAudio(cause, extraDeathSound, deathPos);
        TriggerHaptic();

        Color vignetteCol = cause == DeathCause.Fall
            ? GetFallVignetteColor(ResolveBiomeKind())
            : new Color(0.82f, 0.10f, 0.12f, 1f);
        float peakAlpha = cause == DeathCause.Fall ? 0.55f : 0.68f;
        PulseVignette(vignetteCol, peakAlpha, cause == DeathCause.Fall ? 0.42f : 0.48f);

        if (cause == DeathCause.Fall)
        {
            target.SetVisualVisible(false);
            yield return WaitRealtime(fallFadeDelay);
        }
        else
        {
            Vector3 awayDir = deathPos - impactOrigin;
            awayDir.y = 0f;
            if (awayDir.sqrMagnitude < 0.001f)
                awayDir = -target.transform.forward;

            target.PlayDeathTumble(awayDir, trapTumbleDuration);
            yield return WaitRealtime(trapTumbleDuration);
            SpawnImpactSmokePuff(target.transform.position + Vector3.up * 0.45f);
            target.SetVisualVisible(false);
        }

        yield return FadeScreenAlpha(0f, 1f, fadeOutDuration);
        if (blackHoldDuration > 0f)
            yield return WaitRealtime(blackHoldDuration);

        // Orta noktada ışınlama ve kamera sıfırlama
        RestoreTimeScaleIfOwned();
        executeTeleport?.Invoke();

        Vector3 spawnPos = target != null ? target.transform.position : deathPos;
        SpawnRespawnBurst(spawnPos);
        ProceduralGameAudio.Play(respawnChimeClip, ProceduralGameAudio.RespawnChimeClip, 0.78f, 0.03f);

        if (target != null)
        {
            target.PlayRespawnPop(spawnPopDuration, spawnControlLock);
            target.StartSpawnGrace(spawnGraceDuration);
        }

        yield return FadeScreenAlpha(1f, 0f, fadeInDuration);
    }

    private void TriggerHitStop(float duration, float slowScale)
    {
        if (duration <= 0.001f) return;
        StartCoroutine(HitStopRoutine(duration, Mathf.Clamp(slowScale, 0.01f, 1f)));
    }

    private IEnumerator HitStopRoutine(float duration, float slowScale)
    {
        hitStopId++;
        int myId = hitStopId;
        ownsTimeScale = true;
        Time.timeScale = slowScale;

        yield return new WaitForSecondsRealtime(duration);

        if (myId == hitStopId)
        {
            Time.timeScale = 1f;
            ownsTimeScale = false;
        }
    }

    private void RestoreTimeScaleIfOwned()
    {
        if (ownsTimeScale)
        {
            hitStopId++;
            Time.timeScale = 1f;
            ownsTimeScale = false;
        }
    }

    private void PlayDeathAudio(DeathCause cause, AudioClip extraDeathSound, Vector3 pos)
    {
        if (extraDeathSound != null)
        {
            AudioSource.PlayClipAtPoint(extraDeathSound, pos, GameFeelSettings.EffectiveSfxGain);
            return;
        }

        if (cause == DeathCause.Fall)
        {
            BiomeFeedbackKind biome = ResolveBiomeKind();
            AudioClip clip = (biome == BiomeFeedbackKind.Lake || biome == BiomeFeedbackKind.River)
                ? waterSplashClip
                : mistPoofClip;
            AudioClip fallback = (biome == BiomeFeedbackKind.Lake || biome == BiomeFeedbackKind.River)
                ? ProceduralGameAudio.WaterSplashClip
                : ProceduralGameAudio.MistCloudPoofClip;
            ProceduralGameAudio.Play(clip, fallback, 0.95f, 0.06f);
        }
        else
        {
            ProceduralGameAudio.Play(trapImpactClip, ProceduralGameAudio.TrapImpactClip, 0.95f, 0.06f);
        }
    }

    private void TriggerHaptic()
    {
        if (!GameFeelSettings.HapticsEnabled) return;
        if (Time.unscaledTime - lastHapticTime < 0.25f) return;
        lastHapticTime = Time.unscaledTime;

#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }

    private void PulseVignette(Color color, float peakAlpha, float duration)
    {
        if (vignetteImage == null) return;
        if (GameFeelSettings.ReduceFlash)
            peakAlpha = Mathf.Min(peakAlpha, 0.35f);

        if (vignetteRoutine != null)
            StopCoroutine(vignetteRoutine);
        vignetteRoutine = StartCoroutine(VignetteRoutine(color, peakAlpha, duration));
    }

    private IEnumerator VignetteRoutine(Color baseColor, float peakAlpha, float duration)
    {
        float rise = Mathf.Min(0.06f, duration * 0.25f);
        float fall = Mathf.Max(0.05f, duration - rise);

        float elapsed = 0f;
        while (elapsed < rise)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, rise));
            SetImageColorAlpha(vignetteImage, baseColor, Mathf.Lerp(0f, peakAlpha, t));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < fall)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, fall));
            float ease = 1f - Mathf.Pow(1f - t, 2f);
            SetImageColorAlpha(vignetteImage, baseColor, Mathf.Lerp(peakAlpha, 0f, ease));
            yield return null;
        }

        SetImageColorAlpha(vignetteImage, baseColor, 0f);
        vignetteRoutine = null;
    }

    private IEnumerator FadeScreenAlpha(float from, float to, float duration)
    {
        if (fadeImage == null) yield break;

        Color baseCol = new Color(0.05f, 0.07f, 0.11f, 1f);
        if (duration <= 0.001f)
        {
            SetImageColorAlpha(fadeImage, baseCol, to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = t * t * (3f - 2f * t);
            SetImageColorAlpha(fadeImage, baseCol, Mathf.Lerp(from, to, smooth));
            yield return null;
        }

        SetImageColorAlpha(fadeImage, baseCol, to);
    }

    private static void SetImageColorAlpha(Image img, Color rgb, float alpha)
    {
        rgb.a = Mathf.Clamp01(alpha);
        img.color = rgb;
    }

    private static IEnumerator WaitRealtime(float seconds)
    {
        if (seconds > 0.001f)
            yield return new WaitForSecondsRealtime(seconds);
    }

    private void EnsureOverlayUI()
    {
        if (overlayCanvasGO != null) return;

        overlayCanvasGO = new GameObject("DeathFeedbackCanvas", typeof(Canvas), typeof(CanvasScaler));
        overlayCanvasGO.transform.SetParent(transform, false);

        var canvas = overlayCanvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;

        var scaler = overlayCanvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var vignetteGO = new GameObject("ImpactVignette", typeof(RectTransform), typeof(Image));
        vignetteGO.transform.SetParent(overlayCanvasGO.transform, false);
        StretchFull(vignetteGO.GetComponent<RectTransform>());
        vignetteImage = vignetteGO.GetComponent<Image>();
        vignetteImage.sprite = GetOrCreateVignetteSprite();
        vignetteImage.type = Image.Type.Simple;
        vignetteImage.color = new Color(1f, 0.2f, 0.2f, 0f);
        vignetteImage.raycastTarget = false;

        var fadeGO = new GameObject("TransitionFade", typeof(RectTransform), typeof(Image));
        fadeGO.transform.SetParent(overlayCanvasGO.transform, false);
        StretchFull(fadeGO.GetComponent<RectTransform>());
        fadeImage = fadeGO.GetComponent<Image>();
        fadeImage.color = new Color(0.05f, 0.07f, 0.11f, 0f);
        fadeImage.raycastTarget = false;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Sprite GetOrCreateVignetteSprite()
    {
        if (cachedVignetteSprite != null) return cachedVignetteSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[size * size];
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - half) / half;
                float ny = (y - half) / half;
                float dist = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.28f, dist));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        cachedVignetteSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return cachedVignetteSprite;
    }

    private void SpawnDeathBurst(Vector3 deathPos, DeathCause cause, float surfaceY)
    {
        if (cause == DeathCause.Fall)
        {
            Vector3 splashPos = new Vector3(deathPos.x, surfaceY + 0.18f, deathPos.z);
            BiomeFeedbackKind biome = ResolveBiomeKind();
            switch (biome)
            {
                case BiomeFeedbackKind.Lake:
                    SpawnBurst("LakeSplashBurst", splashPos,
                        new Color(0.24f, 0.85f, 0.92f, 0.95f), new Color(0.92f, 0.98f, 1f, 0.9f),
                        count: 26, minSpeed: 3.5f, maxSpeed: 8.2f, minSize: 0.25f, maxSize: 0.65f,
                        gravity: 1.4f, lifetime: 0.65f, coneAngle: 48f, upBias: 1f);
                    break;
                case BiomeFeedbackKind.River:
                    SpawnBurst("RiverSplashBurst", splashPos,
                        new Color(0.14f, 0.68f, 0.82f, 0.95f), new Color(0.92f, 0.98f, 1f, 0.9f),
                        count: 26, minSpeed: 3.5f, maxSpeed: 8.2f, minSize: 0.25f, maxSize: 0.65f,
                        gravity: 1.4f, lifetime: 0.65f, coneAngle: 48f, upBias: 1f);
                    break;
                case BiomeFeedbackKind.SunsetMist:
                    SpawnBurst("MistPoofBurst", splashPos,
                        new Color(1.0f, 0.72f, 0.36f, 0.88f), new Color(0.96f, 0.44f, 0.22f, 0.85f),
                        count: 24, minSpeed: 2.0f, maxSpeed: 5.5f, minSize: 0.55f, maxSize: 1.25f,
                        gravity: -0.15f, lifetime: 0.75f, coneAngle: 68f, upBias: 0.8f);
                    break;
                default:
                    SpawnBurst("CloudPoofBurst", splashPos,
                        new Color(0.98f, 0.99f, 1f, 0.92f), new Color(0.75f, 0.88f, 1f, 0.85f),
                        count: 26, minSpeed: 2.2f, maxSpeed: 5.8f, minSize: 0.60f, maxSize: 1.35f,
                        gravity: -0.10f, lifetime: 0.78f, coneAngle: 72f, upBias: 0.8f);
                    break;
            }
        }
        else
        {
            Vector3 impactPos = deathPos + Vector3.up * 0.6f;
            SpawnBurst("TrapImpactSparks", impactPos,
                new Color(1.0f, 0.28f, 0.18f, 1f), new Color(1.0f, 0.78f, 0.22f, 1f),
                count: 24, minSpeed: 3.2f, maxSpeed: 7.4f, minSize: 0.16f, maxSize: 0.38f,
                gravity: 1.1f, lifetime: 0.48f, coneAngle: 85f, upBias: 0.5f);
        }
    }

    private void SpawnImpactSmokePuff(Vector3 pos)
    {
        SpawnBurst("DeathPuff", pos,
            new Color(0.85f, 0.86f, 0.9f, 0.72f), new Color(0.45f, 0.48f, 0.55f, 0.65f),
            count: 14, minSpeed: 1.2f, maxSpeed: 3.2f, minSize: 0.35f, maxSize: 0.75f,
            gravity: -0.25f, lifetime: 0.45f, coneAngle: 88f, upBias: 0.4f);
    }

    private void SpawnKnockbackBurst(Vector3 pos)
    {
        SpawnBurst("KnockbackSparks", pos,
            new Color(1.0f, 0.62f, 0.15f, 1f), new Color(1.0f, 0.92f, 0.45f, 1f),
            count: 12, minSpeed: 2.5f, maxSpeed: 5.8f, minSize: 0.14f, maxSize: 0.30f,
            gravity: 0.8f, lifetime: 0.36f, coneAngle: 85f, upBias: 0.4f);
    }

    private void SpawnRespawnBurst(Vector3 pos)
    {
        SpawnBurst("RespawnShimmer", pos + Vector3.up * 0.2f,
            new Color(0.35f, 1.0f, 0.65f, 0.95f), new Color(1.0f, 0.88f, 0.32f, 0.95f),
            count: 20, minSpeed: 1.8f, maxSpeed: 4.5f, minSize: 0.16f, maxSize: 0.34f,
            gravity: -0.35f, lifetime: 0.58f, coneAngle: 62f, upBias: 1f);
    }

    private void SpawnBurst(
        string effectName,
        Vector3 pos,
        Color colorA,
        Color colorB,
        int count,
        float minSpeed,
        float maxSpeed,
        float minSize,
        float maxSize,
        float gravity,
        float lifetime,
        float coneAngle,
        float upBias)
    {
        var go = new GameObject(effectName, typeof(ParticleSystem));
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

        var ps = go.GetComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.25f;
        main.loop = false;
        main.playOnAwake = false;
        main.useUnscaledTime = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.gravityModifier = gravity;
        main.maxParticles = count + 4;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = coneAngle;
        shape.radius = 0.32f;
        shape.randomDirectionAmount = 1f - Mathf.Clamp01(upBias);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = GetOrCreateParticleMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        ps.Play();
        Destroy(go, lifetime + 0.6f);
    }

    private static Material GetOrCreateParticleMaterial()
    {
        if (cachedParticleMaterial != null) return cachedParticleMaterial;

        if (cachedParticleTexture == null)
        {
            const int s = 64;
            cachedParticleTexture = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            float c = (s - 1) * 0.5f;
            Color[] px = new Color[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    px[y * s + x] = new Color(1f, 1f, 1f, a);
                }
            }
            cachedParticleTexture.SetPixels(px);
            cachedParticleTexture.Apply();
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Sprites/Default");

        cachedParticleMaterial = new Material(shader);
        if (cachedParticleMaterial.HasProperty("_BaseMap"))
            cachedParticleMaterial.SetTexture("_BaseMap", cachedParticleTexture);
        if (cachedParticleMaterial.HasProperty("_MainTex"))
            cachedParticleMaterial.SetTexture("_MainTex", cachedParticleTexture);
        if (cachedParticleMaterial.HasProperty("_BaseColor"))
            cachedParticleMaterial.SetColor("_BaseColor", Color.white);
        if (cachedParticleMaterial.HasProperty("_Surface"))
            cachedParticleMaterial.SetFloat("_Surface", 1f);
        if (cachedParticleMaterial.HasProperty("_Blend"))
            cachedParticleMaterial.SetFloat("_Blend", 0f);
        if (cachedParticleMaterial.HasProperty("_ZWrite"))
            cachedParticleMaterial.SetFloat("_ZWrite", 0f);
        if (cachedParticleMaterial.HasProperty("_SrcBlend"))
            cachedParticleMaterial.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (cachedParticleMaterial.HasProperty("_DstBlend"))
            cachedParticleMaterial.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        cachedParticleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        cachedParticleMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return cachedParticleMaterial;
    }

    private static BiomeFeedbackKind ResolveBiomeKind()
    {
        string sceneName = SceneManager.GetActiveScene().name.ToLowerInvariant();
        if (sceneName.Contains("level2")) return BiomeFeedbackKind.River;
        if (sceneName.Contains("level3")) return BiomeFeedbackKind.SunsetMist;
        if (sceneName.Contains("level4")) return BiomeFeedbackKind.CloudSea;
        return BiomeFeedbackKind.Lake;
    }

    private static Color GetFallVignetteColor(BiomeFeedbackKind biome)
    {
        switch (biome)
        {
            case BiomeFeedbackKind.Lake:
            case BiomeFeedbackKind.River:
                return new Color(0.08f, 0.45f, 0.62f, 1f);
            case BiomeFeedbackKind.SunsetMist:
                return new Color(0.75f, 0.34f, 0.14f, 1f);
            default:
                return new Color(0.18f, 0.28f, 0.46f, 1f);
        }
    }

    private static ThirdPersonCamera ResolveMainCameraController()
    {
        if (Camera.main != null)
        {
            var cam = Camera.main.GetComponent<ThirdPersonCamera>();
            if (cam != null) return cam;
        }
        return FindAnyObjectByType<ThirdPersonCamera>();
    }
}
