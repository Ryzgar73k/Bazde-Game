using UnityEngine;

/// <summary>
/// Boşluğa veya tehlikeli tuzaklara yerleştirilen öldürücü alan (Trigger Collider).
/// Oyuncu bu alana temas ettiğinde RespawnManager üzerinden yeniden doğar.
/// </summary>
[RequireComponent(typeof(Collider))]
public class KillZone : MonoBehaviour
{
    public string playerTag = "Player";
    public AudioClip deathSound;
    public GameObject deathVFX;

    [Header("Editör Görünümü")]
    public Color gizmoColor = new Color(1f, 0.1f, 0.1f, 0.4f);

    private Collider zoneCollider;

    void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        if (zoneCollider != null)
            zoneCollider.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null && other.CompareTag(playerTag))
            player = Object.FindAnyObjectByType<PlayerController>();

        if (player != null)
        {
            if (RespawnManager.Instance != null)
            {
                if (RespawnManager.Instance.IsRespawning || player.IsSpawnInvulnerable)
                    return;

                RespawnManager.Instance.RespawnPlayer(
                    player,
                    DeathCause.KillZone,
                    transform.position,
                    deathSound,
                    deathVFX);
            }
            else
            {
                if (deathSound != null)
                    AudioSource.PlayClipAtPoint(deathSound, other.transform.position);

                if (deathVFX != null)
                    Instantiate(deathVFX, other.transform.position, Quaternion.identity);

                player.Teleport(Vector3.zero);
            }
        }
    }

    void OnDrawGizmos()
    {
        if (zoneCollider == null) zoneCollider = GetComponent<Collider>();
        if (zoneCollider == null) return;

        Gizmos.color = gizmoColor;
        Gizmos.matrix = transform.localToWorldMatrix;

        if (zoneCollider is BoxCollider box)
        {
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (zoneCollider is SphereCollider sphere)
        {
            Gizmos.DrawSphere(sphere.center, sphere.radius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }
    }
}
