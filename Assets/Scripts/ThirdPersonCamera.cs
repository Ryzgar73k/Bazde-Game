using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

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

    [Header("Kamera Sarsıntısı (Trauma)")]
    public float traumaDecay = 1.6f;
    public Vector3 maxShakeOffset = new Vector3(0.32f, 0.24f, 0.14f);
    public float maxShakeRoll = 5.5f;

    [Header("Düşüş İzleme (Fall-Watch)")]
    public bool enableFallWatch = true;
    public float fallWatchMarginAboveThreshold = 4.0f;

    float yaw;
    float pitch = 20f;
    float targetYaw;
    float targetPitch = 20f;
    float initialYaw;
    float initialPitch = 20f;
    bool hasInitialPose;
    Vector3 smoothedTarget;
    float currentDistance;
    float trauma;
    float shakeClock;
    bool isDeathWatchLocked;
    Vector3 lockedCameraPos;
    Transform deathLookTarget;
    Vector3 deathLookFallbackPoint;
    readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>(8);

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

    public void AddTrauma(float amount)
    {
        trauma = Mathf.Clamp01(trauma + amount);
    }

    public void LockDeathWatch(Transform lookTarget, Vector3 fallbackPoint)
    {
        lockedCameraPos = transform.position;
        isDeathWatchLocked = true;
        deathLookTarget = lookTarget;
        deathLookFallbackPoint = fallbackPoint;
    }

    public void UnlockDeathWatch()
    {
        isDeathWatchLocked = false;
        deathLookTarget = null;
    }

    /// <summary>Yeniden doğma sonrası kamerayı oyun başındaki veya belirtilen bakış yönüne döndürür.</summary>
    public void ResetView(float? overrideYaw = null)
    {
        UnlockDeathWatch();
        trauma = 0f;

        if (!hasInitialPose)
        {
            initialYaw = transform.eulerAngles.y;
            initialPitch = pitch;
            hasInitialPose = true;
        }

        yaw = targetYaw = overrideYaw ?? initialYaw;
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
        if (VictoryUI.IsAnyVictoryVisible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        // ESC tuşuyla fareyi serbest bırak / UI üzerine tıklanmıyorsa tıkla geri kilitle
        if (!Application.isMobilePlatform)
        {
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (UnityEngine.InputSystem.Mouse.current != null
                && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame
                && Cursor.lockState != CursorLockMode.Locked
                && !IsPointerOverControlUI())
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    bool IsPointerOverControlUI()
    {
        if (EventSystem.current == null || UnityEngine.InputSystem.Mouse.current == null) return false;
        var ped = new PointerEventData(EventSystem.current)
        {
            position = UnityEngine.InputSystem.Mouse.current.position.ReadValue()
        };
        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(ped, uiRaycastResults);
        foreach (var r in uiRaycastResults)
        {
            if (r.gameObject == null) continue;
            if (r.gameObject.GetComponentInParent<LookPad>() != null) continue;
            return true;
        }
        return false;
    }

    void LateUpdate()
    {
        if (VictoryUI.IsAnyVictoryVisible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        float unscaledDt = Time.unscaledDeltaTime;
        if (trauma > 0f)
        {
            trauma = Mathf.Max(0f, trauma - traumaDecay * unscaledDt);
            shakeClock += unscaledDt * 30f;
        }

        float shakeStrength = trauma * trauma * GameFeelSettings.ShakeStrength;
        Vector3 shakeOffset = shakeStrength > 0.0001f
            ? new Vector3(
                maxShakeOffset.x * shakeStrength * Mathf.Sin(shakeClock * 1.7f),
                maxShakeOffset.y * shakeStrength * Mathf.Sin(shakeClock * 2.3f),
                maxShakeOffset.z * shakeStrength * Mathf.Sin(shakeClock * 1.4f))
            : Vector3.zero;
        float shakeRoll = shakeStrength > 0.0001f
            ? maxShakeRoll * shakeStrength * Mathf.Sin(shakeClock * 1.1f)
            : 0f;

        if (isDeathWatchLocked)
        {
            // Tüketilmemiş bakış girdisini temizle ki yeniden doğuşta kamera sıçramasın
            PlayerInputState.ConsumeLook();

            Vector3 lookTarget = deathLookTarget != null
                ? deathLookTarget.position + Vector3.up * 0.4f
                : deathLookFallbackPoint;
            Vector3 toTarget = lookTarget - lockedCameraPos;
            Quaternion desiredRot = toTarget.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(toTarget.normalized, Vector3.up)
                : transform.rotation;

            float rotFactor = 1f - Mathf.Exp(-8f * unscaledDt);
            Quaternion baseRot = Quaternion.Slerp(transform.rotation, desiredRot, rotFactor);
            Quaternion finalRot = baseRot * Quaternion.Euler(0f, 0f, shakeRoll);
            transform.SetPositionAndRotation(lockedCameraPos + baseRot * shakeOffset, finalRot);
            return;
        }

        if (target == null) return;

        float dt = Time.deltaTime;
        Vector2 look = PlayerInputState.ConsumeLook();
        targetYaw += look.x;
        targetPitch = Mathf.Clamp(targetPitch - look.y, minPitch, maxPitch);

        float lookFactor = lookSmoothing > 0.01f ? (1f - Mathf.Exp(-lookSmoothing * dt)) : 1f;
        yaw = Mathf.LerpAngle(yaw, targetYaw, lookFactor);
        pitch = Mathf.Lerp(pitch, targetPitch, lookFactor);

        Vector3 rawTarget = target.position;
        bool isFallWatching = false;
        float minWatchY = float.NegativeInfinity;
        if (enableFallWatch && RespawnManager.Instance != null && RespawnManager.Instance.autoCheckFall)
        {
            minWatchY = RespawnManager.Instance.fallThresholdY + fallWatchMarginAboveThreshold;
            if (rawTarget.y < minWatchY)
                isFallWatching = true;
        }

        if (lockInstant || (!isFallWatching && (rawTarget - smoothedTarget).sqrMagnitude > 16f))
        {
            smoothedTarget = rawTarget;
            if (isFallWatching)
                smoothedTarget.y = Mathf.Max(smoothedTarget.y, minWatchY);
        }
        else
        {
            float horizSpeed = Mathf.Max(1f, horizontalFollowSpeed);
            float horizFactor = 1f - Mathf.Exp(-horizSpeed * dt);
            float newX = Mathf.Lerp(smoothedTarget.x, rawTarget.x, horizFactor);
            float newZ = Mathf.Lerp(smoothedTarget.z, rawTarget.z, horizFactor);

            float targetY = isFallWatching ? Mathf.Max(rawTarget.y, minWatchY) : rawTarget.y;

            // Küçük engel yükseklik dalgalanmalarını yavaşça sönümle, büyük sıçrama/düşüşlerde hızla yetiş
            float dy = Mathf.Abs(targetY - smoothedTarget.y);
            float vertSpeed = Mathf.Lerp(verticalFollowSpeed, Mathf.Max(verticalFollowSpeed, 22f), Mathf.InverseLerp(0.35f, 1.2f, dy));
            float vertFactor = 1f - Mathf.Exp(-Mathf.Max(1f, vertSpeed) * dt);
            float newY = Mathf.Lerp(smoothedTarget.y, targetY, vertFactor);

            if (!isFallWatching && maxVerticalLag > 0.05f && Mathf.Abs(targetY - newY) > maxVerticalLag)
            {
                newY = targetY - Mathf.Sign(targetY - newY) * maxVerticalLag;
            }
            if (isFallWatching)
                newY = Mathf.Max(newY, minWatchY);

            smoothedTarget = new Vector3(newX, newY, newZ);
        }

        Vector3 pivot = smoothedTarget + Vector3.up * height;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 castDir = -(rot * Vector3.forward);

        float targetDist = ResolveCameraCollision(pivot, castDir, distance);
        float blendSpeed = targetDist < currentDistance ? collisionSnapSpeed : collisionRecoverSpeed;
        float distFactor = 1f - Mathf.Exp(-blendSpeed * dt);
        currentDistance = Mathf.Lerp(currentDistance, targetDist, distFactor);

        Vector3 camPos = pivot + castDir * currentDistance;
        if (isFallWatching)
        {
            Vector3 toFaller = (rawTarget + Vector3.up * 0.4f) - camPos;
            if (toFaller.sqrMagnitude > 0.01f)
            {
                Quaternion lookDownRot = Quaternion.LookRotation(toFaller.normalized, Vector3.up);
                float tiltBlend = Mathf.Clamp01((minWatchY - rawTarget.y) / Mathf.Max(1f, fallWatchMarginAboveThreshold));
                rot = Quaternion.Slerp(rot, lookDownRot, tiltBlend * 0.72f);
            }
        }

        transform.SetPositionAndRotation(camPos + rot * shakeOffset, rot * Quaternion.Euler(0f, 0f, shakeRoll));
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
