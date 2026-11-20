using UnityEngine;

/// <summary>
/// KayKit dönen süpürücüler (swiper), dönen testereler (saw_trap) ve sarkaç çekiçler (hammer_spikes) için
/// genel dönüş/salınım ve temas tepkisi (itme veya checkpoint respawn) bileşeni.
/// </summary>
[DefaultExecutionOrder(-20)]
public class RotatingHazard : MonoBehaviour
{
    public enum MotionType { ContinuousSpin, PendulumSwing }
    public enum HitBehavior { Knockback, InstantRespawn, None }

    [Header("Hareket")]
    public MotionType motionType = MotionType.ContinuousSpin;
    public Vector3 localAxis = Vector3.up;
    [Tooltip("ContinuousSpin: derece/sn. PendulumSwing: salınım frekansı (rad/sn).")]
    public float speed = 65f;
    [Tooltip("PendulumSwing maksimum açısı (derece).")]
    public float swingAngle = 55f;
    public float phaseOffset = 0f;

    [Header("Temas Tepkisi")]
    public HitBehavior hitBehavior = HitBehavior.Knockback;
    public float knockbackSpeed = 7.5f;
    public float knockbackUpSpeed = 3.5f;
    public float hitCooldown = 0.35f;

    private Quaternion baseLocalRotation;
    private float nextHitTime;

    void Awake()
    {
        baseLocalRotation = transform.localRotation;
        // Hareketli/dönen tetikleyicilerin duran CharacterController ile çarpışması için kinematik Rigidbody
        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void Update()
    {
        Vector3 axis = localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.up;
        if (motionType == MotionType.ContinuousSpin)
        {
            transform.Rotate(axis, speed * Time.deltaTime, Space.Self);
        }
        else
        {
            float angle = Mathf.Sin(Time.time * speed + phaseOffset) * swingAngle;
            transform.localRotation = baseLocalRotation * Quaternion.AngleAxis(angle, axis);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        HandleHit(other);
    }

    void OnTriggerStay(Collider other)
    {
        HandleHit(other);
    }

    private void HandleHit(Collider other)
    {
        if (hitBehavior == HitBehavior.None || Time.time < nextHitTime) return;

        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        nextHitTime = Time.time + hitCooldown;

        if (hitBehavior == HitBehavior.InstantRespawn)
        {
            if (RespawnManager.Instance != null)
                RespawnManager.Instance.RespawnPlayer(player);
            else
                player.Teleport(Vector3.zero);
            return;
        }

        // Knockback: oyuncuyu yatayda tuzaktan uzağa ve hafif yukarı fırlat
        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.01f) toPlayer = -player.transform.forward;
        Vector3 pushDir = toPlayer.normalized;

        if (motionType == MotionType.ContinuousSpin)
        {
            Vector3 worldAxis = transform.TransformDirection(localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.up);
            Vector3 tangent = Vector3.Cross(worldAxis * Mathf.Sign(speed), player.transform.position - transform.position);
            tangent.y = 0f;
            float radialDist = toPlayer.magnitude;
            float radialWeight = Mathf.Lerp(0.88f, 0.45f, Mathf.Clamp01(radialDist / 2.2f));
            if (tangent.sqrMagnitude > 0.01f)
                pushDir = (tangent.normalized * (1f - radialWeight) + pushDir * radialWeight).normalized;
        }

        player.ApplyKnockback(pushDir * knockbackSpeed + Vector3.up * knockbackUpSpeed, 0.28f);
    }
}
