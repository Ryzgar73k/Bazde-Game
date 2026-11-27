using System.Collections;
using UnityEngine;

/// <summary>
/// Checkpoint, zafer yan bayrakları ve zirvedeki Kürdistan bayrağı için:
/// varsayılan olarak gönderin altında (ve KayKit bayraklarında sarı) duran,
/// tetiklendiğinde pürüzsüzce göndere çekilip yeşile dönen animasyon bileşeni.
/// </summary>
public class HoistableFlag : MonoBehaviour
{
    public bool triggerOnVictory = false;
    public float hoistDuration = 0.55f;
    public bool useBackOvershoot = true;

    [Header("Hareketli Bayrak Bezi")]
    public Transform movingTarget;
    public float loweredLocalY = -0.76f;
    public float raisedLocalY = 0f;

    [Header("Renk/Mesh Geçişi (Sarı -> Yeşil)")]
    public MeshFilter poleMeshFilter;
    public Mesh inactivePoleMesh;
    public Mesh activePoleMesh;
    public MeshFilter clothMeshFilter;
    public Mesh inactiveClothMesh;
    public Mesh activeClothMesh;

    [Header("Dalgalanma (Kürdistan Zirve Bayrağı)")]
    public WavingFlagMesh wavingFlag;
    [Range(0.1f, 1f)] public float loweredWaveScale = 0.42f;

    public bool IsRaised { get; private set; }

    private bool swappedToActiveMesh;
    private float baseWaveAmplitude = 0.24f;
    private Coroutine hoistRoutine;

    void Awake()
    {
        if (movingTarget == null)
            movingTarget = transform;

        if (poleMeshFilter != null && inactivePoleMesh != null)
            poleMeshFilter.sharedMesh = inactivePoleMesh;
        if (clothMeshFilter != null && inactiveClothMesh != null)
            clothMeshFilter.sharedMesh = inactiveClothMesh;

        if (wavingFlag == null && movingTarget != null)
            wavingFlag = movingTarget.GetComponent<WavingFlagMesh>();

        if (wavingFlag != null)
        {
            baseWaveAmplitude = wavingFlag.waveAmplitude;
            wavingFlag.waveAmplitude = baseWaveAmplitude * loweredWaveScale;
        }

        if (movingTarget != null)
        {
            Vector3 lp = movingTarget.localPosition;
            lp.y = loweredLocalY;
            movingTarget.localPosition = lp;
        }
    }

    public void Raise()
    {
        if (IsRaised) return;
        IsRaised = true;

        if (!gameObject.activeInHierarchy)
        {
            ApplyImmediateRaisedState();
            return;
        }

        if (hoistRoutine != null)
            StopCoroutine(hoistRoutine);
        hoistRoutine = StartCoroutine(HoistRoutine());
    }

    private IEnumerator HoistRoutine()
    {
        float dur = Mathf.Max(0.1f, hoistDuration);
        float elapsed = 0f;

        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);

            if (!swappedToActiveMesh && t >= 0.08f)
                SwapToActiveMeshes();

            float eased = useBackOvershoot
                ? EaseOutBack(t)
                : 1f - Mathf.Pow(1f - t, 3f);

            ApplyTransformProgress(eased, t);
            yield return null;
        }

        ApplyImmediateRaisedState();
        hoistRoutine = null;
    }

    private void ApplyImmediateRaisedState()
    {
        SwapToActiveMeshes();
        ApplyTransformProgress(1f, 1f);
    }

    private void SwapToActiveMeshes()
    {
        swappedToActiveMesh = true;
        if (poleMeshFilter != null && activePoleMesh != null)
            poleMeshFilter.sharedMesh = activePoleMesh;
        if (clothMeshFilter != null && activeClothMesh != null)
            clothMeshFilter.sharedMesh = activeClothMesh;
    }

    private void ApplyTransformProgress(float moveProgress, float waveProgress)
    {
        if (movingTarget != null)
        {
            Vector3 lp = movingTarget.localPosition;
            lp.y = Mathf.LerpUnclamped(loweredLocalY, raisedLocalY, moveProgress);
            movingTarget.localPosition = lp;
        }

        if (wavingFlag != null)
        {
            wavingFlag.waveAmplitude = Mathf.Lerp(baseWaveAmplitude * loweredWaveScale, baseWaveAmplitude, Mathf.Clamp01(waveProgress));
        }
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.45f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
