using UnityEngine;

/// <summary>
/// Üzerine basan oyuncuyu belirli aralıklarla (varsayılan 2 saniyede bir) havaya fırlatan trambolin bileşeni.
/// (Örn: Merkez_Trambolin_Yuzeyi nesnesine eklenir)
/// </summary>
public class TrampolinePad : MonoBehaviour
{
    [Header("Trambolin Ayarları")]
    [Tooltip("Üstünde durmaya devam edilirse kaç saniyede bir yeniden fırlatsın?")]
    public float bounceInterval = 0.25f;

    [Tooltip("Oyuncuyu kaç metre yükseğe fırlatsın?")]
    public float bounceHeight = 6f;

    [Tooltip("Oyuncu üstüne bastığında beklemeden hemen fırlatsın mı?")]
    public bool bounceImmediatelyOnStep = true;

    [Tooltip("Üzerinde oyuncuyu algılama mesafesi (metre)")]
    public float detectionPadding = 0.28f;

    public const int LatestConfigVersion = 3;

    [HideInInspector] public int configVersion = 0;

    private float timer;
    private Collider padCollider;
    private Renderer padRenderer;
    private PlayerController cachedPlayer;
    private CharacterController cachedPlayerCC;
    private bool playerTouchReported;

    public void ApplyConfigMigration()
    {
        if (configVersion < 2)
        {
            bounceImmediatelyOnStep = true;
            if (bounceInterval >= 1.5f) bounceInterval = 0.25f;
            configVersion = 2;
        }
        if (configVersion < 3)
        {
            if (detectionPadding > 0.5f) detectionPadding = 0.28f;
            configVersion = 3;
        }
    }

    void Awake()
    {
        var all = GetComponents<TrampolinePad>();
        if (all.Length > 1 && all[0] != this) { enabled = false; return; }
        ApplyConfigMigration();
        padCollider = GetComponent<Collider>();
        padRenderer = GetComponent<Renderer>();
        timer = bounceImmediatelyOnStep ? bounceInterval : 0f;
    }

    public void NotifyPlayerStepped(PlayerController player)
    {
        cachedPlayer = player;
        if (cachedPlayerCC == null && player != null)
            cachedPlayerCC = player.GetComponent<CharacterController>();
        if (IsPlayerOnTrampoline())
            playerTouchReported = true;
    }

    void Update()
    {
        if (cachedPlayer == null)
        {
            cachedPlayer = Object.FindAnyObjectByType<PlayerController>();
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
        if (cachedPlayer == null || cachedPlayerCC == null)
            return false;

        // Oyuncunun gerçek ayak tabanı pozisyonunu (skinWidth dahil) al
        Vector3 playerFeet = cachedPlayer.GetWorldFootPosition();
        float verticalPad = Mathf.Clamp(detectionPadding, 0.12f, 0.35f);

        if (padCollider != null)
        {
            Bounds colBounds = padCollider.bounds;
            float footRadius = Mathf.Clamp(cachedPlayerCC.radius * 0.6f, 0.06f, 0.11f);

            // İçbükey MeshCollider ClosestPoint desteklemediğinden dikey ışınla gerçek yüzey şeklini kontrol et
            if (padCollider is MeshCollider nonConvexMesh && !nonConvexMesh.convex)
            {
                Vector3 rayOrigin = new Vector3(playerFeet.x, colBounds.max.y + 0.12f, playerFeet.z);
                float rayDist = colBounds.size.y + 0.35f;
                if (!nonConvexMesh.Raycast(new Ray(rayOrigin, Vector3.down), out RaycastHit hit, rayDist))
                    return false;

                return playerFeet.y >= (hit.point.y - 0.15f) && playerFeet.y <= (hit.point.y + verticalPad);
            }

            // Gerçek collider geometrisi (dairesel/çokgen MeshCollider vb.) ile şekil uyumlu kontrol
            Vector3 closest = padCollider.ClosestPoint(playerFeet);
            float dx = closest.x - playerFeet.x;
            float dz = closest.z - playerFeet.z;
            if ((dx * dx + dz * dz) > (footRadius * footRadius))
                return false;

            float minTopY = colBounds.max.y - Mathf.Min(0.25f, colBounds.size.y * 0.6f);
            return playerFeet.y >= minTopY
                && playerFeet.y >= (closest.y - 0.12f)
                && playerFeet.y <= (closest.y + verticalPad);
        }

        if (padRenderer == null)
            return false;

        Bounds b = padRenderer.bounds;
        Vector2 centerXZ = new Vector2(b.center.x, b.center.z);
        Vector2 feetXZ = new Vector2(playerFeet.x, playerFeet.z);
        float maxRadius = Mathf.Min(b.extents.x, b.extents.z);
        if (Vector2.Distance(centerXZ, feetXZ) > maxRadius)
            return false;

        return playerFeet.y >= (b.max.y - 0.25f) && playerFeet.y <= (b.max.y + verticalPad);
    }
}

