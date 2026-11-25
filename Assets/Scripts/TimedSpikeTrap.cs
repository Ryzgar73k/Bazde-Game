using UnityEngine;

/// <summary>
/// KayKit floor_spikes_trap_* için zamanlı diken tuzağı:
/// Güvenli -> Uyarı (hafif yükselme) -> Aktif (tam açık, temas halinde respawn).
/// </summary>
public class TimedSpikeTrap : MonoBehaviour
{
    [Header("Referanslar")]
    public Transform spikesVisual;
    public Collider dangerTrigger;

    [Header("Zamanlama (saniye)")]
    public float safeDuration = 1.7f;
    public float warningDuration = 0.55f;
    public float activeDuration = 1.35f;
    public float startDelay = 0f;

    [Header("Konumlar (Yerel Y)")]
    public float retractedY = -0.55f;
    public float warningY = -0.32f;
    public float activeY = 0f;

    private float cycleDuration;
    private float lastRespawnTime;

    void Awake()
    {
        if (spikesVisual == null && transform.childCount > 0)
            spikesVisual = transform.GetChild(0);

        cycleDuration = Mathf.Max(0.5f, safeDuration + warningDuration + activeDuration);
        if (dangerTrigger != null)
        {
            dangerTrigger.isTrigger = true;
            // Statik trigger'da duran CharacterController'ı da algılamak için kinematik Rigidbody ekle
            var triggerGO = dangerTrigger.gameObject;
            var rb = triggerGO.GetComponent<Rigidbody>();
            if (rb == null) rb = triggerGO.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Tetikleyici alt nesnedeyse olayları bu bileşene ilet
            if (triggerGO != gameObject)
            {
                var relay = triggerGO.GetComponent<SpikeTriggerRelay>();
                if (relay == null) relay = triggerGO.AddComponent<SpikeTriggerRelay>();
                relay.trap = this;
            }
        }
    }

    void Update()
    {
        float t = Mathf.Repeat(Time.time + startDelay, cycleDuration);
        float targetY;
        bool isDanger = false;

        if (t < safeDuration)
        {
            targetY = retractedY;
        }
        else if (t < safeDuration + warningDuration)
        {
            float w = (t - safeDuration) / Mathf.Max(0.01f, warningDuration);
            targetY = Mathf.Lerp(retractedY, warningY, w);
        }
        else
        {
            float a = (t - safeDuration - warningDuration) / Mathf.Max(0.01f, activeDuration);
            targetY = a < 0.12f ? Mathf.Lerp(warningY, activeY, a / 0.12f) : activeY;
            isDanger = true;
        }

        if (spikesVisual != null)
        {
            Vector3 lp = spikesVisual.localPosition;
            lp.y = Mathf.MoveTowards(lp.y, targetY, 8f * Time.deltaTime);
            spikesVisual.localPosition = lp;
        }

        if (dangerTrigger != null && dangerTrigger.enabled != isDanger)
            dangerTrigger.enabled = isDanger;
    }

    void OnTriggerEnter(Collider other)
    {
        TryTriggerTrap(other);
    }

    void OnTriggerStay(Collider other)
    {
        TryTriggerTrap(other);
    }

    internal void TryTriggerTrap(Collider other)
    {
        if (dangerTrigger != null && !dangerTrigger.enabled) return;
        if (Time.time - lastRespawnTime < 0.35f) return;

        var player = other.GetComponentInParent<PlayerController>();
        if (player == null || player.IsSpawnInvulnerable) return;
        if (RespawnManager.Instance != null && RespawnManager.Instance.IsRespawning) return;

        lastRespawnTime = Time.time;
        if (RespawnManager.Instance != null)
            RespawnManager.Instance.RespawnPlayer(player, DeathCause.SpikeTrap, transform.position);
        else
            player.Teleport(Vector3.zero);
    }
}

public class SpikeTriggerRelay : MonoBehaviour
{
    public TimedSpikeTrap trap;

    void OnTriggerEnter(Collider other)
    {
        if (trap != null) trap.TryTriggerTrap(other);
    }

    void OnTriggerStay(Collider other)
    {
        if (trap != null) trap.TryTriggerTrap(other);
    }
}
