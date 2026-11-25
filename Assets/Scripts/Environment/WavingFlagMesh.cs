using UnityEngine;

/// <summary>
/// Hafif, mobil ve URP tam uyumlu dalgalanan bayrak bileşeni.
/// Alt bölümlü bir kumaş gridi oluşturur ve rüzgâr dalgasını vertex pozisyonlarına uygular.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WavingFlagMesh : MonoBehaviour
{
    [Header("Boyut ve Çözünürlük")]
    public float width = 3.2f;
    public float height = 2.0f;
    [Range(4, 28)] public int segmentsX = 14;
    [Range(2, 16)] public int segmentsY = 8;

    [Header("Rüzgâr Dalgalanması")]
    public float waveAmplitude = 0.24f;
    public float waveFrequency = 2.6f;
    public float waveSpeed = 3.4f;
    public float secondaryWave = 0.08f;

    private Mesh runtimeMesh;
    private Vector3[] baseVertices;
    private Vector3[] displacedVertices;
    private Vector2[] uvs;
    private int cachedSegX = -1;
    private int cachedSegY = -1;
    private float cachedWidth = -1f;
    private float cachedHeight = -1f;

    void OnEnable()
    {
        EnsureMeshBuilt();
        ApplyWave(Application.isPlaying ? Time.time : 0.45f);
    }

    void OnDisable()
    {
        if (!Application.isPlaying && runtimeMesh != null)
        {
            var mf = GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh == runtimeMesh)
                mf.sharedMesh = null;
            DestroyImmediate(runtimeMesh);
            runtimeMesh = null;
        }
    }

    void OnDestroy()
    {
        if (runtimeMesh != null)
        {
            if (Application.isPlaying) Destroy(runtimeMesh);
            else DestroyImmediate(runtimeMesh);
            runtimeMesh = null;
        }
    }

    void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        EnsureMeshBuilt();
        ApplyWave(Application.isPlaying ? Time.time : 0.45f);
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        EnsureMeshBuilt();
        ApplyWave(Time.time);
    }

    private void EnsureMeshBuilt()
    {
        int sx = Mathf.Clamp(segmentsX, 4, 28);
        int sy = Mathf.Clamp(segmentsY, 2, 16);

        var mf = GetComponent<MeshFilter>();
        if (mf == null) return;

        bool needsRebuild = runtimeMesh == null
            || mf.sharedMesh != runtimeMesh
            || cachedSegX != sx
            || cachedSegY != sy
            || !Mathf.Approximately(cachedWidth, width)
            || !Mathf.Approximately(cachedHeight, height);

        if (!needsRebuild) return;

        if (runtimeMesh == null)
        {
            runtimeMesh = new Mesh
            {
                name = "WavingFlag_RuntimeMesh",
                hideFlags = HideFlags.DontSave
            };
        }
        else
        {
            runtimeMesh.Clear();
        }

        int cols = sx + 1;
        int rows = sy + 1;
        int vertCount = cols * rows;

        baseVertices = new Vector3[vertCount];
        displacedVertices = new Vector3[vertCount];
        uvs = new Vector2[vertCount];

        for (int y = 0; y < rows; y++)
        {
            float v = (float)y / sy;
            float py = (v - 0.5f) * height;
            for (int x = 0; x < cols; x++)
            {
                float u = (float)x / sx;
                int idx = y * cols + x;
                baseVertices[idx] = new Vector3(u * width, py, 0f);
                displacedVertices[idx] = baseVertices[idx];
                uvs[idx] = new Vector2(u, v);
            }
        }

        int[] tris = new int[sx * sy * 6];
        int t = 0;
        for (int y = 0; y < sy; y++)
        {
            for (int x = 0; x < sx; x++)
            {
                int i0 = y * cols + x;
                int i1 = i0 + 1;
                int i2 = i0 + cols;
                int i3 = i2 + 1;

                tris[t++] = i0;
                tris[t++] = i2;
                tris[t++] = i1;

                tris[t++] = i1;
                tris[t++] = i2;
                tris[t++] = i3;
            }
        }

        runtimeMesh.vertices = displacedVertices;
        runtimeMesh.uv = uvs;
        runtimeMesh.triangles = tris;
        runtimeMesh.RecalculateNormals();
        runtimeMesh.RecalculateBounds();

        mf.sharedMesh = runtimeMesh;

        cachedSegX = sx;
        cachedSegY = sy;
        cachedWidth = width;
        cachedHeight = height;
    }

    private void ApplyWave(float time)
    {
        if (runtimeMesh == null || baseVertices == null || displacedVertices == null) return;

        for (int i = 0; i < baseVertices.Length; i++)
        {
            Vector3 b = baseVertices[i];
            float u = uvs[i].x;
            float v = uvs[i].y;

            // Direğe bağlı kenar (u = 0) sabit kalır, u = 1 ucu tam dalgalanır
            float pin = Mathf.Pow(u, 1.35f);
            float phase = u * waveFrequency * Mathf.PI * 2f - time * waveSpeed;
            float zOffset = Mathf.Sin(phase + v * 1.4f) * waveAmplitude * pin
                          + Mathf.Cos(phase * 1.9f - v * 2.1f) * secondaryWave * pin;
            float yOffset = Mathf.Sin(phase * 0.85f + u * 2.0f) * (waveAmplitude * 0.22f) * pin;

            displacedVertices[i] = new Vector3(b.x, b.y + yOffset, b.z + zOffset);
        }

        runtimeMesh.vertices = displacedVertices;
        runtimeMesh.RecalculateNormals();
    }
}
