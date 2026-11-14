using UnityEngine;

/// <summary>
/// CharacterController tabanlı yürüme / koşma / zıplama.
/// Hareket kameraya göre yapılır. Coyote time ve jump buffer içerir (parkur hissiyatı için).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Hareket")]
    public float walkSpeed = 5.5f;
    public float runSpeed = 10f;
    public float acceleration = 40f;
    [Range(0f, 1f)]
    public float airControl = 0.85f; // Havada ileri/yön kontrolü korunsun
    public float turnSpeed = 900f;

    [Header("Zıplama")]
    public float jumpHeight = 2.6f;
    [Tooltip("Zıplarken karakteri ileri fırlatan ekstra ivme")]
    public float jumpForwardBoost = 4f;
    public float gravity = -26f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;

    [Header("Basamak ve Eğim (Zıplamadan Aşma)")]
    [Tooltip("Karakterin zıplamadan yürüyerek çıkabileceği maksimum basamak/engel yüksekliği (metre)")]
    [Range(0.05f, 1.8f)]
    public float maxStepHeight = 0.55f;
    [Tooltip("Karakterin kaymadan yürüyerek çıkabileceği maksimum rampa/eğim açısı (derece)")]
    [Range(15f, 80f)]
    public float maxSlopeAngle = 55f;

    [Header("Referans")]
    [Tooltip("Hareket yönü bu kameraya göre belirlenir. Boşsa Camera.main kullanılır.")]
    public Transform cameraTransform;
    [Tooltip("Opsiyonel: Model üzerindeki Animator")]
    public Animator animator;

    [Tooltip("Zıplama animasyonunun ileri hareketinin çarpanı (1 = animasyondaki gibi)")]
    public float rootMotionJumpScale = 1f;

    CharacterController cc;
    RootMotionRelay rootRelay;
    Vector3 horizontalVelocity;
    float verticalVelocity;
    float coyoteCounter;
    float jumpBufferCounter;

    // Animator parametre ID'leri (performans ve güvenlik için)
    static readonly int SpeedParam = Animator.StringToHash("Speed");
    static readonly int AnimSpeedMultiplierParam = Animator.StringToHash("AnimSpeedMultiplier");
    static readonly int IsGroundedParam = Animator.StringToHash("IsGrounded");
    static readonly int JumpTrigger = Animator.StringToHash("Jump");

    public bool IsGrounded => cc.isGrounded;
    public bool IsRunning { get; private set; }

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        ApplyStepSettings();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        if (animator == null) animator = GetComponentInChildren<Animator>();

        // Animator root motion'ı modeli doğrudan oynatmaz; RootMotionRelay üzerinden kapsüle aktarılır
        if (animator != null)
        {
            animator.applyRootMotion = false;
            rootRelay = animator.GetComponent<RootMotionRelay>();
            if (rootRelay == null) rootRelay = animator.gameObject.AddComponent<RootMotionRelay>();
        }
    }

    void OnValidate()
    {
        if (cc == null) cc = GetComponent<CharacterController>();
        ApplyStepSettings();
    }

    void ApplyStepSettings()
    {
        if (cc == null) return;
        // Unity kuralı: stepOffset değeri CharacterController.height değerinden küçük olmalıdır
        cc.stepOffset = Mathf.Clamp(maxStepHeight, 0.01f, Mathf.Max(0.1f, cc.height - 0.05f));
        cc.slopeLimit = Mathf.Clamp(maxSlopeAngle, 15f, 85f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        ApplyStepSettings();
        bool grounded = cc.isGrounded;

        // --- Zıplama zamanlayıcıları ---
        coyoteCounter = grounded ? coyoteTime : coyoteCounter - dt;
        if (PlayerInputState.JumpPressed) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= dt;

        // --- Yatay hareket ---
        Vector2 input = PlayerInputState.Move;
        Vector3 forward = Vector3.forward, right = Vector3.right;
        if (cameraTransform != null)
        {
            forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        }
        Vector3 wishDir = forward * input.y + right * input.x;

        IsRunning = PlayerInputState.Run && input.sqrMagnitude > 0.01f;
        float targetSpeed = IsRunning ? runSpeed : walkSpeed;
        Vector3 targetVelocity = wishDir * targetSpeed;

        if (grounded)
        {
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, acceleration * dt);
        }
        else
        {
            // Havada: Eğer ileri gitmeye çalışıyorsa hızı korusun ve yönlendirilsin, ani fren yapmasın
            if (wishDir.sqrMagnitude > 0.01f)
            {
                float currentSpeed = Mathf.Max(horizontalVelocity.magnitude, targetSpeed);
                Vector3 airTarget = wishDir * currentSpeed;
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, airTarget, acceleration * airControl * dt);
            }
        }

        // Karakteri hareket yönüne döndür
        if (wishDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(wishDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * dt);
        }

        // --- Dikey hareket ---
        if (grounded && verticalVelocity < 0f) verticalVelocity = -2f; // yere yapışık tut

        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            // Zıplama anında ileri doğru hareket varsa veya tuşa basılıyorsa güçlü bir fırlatma ivmesi ekle
            Vector3 boostDir = wishDir.sqrMagnitude > 0.01f ? wishDir : transform.forward;
            if (input.sqrMagnitude > 0.01f || horizontalVelocity.sqrMagnitude > 1f)
            {
                horizontalVelocity = boostDir * Mathf.Max(horizontalVelocity.magnitude + jumpForwardBoost, runSpeed);
            }

            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
            if (animator != null) animator.SetTrigger(JumpTrigger);
        }

        verticalVelocity += gravity * dt;

        // Zıplama animasyonu oynarken ileri hareketi animasyonun root motion'ından al:
        // kapsül (ve kamera) animasyonla birlikte ilerler, animasyon bitince geri atlama olmaz.
        Vector3 rootDelta = rootRelay != null ? rootRelay.Consume() : Vector3.zero;
        bool inJumpAnim = animator != null &&
            (animator.GetCurrentAnimatorStateInfo(0).IsName("Jump") || animator.GetNextAnimatorStateInfo(0).IsName("Jump"));

        Vector3 move;
        if (inJumpAnim && !grounded)
        {
            Vector3 flat = Vector3.ProjectOnPlane(rootDelta, Vector3.up) * rootMotionJumpScale;
            move = flat + Vector3.up * verticalVelocity * dt;
        }
        else
        {
            move = (horizontalVelocity + Vector3.up * verticalVelocity) * dt;
        }

        // 1. Eğer oyuncu dönen çarkın (OnSag_Cark_Dis_Cember) üstündeyse, çarkın dönüşünü oyuncuya aktar
        if (grounded && activeWheelRotator != null)
        {
            move += activeWheelRotator.CalculatePlayerDisplacement(transform.position, dt);
        }
        else if (grounded && activeCylinder != null)
        {
            move += activeCylinder.CalculatePlayerDisplacement(transform.position, dt);
        }

        // Her karede yeni çarpışma kontrolü için sıfırla (cc.Move içinde OnControllerColliderHit tekrar doldurur)
        activeWheelRotator = null;
        activeCylinder = null;
        cc.Move(move);

        // Animator parametrelerini güncelle
        if (animator != null)
        {
            float speedMag = horizontalVelocity.magnitude;
            animator.SetFloat(SpeedParam, speedMag);
            // Karakter ne kadar hızlı hareket ederse koşma animasyonu da o oranda hızlanır (varsayılan 1x, depar atınca 1.4x)
            float speedMultiplier = Mathf.Max(0.8f, speedMag / 6f);
            animator.SetFloat(AnimSpeedMultiplierParam, speedMultiplier);
            animator.SetBool(IsGroundedParam, grounded);
        }
    }

    void LateUpdate()
    {
        PlayerInputState.EndFrame();
    }

    // Üzerinde durulan dönen çark ve döner silindir takibi (birbirinden ayrı)
    ContinuousRotator activeWheelRotator;
    RotatingCylinderObstacle activeCylinder;

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Karakterin alt kısmı (ayakları) bir nesneye değiyorsa
        if (hit.moveDirection.y < -0.3f && hit.normal.y > 0.35f)
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

    /// <summary>
    /// Trambolin veya fırlatıcı yüzeylerin oyuncuyu havaya fırlatması için kullanılır.
    /// </summary>
    public void Bounce(float bounceHeight)
    {
        verticalVelocity = Mathf.Sqrt(bounceHeight * -2f * gravity);
        coyoteCounter = 0f;
        jumpBufferCounter = 0f;
        activeWheelRotator = null;
        activeCylinder = null;
        if (animator != null) animator.SetTrigger(JumpTrigger);
    }
}

