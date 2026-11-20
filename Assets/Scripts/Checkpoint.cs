using UnityEngine;

/// <summary>
/// Oyuncunun üzerinden geçtiğinde son geçerli checkpoint noktasını güncelleyen bileşen.
/// Görsel renk değişimi, URP Lit emisyonu, ışık ve ses efektlerini destekler.
/// </summary>
[SelectionBase]
public class Checkpoint : MonoBehaviour
{
    [Header("Checkpoint Ayarları")]
    [Tooltip("İsteğe bağlı sıralama veya kimlik numarası")]
    public int checkpointIndex = 0;

    [Tooltip("Özel doğma noktası transformu. Boş bırakılırsa bu nesnenin transformu kullanılır.")]
    public Transform customSpawnPoint;

    [Tooltip("Doğma noktasına eklenecek dikey/yatay ofset (zemine gömülmeyi önler)")]
    public Vector3 spawnOffset = new Vector3(0f, 0.25f, 0f);

    [Tooltip("Oyuncu bu checkpoint'te doğduğunda checkpoint'in ileri baktığı yöne dönsün mü?")]
    public bool faceForwardOnRespawn = true;

    [Header("Görsel Geri Bildirim")]
    [Tooltip("Renk değişimi uygulanacak Renderer (boşsa çocuk nesnelerden otomatik aranır)")]
    public Renderer visualRenderer;

    [Tooltip("Checkpoint henüz alınmamışken kullanılacak renk")]
    public Color inactiveColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    [Tooltip("Checkpoint aktifleştiğinde kullanılacak renk")]
    public Color activeColor = new Color(0.15f, 0.95f, 0.35f, 1f);

    [Tooltip("Aktifleştiğinde URP Lit emisyon parlaması açılsın mı?")]
    public bool useEmission = true;

    [Tooltip("Aktif emisyon rengi ve parlaklığı")]
    [ColorUsage(true, true)]
    public Color activeEmissionColor = new Color(0.2f, 1.2f, 0.5f, 1f);

    [Header("Işık ve Efektler")]
    [Tooltip("Opsiyonel nokta ışığı (aktifleşince yeşile döner veya açılır)")]
    public Light checkpointLight;

    [Tooltip("Aktifleştiğinde açılacak parçacık efekti veya dekoratif nesne")]
    public GameObject activeVFX;

    [Tooltip("Aktifleştiğinde çalacak ses efekti")]
    public AudioClip activateSound;

    [Range(0f, 1f)]
    public float soundVolume = 0.85f;

    /// <summary>Bu checkpoint daha önce tetiklendi mi?</summary>
    public bool IsActivated { get; private set; }

    static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
    static readonly int LegacyColorProp = Shader.PropertyToID("_Color");
    static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        if (visualRenderer == null)
            visualRenderer = GetComponentInChildren<Renderer>();

        if (checkpointLight == null)
            checkpointLight = GetComponentInChildren<Light>();

        var col = GetComponent<Collider>();
        if (col == null)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(3f, 2.5f, 3f);
            box.center = new Vector3(0f, 1.25f, 0f);
        }
        else
        {
            col.isTrigger = true;
        }

        UpdateVisuals(false);

        if (activeVFX != null)
            activeVFX.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsActivated) return;

        var player = other.GetComponentInParent<PlayerController>();
        if (player != null || other.CompareTag("Player"))
        {
            ActivateCheckpoint();
        }
    }

    /// <summary>
    /// Checkpoint'i aktif hale getirir, RespawnManager'a bildirir ve görsel efektleri oynatır.
    /// </summary>
    public void ActivateCheckpoint()
    {
        if (IsActivated) return;
        IsActivated = true;

        UpdateVisuals(true);

        if (activeVFX != null)
            activeVFX.SetActive(true);

        if (activateSound != null)
            AudioSource.PlayClipAtPoint(activateSound, transform.position, soundVolume);

        if (RespawnManager.Instance != null)
            RespawnManager.Instance.SetActiveCheckpoint(this);
    }

    /// <summary>
    /// URP Lit materyali ve ışık renklerini günceller.
    /// </summary>
    public void UpdateVisuals(bool active)
    {
        Color targetColor = active ? activeColor : inactiveColor;

        if (visualRenderer != null)
        {
            Material mat = visualRenderer.material;
            if (mat != null)
            {
                if (mat.HasProperty(BaseColorProp))
                    mat.SetColor(BaseColorProp, targetColor);
                else if (mat.HasProperty(LegacyColorProp))
                    mat.SetColor(LegacyColorProp, targetColor);

                if (useEmission && mat.HasProperty(EmissionColorProp))
                {
                    if (active)
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor(EmissionColorProp, activeEmissionColor);
                    }
                    else
                    {
                        mat.SetColor(EmissionColorProp, Color.black);
                    }
                }
            }
        }

        if (checkpointLight != null)
        {
            checkpointLight.color = targetColor;
            checkpointLight.enabled = active || checkpointLight.enabled;
        }
    }

    public Vector3 GetSpawnPosition()
    {
        if (customSpawnPoint != null)
            return customSpawnPoint.position + spawnOffset;

        return transform.position + spawnOffset;
    }

    public Quaternion GetSpawnRotation()
    {
        if (customSpawnPoint != null)
            return customSpawnPoint.rotation;

        return faceForwardOnRespawn ? transform.rotation : Quaternion.identity;
    }

    void OnDrawGizmos()
    {
        Vector3 spawnPos = customSpawnPoint != null ? customSpawnPoint.position + spawnOffset : transform.position + spawnOffset;
        Gizmos.color = IsActivated ? new Color(0.2f, 1f, 0.4f, 0.7f) : new Color(1f, 0.9f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(spawnPos, 0.5f);

        Vector3 forward = (customSpawnPoint != null ? customSpawnPoint.forward : transform.forward) * 1.5f;
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(spawnPos, forward);
    }
}
