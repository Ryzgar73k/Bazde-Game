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
    public float walkSpeed = 4.2f;
    public float runSpeed = 7.2f;
    public float acceleration = 40f;
    public float deceleration = 30f;
    [Tooltip("Ters yöne dönerken uygulanan ivme çarpanı")]
    public float turnAccelerationMultiplier = 1.6f;
    [Range(0f, 1f)]
    public float airControl = 0.85f; // Havada ileri/yön kontrolü korunsun
    [Tooltip("Havada yön tuşları bırakıldığında uygulanan hafif hava direnci")]
    public float airReleaseDrag = 2.2f;
    public float turnSpeed = 900f;

    [Header("Zıplama")]
    public float jumpHeight = 1.45f;
    [Tooltip("Zıplarken ileri hareket halindeysen eklenecek ekstra hız (0 = ekstra boost yok)")]
    public float jumpForwardBoost = 0f;
    public float gravity = -26f;
    [Tooltip("Zıplama tuşu erken bırakıldığında yükseliş sırasında uygulanan yerçekimi çarpanı (kısa basış = alçak zıplama)")]
    [Range(1f, 6f)]
    public float lowJumpGravityMultiplier = 2.6f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;
    [Tooltip("Zıplamada root motion kapsülü sürüklesin mi? Kapalı tutarak yerinde zıplama ve havada kontrol korunur.")]
    public bool useRootMotionInJump = false;

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

    public const int LatestConfigVersion = 5;

    [HideInInspector] public int configVersion = 0;

    CharacterController cc;
    RootMotionRelay rootRelay;
    Vector3 horizontalVelocity;
    float verticalVelocity;
    float coyoteCounter;
    float jumpBufferCounter;
    float airborneTimer;
    bool effectiveGrounded;
    bool isJumpOrFallAirborne;
    bool canCutJump;
    bool hasDirectionalInputThisFrame;
    bool isSlippingOffEdge;
    Vector3 edgeSlipDir;
    bool hasGroundContact;
    Vector3 bestGroundNormal;
    Vector3 bestGroundPoint;
    float bestGroundNormalY;
    readonly Collider[] obstacleOverlapBuffer = new Collider[16];
    readonly RaycastHit[] footholdHitBuffer = new RaycastHit[16];

    // Animator parametre ID'leri (performans ve güvenlik için)
    static readonly int SpeedParam = Animator.StringToHash("Speed");
    static readonly int AnimSpeedMultiplierParam = Animator.StringToHash("AnimSpeedMultiplier");
    static readonly int IsGroundedParam = Animator.StringToHash("IsGrounded");
    static readonly int VerticalVelocityParam = Animator.StringToHash("VerticalVelocity");
    static readonly int JumpTrigger = Animator.StringToHash("Jump");
    static readonly int JumpStateHash = Animator.StringToHash("Jump");

    RuntimeAnimatorController cachedController;
    int cachedParamCount = -1;
    bool hasSpeedParam;
    bool hasAnimSpeedMultiplierParam;
    bool hasIsGroundedParam;
    bool hasVerticalVelocityParam;
    bool hasJumpParam;

    public bool IsGrounded => effectiveGrounded;
    public float VerticalVelocity => verticalVelocity;
    public bool IsRunning { get; private set; }

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
    [Tooltip("Karakterin ayak Y konumu bunun altına düşerse (denize düştü) başlangıç noktasına döner")]
    public float killY = -1.5f;
    Vector3 spawnPos;
    Quaternion spawnRot;

    void Respawn()
    {
        cc.enabled = false;
        transform.SetPositionAndRotation(spawnPos, spawnRot);
        cc.enabled = true;
        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
        coyoteCounter = 0f;
        jumpBufferCounter = 0f;
        airborneTimer = 0f;
        effectiveGrounded = false;
        isJumpOrFallAirborne = false;
        isSlippingOffEdge = false;
        canCutJump = false;
        activeWheelRotator = null;
        activeCylinder = null;
        lastWalkableNormal = Vector3.up;
        hadWalkableNormalLastFrame = false;
        if (rootRelay != null) rootRelay.Consume();
        if (animator != null && hasJumpParam) animator.ResetTrigger(JumpTrigger);
        Physics.SyncTransforms();
        var camFollow = cameraTransform != null ? cameraTransform.GetComponent<ThirdPersonCamera>() : null;
        if (camFollow != null) camFollow.ResetView();
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
        if (GetWorldFootPosition().y < killY) { Respawn(); return; }
        // Dönen çark/silindirler -20 sırasında döndürüldüğü için PhysX çarpışma/ışın sorgularını kare başında senkronize et
        Physics.SyncTransforms();

        bool grounded = effectiveGrounded;
        ApplyStepSettings(grounded && !isJumpOrFallAirborne && verticalVelocity <= 0.1f);

        // --- Zıplama zamanlayıcıları ---
        coyoteCounter = grounded ? coyoteTime : coyoteCounter - dt;
        if (PlayerInputState.JumpPressed) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= dt;

        // --- Yatay hareket ---
        Vector2 input = PlayerInputState.Move;
        bool hasMoveInput = input.sqrMagnitude > 0.01f;
        hasDirectionalInputThisFrame = hasMoveInput;
        Vector3 forward = Vector3.forward, right = Vector3.right;
        if (cameraTransform != null)
        {
            forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        }
        Vector3 wishDir = forward * input.y + right * input.x;
        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

        // Karakteri hareket yönüne döndür ve ters yöne dönerken yerinde koşmayı engelle
        float alignment = 1f;
        if (wishDir.sqrMagnitude > 0.01f)
        {
            Vector3 desiredDir = wishDir.normalized;
            Quaternion targetRot = Quaternion.LookRotation(desiredDir, Vector3.up);
            float rotRate = Mathf.Clamp(turnSpeed / 60f, 6f, 24f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-rotRate * dt));
            float dot = Vector3.Dot(transform.forward, desiredDir);
            alignment = Mathf.Clamp01((dot + 0.3f) / 1.3f);
        }

        IsRunning = PlayerInputState.Run && hasMoveInput;
        float targetSpeed = IsRunning ? runSpeed : walkSpeed;
        Vector3 targetVelocity = wishDir * (targetSpeed * (grounded ? alignment : 1f));

        if (grounded)
        {
            if (wishDir.sqrMagnitude > 0.001f)
            {
                bool isReversing = horizontalVelocity.sqrMagnitude > 0.25f
                    && Vector3.Dot(horizontalVelocity.normalized, wishDir.normalized) < -0.15f;
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
            // Havada: Yön veriliyorsa hızı koru ve yönlendir; tuşlar bırakıldığında hafif hava direnci uygula
            if (wishDir.sqrMagnitude > 0.01f)
            {
                float currentSpeed = Mathf.Max(horizontalVelocity.magnitude, targetSpeed);
                Vector3 airTarget = wishDir.normalized * currentSpeed;
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
                    Vector3 boostDir = wishDir.normalized;
                    horizontalVelocity = boostDir * Mathf.Max(horizontalVelocity.magnitude + jumpForwardBoost, runSpeed);
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
            if (animator != null)
            {
                EnsureAnimatorParamsCached();
                SyncGroundedAndVerticalVelocity(false);
                if (hasJumpParam) animator.SetTrigger(JumpTrigger);
            }
        }

        // Kısa zıplama: zıplama tuşu erken bırakılırsa yükselirken daha güçlü yerçekimi uygula
        float gravMult = (canCutJump && verticalVelocity > 0f && !PlayerInputState.JumpHeld)
            ? Mathf.Max(1f, lowJumpGravityMultiplier)
            : 1f;
        verticalVelocity += gravity * gravMult * dt;
        if (verticalVelocity <= 0f) canCutJump = false;

        // Zıplama animasyonu root motion verisini her kare tüket; varsayılan olarak kapsül fiziği kullanılır
        Vector3 rootDelta = rootRelay != null ? rootRelay.Consume() : Vector3.zero;
        bool inJumpAnim = useRootMotionInJump && animator != null &&
            (animator.GetCurrentAnimatorStateInfo(0).IsName("Jump") || animator.GetNextAnimatorStateInfo(0).IsName("Jump"));

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

        // 1. Eğer oyuncu dönen çarkın veya döner silindirin üstündeyse gerçek ayak pozisyonuna göre sürükle
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
            // Idle (Speed≈0) 1x hızda kalır; yürüme (~0.9x) ve Shift depar (~1.54x) hareket hızıyla orantılı hızlanır
            float refWalk = Mathf.Max(1f, walkSpeed);
            float moveMultiplier = Mathf.Clamp((speedMag / refWalk) * 0.9f, 0.8f, 2.0f);
            float speedMultiplier = Mathf.Lerp(1f, moveMultiplier, Mathf.InverseLerp(0.15f, 0.6f, speedMag));
            if (hasAnimSpeedMultiplierParam)
                animator.SetFloat(AnimSpeedMultiplierParam, speedMultiplier);
            // Küçük yükseklik inişlerinde düşüş animasyonunu tetikleme; yalnızca gerçek zıplama veya yüksek düşüşte havada say
            bool animGrounded = !isJumpOrFallAirborne && verticalVelocity <= 0.1f;
            if (animGrounded && !jumpedThisFrame && hasJumpParam)
                animator.ResetTrigger(JumpTrigger);
            SyncGroundedAndVerticalVelocity(animGrounded);
        }
    }

    void LateUpdate()
    {
        PlayerInputState.EndFrame();
    }

    // Üzerinde durulan dönen çark ve döner silindir takibi (birbirinden ayrı)
    ContinuousRotator activeWheelRotator;
    RotatingCylinderObstacle activeCylinder;
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
    public void Bounce(float bounceHeight)
    {
        verticalVelocity = Mathf.Sqrt(bounceHeight * -2f * gravity);
        coyoteCounter = 0f;
        jumpBufferCounter = 0f;
        airborneTimer = 0.2f;
        effectiveGrounded = false;
        isJumpOrFallAirborne = true;
        canCutJump = false;
        activeWheelRotator = null;
        activeCylinder = null;
        if (animator != null)
        {
            EnsureAnimatorParamsCached();
            SyncGroundedAndVerticalVelocity(false);
            if (hasJumpParam) animator.ResetTrigger(JumpTrigger);
            if (animator.HasState(0, JumpStateHash))
                animator.CrossFadeInFixedTime(JumpStateHash, 0.05f, 0, 0f);
        }
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
        hasIsGroundedParam = false;
        hasVerticalVelocityParam = false;
        hasJumpParam = false;
        if (ctrl == null) return;

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            int hash = p.nameHash;
            if (hash == SpeedParam) hasSpeedParam = true;
            else if (hash == AnimSpeedMultiplierParam) hasAnimSpeedMultiplierParam = true;
            else if (hash == IsGroundedParam) hasIsGroundedParam = true;
            else if (hash == VerticalVelocityParam) hasVerticalVelocityParam = true;
            else if (hash == JumpTrigger) hasJumpParam = true;
        }
    }

    void SyncGroundedAndVerticalVelocity(bool groundedState)
    {
        if (hasIsGroundedParam) animator.SetBool(IsGroundedParam, groundedState);
        if (hasVerticalVelocityParam) animator.SetFloat(VerticalVelocityParam, verticalVelocity);
    }
}

