using System.Collections;
using UnityEngine;

/// <summary>
/// Oyuncunun düşme veya ölme durumlarında en son aktif checkpoint noktasına
/// (checkpoint yoksa sahne başlangıç noktasına) güvenli bir şekilde ışınlanmasını yöneten sistem.
/// </summary>
public class RespawnManager : MonoBehaviour
{
    public static RespawnManager Instance { get; private set; }

    [Header("Oyuncu ve Doğma Noktası")]
    [Tooltip("İzlenecek oyuncu referansı (boş bırakılırsa sahnede otomatik bulunur)")]
    public PlayerController player;

    [Tooltip("Varsayılan başlangıç doğma noktası (boşsa oyuncunun sahnedeki ilk pozisyonu kullanılır)")]
    public Transform defaultSpawnPoint;

    [Tooltip("Varsayılan doğma noktasına eklenecek ofset")]
    public Vector3 defaultSpawnOffset = new Vector3(0f, 0.25f, 0f);

    [Header("Düşme (Y Koordinatı) İzleme")]
    [Tooltip("Oyuncunun Y pozisyonu sürekli kontrol edilsin mi?")]
    public bool autoCheckFall = true;

    [Tooltip("Oyuncu bu Y yüksekliğinin altına düştüğünde otomatik yeniden doğar")]
    public float fallThresholdY = -12f;

    [Header("Respawn Efektleri")]
    [Tooltip("Yeniden doğma gecikmesi (saniye)")]
    public float respawnDelay = 0.05f;

    [Tooltip("Yeniden doğma anında çalacak ses")]
    public AudioClip respawnSound;

    [Tooltip("Yeniden doğma anında oluşacak parçacık efekti")]
    public GameObject respawnVFX;

    [Header("Aktif Checkpoint Durumu")]
    [SerializeField]
    private Checkpoint currentCheckpoint;

    public Checkpoint CurrentCheckpoint => currentCheckpoint;
    public bool IsRespawning => isRespawning;
    public int DeathCount { get; private set; }

    public event System.Action<Checkpoint> OnCheckpointChanged;
    public event System.Action<PlayerController, DeathCause, Vector3> OnPlayerDied;
    public event System.Action<PlayerController> OnPlayerRespawned;

    private Vector3 initialSpawnPosition;
    private Quaternion initialSpawnRotation;
    private bool isRespawning;
    private bool hasPlayedFallWhoosh;
    private DeathFeedbackController feedbackController;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        feedbackController = GetComponent<DeathFeedbackController>();
        if (feedbackController == null)
            feedbackController = gameObject.AddComponent<DeathFeedbackController>();

        if (player == null)
            player = Object.FindAnyObjectByType<PlayerController>();

        if (player != null)
        {
            initialSpawnPosition = player.transform.position;
            initialSpawnRotation = player.transform.rotation;
        }
        else if (defaultSpawnPoint != null)
        {
            initialSpawnPosition = defaultSpawnPoint.position + defaultSpawnOffset;
            initialSpawnRotation = defaultSpawnPoint.rotation;
        }
    }

    void Start()
    {
        if (defaultSpawnPoint != null)
        {
            initialSpawnPosition = defaultSpawnPoint.position + defaultSpawnOffset;
            initialSpawnRotation = defaultSpawnPoint.rotation;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (!autoCheckFall || isRespawning || VictoryUI.IsAnyVictoryVisible) return;

        if (player == null)
        {
            player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null) return;
        }

        if (player.IsGrounded)
        {
            hasPlayedFallWhoosh = false;
        }
        else if (!hasPlayedFallWhoosh && player.VerticalVelocity < -8.0f && player.GetWorldFootPosition().y < fallThresholdY + 6.5f)
        {
            hasPlayedFallWhoosh = true;
            if (feedbackController != null)
                feedbackController.NotifyFallingTowardAbyss();
        }

        if (player.GetWorldFootPosition().y < fallThresholdY)
        {
            RespawnPlayer(player, DeathCause.Fall);
        }
    }

    public void SetActiveCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint == null) return;
        if (currentCheckpoint != null && checkpoint.checkpointIndex < currentCheckpoint.checkpointIndex) return;
        currentCheckpoint = checkpoint;
        OnCheckpointChanged?.Invoke(checkpoint);
    }

    public void RespawnPlayer(PlayerController target = null)
    {
        RespawnPlayer(target, DeathCause.Fall);
    }

    public void RespawnPlayer(
        PlayerController target,
        DeathCause cause,
        Vector3? impactOrigin = null,
        AudioClip overrideDeathSound = null,
        GameObject overrideDeathVFX = null)
    {
        if (isRespawning || VictoryUI.IsAnyVictoryVisible) return;

        if (target == null) target = player;
        if (target == null) target = Object.FindAnyObjectByType<PlayerController>();
        if (target == null) return;

        if (cause != DeathCause.Fall && target.IsSpawnInvulnerable)
            return;

        DeathCount++;
        Vector3 deathPos = target.transform.position;
        isRespawning = true;
        target.canControl = false;
        OnPlayerDied?.Invoke(target, cause, deathPos);

        if (feedbackController == null)
            feedbackController = GetComponent<DeathFeedbackController>();

        if (feedbackController != null && gameObject.activeInHierarchy)
        {
            StartCoroutine(DeathAndRespawnRoutine(target, cause, deathPos, impactOrigin ?? deathPos, overrideDeathSound, overrideDeathVFX));
        }
        else if (respawnDelay > 0f && gameObject.activeInHierarchy)
        {
            StartCoroutine(RespawnRoutine(target));
        }
        else
        {
            ExecuteRespawn(target);
            target.canControl = !VictoryUI.IsAnyVictoryVisible;
            hasPlayedFallWhoosh = false;
            isRespawning = false;
        }
    }

    private IEnumerator DeathAndRespawnRoutine(
        PlayerController target,
        DeathCause cause,
        Vector3 deathPos,
        Vector3 impactOrigin,
        AudioClip overrideDeathSound,
        GameObject overrideDeathVFX)
    {
        yield return feedbackController.PlayDeathAndRespawnSequence(
            target,
            cause,
            deathPos,
            impactOrigin,
            fallThresholdY,
            overrideDeathSound,
            overrideDeathVFX,
            () => ExecuteRespawn(target));

        if (target != null)
            target.canControl = !VictoryUI.IsAnyVictoryVisible;
        hasPlayedFallWhoosh = false;
        isRespawning = false;
    }

    private IEnumerator RespawnRoutine(PlayerController target)
    {
        isRespawning = true;
        yield return new WaitForSecondsRealtime(respawnDelay);
        ExecuteRespawn(target);
        if (target != null)
            target.canControl = !VictoryUI.IsAnyVictoryVisible;
        hasPlayedFallWhoosh = false;
        isRespawning = false;
    }

    private void ExecuteRespawn(PlayerController target)
    {
        Vector3 targetPos;
        Quaternion targetRot;

        if (currentCheckpoint != null)
        {
            targetPos = currentCheckpoint.GetSpawnPosition();
            targetRot = currentCheckpoint.GetSpawnRotation();
        }
        else if (defaultSpawnPoint != null)
        {
            targetPos = defaultSpawnPoint.position + defaultSpawnOffset;
            targetRot = defaultSpawnPoint.rotation;
        }
        else
        {
            targetPos = initialSpawnPosition;
            targetRot = initialSpawnRotation;
        }

        target.Teleport(targetPos, targetRot);

        if (respawnSound != null)
            AudioSource.PlayClipAtPoint(respawnSound, targetPos);

        if (respawnVFX != null)
            Instantiate(respawnVFX, targetPos, targetRot);

        OnPlayerRespawned?.Invoke(target);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
        Vector3 center = transform.position;
        center.y = fallThresholdY;
        Gizmos.DrawWireCube(center, new Vector3(120f, 0.1f, 120f));
    }
}
