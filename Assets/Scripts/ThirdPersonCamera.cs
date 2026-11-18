using UnityEngine;

/// <summary>
/// Basit üçüncü şahıs takip kamerası. Ekranın sağ tarafını sürükleyerek (veya fare sağ tık) döner.
/// </summary>
[DefaultExecutionOrder(50)]
public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    [Header("Kamera Mesafesi")]
    public float distance = 3.2f;      // Daha yakın ve dinamik takip (önceki 5f idi)
    public float height = 1.6f;        // Karakterin göğüs/omuz hizası
    public float minPitch = -20f;
    public float maxPitch = 60f;
    [Header("Takip Yumuşatma (İnterpolasyon)")]
    [Tooltip("Yatay (X/Z) takip yumuşatma hızı")]
    public float horizontalFollowSpeed = 24f;
    [Tooltip("Dikey (Y) takip yumuşatma hızı (ufak engel titreşimlerini emer)")]
    public float verticalFollowSpeed = 9.5f;
    [Tooltip("Büyük zıplama/düşüşte dikey maksimum kamera gecikmesi (metre)")]
    public float maxVerticalLag = 0.85f;
    [Tooltip("Kamera bakış dönüş (Yaw/Pitch) yumuşatma hızı")]
    public float lookSmoothing = 26f;
    [Tooltip("Zıplama ve ani hareketlerde anında kilitlenme (titreme önlemek için kapalı tutulur)")]
    public bool lockInstant = false;

    [HideInInspector] public int configVersion = 0;
    public const int LatestConfigVersion = 2;

    [Header("Engel / Duvar Çarpışması")]
    [Tooltip("Kameranın içinden geçmesini önleyeceği çarpışma katmanları")]
    public LayerMask collisionMask = ~0;
    public float cameraRadius = 0.22f;
    public float minCollisionDistance = 0.6f;
    public float collisionSnapSpeed = 30f;
    public float collisionRecoverSpeed = 12f;

    float yaw;
    float pitch = 20f;
    float targetYaw;
    float targetPitch = 20f;
    float initialYaw;
    float initialPitch = 20f;
    bool hasInitialPose;
    Vector3 smoothedTarget;
    float currentDistance;
    readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    public void ApplyConfigMigration()
    {
        if (configVersion < 2)
        {
            lockInstant = false;
            configVersion = 2;
        }
    }

    void Awake()
    {
        ApplyConfigMigration();
        initialYaw = transform.eulerAngles.y;
        initialPitch = pitch;
        hasInitialPose = true;
    }

    void Start()
    {
        if (target != null) smoothedTarget = target.position;
        yaw = initialYaw;
        targetYaw = yaw;
        targetPitch = pitch;
        currentDistance = distance;

        // PC editöründe fareyi ekranda kilitle (istediğin gibi serbestçe dönmek için)
        if (!Application.isMobilePlatform)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    /// <summary>Yeniden doğma sonrası kamerayı oyun başındaki bakış yönüne ve mesafesine döndürür.</summary>
    public void ResetView()
    {
        if (!hasInitialPose)
        {
            initialYaw = transform.eulerAngles.y;
            initialPitch = pitch;
            hasInitialPose = true;
        }

        yaw = targetYaw = initialYaw;
        pitch = targetPitch = initialPitch;
        if (target != null) smoothedTarget = target.position;
        currentDistance = distance;

        Vector3 pivot = smoothedTarget + Vector3.up * height;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 castDir = -(rot * Vector3.forward);
        currentDistance = ResolveCameraCollision(pivot, castDir, distance);
        transform.SetPositionAndRotation(pivot + castDir * currentDistance, rot);
    }

    void Update()
    {
        // ESC tuşuyla fareyi serbest bırak / tıkla geri kilitle
        if (!Application.isMobilePlatform)
        {
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        float dt = Time.deltaTime;
        Vector2 look = PlayerInputState.Look;
        targetYaw += look.x;
        targetPitch = Mathf.Clamp(targetPitch - look.y, minPitch, maxPitch);

        float lookFactor = lookSmoothing > 0.01f ? (1f - Mathf.Exp(-lookSmoothing * dt)) : 1f;
        yaw = Mathf.LerpAngle(yaw, targetYaw, lookFactor);
        pitch = Mathf.Lerp(pitch, targetPitch, lookFactor);

        Vector3 rawTarget = target.position;
        if (lockInstant || (rawTarget - smoothedTarget).sqrMagnitude > 16f)
        {
            smoothedTarget = rawTarget;
        }
        else
        {
            float horizSpeed = Mathf.Max(1f, horizontalFollowSpeed);
            float horizFactor = 1f - Mathf.Exp(-horizSpeed * dt);
            float newX = Mathf.Lerp(smoothedTarget.x, rawTarget.x, horizFactor);
            float newZ = Mathf.Lerp(smoothedTarget.z, rawTarget.z, horizFactor);

            // Küçük engel yükseklik dalgalanmalarını yavaşça sönümle, büyük sıçrama/düşüşlerde hızla yetiş
            float dy = Mathf.Abs(rawTarget.y - smoothedTarget.y);
            float vertSpeed = Mathf.Lerp(verticalFollowSpeed, Mathf.Max(verticalFollowSpeed, 22f), Mathf.InverseLerp(0.35f, 1.2f, dy));
            float vertFactor = 1f - Mathf.Exp(-Mathf.Max(1f, vertSpeed) * dt);
            float newY = Mathf.Lerp(smoothedTarget.y, rawTarget.y, vertFactor);

            if (maxVerticalLag > 0.05f && Mathf.Abs(rawTarget.y - newY) > maxVerticalLag)
            {
                newY = rawTarget.y - Mathf.Sign(rawTarget.y - newY) * maxVerticalLag;
            }

            smoothedTarget = new Vector3(newX, newY, newZ);
        }

        Vector3 pivot = smoothedTarget + Vector3.up * height;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 castDir = -(rot * Vector3.forward);

        float targetDist = ResolveCameraCollision(pivot, castDir, distance);
        float blendSpeed = targetDist < currentDistance ? collisionSnapSpeed : collisionRecoverSpeed;
        float distFactor = 1f - Mathf.Exp(-blendSpeed * dt);
        currentDistance = Mathf.Lerp(currentDistance, targetDist, distFactor);

        transform.SetPositionAndRotation(pivot + castDir * currentDistance, rot);
    }

    float ResolveCameraCollision(Vector3 pivot, Vector3 castDir, float maxDist)
    {
        int count = Physics.SphereCastNonAlloc(
            pivot,
            cameraRadius,
            castDir,
            hitBuffer,
            maxDist,
            collisionMask,
            QueryTriggerInteraction.Ignore);

        float bestDist = maxDist;
        Transform targetRoot = target != null ? target.root : null;

        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (h.collider == null || h.collider.isTrigger) continue;
            if (target != null && (h.collider.transform == target || h.collider.transform.IsChildOf(target))) continue;
            if (targetRoot != null && h.collider.transform.IsChildOf(targetRoot)) continue;
            if (h.distance <= 0.001f && h.point == Vector3.zero) continue;

            float safeDist = Mathf.Max(minCollisionDistance, h.distance - 0.05f);
            if (safeDist < bestDist)
                bestDist = safeDist;
        }

        return bestDist;
    }
}
