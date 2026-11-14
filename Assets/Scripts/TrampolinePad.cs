using UnityEngine;

/// <summary>
/// Üzerine basan oyuncuyu belirli aralıklarla (varsayılan 2 saniyede bir) havaya fırlatan trambolin bileşeni.
/// (Örn: Merkez_Trambolin_Yuzeyi nesnesine eklenir)
/// </summary>
public class TrampolinePad : MonoBehaviour
{
    [Header("Trambolin Ayarları")]
    [Tooltip("Kaç saniyede bir havaya fırlatsın?")]
    public float bounceInterval = 2f;

    [Tooltip("Oyuncuyu kaç metre yükseğe fırlatsın?")]
    public float bounceHeight = 6f;

    [Tooltip("Oyuncu üstüne bastığında ilk zıplatmayı beklemeden hemen yapsın mı? (Kapalıysa 2 saniye bekleyip fırlatır)")]
    public bool bounceImmediatelyOnStep = false;

    [Tooltip("Üzerinde oyuncuyu algılama mesafesi (metre)")]
    public float detectionPadding = 1.2f;

    private float timer;
    private Collider padCollider;
    private Renderer padRenderer;
    private PlayerController cachedPlayer;
    private CharacterController cachedPlayerCC;
    private bool playerTouchReported;

    void Awake()
    {
        var all = GetComponents<TrampolinePad>();
        if (all.Length > 1 && all[0] != this) { enabled = false; return; }
        padCollider = GetComponent<Collider>();
        padRenderer = GetComponent<Renderer>();
        timer = bounceImmediatelyOnStep ? bounceInterval : 0f;
    }

    public void NotifyPlayerStepped(PlayerController player)
    {
        cachedPlayer = player;
        if (cachedPlayerCC == null && player != null)
            cachedPlayerCC = player.GetComponent<CharacterController>();
        playerTouchReported = true;
    }

    void Update()
    {
        if (cachedPlayer == null)
        {
            cachedPlayer = Object.FindFirstObjectByType<PlayerController>();
            if (cachedPlayer != null)
                cachedPlayerCC = cachedPlayer.GetComponent<CharacterController>();
        }

        if (cachedPlayer == null || cachedPlayerCC == null)
            return;

        bool onPad = playerTouchReported || IsPlayerOnTrampoline();
        playerTouchReported = false;

        if (onPad)
        {
            timer += Time.deltaTime;
            if (timer >= bounceInterval)
            {
                cachedPlayer.Bounce(bounceHeight);
                timer = 0f;
            }
        }
        else
        {
            // Oyuncu trambolinden indiğinde sayacı sıfırla
            timer = bounceImmediatelyOnStep ? bounceInterval : 0f;
        }
    }

    private bool IsPlayerOnTrampoline()
    {
        Bounds b;
        if (padCollider != null)
            b = padCollider.bounds;
        else if (padRenderer != null)
            b = padRenderer.bounds;
        else
            return false;

        // Oyuncu kapsülünün en alt noktasını (ayaklarını) hesapla
        Vector3 capsuleCenterWorld = cachedPlayer.transform.TransformPoint(cachedPlayerCC.center);
        float bottomY = capsuleCenterWorld.y - (cachedPlayerCC.height * 0.5f);
        Vector3 playerFeet = new Vector3(capsuleCenterWorld.x, bottomY, capsuleCenterWorld.z);

        // Oyuncu yatayda (X ve Z) trambolinin sınırları içinde mi?
        bool insideXZ = playerFeet.x >= (b.min.x - 0.45f) && playerFeet.x <= (b.max.x + 0.45f) &&
                        playerFeet.z >= (b.min.z - 0.45f) && playerFeet.z <= (b.max.z + 0.45f);

        if (!insideXZ)
            return false;

        // Oyuncunun ayakları trambolin yüksekliği civarında mı?
        return playerFeet.y >= (b.min.y - 0.5f) && playerFeet.y <= (b.max.y + detectionPadding);
    }
}

