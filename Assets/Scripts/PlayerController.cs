using System.Collections;
using UnityEngine;

/// <summary>
/// CharacterController tabanlı yürüme / koşma / zıplama.
/// Hareket kameraya göre yapılır. Coyote time ve jump buffer içerir (parkur hissiyatı için).
/// </summary>
[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Hareket")]
    public float walkSpeed = 2.1f;
    public float runSpeed = 4.5f;
    public float acceleration = 28f;
    public float deceleration = 32f;
    [Tooltip("Ters yöne dönerken uygulanan ivme çarpanı")]
    public float turnAccelerationMultiplier = 1.35f;
    [Range(0f, 1f)]
    public float airControl = 0.85f; // Havada ileri/yön kontrolü korunsun
    [Tooltip("Havada yön tuşları bırakıldığında uygulanan hava direnci")]
    public float airReleaseDrag = 9.0f;
    public float turnSpeed = 390f;

    [Header("Zıplama")]
    public float jumpHeight = 1.70f;
    [Tooltip("Zıplarken ileri hareket halindeysen eklenecek ekstra hız (0 = ekstra boost yok)")]
    public float jumpForwardBoost = 0f;
    public float gravity = -26f;
    [Tooltip("Zıplama tuşu erken bırakıldığında yükseliş sırasında uygulanan yerçekimi çarpanı (kısa basış = alçak zıplama)")]
    [Range(1f, 6f)]
    public float lowJumpGravityMultiplier = 2.6f;
    [Tooltip("Düşüş sırasında uygulanan yerçekimi çarpanı (havada süzülme yerine tok iniş)")]
    [Range(1f, 2.5f)]
    public float fallGravityMultiplier = 1.35f;
    [Tooltip("Trambolinden fırlarken yatay hız tavanı (platformu aşmayı önler)")]
    public float trampolineMaxHorizSpeed = 4.3f;
    public float coyoteTime = 0.16f;
    public float jumpBufferTime = 0.16f;
    [Tooltip("Zıplamada root motion kapsülü sürüklesin mi? Kapalı tutarak yerinde zıplama ve havada kontrol korunur.")]
    public bool useRootMotionInJump = false;

    [Header("Kenara Tutunma (Auto Mantle)")]
    public bool enableAutoMantle = true;
    public float mantleMinHeight = 0.22f;
    public float mantleMaxHeight = 1.05f;
    public float mantleDuration = 0.54f;

    [Header("Basamak ve Eğim (Zıplamadan Aşma)")]
    [Tooltip("Karakterin zıplamadan yürüyerek çıkabileceği maksimum basamak/engel yüksekliği (metre)")]
    [Range(0.05f, 1.8f)]
    public float maxStepHeight = 0.24f;
    [Tooltip("Karakterin kaymadan yürüyerek çıkabileceği maksimum rampa/eğim açısı (derece)")]
    [Range(15f, 80f)]
    public float maxSlopeAngle = 55f;
    [Tooltip("Kapsül kenarına veya dik yüzeye takıldığında yana/aşağı kayma hızı (havada asılı kalmayı önler)")]
    public float edgeSlideSpeed = 3.6f;

    [Header("Referans")]
    [Tooltip("Hareket yönü bu kameraya göre belirlenir. Boşsa Camera.main kullanılır.")]
    public Transform cameraTransform;
    [Tooltip("Opsiyonel: Model üzerindeki Animator")]
    public Animator animator;

    [Tooltip("Zıplama animasyonunun ileri hareketinin çarpanı (useRootMotionInJump açıksa)")]
    public float rootMotionJumpScale = 1f;

    [Header("Kontrol Durumu")]
    [Tooltip("Karakterin oyuncu tarafından kontrol edilip edilemeyeceğini belirler (bitiş ekranında kapatılır)")]
    public bool canControl = true;

    public const int LatestConfigVersion = 10;

    [HideInInspector] public int configVersion = 0;

    CharacterController cc;
    RootMotionRelay rootRelay;
    Vector3 horizontalVelocity;
    float verticalVelocity;
    float airHorizSpeedCap = -1f;
    float coyoteCounter;
    float jumpBufferCounter;
    float airborneTimer;
    bool effectiveGrounded;
    bool isJumpOrFallAirborne;
    bool canCutJump;
    bool wasAirborneForAnim;
    float airborneAnimDuration;
    float minAirborneVy;
    bool isMantling;
    float mantleElapsed;
    bool isMantleHandIKActive;
    float mantleActiveDuration;
    Vector3 mantleStartPos;
    Vector3 mantleMidPos;
    Vector3 mantleEndPos;
    Vector3 mantleLeftHandPos;
    Vector3 mantleRightHandPos;
    Vector3 mantleWallNormal;
    Quaternion mantleTargetRot;
    bool hasDirectionalInputThisFrame;
    Vector3 lastWishDir;
    bool isSlippingOffEdge;
    Vector3 edgeSlipDir;
    bool hasGroundContact;
    Vector3 bestGroundNormal;
    Vector3 bestGroundPoint;
    float bestGroundNormalY;
    readonly Collider[] obstacleOverlapBuffer = new Collider[16];
    readonly Collider[] mantleOverlapBuffer = new Collider[12];
    readonly RaycastHit[] footholdHitBuffer = new RaycastHit[16];
    readonly RaycastHit[] mantleHitBuffer = new RaycastHit[12];

    // Animator parametre ID'leri (performans ve güvenlik için)
    static readonly int SpeedParam = Animator.StringToHash("Speed");
    static readonly int AnimSpeedMultiplierParam = Animator.StringToHash("AnimSpeedMultiplier");
    static readonly int BounceSpeedParam = Animator.StringToHash("BounceSpeed");
    static readonly int IsGroundedParam = Animator.StringToHash("IsGrounded");
    static readonly int VerticalVelocityParam = Animator.StringToHash("VerticalVelocity");
    static readonly int JumpTrigger = Animator.StringToHash("Jump");
    static readonly int BounceTrigger = Animator.StringToHash("Bounce");
    static readonly int LandTrigger = Animator.StringToHash("Land");
    static readonly int MantleTrigger = Animator.StringToHash("Mantle");
    static readonly int JumpVariantParam = Animator.StringToHash("JumpVariant");
    static readonly int BounceVariantParam = Animator.StringToHash("BounceVariant");
    static readonly int JumpStateHash = Animator.StringToHash("Jump");
    static readonly int JumpUpStateHash = Animator.StringToHash("JumpUp");

    RuntimeAnimatorController cachedController;
    int cachedParamCount = -1;
    bool hasSpeedParam;
    bool hasAnimSpeedMultiplierParam;
    bool hasBounceSpeedParam;
    bool hasIsGroundedParam;
    bool hasVerticalVelocityParam;
    bool hasJumpParam;
    bool hasBounceParam;
    bool hasLandParam;
    bool hasMantleParam;
    bool hasJumpVariantParam;
    bool hasBounceVariantParam;

    public bool IsGrounded => effectiveGrounded;
    public float VerticalVelocity => verticalVelocity;
    public bool IsRunning { get; private set; }
    public bool IsMantling => isMantling;
    public bool IsMantleHandIKActive => isMantling && isMantleHandIKActive;
    public float MantleProgress => isMantling && mantleActiveDuration > 0.001f ? Mathf.Clamp01(mantleElapsed / mantleActiveDuration) : 0f;
    public Vector3 MantleLeftHandPos => mantleLeftHandPos;
    public Vector3 MantleRightHandPos => mantleRightHandPos;
    public Vector3 MantleWallNormal => mantleWallNormal;
    public float SpawnGraceRemaining => spawnGraceTimer;
    public bool IsSpawnInvulnerable => spawnGraceTimer > 0f;

    public void ApplyConfigMigration()
    {
        if (cc == null) cc = GetComponent<CharacterController>();
        if (configVersion < 2)
        {
            if (Mathf.Approximately(jumpHeight, 2.6f)) jumpHeight = 1.45f;
            if (Mathf.Approximately(jumpForwardBoost, 4f)) jumpForwardBoost = 0f;
            if (turnSpeed < 720f) turnSpeed = 900f;
            configVersion = 2;
        }
        if (configVersion < 3)
        {
            if (walkSpeed <= 3.1f) walkSpeed = 4.2f;
            if (runSpeed <= 4.6f) runSpeed = 7.2f;
            configVersion = 3;
        }
        if (configVersion < 4)
        {
            if (cc != null && (cc.radius > 0.22f || cc.height > 1.35f))
            {
                float bottomLocalY = cc.center.y - (cc.height * 0.5f) - cc.skinWidth;
                cc.radius = 0.16f;
                cc.height = 1.08f;
                cc.skinWidth = 0.025f;
                cc.center = new Vector3(cc.center.x, bottomLocalY + (cc.height * 0.5f) + cc.skinWidth, cc.center.z);
            }
            configVersion = 4;
        }
        if (configVersion < 5)
        {
            if (Mathf.Approximately(maxStepHeight, 0.45f)) maxStepHeight = 0.24f;
            configVersion = 5;
        }
        if (configVersion < 6)
        {
            walkSpeed = 4.0f;
            runSpeed = 7.2f;
            acceleration = 45f;
            deceleration = 38f;
            turnAccelerationMultiplier = 1.7f;
            jumpHeight = 1.70f;
            fallGravityMultiplier = 1.35f;
            airReleaseDrag = 9.0f;
            coyoteTime = 0.16f;
            jumpBufferTime = 0.16f;
            trampolineMaxHorizSpeed = 5.2f;
            configVersion = 6;
        }
        if (configVersion < 7)
        {
            mantleDuration = 0.42f;
            configVersion = 7;
        }
        if (configVersion < 8)
        {
            mantleMaxHeight = 1.05f;
            mantleDuration = 0.54f;
            configVersion = 8;
        }
        if (configVersion < 9)
        {
            walkSpeed = 2.5f;
            runSpeed = 5.6f;
            trampolineMaxHorizSpeed = 5.0f;
            configVersion = 9;
        }
        if (configVersion < 10)
        {
            walkSpeed = 2.1f;
            runSpeed = 4.5f;
            acceleration = 28f;
            deceleration = 32f;
            turnAccelerationMultiplier = 1.35f;
            turnSpeed = 390f;
            trampolineMaxHorizSpeed = 4.3f;
            configVersion = 10;
        }
    }

    /// <summary>
    /// CharacterController kapsülünün en alt noktasını dünya koordinatında döndürür (skinWidth dahil).
    /// Döner silindir/çark hesaplamaları ve Foot IK tabanı bu noktayı kullanır.
    /// </summary>
    public Vector3 GetWorldFootPosition()
    {
        if (cc == null) return transform.position;
        float bottomLocalY = cc.center.y - (cc.height * 0.5f) - cc.skinWidth;
        return transform.TransformPoint(new Vector3(cc.center.x, bottomLocalY, cc.center.z));
    }

    void AlignVisualModelToCapsule()
    {
        if (animator == null || cc == null || animator.transform.parent != transform) return;
        float bottomLocalY = cc.center.y - (cc.height * 0.5f) - cc.skinWidth;
        Vector3 lp = animator.transform.localPosition;
        if (Mathf.Abs(lp.y - bottomLocalY) > 0.005f)
        {
            lp.y = bottomLocalY;
            animator.transform.localPosition = lp;
        }
    }

    [Header("Düşme / Yeniden Doğma")]
    [Tooltip("Karakterin ayak Y konumu bunun altına düşerse (RespawnManager yoksa) başlangıç noktasına döner")]
    public float killY = -1.5f;
    Vector3 spawnPos;
    Quaternion spawnRot;
    float knockbackTimer;
    float spawnGraceTimer;
    Transform visualRoot;
    Vector3 visualBaseScale = Vector3.one;
    Quaternion visualBaseLocalRot = Quaternion.identity;
    Renderer[] cachedRenderers;
    Coroutine visualAnimRoutine;

    /// <summary>
    /// Yatay/dikey hızları, zıplama ve platform/engel takip durumlarını sıfırlar.
    /// </summary>
    public void ResetVelocity()
    {
        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
        airHorizSpeedCap = -1f;
        coyoteCounter = 0f;
        jumpBufferCounter = 0f;
        airborneTimer = 0f;
        knockbackTimer = 0f;
        isMantling = false;
        isMantleHandIKActive = false;
        mantleElapsed = 0f;
        mantleActiveDuration = 0f;
        effectiveGrounded = false;
        isJumpOrFallAirborne = false;
        wasAirborneForAnim = false;
        airborneAnimDuration = 0f;
        minAirborneVy = 0f;
        isSlippingOffEdge = false;
        edgeSlipDir = Vector3.zero;
        canCutJump = false;
        activeWheelRotator = null;
        activeCylinder = null;
        activeMovingPlatform = null;
        lastWalkableNormal = Vector3.up;
        hadWalkableNormalLastFrame = false;
        if (cc != null && !cc.enabled) cc.enabled = true;
        if (rootRelay != null) rootRelay.Consume();
        if (animator != null)
        {
            if (hasJumpParam) animator.ResetTrigger(JumpTrigger);
            if (hasBounceParam) animator.ResetTrigger(BounceTrigger);
            if (hasLandParam) animator.ResetTrigger(LandTrigger);
            if (hasMantleParam) animator.ResetTrigger(MantleTrigger);
        }
    }

    /// <summary>
    /// CharacterController'ı güvenli biçimde devre dışı bırakıp oyuncuyu yeni konuma/rotasyona ışınlar.
    /// </summary>
    public void Teleport(Vector3 newPosition, Quaternion? newRotation = null, bool alignCameraToRotation = true)
    {
        if (visualAnimRoutine != null)
        {
            StopCoroutine(visualAnimRoutine);
            visualAnimRoutine = null;
        }
        if (visualRoot != null)
        {
            visualRoot.localScale = visualBaseScale;
            visualRoot.localRotation = visualBaseLocalRot;
        }
        SetVisualVisible(true);

        if (cc == null) cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        transform.position = newPosition;
        if (newRotation.HasValue)
            transform.rotation = newRotation.Value;

        if (cc != null) cc.enabled = true;
        ResetVelocity();
        Physics.SyncTransforms();

        var camFollow = cameraTransform != null ? cameraTransform.GetComponent<ThirdPersonCamera>() : null;
        if (camFollow != null)
            camFollow.ResetView(alignCameraToRotation && newRotation.HasValue ? newRotation.Value.eulerAngles.y : (float?)null);
    }

    public void SetVisualVisible(bool visible)
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
            cachedRenderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
                cachedRenderers[i].enabled = visible;
        }

        if (visible && visualAnimRoutine == null && visualRoot != null)
        {
            visualRoot.localScale = visualBaseScale;
            visualRoot.localRotation = visualBaseLocalRot;
        }
    }

    public void StartSpawnGrace(float duration = 0.6f)
    {
        spawnGraceTimer = Mathf.Max(spawnGraceTimer, duration);
    }

    public void PlayDeathTumble(Vector3 awayDir, float duration = 0.28f)
    {
        if (visualRoot == null) return;
        if (visualAnimRoutine != null) StopCoroutine(visualAnimRoutine);
        visualAnimRoutine = StartCoroutine(DeathTumbleRoutine(awayDir, duration));
    }

    public void PlayRespawnPop(float duration = 0.20f, float controlLock = 0.14f)
    {
        knockbackTimer = Mathf.Max(knockbackTimer, controlLock);
        if (visualRoot == null) return;
        if (visualAnimRoutine != null) StopCoroutine(visualAnimRoutine);
        visualAnimRoutine = StartCoroutine(RespawnPopRoutine(duration));
    }

    IEnumerator DeathTumbleRoutine(Vector3 awayDir, float duration)
    {
        Vector3 localDir = awayDir.sqrMagnitude > 0.001f
            ? transform.InverseTransformDirection(awayDir.normalized)
            : Vector3.back;
        Vector3 localTiltAxis = Vector3.Cross(Vector3.up, localDir);
        if (localTiltAxis.sqrMagnitude < 0.001f) localTiltAxis = Vector3.right;
        localTiltAxis.Normalize();

        Quaternion endRot = visualBaseLocalRot * Quaternion.AngleAxis(78f, localTiltAxis);
        Vector3 squashScale = new Vector3(visualBaseScale.x * 1.12f, visualBaseScale.y * 0.85f, visualBaseScale.z * 1.12f);

        float dur = Mathf.Max(0.05f, duration);
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float easeOut = 1f - Mathf.Pow(1f - t, 2.2f);
            visualRoot.localRotation = Quaternion.Slerp(visualBaseLocalRot, endRot, easeOut);
            visualRoot.localScale = Vector3.Lerp(visualBaseScale, squashScale, easeOut);
            yield return null;
        }

        visualRoot.localRotation = endRot;
        visualAnimRoutine = null;
    }

    IEnumerator RespawnPopRoutine(float duration)
    {
        Vector3 startScale = new Vector3(
            visualBaseScale.x * 1.34f,
            visualBaseScale.y * 0.66f,
            visualBaseScale.z * 1.34f);

        visualRoot.localRotation = visualBaseLocalRot;
        visualRoot.localScale = startScale;

        float dur = Mathf.Max(0.05f, duration);
        float elapsed = 0f;
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float u = t - 1f;
            float easeOutBack = 1f + c3 * u * u * u + c1 * u * u;
            visualRoot.localScale = Vector3.LerpUnclamped(startScale, visualBaseScale, easeOutBack);
            yield return null;
        }

        visualRoot.localScale = visualBaseScale;
        visualRoot.localRotation = visualBaseLocalRot;
        visualAnimRoutine = null;
    }

    /// <summary>
    /// Tuzak veya süpürücü engellerden gelen temiz itme/savurma darbesi uygular.
    /// </summary>
    public void ApplyKnockback(Vector3 knockVelocity, float controlLockDuration = 0.22f)
    {
        isMantling = false;
        isMantleHandIKActive = false;
        if (cc != null && !cc.enabled) cc.enabled = true;
        horizontalVelocity = new Vector3(knockVelocity.x, 0f, knockVelocity.z);
        airHorizSpeedCap = -1f;
        if (knockVelocity.y > 0.01f)
        {
            verticalVelocity = Mathf.Max(verticalVelocity, knockVelocity.y);
            effectiveGrounded = false;
            if (knockVelocity.y > 4.5f)
                isJumpOrFallAirborne = true;
        }
        if (animator != null && hasJumpParam)
            animator.ResetTrigger(JumpTrigger);
        canCutJump = false;
        activeWheelRotator = null;
        activeCylinder = null;
        activeMovingPlatform = null;
        knockbackTimer = Mathf.Max(knockbackTimer, controlLockDuration);
    }

    void Respawn()
    {
        Teleport(spawnPos, spawnRot, alignCameraToRotation: false);
    }

    void Awake()
    {
        spawnPos = transform.position;
        spawnRot = transform.rotation;
        cc = GetComponent<CharacterController>();
        ApplyConfigMigration();
        ApplyStepSettings(true);
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        if (animator == null) animator = GetComponentInChildren<Animator>();

        // Animator root motion'ı modeli doğrudan oynatmaz; gerektiğinde RootMotionRelay üzerinden alınır
        if (animator != null)
        {
            animator.applyRootMotion = false;
            rootRelay = animator.GetComponent<RootMotionRelay>();
            if (rootRelay == null) rootRelay = animator.gameObject.AddComponent<RootMotionRelay>();
            if (animator.GetComponent<FootIK>() == null) animator.gameObject.AddComponent<FootIK>();
            AlignVisualModelToCapsule();
            EnsureAnimatorParamsCached();
        }

        visualRoot = animator != null ? animator.transform : (transform.childCount > 0 ? transform.GetChild(0) : null);
        if (visualRoot != null)
        {
            visualBaseScale = visualRoot.localScale;
            visualBaseLocalRot = visualRoot.localRotation;
        }
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
    }

    void OnValidate()
    {
        if (cc == null) cc = GetComponent<CharacterController>();
        ApplyStepSettings(true);
    }

    void ApplyStepSettings(bool allowStep)
    {
        if (cc == null) return;
        // Unity kuralı: stepOffset değeri CharacterController.height değerinden küçük olmalıdır.
        // Havadayken stepOffset=0 tutulur; aksi halde engel kenarına sürtünce kapsül havada yukarı zıplar.
        float clampedStep = Mathf.Clamp(maxStepHeight, 0.01f, Mathf.Max(0.1f, cc.height - 0.05f));
        cc.stepOffset = allowStep ? clampedStep : 0f;
        cc.slopeLimit = Mathf.Clamp(maxSlopeAngle, 15f, 85f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (spawnGraceTimer > 0f) spawnGraceTimer = Mathf.Max(0f, spawnGraceTimer - dt);
        if (RespawnManager.Instance != null && RespawnManager.Instance.IsRespawning) return;
        if (RespawnManager.Instance == null && GetWorldFootPosition().y < killY) { Respawn(); return; }
        if (knockbackTimer > 0f) knockbackTimer -= dt;
        // Dönen çark/silindirler -20 sırasında döndürüldüğü için PhysX çarpışma/ışın sorgularını kare başında senkronize et
        Physics.SyncTransforms();

        if (isMantling)
        {
            UpdateMantle(dt);
            return;
        }

        bool grounded = effectiveGrounded;
        ApplyStepSettings(grounded && !isJumpOrFallAirborne && verticalVelocity <= 0.1f);

        bool inputEnabled = canControl && knockbackTimer <= 0f;

        // --- Zıplama zamanlayıcıları ---
        coyoteCounter = grounded ? coyoteTime : coyoteCounter - dt;
        if (inputEnabled && PlayerInputState.JumpPressed) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= dt;

        // --- Yatay hareket ---
        Vector2 input = inputEnabled ? PlayerInputState.Move : Vector2.zero;
        bool hasMoveInput = input.sqrMagnitude > 0.01f;
        hasDirectionalInputThisFrame = hasMoveInput;
        Vector3 forward = Vector3.forward, right = Vector3.right;
        if (cameraTransform != null)
        {
            forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        }
        Vector3 wishDir = forward * input.y + right * input.x;
        Vector3 wishNormalized = wishDir.sqrMagnitude > 0.0001f ? wishDir.normalized : Vector3.zero;
        lastWishDir = hasMoveInput ? wishNormalized : Vector3.zero;

        // Karakteri hareket yönüne döndür ve ters yöne dönerken yerinde koşmayı engelle
        float alignment = 1f;
        if (wishNormalized.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(wishNormalized, Vector3.up);
            float rotRate = Mathf.Clamp(turnSpeed / 60f, 3.0f, 20f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-rotRate * dt));
            float dot = Vector3.Dot(transform.forward, wishNormalized);
            alignment = Mathf.Clamp01((dot + 0.3f) / 1.3f);
        }

        float targetSpeed = 0f;
        if (hasMoveInput)
        {
            if (PlayerInputState.IsAnalogStickMove)
            {
                float mag = Mathf.Clamp01(input.magnitude);
                if (mag <= 0.55f)
                {
                    targetSpeed = walkSpeed * (mag / 0.55f);
                    IsRunning = false;
                }
                else
                {
                    targetSpeed = Mathf.Lerp(walkSpeed, runSpeed, (mag - 0.55f) / 0.45f);
                    IsRunning = mag >= 0.65f;
                }
            }
            else
            {
                IsRunning = inputEnabled && PlayerInputState.Run;
                targetSpeed = IsRunning ? runSpeed : walkSpeed;
            }
        }
        else
        {
            IsRunning = false;
        }
        Vector3 targetVelocity = wishNormalized * (targetSpeed * (grounded ? alignment : 1f));

        if (grounded)
        {
            airHorizSpeedCap = -1f;
            if (hasMoveInput)
            {
                bool isReversing = horizontalVelocity.sqrMagnitude > 0.25f
                    && Vector3.Dot(horizontalVelocity.normalized, wishNormalized) < -0.15f;
                float accelRate = isReversing ? acceleration * turnAccelerationMultiplier : acceleration;
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, accelRate * dt);
            }
            else
            {
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, deceleration * dt);
                if (horizontalVelocity.sqrMagnitude < 0.0025f)
                    horizontalVelocity = Vector3.zero;
            }
        }
        else
        {
            // Havada: Yön veriliyorsa hızı koru ve yönlendir; tuşlar bırakıldığında dengeli hava direnci uygula
            if (hasMoveInput)
            {
                float cappedTarget = airHorizSpeedCap > 0f ? Mathf.Min(targetSpeed, airHorizSpeedCap) : targetSpeed;
                float currentSpeed = Mathf.Max(horizontalVelocity.magnitude, cappedTarget);
                if (airHorizSpeedCap > 0f) currentSpeed = Mathf.Min(currentSpeed, airHorizSpeedCap);
                Vector3 airTarget = wishNormalized * currentSpeed;
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, airTarget, acceleration * airControl * dt);
            }
            else
            {
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, airReleaseDrag * dt);
            }
        }

        // --- Dikey hareket ---
        if (grounded && verticalVelocity < 0f)
        {
            verticalVelocity = -1.2f; // zemine yapışık tutarken tek karede düşüş eşiğine sokma
            canCutJump = false;
        }

        bool jumpedThisFrame = false;
        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            jumpedThisFrame = true;
            canCutJump = true;
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            if (hasMoveInput)
            {
                if (jumpForwardBoost > 0.001f)
                {
                    horizontalVelocity = wishNormalized * Mathf.Max(horizontalVelocity.magnitude + jumpForwardBoost, runSpeed);
                }
            }
            else if (horizontalVelocity.sqrMagnitude < 0.25f)
            {
                // Neredeyse duruyorken dikey zıplama temiz kalsın; koşarken 1 kare erken tuş bırakmada hız sıfırlanmasın
                horizontalVelocity *= 0.35f;
            }

            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
            airborneTimer = 0.2f;
            effectiveGrounded = false;
            isJumpOrFallAirborne = true;
            activeWheelRotator = null;
            activeCylinder = null;
            activeMovingPlatform = null;
            if (animator != null)
            {
                EnsureAnimatorParamsCached();
                SyncGroundedAndVerticalVelocity(false);
                bool useRunJump = hasMoveInput && horizontalVelocity.magnitude >= walkSpeed + 0.6f;
                if (hasJumpVariantParam) animator.SetInteger(JumpVariantParam, useRunJump ? 1 : 0);
                if (hasLandParam) animator.ResetTrigger(LandTrigger);
                if (hasJumpParam) animator.SetTrigger(JumpTrigger);
            }
        }

        // Kısa zıplama + tok düşüş eğrisi
        float gravMult = 1f;
        if (canCutJump && verticalVelocity > 0f && (!inputEnabled || !PlayerInputState.JumpHeld))
            gravMult = Mathf.Max(1f, lowJumpGravityMultiplier);
        else if (verticalVelocity < 0f)
            gravMult = Mathf.Max(1f, fallGravityMultiplier);

        verticalVelocity += gravity * gravMult * dt;
        if (verticalVelocity <= 0f) canCutJump = false;

        // Zıplama animasyonu root motion verisini her kare tüket; varsayılan olarak kapsül fiziği kullanılır
        Vector3 rootDelta = rootRelay != null ? rootRelay.Consume() : Vector3.zero;
        bool inJumpAnim = useRootMotionInJump && animator != null &&
            (animator.GetCurrentAnimatorStateInfo(0).IsName("Jump")
             || animator.GetCurrentAnimatorStateInfo(0).IsName("JumpUp")
             || animator.GetNextAnimatorStateInfo(0).IsName("Jump")
             || animator.GetNextAnimatorStateInfo(0).IsName("JumpUp"));

        Vector3 move;
        if (inJumpAnim && !grounded && hasMoveInput)
        {
            Vector3 flat = Vector3.ProjectOnPlane(rootDelta, Vector3.up) * rootMotionJumpScale;
            move = flat + Vector3.up * verticalVelocity * dt;
        }
        else if (grounded && !jumpedThisFrame && hadWalkableNormalLastFrame && horizontalVelocity.sqrMagnitude > 0.001f)
        {
            // Yokuş/kavisli silindir inişlerinde yerden kopup sekmemek için hızı yüzey eğimine yansıt
            Vector3 slopeMotion = Vector3.ProjectOnPlane(horizontalVelocity, lastWalkableNormal);
            if (slopeMotion.y < -0.001f)
            {
                slopeMotion = slopeMotion.normalized * horizontalVelocity.magnitude;
                move = new Vector3(slopeMotion.x, Mathf.Min(slopeMotion.y, verticalVelocity), slopeMotion.z) * dt;
            }
            else
            {
                move = (horizontalVelocity + Vector3.up * verticalVelocity) * dt;
            }
        }
        else
        {
            move = (horizontalVelocity + Vector3.up * verticalVelocity) * dt;
        }

        // 1. Eğer oyuncu hareketli platform, dönen çark veya döner silindirin üstündeyse taşı/sürükle
        if (grounded && activeMovingPlatform != null && activeMovingPlatform.isActiveAndEnabled)
        {
            move += activeMovingPlatform.FrameDelta;
        }

        Vector3 footPos = GetWorldFootPosition();
        if (grounded && activeWheelRotator != null)
        {
            move += activeWheelRotator.CalculatePlayerDisplacement(footPos, dt);
        }
        else if (grounded && activeCylinder != null)
        {
            move += activeCylinder.CalculatePlayerDisplacement(footPos, dt);
        }

        if (isSlippingOffEdge && edgeSlipDir.sqrMagnitude > 0.001f)
        {
            // Havada eşiğe/paha takılı kalındığında kenara doğru basılan ileri hızı iptal edip aşağı kaymayı serbest bırak
            float intoEdge = Vector3.Dot(horizontalVelocity, -edgeSlipDir);
            if (intoEdge > 0f)
            {
                horizontalVelocity += edgeSlipDir * intoEdge;
                move += edgeSlipDir * (intoEdge * dt);
            }
            move += edgeSlipDir * (edgeSlideSpeed * dt);
        }

        // Her karede yeni çarpışma kontrolü için sıfırla (cc.Move içinde OnControllerColliderHit tekrar doldurur)
        activeWheelRotator = null;
        activeCylinder = null;
        activeMovingPlatform = null;
        hasGroundContact = false;
        bestGroundNormalY = -1f;
        bestGroundNormal = Vector3.up;
        bestGroundPoint = transform.position;

        bool allowStepDownSnap = grounded && !jumpedThisFrame && verticalVelocity <= 0.1f;

        cc.Move(move);

        if (allowStepDownSnap && !cc.isGrounded && verticalVelocity <= 0.1f)
        {
            TrySnapDownToStep();
        }

        // Dönen engellerin yandan karaktere çarptığında içinden geçmeyip itmesini sağla
        ResolveRotatingObstaclePushes(dt);

        // Kapsül kenarıyla boşlukta veya dik eğimde asılı kalmayı engellemek için ayak altı gerçek zemin desteğini doğrula
        UpdateGroundedAndSlipState(dt);

        // Ucu ucuna kenarda kalındığında otomatik kenar tırmanışı (statik platformlar için)
        if (!effectiveGrounded && enableAutoMantle && inputEnabled && hasMoveInput
            && verticalVelocity <= 2.5f && verticalVelocity > -14f && airborneTimer >= 0.06f)
        {
            if (TryStartAutoMantle(GetWorldFootPosition(), wishNormalized))
                return;
        }

        float minWalkableNormalY = Mathf.Cos(Mathf.Clamp(maxSlopeAngle, 15f, 85f) * Mathf.Deg2Rad);
        if (effectiveGrounded && hasGroundContact && bestGroundNormalY >= minWalkableNormalY)
        {
            lastWalkableNormal = bestGroundNormal;
            hadWalkableNormalLastFrame = true;
        }
        else if (!effectiveGrounded)
        {
            lastWalkableNormal = Vector3.up;
            hadWalkableNormalLastFrame = false;
        }

        const float minFallAnimClearance = 1.15f;
        if (!effectiveGrounded && !isJumpOrFallAirborne
            && airborneTimer >= 0.24f && verticalVelocity < -3.5f
            && !HasWalkableGroundBelow(minFallAnimClearance))
        {
            isJumpOrFallAirborne = true;
        }

        // Animator parametrelerini güncelle
        if (animator != null)
        {
            EnsureAnimatorParamsCached();
            float speedMag = horizontalVelocity.magnitude;
            if (hasSpeedParam)
            {
                if (speedMag < 0.02f && animator.GetFloat(SpeedParam) < 0.04f)
                    animator.SetFloat(SpeedParam, 0f);
                else
                    animator.SetFloat(SpeedParam, speedMag, 0.06f, dt);
            }

            float refWalk = Mathf.Max(1f, walkSpeed);
            float refRun = Mathf.Max(refWalk + 0.5f, runSpeed);
            float walkMult = Mathf.Clamp(speedMag / 1.85f, 0.85f, 1.12f);
            float runMult = Mathf.Lerp(0.96f, 1.15f, Mathf.InverseLerp(refWalk, refRun, speedMag));
            float activeMoveMult = speedMag > refWalk ? runMult : walkMult;
            float speedMultiplier = Mathf.Lerp(1f, activeMoveMult, Mathf.InverseLerp(0.15f, 0.55f, speedMag));
            if (hasAnimSpeedMultiplierParam)
                animator.SetFloat(AnimSpeedMultiplierParam, speedMultiplier);

            // Küçük yükseklik inişlerinde düşüş animasyonunu tetikleme; yalnızca gerçek zıplama veya yüksek düşüşte havada say
            bool animGrounded = !isJumpOrFallAirborne && verticalVelocity <= 0.1f;
            if (!animGrounded)
            {
                wasAirborneForAnim = true;
                airborneAnimDuration += dt;
                if (verticalVelocity < minAirborneVy) minAirborneVy = verticalVelocity;
            }
            else if (wasAirborneForAnim)
            {
                if (hasLandParam && (airborneAnimDuration >= 0.14f || minAirborneVy < -4.0f))
                    animator.SetTrigger(LandTrigger);
                wasAirborneForAnim = false;
                airborneAnimDuration = 0f;
                minAirborneVy = 0f;
            }

            if (animGrounded && !jumpedThisFrame && hasJumpParam)
                animator.ResetTrigger(JumpTrigger);
            if (animGrounded && hasBounceParam)
                animator.ResetTrigger(BounceTrigger);
            SyncGroundedAndVerticalVelocity(animGrounded);
        }
    }

    void LateUpdate()
    {
        PlayerInputState.EndFrame();
    }

    // Üzerinde durulan hareketli platform, dönen çark ve döner silindir takibi
    ContinuousRotator activeWheelRotator;
    RotatingCylinderObstacle activeCylinder;
    MovingPlatform activeMovingPlatform;
    Vector3 lastWalkableNormal = Vector3.up;
    bool hadWalkableNormalLastFrame;

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Vector3 capsuleCenter = cc != null ? transform.TransformPoint(cc.center) : transform.position;
        if (hit.normal.y > 0.05f && hit.point.y < capsuleCenter.y)
        {
            hasGroundContact = true;
            if (hit.normal.y > bestGroundNormalY)
            {
                bestGroundNormalY = hit.normal.y;
                bestGroundNormal = hit.normal;
                bestGroundPoint = hit.point;
            }
        }

        // Karakterin alt kısmı (ayakları) bir nesneye değiyorsa (hızlı yatay dönmede de kopmasın)
        if (hit.normal.y > 0.35f && verticalVelocity <= 0.1f)
        {
            var mp = hit.collider.GetComponentInParent<MovingPlatform>();
            if (mp != null) activeMovingPlatform = mp;

            // Dönen çark / döner silindir grubunun pivotunun altındaysa onu aktif platform yap
            var marker = hit.collider.GetComponentInParent<RotatorPivotMarker>();
            if (marker != null)
            {
                if (marker.cylinder != null) activeCylinder = marker.cylinder;
                else if (marker.wheel != null) activeWheelRotator = marker.wheel;
            }

            // 3. Trambolinin üstündeyse doğrudan temas bildir
            var trampoline = hit.collider.GetComponentInParent<TrampolinePad>();
            if (trampoline != null)
            {
                trampoline.NotifyPlayerStepped(this);
            }
        }
    }

    void UpdateGroundedAndSlipState(float dt)
    {
        if (cc == null || !cc.isGrounded)
        {
            effectiveGrounded = false;
            isSlippingOffEdge = false;
            edgeSlipDir = Vector3.zero;
            airborneTimer += dt;
            return;
        }

        if (EvaluateFootholdSupport(out Vector3 slipDir))
        {
            effectiveGrounded = true;
            isSlippingOffEdge = false;
            edgeSlipDir = Vector3.zero;
            airborneTimer = 0f;
            if (verticalVelocity <= 0.1f)
                isJumpOrFallAirborne = false;
            return;
        }

        effectiveGrounded = false;
        isSlippingOffEdge = true;
        edgeSlipDir = slipDir;
        airborneTimer += dt;
        // Kapsül fiziksel olarak platform köşesine basılıyken sahte dikey düşüş hızı birikmesin
        if (verticalVelocity < -3.5f)
            verticalVelocity = -3.5f;
    }

    void GetFootProbeBasis(out Vector3 worldCenter, out Vector3 bottomSphereCenter, out float probeRadius, out float minWalkableY)
    {
        worldCenter = transform.TransformPoint(cc.center);
        float halfSpine = Mathf.Max(0f, cc.height * 0.5f - cc.radius);
        bottomSphereCenter = worldCenter - Vector3.up * halfSpine;
        // Dar ayak merkezi destegi: karakterin agirlik merkezi kenarin disina ciktiginda pahli kenarda asili kalmasin
        probeRadius = Mathf.Clamp(cc.radius * 0.45f, 0.06f, 0.075f);
        minWalkableY = Mathf.Cos(Mathf.Clamp(maxSlopeAngle, 15f, 85f) * Mathf.Deg2Rad);
    }

    void TrySnapDownToStep()
    {
        if (cc == null) return;

        float maxSnapDown = Mathf.Clamp(maxStepHeight, 0.12f, 0.24f);
        GetFootProbeBasis(out _, out Vector3 bottomSphereCenter, out float probeRadius, out float minWalkableY);
        float skinBottomOffset = cc.radius + cc.skinWidth;
        float capsuleBottomY = bottomSphereCenter.y - skinBottomOffset;
        float maxDist = skinBottomOffset + maxSnapDown;

        TryFindFootholdHits(bottomSphereCenter, 0f, maxDist, minWalkableY, out RaycastHit walkableHit, out _, out bool hasWalkable, out _);
        if (!hasWalkable)
            TryFindFootholdHits(bottomSphereCenter, probeRadius, maxDist, minWalkableY, out walkableHit, out _, out hasWalkable, out _);

        if (!hasWalkable) return;

        float gap = capsuleBottomY - walkableHit.point.y;
        if (gap > 0.002f && gap <= maxSnapDown)
        {
            cc.Move(Vector3.down * (gap + 0.015f));
        }
    }

    bool HasWalkableGroundBelow(float maxClearance)
    {
        if (cc == null) return false;

        GetFootProbeBasis(out _, out Vector3 bottomSphereCenter, out float probeRadius, out float minWalkableY);
        float skinBottomOffset = cc.radius + cc.skinWidth;
        float capsuleBottomY = bottomSphereCenter.y - skinBottomOffset;

        if (HasWalkableHitAt(bottomSphereCenter, capsuleBottomY, maxClearance, probeRadius, minWalkableY, skinBottomOffset))
            return true;

        // Dar platform aralıkları / silindir kenarından inerken ayak önündeki yüzeyi de gör
        Vector3 horizVel = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z);
        if (horizVel.sqrMagnitude > 0.25f)
        {
            const float headLift = 0.25f;
            Vector3 aheadOrigin = bottomSphereCenter + Vector3.up * headLift + horizVel.normalized * 0.35f;
            if (HasWalkableHitAt(aheadOrigin, capsuleBottomY, maxClearance, probeRadius, minWalkableY, skinBottomOffset + headLift))
                return true;
        }

        return false;
    }

    bool HasWalkableHitAt(
        Vector3 origin,
        float capsuleBottomY,
        float maxClearance,
        float probeRadius,
        float minWalkableY,
        float baseVerticalOffset)
    {
        float centerMaxDist = baseVerticalOffset + maxClearance;
        TryFindFootholdHits(origin, 0f, centerMaxDist, minWalkableY, out RaycastHit walkableHit, out _, out bool hasWalkable, out _);
        if (hasWalkable && walkableHit.distance > 0.001f && (capsuleBottomY - walkableHit.point.y) <= maxClearance)
            return true;

        float sphereMaxDist = Mathf.Max(0.05f, baseVerticalOffset - probeRadius + maxClearance);
        TryFindFootholdHits(origin, probeRadius, sphereMaxDist, minWalkableY, out walkableHit, out _, out hasWalkable, out _);
        return hasWalkable && walkableHit.distance > 0.001f && (capsuleBottomY - walkableHit.point.y) <= maxClearance;
    }

    // shortcut: yalnızca hareket/bakış ekseninde 0.20-0.35m ötesine bakar; yan boşlukta asılı kalma yine kaydırılır
    bool HasLevelGroundBeside(Vector3 bottomSphereCenter, float capsuleBottomY, float skinBottomOffset, float minWalkableY)
    {
        Vector3 dir = Vector3.ProjectOnPlane(horizontalVelocity.sqrMagnitude > 0.04f ? horizontalVelocity : transform.forward, Vector3.up).normalized;
        float step = Mathf.Clamp(maxStepHeight, 0.08f, 0.28f);
        float len = skinBottomOffset + step + 0.04f;
        for (int s = -1; s <= 1; s += 2)
        {
            for (float d = 0.2f; d <= 0.36f; d += 0.16f)
            {
                Vector3 o = bottomSphereCenter + dir * (d * s);
                if (Physics.Raycast(o, Vector3.down, out RaycastHit h, len, ~0, QueryTriggerInteraction.Ignore)
                    && !h.collider.transform.IsChildOf(transform)
                    && h.normal.y >= Mathf.Max(minWalkableY, 0.9f)
                    && h.point.y >= capsuleBottomY - 0.06f - 0.001f)
                    return true;
            }
        }
        if (hasDirectionalInputThisFrame && lastWishDir.sqrMagnitude > 0.01f)
        {
            const float lipHeadroom = 0.16f;
            float forwardLen = skinBottomOffset + lipHeadroom + step + 0.04f;
            for (float d = 0.14f; d <= 0.28f; d += 0.14f)
            {
                Vector3 o = bottomSphereCenter + Vector3.up * lipHeadroom + lastWishDir * d;
                if (Physics.Raycast(o, Vector3.down, out RaycastHit h, forwardLen, ~0, QueryTriggerInteraction.Ignore)
                    && !h.collider.transform.IsChildOf(transform)
                    && h.normal.y >= Mathf.Max(minWalkableY, 0.9f)
                    && h.point.y >= capsuleBottomY - 0.06f - 0.001f
                    && h.point.y <= capsuleBottomY + 0.18f)
                    return true;
            }
        }
        return false;
    }

    static bool IsHitSupportingBottom(RaycastHit hit, float capsuleBottomY, float skinBottomOffset)
    {
        // Düz zeminde yalnızca ayak tabanının hemen altını (<= 6.5cm) kabul et; eğimde küre teğet mesafesi kadar pay tanı
        float slopeTolerance = skinBottomOffset * ((1f / Mathf.Max(0.35f, hit.normal.y)) - 1f);
        return hit.point.y >= capsuleBottomY - (0.065f + slopeTolerance);
    }

    bool EvaluateFootholdSupport(out Vector3 slipDir)
    {
        slipDir = Vector3.zero;
        if (cc == null) return false;

        GetFootProbeBasis(out Vector3 worldCenter, out Vector3 bottomSphereCenter, out float probeRadius, out float minWalkableY);
        float skinBottomOffset = cc.radius + cc.skinWidth;
        float capsuleBottomY = bottomSphereCenter.y - skinBottomOffset;
        float effectiveStep = Mathf.Clamp(maxStepHeight, 0.08f, 0.28f);
        float sphereProbeDist = Mathf.Max(0.05f, skinBottomOffset - probeRadius + 0.08f);
        float centerProbeDist = (skinBottomOffset / Mathf.Max(0.25f, minWalkableY)) + effectiveStep + 0.04f;

        // 1. Merkez dikey ışın: düz taban desteği VEYA kısa bir engel/basamak aşılırken altındaki gerçek zemin (<= maxStepHeight)
        TryFindFootholdHits(bottomSphereCenter, 0f, centerProbeDist, minWalkableY, out RaycastHit centerWalkableHit, out _, out bool hasCenterWalkable, out _);
        if (hasCenterWalkable)
        {
            if (IsHitSupportingBottom(centerWalkableHit, capsuleBottomY, skinBottomOffset))
                return true;

            // Tam alt boşluk/uçurum değil, adım yüksekliği dahilinde zemin varsa ve oyuncu yürüyorsa basamak aşımını kesme
            float centerDrop = capsuleBottomY - centerWalkableHit.point.y;
            bool isActivelyStepping = hasDirectionalInputThisFrame || horizontalVelocity.sqrMagnitude > 0.25f;
            if (isActivelyStepping && !isJumpOrFallAirborne && centerDrop <= effectiveStep + 0.02f)
                return true;
        }

        // 2. Dar küre taraması: merkez ışını kıl payı boşluktayken yalnızca düz sayılabilecek üst yüzeye (normal.y >= 0.90) basışta izin ver; pahlı dış kenara izin verme
        TryFindFootholdHits(bottomSphereCenter, probeRadius, sphereProbeDist, minWalkableY, out RaycastHit probeWalkableHit, out RaycastHit probeSteepHit, out bool hasProbeWalkable, out bool hasProbeSteep);
        if (hasProbeWalkable &&
            probeWalkableHit.normal.y >= Mathf.Max(minWalkableY, 0.90f) &&
            IsHitSupportingBottom(probeWalkableHit, capsuleBottomY, skinBottomOffset))
            return true;

        // 3. Kapsülden dar boşluk: ayak altı boş ama hemen önde/arkada aynı seviyede zemin varsa boşluğu yürüyerek geç, kenara itme
        if (HasLevelGroundBeside(bottomSphereCenter, capsuleBottomY, skinBottomOffset, minWalkableY))
            return true;

        if (hasProbeSteep)
        {
            Vector3 steepHoriz = Vector3.ProjectOnPlane(probeSteepHit.normal, Vector3.up);
            if (steepHoriz.sqrMagnitude > 0.001f)
            {
                slipDir = steepHoriz.normalized;
                return false;
            }
        }

        if (hasGroundContact)
        {
            Vector3 contactHoriz = Vector3.ProjectOnPlane(bestGroundNormal, Vector3.up);
            if (bestGroundNormalY < 0.985f && contactHoriz.sqrMagnitude > 0.001f)
                slipDir = contactHoriz.normalized;
            else
            {
                Vector3 outward = Vector3.ProjectOnPlane(worldCenter - bestGroundPoint, Vector3.up);
                if (outward.sqrMagnitude > 0.0001f) slipDir = outward.normalized;
            }
        }

        return false;
    }

    void TryFindFootholdHits(
        Vector3 origin,
        float radius,
        float maxDist,
        float minWalkableY,
        out RaycastHit bestWalkable,
        out RaycastHit bestSteep,
        out bool foundWalkable,
        out bool foundSteep)
    {
        bestWalkable = default;
        bestSteep = default;
        foundWalkable = false;
        foundSteep = false;

        int count = radius <= 0.0001f
            ? Physics.RaycastNonAlloc(origin, Vector3.down, footholdHitBuffer, maxDist, ~0, QueryTriggerInteraction.Ignore)
            : Physics.SphereCastNonAlloc(origin, radius, Vector3.down, footholdHitBuffer, maxDist, ~0, QueryTriggerInteraction.Ignore);

        float bestWalkableDist = float.MaxValue;
        float bestSteepDist = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = footholdHitBuffer[i];
            if (h.collider == null || h.collider.isTrigger) continue;
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (radius > 0f && h.distance <= 0.001f && h.point == Vector3.zero) continue;

            if (h.normal.y >= minWalkableY)
            {
                if (h.distance < bestWalkableDist)
                {
                    bestWalkableDist = h.distance;
                    bestWalkable = h;
                    foundWalkable = true;
                }
            }
            else if (h.distance < bestSteepDist)
            {
                bestSteepDist = h.distance;
                bestSteep = h;
                foundSteep = true;
            }
        }
    }

    void ResolveRotatingObstaclePushes(float dt)
    {
        if (cc == null || dt <= 0f) return;

        Vector3 worldCenter = transform.TransformPoint(cc.center);
        float halfSpine = Mathf.Max(0f, cc.height * 0.5f - cc.radius);
        Vector3 pBottom = worldCenter - Vector3.up * halfSpine;
        Vector3 pTop = worldCenter + Vector3.up * halfSpine;
        float capsuleRad = cc.radius + cc.skinWidth * 0.5f;

        int count = Physics.OverlapCapsuleNonAlloc(
            pBottom, pTop, capsuleRad + 0.04f,
            obstacleOverlapBuffer, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider col = obstacleOverlapBuffer[i];
            if (col == null || col.transform.IsChildOf(transform)) continue;
            if (col is MeshCollider mc && !mc.convex) continue;

            var marker = col.GetComponentInParent<RotatorPivotMarker>();
            if (marker == null) continue;
            if (marker.cylinder == null && marker.wheel == null) continue;

            Vector3 bestPt = worldCenter;
            Vector3 bestClosest = worldCenter;
            float bestDistSq = float.MaxValue;

            const int samples = 5;
            for (int s = 0; s < samples; s++)
            {
                float t = s / (float)(samples - 1);
                Vector3 pt = Vector3.Lerp(pBottom, pTop, t);
                Vector3 cp = Physics.ClosestPoint(pt, col, col.transform.position, col.transform.rotation);
                float dSq = (pt - cp).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    bestPt = pt;
                    bestClosest = cp;
                }
            }

            float bestDist = Mathf.Sqrt(bestDistSq);
            if (bestDist >= capsuleRad) continue;

            Vector3 delta = bestPt - bestClosest;
            float minWalkableY = Mathf.Cos(Mathf.Clamp(maxSlopeAngle, 15f, 85f) * Mathf.Deg2Rad);
            bool onSupportedRotatorTop =
                ((marker.cylinder != null && marker.cylinder == activeCylinder) ||
                 (marker.wheel != null && marker.wheel == activeWheelRotator)) &&
                bestClosest.y <= pBottom.y - capsuleRad * 0.2f;
            if (onSupportedRotatorTop || (bestDist > 0.001f && (delta.y / bestDist) >= minWalkableY))
                continue; // Oyuncu zaten yürünebilir üst eğimde duruyor

            Vector3 obsVel = marker.cylinder != null
                ? marker.cylinder.GetPointVelocity(bestPt)
                : marker.wheel.GetPointVelocity(bestPt);
            Vector3 obsHorizVel = Vector3.ProjectOnPlane(obsVel, Vector3.up);

            Vector3 pushDir;
            float pushDist;
            if (bestDist > 0.001f)
            {
                Vector3 horizDelta = Vector3.ProjectOnPlane(delta, Vector3.up);
                if (horizDelta.sqrMagnitude > 0.00001f)
                {
                    pushDir = horizDelta.normalized;
                    if (obsHorizVel.sqrMagnitude > 0.01f && Vector3.Dot(pushDir, obsHorizVel) < -0.25f * obsHorizVel.magnitude)
                        pushDir = obsHorizVel.normalized;
                }
                else if (obsHorizVel.sqrMagnitude > 0.0001f)
                {
                    pushDir = obsHorizVel.normalized;
                }
                else continue;

                pushDist = (capsuleRad - bestDist) + 0.015f;
            }
            else
            {
                if (obsHorizVel.sqrMagnitude > 0.0001f)
                {
                    pushDir = obsHorizVel.normalized;
                }
                else
                {
                    Vector3 pivotPos = marker.cylinder != null ? marker.cylinder.CurrentPivotWorld : marker.wheel.CurrentPivotWorld;
                    Vector3 outVec = Vector3.ProjectOnPlane(worldCenter - pivotPos, Vector3.up);
                    if (outVec.sqrMagnitude < 0.0001f) outVec = transform.forward;
                    pushDir = outVec.normalized;
                }
                pushDist = capsuleRad + obsHorizVel.magnitude * dt + 0.02f;
            }

            cc.Move(pushDir * pushDist);

            // Yalnızca engel gerçekten oyuncuya doğru süpürüyorsa itme darbesi uygula; uzaklaşırken sahte savurma yapma
            float sweepAlong = Mathf.Max(0f, Vector3.Dot(obsHorizVel, pushDir));
            float knockSpeed = sweepAlong > 0.1f ? Mathf.Max(sweepAlong * 1.15f, 1.8f) : sweepAlong;
            float curAlong = Vector3.Dot(horizontalVelocity, pushDir);
            if (curAlong < knockSpeed)
            {
                horizontalVelocity += pushDir * (knockSpeed - curAlong);
            }
        }
    }

    /// <summary>
    /// Trambolin veya fırlatıcı yüzeylerin oyuncuyu havaya fırlatması için kullanılır.
    /// </summary>
    public void Bounce(float bounceHeight, float maxHorizSpeedOverride = -1f)
    {
        isMantling = false;
        isMantleHandIKActive = false;
        if (cc != null && !cc.enabled) cc.enabled = true;

        verticalVelocity = Mathf.Sqrt(bounceHeight * -2f * gravity);
        float maxCap = maxHorizSpeedOverride > 0f ? maxHorizSpeedOverride : trampolineMaxHorizSpeed;
        airHorizSpeedCap = maxCap > 0f ? maxCap : -1f;
        float hMag = horizontalVelocity.magnitude;
        if (maxCap > 0f && hMag > maxCap && hMag > 0.001f)
        {
            horizontalVelocity *= (maxCap / hMag);
        }

        coyoteCounter = 0f;
        jumpBufferCounter = 0f;
        airborneTimer = 0.2f;
        effectiveGrounded = false;
        isJumpOrFallAirborne = true;
        canCutJump = false;
        activeWheelRotator = null;
        activeCylinder = null;
        activeMovingPlatform = null;
        if (animator != null)
        {
            EnsureAnimatorParamsCached();
            SyncGroundedAndVerticalVelocity(false);
            float estRise = Mathf.Sqrt(2f * Mathf.Max(1f, bounceHeight) / Mathf.Max(1f, Mathf.Abs(gravity)));
            float estTotal = estRise * 1.56f;
            float bounceSpeed = Mathf.Clamp(0.95f / Mathf.Max(0.85f, estTotal), 0.56f, 0.82f);
            if (hasBounceSpeedParam) animator.SetFloat(BounceSpeedParam, bounceSpeed);
            if (hasBounceVariantParam) animator.SetInteger(BounceVariantParam, Random.Range(0, 2));
            if (hasJumpParam) animator.ResetTrigger(JumpTrigger);
            if (hasLandParam) animator.ResetTrigger(LandTrigger);
            if (hasBounceParam)
                animator.SetTrigger(BounceTrigger);
            else if (animator.HasState(0, JumpUpStateHash))
                animator.CrossFadeInFixedTime(JumpUpStateHash, 0.05f, 0, 0f);
            else if (animator.HasState(0, JumpStateHash))
                animator.CrossFadeInFixedTime(JumpStateHash, 0.05f, 0, 0f);
        }
    }

    bool TryStartAutoMantle(Vector3 footPos, Vector3 wishNormalized)
    {
        if (cc == null || wishNormalized.sqrMagnitude < 0.01f) return false;

        Vector3 wallOrigin = footPos + Vector3.up * 0.30f;
        float wallDist = cc.radius + 0.42f;
        int wallCount = Physics.SphereCastNonAlloc(wallOrigin, 0.08f, wishNormalized, mantleHitBuffer, wallDist, ~0, QueryTriggerInteraction.Ignore);
        bool foundWall = false;
        RaycastHit wallHit = default;
        float bestDist = float.MaxValue;
        for (int i = 0; i < wallCount; i++)
        {
            var h = mantleHitBuffer[i];
            if (h.collider == null || h.collider.transform.root == transform.root) continue;
            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                wallHit = h;
                foundWall = true;
            }
        }
        if (!foundWall || IsHazardOrDynamicCollider(wallHit.collider)) return false;

        Vector3 wallNormalFlat = Vector3.ProjectOnPlane(wallHit.normal, Vector3.up);
        if (wallNormalFlat.sqrMagnitude < 0.01f) wallNormalFlat = -wishNormalized;
        else wallNormalFlat.Normalize();
        Vector3 intoWallDir = -wallNormalFlat;
        if (Vector3.Dot(wishNormalized, intoWallDir) < 0.35f) return false;

        Vector3 topOrigin = wallHit.point + Vector3.up * (mantleMaxHeight + 0.35f) + intoWallDir * 0.28f;
        float topDist = mantleMaxHeight + 0.55f;
        int topCount = Physics.RaycastNonAlloc(topOrigin, Vector3.down, mantleHitBuffer, topDist, ~0, QueryTriggerInteraction.Ignore);
        bool foundTop = false;
        RaycastHit topHit = default;
        float bestTopDist = float.MaxValue;
        for (int i = 0; i < topCount; i++)
        {
            var h = mantleHitBuffer[i];
            if (h.collider == null || h.collider.transform.root == transform.root) continue;
            if (h.distance < bestTopDist)
            {
                bestTopDist = h.distance;
                topHit = h;
                foundTop = true;
            }
        }
        if (!foundTop || topHit.normal.y < 0.82f || IsHazardOrDynamicCollider(topHit.collider)) return false;

        float ledgeDelta = topHit.point.y - footPos.y;
        if (ledgeDelta < mantleMinHeight || ledgeDelta > mantleMaxHeight) return false;

        Vector3 destFoot = topHit.point + Vector3.up * 0.03f;
        if (!HasPlayerSpaceAtFoot(destFoot)) return false;

        Vector3 rootOffset = transform.position - footPos;
        bool isRealMantle = ledgeDelta >= 0.48f;
        mantleTargetRot = Quaternion.LookRotation(intoWallDir, Vector3.up);

        if (isRealMantle)
        {
            isMantleHandIKActive = true;
            mantleActiveDuration = Mathf.Max(0.30f, mantleDuration);
            transform.rotation = mantleTargetRot;

            Vector3 ledgeRight = Vector3.Cross(Vector3.up, intoWallDir).normalized;
            Vector3 ledgeEdge = new Vector3(wallHit.point.x, topHit.point.y, wallHit.point.z) + intoWallDir * 0.06f;
            mantleLeftHandPos = ledgeEdge - ledgeRight * 0.17f + Vector3.up * 0.03f;
            mantleRightHandPos = ledgeEdge + ledgeRight * 0.17f + Vector3.up * 0.03f;
            mantleWallNormal = wallNormalFlat;

            Vector3 wallAnchorXZ = wallHit.point + wallNormalFlat * (cc.radius + 0.16f);
            Vector3 midXZ = wallHit.point + wallNormalFlat * (cc.radius + 0.10f);
            mantleStartPos = new Vector3(wallAnchorXZ.x, transform.position.y, wallAnchorXZ.z);
            mantleEndPos = (destFoot + intoWallDir * 0.10f) + rootOffset;
            float chestAtLipY = (topHit.point.y - 0.55f) + rootOffset.y;
            mantleMidPos = new Vector3(midXZ.x, Mathf.Clamp(chestAtLipY, mantleStartPos.y, mantleEndPos.y - 0.10f), midXZ.z);
        }
        else
        {
            isMantleHandIKActive = false;
            mantleActiveDuration = 0.22f;

            Vector3 midXZ = wallHit.point + wallNormalFlat * (cc.radius + 0.06f);
            mantleStartPos = transform.position;
            mantleEndPos = destFoot + rootOffset;
            mantleMidPos = new Vector3(midXZ.x, mantleEndPos.y + 0.02f, midXZ.z);
        }

        mantleElapsed = 0f;
        isMantling = true;

        cc.enabled = false;
        horizontalVelocity = intoWallDir * Mathf.Min(walkSpeed * 0.5f, horizontalVelocity.magnitude);
        verticalVelocity = 0f;
        airHorizSpeedCap = -1f;
        coyoteCounter = 0f;
        jumpBufferCounter = 0f;
        canCutJump = false;

        if (animator != null)
        {
            EnsureAnimatorParamsCached();
            if (hasJumpParam) animator.ResetTrigger(JumpTrigger);
            if (hasBounceParam) animator.ResetTrigger(BounceTrigger);
            if (isRealMantle && hasMantleParam) animator.SetTrigger(MantleTrigger);
        }
        return true;
    }

    void UpdateMantle(float dt)
    {
        float dur = Mathf.Max(0.12f, mantleActiveDuration);
        mantleElapsed += dt;
        float t = Mathf.Clamp01(mantleElapsed / dur);
        float split = isMantleHandIKActive ? 0.48f : 0.55f;

        Vector3 pos;
        if (t <= split)
        {
            float u = Mathf.SmoothStep(0f, 1f, t / split);
            pos = Vector3.Lerp(mantleStartPos, mantleMidPos, u);
        }
        else
        {
            float u = Mathf.SmoothStep(0f, 1f, (t - split) / (1f - split));
            pos = Vector3.Lerp(mantleMidPos, mantleEndPos, u);
        }

        transform.position = pos;
        transform.rotation = Quaternion.Slerp(transform.rotation, mantleTargetRot, 1f - Mathf.Exp(-26f * dt));

        if (animator != null)
        {
            EnsureAnimatorParamsCached();
            if (hasSpeedParam) animator.SetFloat(SpeedParam, 0.5f);
            SyncGroundedAndVerticalVelocity(false);
        }

        if (t >= 1f)
        {
            transform.position = mantleEndPos;
            isMantling = false;
            isMantleHandIKActive = false;
            cc.enabled = true;
            effectiveGrounded = true;
            isJumpOrFallAirborne = false;
            wasAirborneForAnim = false;
            airborneTimer = 0f;
            airborneAnimDuration = 0f;
            minAirborneVy = 0f;
            verticalVelocity = -0.5f;
            Physics.SyncTransforms();
        }
    }

    bool HasPlayerSpaceAtFoot(Vector3 destFoot)
    {
        Vector3 pBottom = destFoot + Vector3.up * (cc.radius + 0.05f);
        Vector3 pTop = destFoot + Vector3.up * Mathf.Max(cc.radius + 0.06f, cc.height - cc.radius);
        int count = Physics.OverlapCapsuleNonAlloc(pBottom, pTop, cc.radius * 0.9f, mantleOverlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var col = mantleOverlapBuffer[i];
            if (col == null || col.transform.root == transform.root) continue;
            return false;
        }
        return true;
    }

    static bool IsHazardOrDynamicCollider(Collider col)
    {
        if (col == null) return true;
        return col.GetComponentInParent<RotatingHazard>() != null
            || col.GetComponentInParent<TimedSpikeTrap>() != null
            || col.GetComponentInParent<KillZone>() != null
            || col.GetComponentInParent<TrampolinePad>() != null
            || col.GetComponentInParent<MovingPlatform>() != null
            || col.GetComponentInParent<ContinuousRotator>() != null
            || col.GetComponentInParent<RotatingCylinderObstacle>() != null
            || col.GetComponentInParent<RotatorPivotMarker>() != null;
    }

    /// <summary>
    /// Controller üzerindeki parametreleri önbelleğe alır; sıcak yeniden derleme veya eski controller durumunda hata akışını önler.
    /// </summary>
    void EnsureAnimatorParamsCached()
    {
        if (animator == null) return;
        RuntimeAnimatorController ctrl = animator.runtimeAnimatorController;
        int paramCount = ctrl != null ? animator.parameterCount : 0;
        if (cachedController == ctrl && cachedParamCount == paramCount) return;

        cachedController = ctrl;
        cachedParamCount = paramCount;
        hasSpeedParam = false;
        hasAnimSpeedMultiplierParam = false;
        hasBounceSpeedParam = false;
        hasIsGroundedParam = false;
        hasVerticalVelocityParam = false;
        hasJumpParam = false;
        hasBounceParam = false;
        hasLandParam = false;
        hasMantleParam = false;
        hasJumpVariantParam = false;
        hasBounceVariantParam = false;
        if (ctrl == null) return;

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            int hash = p.nameHash;
            if (hash == SpeedParam) hasSpeedParam = true;
            else if (hash == AnimSpeedMultiplierParam) hasAnimSpeedMultiplierParam = true;
            else if (hash == BounceSpeedParam) hasBounceSpeedParam = true;
            else if (hash == IsGroundedParam) hasIsGroundedParam = true;
            else if (hash == VerticalVelocityParam) hasVerticalVelocityParam = true;
            else if (hash == JumpTrigger) hasJumpParam = true;
            else if (hash == BounceTrigger) hasBounceParam = true;
            else if (hash == LandTrigger) hasLandParam = true;
            else if (hash == MantleTrigger) hasMantleParam = true;
            else if (hash == JumpVariantParam) hasJumpVariantParam = true;
            else if (hash == BounceVariantParam) hasBounceVariantParam = true;
        }
    }

    void SyncGroundedAndVerticalVelocity(bool groundedState)
    {
        if (hasIsGroundedParam) animator.SetBool(IsGroundedParam, groundedState);
        if (hasVerticalVelocityParam) animator.SetFloat(VerticalVelocityParam, verticalVelocity);
    }
}

