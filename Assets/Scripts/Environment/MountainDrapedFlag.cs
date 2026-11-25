using UnityEngine;

/// <summary>
/// Dağ yamacına serilen dev Kürdistan bayrağı için hafif, mobil ve URP uyumlu rüzgâr dalgalanması.
/// Kalıcı mesh varlığını bozmamak için çalışma zamanında runtime kopya üzerinde çalışır.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MountainDrapedFlag : MonoBehaviour
{
    [Header("Ağır Rüzgâr Dalgası")]
    public float waveAmplitude = 0.65f;
    public float waveFrequencyX = 2.2f;
    public float waveFrequencyY = 1.4f;
    public float waveSpeed = 1.05f;
    public float secondaryAmplitude = 0.22f;

    private Mesh runtimeMesh;
    private Vector3[] baseVertices;
    private Vector3[] baseNormals;
    private Vector3[] displacedVertices;
    private Vector2[] uvs;
    private int frameCounter;

    void Start()
    {
        if (!Application.isPlaying) return;

        var mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        runtimeMesh = Instantiate(mf.sharedMesh);
        runtimeMesh.name = mf.sharedMesh.name + "_Runtime";
        runtimeMesh.hideFlags = HideFlags.DontSave;
        mf.sharedMesh = runtimeMesh;

        baseVertices = runtimeMesh.vertices;
        baseNormals = runtimeMesh.normals;
        displacedVertices = new Vector3[baseVertices.Length];
        uvs = runtimeMesh.uv;

        if (baseNormals == null || baseNormals.Length != baseVertices.Length)
        {
            runtimeMesh.RecalculateNormals();
            baseNormals = runtimeMesh.normals;
        }
    }

    void Update()
    {
        if (!Application.isPlaying || runtimeMesh == null || baseVertices == null) return;

        float t = Time.time;
        const float twoPi = Mathf.PI * 2f;

        for (int i = 0; i < baseVertices.Length; i++)
        {
            float u = uvs != null && i < uvs.Length ? uvs[i].x : 0.5f;
            float v = uvs != null && i < uvs.Length ? uvs[i].y : 0.5f;

            // Dört kenar ve köşeler dağ yamacına sabit kalır, orta kısım yavaşça nefes alır
            float pin = Mathf.Sin(u * Mathf.PI) * Mathf.Sin(v * Mathf.PI);
            pin = Mathf.Pow(Mathf.Clamp01(pin), 0.75f);

            float primary = Mathf.Sin(u * waveFrequencyX * twoPi + v * waveFrequencyY * twoPi - t * waveSpeed);
            float secondary = Mathf.Cos(u * 3.1f * twoPi - t * waveSpeed * 0.7f);
            float rawWave = primary * waveAmplitude + secondary * secondaryAmplitude;

            // Daima yamaçtan dışa doğru pozitif ofset bırakarak Z-fighting ve kaya içine batmayı önle
            float outwardDisp = Mathf.Max(0f, (rawWave * 0.5f + waveAmplitude * 0.5f)) * pin;
            displacedVertices[i] = baseVertices[i] + baseNormals[i] * outwardDisp;
        }

        runtimeMesh.vertices = displacedVertices;
        frameCounter++;
        if ((frameCounter & 1) == 0)
            runtimeMesh.RecalculateNormals();
    }

    void OnDestroy()
    {
        if (runtimeMesh != null)
        {
            Destroy(runtimeMesh);
            runtimeMesh = null;
        }
    }
}
