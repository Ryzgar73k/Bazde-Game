using UnityEngine;

/// <summary>
/// Newroz ateş çanakları ve başlangıç/checkpoint meşaleleri için hafif Perlin gürültülü alev titreşimi.
/// Mobil performans için gölgesiz nokta ışığı ve küçük bir görsel alev ölçeği kullanır.
/// </summary>
public class FlickeringFlameLight : MonoBehaviour
{
    public Light targetLight;
    public Transform flameVisual;

    [Header("Titreşim Ayarları")]
    public float baseIntensity = 2.2f;
    public float intensityVariance = 0.45f;
    public float flickerSpeed = 7.5f;
    public float scalePulse = 0.14f;

    private Vector3 baseFlameScale = Vector3.one;
    private float noiseSeed;

    void Awake()
    {
        if (targetLight != null)
            targetLight.shadows = LightShadows.None;

        if (flameVisual != null)
            baseFlameScale = flameVisual.localScale;

        Vector3 p = transform.position;
        noiseSeed = Mathf.Abs(p.x * 0.37f + p.z * 0.73f) % 100f;
    }

    void Update()
    {
        float t = Time.time * flickerSpeed + noiseSeed;
        float n1 = Mathf.PerlinNoise(t, noiseSeed);
        float n2 = Mathf.PerlinNoise(noiseSeed, t * 1.35f);

        if (targetLight != null)
        {
            targetLight.intensity = Mathf.Max(0.2f, baseIntensity + (n1 - 0.5f) * 2f * intensityVariance);
        }

        if (flameVisual != null)
        {
            float sXZ = 1f + (n2 - 0.5f) * scalePulse;
            float sY = 1f + (n1 - 0.5f) * (scalePulse * 1.8f);
            flameVisual.localScale = new Vector3(baseFlameScale.x * sXZ, baseFlameScale.y * sY, baseFlameScale.z * sXZ);
        }
    }
}
