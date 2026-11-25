using UnityEngine;

/// <summary>
/// Harici ses dosyası olmadığında çalışma zamanında hafif prosedürel AudioClip üreten
/// ve 2B AudioSource üzerinden pitch/volüm çeşitliliğiyle çalan yardımcı ses sistemi.
/// </summary>
public static class ProceduralGameAudio
{
    private const int SampleRate = 44100;
    private const float MaxPeak = 0.85f;

    private static AudioSource sharedSource;
    private static AudioClip fallWhooshClip;
    private static AudioClip waterSplashClip;
    private static AudioClip mistCloudPoofClip;
    private static AudioClip trapImpactClip;
    private static AudioClip knockbackHitClip;
    private static AudioClip respawnChimeClip;

    public static AudioClip FallWhooshClip => fallWhooshClip ??= BuildFallWhooshClip();
    public static AudioClip WaterSplashClip => waterSplashClip ??= BuildWaterSplashClip();
    public static AudioClip MistCloudPoofClip => mistCloudPoofClip ??= BuildMistCloudPoofClip();
    public static AudioClip TrapImpactClip => trapImpactClip ??= BuildTrapImpactClip();
    public static AudioClip KnockbackHitClip => knockbackHitClip ??= BuildKnockbackHitClip();
    public static AudioClip RespawnChimeClip => respawnChimeClip ??= BuildRespawnChimeClip();

    public static void Play(AudioClip overrideClip, AudioClip fallbackClip, float volumeScale = 1f, float pitchJitter = 0.06f)
    {
        float gain = GameFeelSettings.EffectiveSfxGain;
        if (gain <= 0.0001f) return;

        AudioClip clip = overrideClip != null ? overrideClip : fallbackClip;
        if (clip == null) return;

        AudioSource source = EnsureSource();
        if (source == null) return;

        source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        source.PlayOneShot(clip, Mathf.Clamp01(volumeScale * gain));
    }

    private static AudioSource EnsureSource()
    {
        if (sharedSource != null) return sharedSource;

        var go = new GameObject("ProceduralGameAudioSource");
        Object.DontDestroyOnLoad(go);
        sharedSource = go.AddComponent<AudioSource>();
        sharedSource.playOnAwake = false;
        sharedSource.loop = false;
        sharedSource.spatialBlend = 0f;
        return sharedSource;
    }

    private static AudioClip BuildFallWhooshClip()
    {
        const float duration = 0.38f;
        int len = Mathf.RoundToInt(SampleRate * duration);
        float[] data = new float[len];
        System.Random rng = new System.Random(1041);

        float lp1 = 0f;
        float lp2 = 0f;
        float phase = 0f;

        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            float env = Mathf.Sin(t * Mathf.PI) * Mathf.Pow(1f - t * 0.35f, 0.7f);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

            float cutoff = Mathf.Lerp(0.14f, 0.03f, t);
            lp1 += (noise - lp1) * cutoff;
            lp2 += (lp1 - lp2) * (cutoff * 0.9f);

            float freq = Mathf.Lerp(320f, 115f, t);
            phase += 2f * Mathf.PI * freq / SampleRate;
            float tone = Mathf.Sin(phase) * 0.22f;

            data[i] = (lp2 * 1.4f + tone) * env;
        }

        return CreateNormalizedClip("Proc_FallWhoosh", data);
    }

    private static AudioClip BuildWaterSplashClip()
    {
        const float duration = 0.48f;
        int len = Mathf.RoundToInt(SampleRate * duration);
        float[] data = new float[len];
        System.Random rng = new System.Random(2083);

        float lp = 0f;
        float prevLp = 0f;
        float plopPhase = 0f;

        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

            // Başlangıç sıçraması + kabarcık süzgeci
            float cutoff = Mathf.Lerp(0.45f, 0.12f, t);
            lp += (noise - lp) * cutoff;
            float hp = lp - prevLp * 0.72f;
            prevLp = lp;

            float splashEnv = Mathf.Exp(-t * 6.5f);
            float plopFreq = Mathf.Lerp(240f, 95f, Mathf.Clamp01(t * 4.5f));
            plopPhase += 2f * Mathf.PI * plopFreq / SampleRate;
            float plop = Mathf.Sin(plopPhase) * Mathf.Exp(-t * 14f) * 0.55f;

            float bubble = (Mathf.Sin(t * 78f) * 0.5f + 0.5f) * lp * Mathf.Exp(-t * 5f) * 0.45f;
            data[i] = (hp * splashEnv * 0.95f) + plop + bubble;
        }

        return CreateNormalizedClip("Proc_WaterSplash", data);
    }

    private static AudioClip BuildMistCloudPoofClip()
    {
        const float duration = 0.42f;
        int len = Mathf.RoundToInt(SampleRate * duration);
        float[] data = new float[len];
        System.Random rng = new System.Random(3097);

        float lp1 = 0f;
        float lp2 = 0f;
        float phase = 0f;

        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float cutoff = Mathf.Lerp(0.18f, 0.04f, t);
            lp1 += (noise - lp1) * cutoff;
            lp2 += (lp1 - lp2) * 0.12f;

            float freq = Mathf.Lerp(110f, 48f, Mathf.Clamp01(t * 2.5f));
            phase += 2f * Mathf.PI * freq / SampleRate;
            float thump = Mathf.Sin(phase) * Mathf.Exp(-t * 11f) * 0.55f;
            float airyEnv = Mathf.Sin(Mathf.Min(1f, t * 2.2f) * Mathf.PI * 0.5f) * Mathf.Exp(-t * 4.8f);

            data[i] = lp2 * airyEnv * 1.45f + thump;
        }

        return CreateNormalizedClip("Proc_MistCloudPoof", data);
    }

    private static AudioClip BuildTrapImpactClip()
    {
        const float duration = 0.30f;
        int len = Mathf.RoundToInt(SampleRate * duration);
        float[] data = new float[len];
        System.Random rng = new System.Random(4129);

        float phaseSub = 0f;
        float phaseClang = 0f;
        float prevNoise = 0f;

        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            float subFreq = Mathf.Lerp(130f, 42f, Mathf.Pow(t, 0.55f));
            phaseSub += 2f * Mathf.PI * subFreq / SampleRate;
            float sub = Mathf.Sin(phaseSub) * Mathf.Exp(-t * 12f);

            // Metalik diken çıtırısı
            phaseClang += 2f * Mathf.PI * 920f / SampleRate;
            float clang = (Mathf.Sin(phaseClang) + 0.5f * Mathf.Sin(phaseClang * 1.63f)) * Mathf.Exp(-t * 24f) * 0.35f;

            float rawNoise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float crunch = (rawNoise - prevNoise * 0.5f) * Mathf.Exp(-t * 20f) * 0.55f;
            prevNoise = rawNoise;

            data[i] = sub * 0.85f + clang + crunch;
        }

        return CreateNormalizedClip("Proc_TrapImpact", data);
    }

    private static AudioClip BuildKnockbackHitClip()
    {
        const float duration = 0.18f;
        int len = Mathf.RoundToInt(SampleRate * duration);
        float[] data = new float[len];
        System.Random rng = new System.Random(5171);

        float phase = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            float freq = Mathf.Lerp(190f, 68f, t);
            phase += 2f * Mathf.PI * freq / SampleRate;
            float thwack = Mathf.Sin(phase) * Mathf.Exp(-t * 18f);
            float pop = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 30f) * 0.32f;
            data[i] = thwack * 0.9f + pop;
        }

        return CreateNormalizedClip("Proc_KnockbackHit", data);
    }

    private static AudioClip BuildRespawnChimeClip()
    {
        const float duration = 0.40f;
        int len = Mathf.RoundToInt(SampleRate * duration);
        float[] data = new float[len];

        // Sıcak C5 -> E5 -> G5 yükselen akor ışıltısı
        float[] freqs = { 523.25f, 659.25f, 783.99f };
        float[] starts = { 0.00f, 0.06f, 0.12f };
        float[] phases = new float[3];

        for (int i = 0; i < len; i++)
        {
            float timeSec = (float)i / SampleRate;
            float sum = 0f;

            for (int n = 0; n < 3; n++)
            {
                if (timeSec < starts[n]) continue;
                float localT = timeSec - starts[n];
                float env = Mathf.Min(1f, localT / 0.012f) * Mathf.Exp(-localT * 8.5f);
                phases[n] += 2f * Mathf.PI * freqs[n] / SampleRate;
                sum += (Mathf.Sin(phases[n]) + 0.2f * Mathf.Sin(phases[n] * 2f)) * env * 0.42f;
            }

            data[i] = sum;
        }

        return CreateNormalizedClip("Proc_RespawnChime", data);
    }

    private static AudioClip CreateNormalizedClip(string clipName, float[] samples)
    {
        float max = 0.0001f;
        for (int i = 0; i < samples.Length; i++)
        {
            float a = Mathf.Abs(samples[i]);
            if (a > max) max = a;
        }

        float scale = MaxPeak / max;
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = Mathf.Clamp(samples[i] * scale, -1f, 1f);
        }

        var clip = AudioClip.Create(clipName, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
