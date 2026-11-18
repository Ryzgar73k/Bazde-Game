using UnityEngine;

/// <summary>
/// Unity Humanoid Foot IK (Sıfır Düz-Zemin Bozulması & Yumuşak Zemin Adaptasyonu):
/// - Identity Law: Düz zeminde (groundDelta ≈ 0) hedef pozisyon ve rotasyonu doğrudan animasyon
///   değerinde tutarak diz çökmesini, ayak kıvrılmasını ve ani sıçramaları (snap) tamamen önler.
/// - Kalça (Pelvis) ofsetini animasyon ayak yüksekliğinden değil yalnızca referans zemin ile
///   ayak altı zemini arasındaki gerçek yükseklik farkından (groundDelta) hesaplar.
/// - Dünya XZ ayak kilitlemesi ve düz-zemin diz ipucu uygulamaz; yalnızca yüksek basamağa
///   çıkıldığında kalça-ayak orta noktasına yumuşak diz yönlendirmesi verir.
/// - Köşe taşmalarında (overhang) ve yuvarlatılmış kenar pahlarında (bevel) ayağı destekli
///   canlı pozisyona yumuşakça geri söndürür.
/// </summary>
[RequireComponent(typeof(Animator))]
public class FootIK : MonoBehaviour
{
    [Header("Zemin Tarama")]
    public LayerMask groundMask = ~0;
    [Tooltip("İnce ışınlar isabet etmediğinde yedek kullanılacak dar tarama küresi yarıçapı (metre)")]
    public float footCastRadius = 0.032f;
    [Tooltip("Ayak bileğinden yukarı doğru tarama başlangıç payı (metre)")]
    public float raycastUpOffset = 0.25f;
    [Tooltip("Aşağı doğru maksimum zemin tarama mesafesi (metre)")]
    public float raycastDownDistance = 0.60f;
    [Tooltip("IK'nın ayağı tabandan yukarı çıkarabileceği maksimum adım yüksekliği (duvara tırmanmayı önler)")]
    public float maxFootRise = 0.20f;
    [Tooltip("Yürünebilir kabul edilen minimum zemin normali Y bileşeni (~0.65 ≈ 49 derece)")]
    public float minWalkableNormalY = 0.65f;
    [Tooltip("Ayak bileği ile ayakkabı tabanı arasındaki minimum/maksimum mesafe (metre)")]
    public float minSoleHeight = 0.035f;
    public float maxSoleHeight = 0.068f;
    [Tooltip("İnce taban yükseklik ayarı (+ yukarı, - aşağı, metre)")]
    [Range(-0.05f, 0.05f)]
    public float footGroundOffset = 0f;

    [Header("Düz Zemin Eşiği & Yumuşatma")]
    [Tooltip("Referans zeminden bu değerden az yükseklik farkı düz kabul edilir (metre)")]
    public float flatDeadzoneHeight = 0.015f;
    [Tooltip("Düz kabul edilecek minimum zemin normali Y değeri")]
    public float flatDeadzoneNormalY = 0.97f;
    [Tooltip("Ayak yükseklik ofsetinin yeni zemine uyum hızı (metre/saniye)")]
    public float footHeightLerpSpeed = 1.8f;

    [Header("Kalça (Pelvis), Diz ve Eğim")]
    [Tooltip("Eğimde veya basamak kenarında kalçanın inebileceği maksimum ergonomik mesafe (metre)")]
    public float maxPelvisDrop = 0.18f;
    [Tooltip("Kalçanın aşağı iniş yumuşatma hızı")]
    public float pelvisLerpSpeed = 9f;
    [Tooltip("Kalçanın düz zemine toparlanma hızı")]
    public float pelvisUpLerpSpeed = 13f;
    public float ikWeightLerpSpeed = 14f;
    [Tooltip("Ayak tabanının uyum sağlayacağı maksimum ileri/geri eğim açısı")]
    public float maxSlopeAlignAngle = 26f;
    [Tooltip("Ayak bileğinin yana doğru (roll) bükülebileceği maksimum açı")]
    public float maxSideRollAngle = 14f;
    [Range(0f, 1f)]
    public float kneeHintWeight = 0.35f;
    [Tooltip("Basamak çıkarken dizin öne doğru bükülme yönü mesafesi")]
    public float kneeForwardOffset = 0.22f;
    [Tooltip("Dizin hafif dışa açılma payı")]
    public float kneeOutwardOffset = 0.012f;

    [Header("Koşu Geçişi ve Köşe Koruması")]
    [Tooltip("Bu yatay Animator Speed değerinden sonra Foot IK ağırlığı koşu için azalmaya başlar")]
    public float runIkFadeStart = 0.6f;
    [Tooltip("Bu Speed değerinde Foot IK yerini tamamen koşu animasyonuna bırakır")]
    public float runIkFadeEnd = 2.4f;
    [Tooltip("Hareket halinde diz IK ipucunun tamamen kapanacağı Speed değeri")]
    public float kneeHintFadeEnd = 1.2f;
    [Tooltip("Düz olmayan köşe pahı / boru kenarı taşmalarında geçerli maksimum düşüş eşiği (metre)")]
    public float cornerEdgeDropThreshold = 0.12f;
    [Tooltip("Yalnızca köşe kenar küre teması yakalandığında düz sayılacak minimum normal Y")]
    public float edgeBevelNormalThreshold = 0.84f;

    [Header("Duvar Koruması")]
    public bool checkShinWallClearance = true;

    struct FootSideState
    {
        public float smoothWeight;
        public float smoothedHeightOffset;
        public Vector3 smoothedNormal;
        public bool initialized;
    }

    struct GroundSample
    {
        public bool hasHit;
        public RaycastHit hit;
        public bool usedSphereFallback;
        public bool isOverhanging;
        public float groundDelta;
    }

    Animator anim;
    PlayerController playerController;
    Transform rootTransform;
    Transform leftUpperLeg;
    Transform rightUpperLeg;

    readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    float ikBlend;
    float currentPelvisOffset;
    FootSideState leftState;
    FootSideState rightState;

    static readonly int SpeedParam = Animator.StringToHash("Speed");

    void Awake()
    {
        anim = GetComponent<Animator>();
        playerController = GetComponentInParent<PlayerController>();
        rootTransform = playerController != null ? playerController.transform : transform.root;

        if (anim != null && anim.isHuman)
        {
            leftUpperLeg = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            rightUpperLeg = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        }
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || anim == null || !anim.isHuman || !anim.isInitialized) return;
        if (!anim.IsInTransition(layerIndex) && anim.GetCurrentAnimatorClipInfoCount(layerIndex) == 0)
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        bool isGrounded = playerController == null || playerController.IsGrounded;
        bool isJumpingUp = playerController != null && playerController.VerticalVelocity > 1.2f;
        float speed = anim.GetFloat(SpeedParam);

        float speedIkFade = 1f - Mathf.InverseLerp(runIkFadeStart, runIkFadeEnd, speed);
        float targetBlend = (isGrounded && !isJumpingUp) ? speedIkFade : 0f;
        ikBlend = Mathf.MoveTowards(ikBlend, targetBlend, dt * ikWeightLerpSpeed);

        Vector3 animLeftPos = anim.GetIKPosition(AvatarIKGoal.LeftFoot);
        Quaternion animLeftRot = anim.GetIKRotation(AvatarIKGoal.LeftFoot);
        Vector3 animRightPos = anim.GetIKPosition(AvatarIKGoal.RightFoot);
        Quaternion animRightRot = anim.GetIKRotation(AvatarIKGoal.RightFoot);

        if (ikBlend <= 0.001f)
        {
            FadeOutIK(animLeftPos, animLeftRot, animRightPos, animRightRot, dt);
            return;
        }

        float soleL = Mathf.Clamp(anim.leftFeetBottomHeight, minSoleHeight, maxSoleHeight) + footGroundOffset;
        float soleR = Mathf.Clamp(anim.rightFeetBottomHeight, minSoleHeight, maxSoleHeight) + footGroundOffset;
        float modelFloorY = transform.position.y;

        // Animasyonun kendi ayağı yerden ne kadar kaldırdığını ölç (hızdan bağımsız: blend/idle dahil)
        float animLiftL = Mathf.Max(0f, (animLeftPos.y - soleL) - modelFloorY);
        float animLiftR = Mathf.Max(0f, (animRightPos.y - soleR) - modelFloorY);

        Vector3 rootPos = rootTransform != null ? rootTransform.position : transform.position;
        Vector3 refFwd = rootTransform != null ? rootTransform.forward : transform.forward;
        Vector3 refRight = rootTransform != null ? rootTransform.right : transform.right;

        bool hasCenterFloor = TryRaycastBestGround(
            new Vector3(rootPos.x, modelFloorY + raycastUpOffset, rootPos.z),
            raycastUpOffset + raycastDownDistance,
            modelFloorY - maxPelvisDrop - 0.12f,
            modelFloorY + maxFootRise,
            out RaycastHit centerHit);

        float referenceFloorY = (hasCenterFloor && Mathf.Abs(centerHit.point.y - modelFloorY) <= 0.05f)
            ? centerHit.point.y
            : modelFloorY;

        GroundSample sampleL = SampleFootGround(
            animLeftPos,
            GetPlanarFootForward(animLeftRot, refFwd),
            leftUpperLeg,
            soleL,
            modelFloorY);

        GroundSample sampleR = SampleFootGround(
            animRightPos,
            GetPlanarFootForward(animRightRot, refFwd),
            rightUpperLeg,
            soleR,
            modelFloorY);

        ResolveCornerAndBevelSamples(modelFloorY, hasCenterFloor, centerHit, ref sampleL, ref sampleR);
        FinalizeGroundDelta(referenceFloorY, ref sampleL);
        FinalizeGroundDelta(referenceFloorY, ref sampleR);

        float targetPelvisOffset = ComputeTargetPelvisOffset(animLiftL, sampleL, animLiftR, sampleR);
        float scaledTargetPelvis = targetPelvisOffset * ikBlend;
        float pelvisRate = scaledTargetPelvis < currentPelvisOffset ? pelvisLerpSpeed : pelvisUpLerpSpeed;
        currentPelvisOffset = Mathf.Lerp(currentPelvisOffset, scaledTargetPelvis, 1f - Mathf.Exp(-pelvisRate * dt));
        if (Mathf.Abs(currentPelvisOffset) > 0.0002f)
            anim.bodyPosition += Vector3.up * currentPelvisOffset;
        else if (Mathf.Abs(scaledTargetPelvis) < 0.0002f)
            currentPelvisOffset = 0f;

        ApplyFootGoal(
            AvatarIKGoal.LeftFoot,
            AvatarIKHint.LeftKnee,
            leftUpperLeg,
            true,
            animLeftPos,
            animLeftRot,
            soleL,
            animLiftL,
            sampleL,
            speed,
            refFwd,
            refRight,
            dt,
            ref leftState);

        ApplyFootGoal(
            AvatarIKGoal.RightFoot,
            AvatarIKHint.RightKnee,
            rightUpperLeg,
            false,
            animRightPos,
            animRightRot,
            soleR,
            animLiftR,
            sampleR,
            speed,
            refFwd,
            refRight,
            dt,
            ref rightState);
    }

    void FadeOutIK(
        Vector3 animLeftPos,
        Quaternion animLeftRot,
        Vector3 animRightPos,
        Quaternion animRightRot,
        float dt)
    {
        currentPelvisOffset = Mathf.Lerp(currentPelvisOffset, 0f, 1f - Mathf.Exp(-pelvisUpLerpSpeed * dt));
        if (Mathf.Abs(currentPelvisOffset) > 0.0002f)
            anim.bodyPosition += Vector3.up * currentPelvisOffset;
        else
            currentPelvisOffset = 0f;

        FadeSingleGoalToZero(AvatarIKGoal.LeftFoot, AvatarIKHint.LeftKnee, animLeftPos, animLeftRot, dt, ref leftState);
        FadeSingleGoalToZero(AvatarIKGoal.RightFoot, AvatarIKHint.RightKnee, animRightPos, animRightRot, dt, ref rightState);
    }

    void FadeSingleGoalToZero(
        AvatarIKGoal goal,
        AvatarIKHint kneeHint,
        Vector3 animFootPos,
        Quaternion animFootRot,
        float dt,
        ref FootSideState state)
    {
        AdvanceFootState(false, 0f, Vector3.up, 0f, ikWeightLerpSpeed * 0.85f, dt, ref state);

        anim.SetIKPositionWeight(goal, state.smoothWeight);
        anim.SetIKRotationWeight(goal, state.smoothWeight);
        anim.SetIKHintPositionWeight(kneeHint, 0f);

        if (state.smoothWeight > 0.001f)
        {
            anim.SetIKPosition(goal, animFootPos + Vector3.up * state.smoothedHeightOffset);
            anim.SetIKRotation(goal, Quaternion.FromToRotation(Vector3.up, state.smoothedNormal) * animFootRot);
        }
    }

    void AdvanceFootState(
        bool hasTarget,
        float targetHeight,
        Vector3 targetNormal,
        float targetWeight,
        float weightRate,
        float dt,
        ref FootSideState state)
    {
        if (!state.initialized)
        {
            state.smoothedNormal = Vector3.up;
            state.initialized = true;
        }

        state.smoothedHeightOffset = Mathf.MoveTowards(
            state.smoothedHeightOffset,
            hasTarget ? targetHeight : 0f,
            dt * footHeightLerpSpeed);
        state.smoothedNormal = Vector3.Slerp(
            state.smoothedNormal,
            hasTarget ? targetNormal : Vector3.up,
            1f - Mathf.Exp(-ikWeightLerpSpeed * dt));
        state.smoothWeight = Mathf.MoveTowards(
            state.smoothWeight,
            hasTarget ? targetWeight : 0f,
            dt * weightRate);
    }

    GroundSample SampleFootGround(
        Vector3 queryPos,
        Vector3 footFwd,
        Transform upperLeg,
        float soleHeight,
        float modelFloorY)
    {
        GroundSample sample = default;
        sample.hasHit = TryFindGroundUnderFoot(
            queryPos,
            footFwd,
            modelFloorY,
            out sample.hit,
            out sample.usedSphereFallback);

        if (sample.hasHit && sample.hit.point.y > modelFloorY + 0.05f &&
            IsFootBlockedByWall(upperLeg, sample.hit.point, soleHeight, modelFloorY))
        {
            sample.hasHit = false;
        }

        return sample;
    }

    void ResolveCornerAndBevelSamples(
        float modelFloorY,
        bool hasCenterFloor,
        RaycastHit centerHit,
        ref GroundSample sampleL,
        ref GroundSample sampleR)
    {
        bool centerOnTop = hasCenterFloor && centerHit.point.y >= modelFloorY - 0.06f;
        bool footLOnTop = sampleL.hasHit && sampleL.hit.point.y >= modelFloorY - 0.06f;
        bool footROnTop = sampleR.hasHit && sampleR.hit.point.y >= modelFloorY - 0.06f;
        bool hasTopSupport = centerOnTop || footLOnTop || footROnTop;

        float supportedFloorY = centerOnTop ? centerHit.point.y : modelFloorY;

        if (hasTopSupport)
        {
            MarkOverhangIfNeeded(supportedFloorY, ref sampleL);
            MarkOverhangIfNeeded(supportedFloorY, ref sampleR);
        }

        FlattenCornerBevelNormal(hasCenterFloor, centerHit, ref sampleL);
        FlattenCornerBevelNormal(hasCenterFloor, centerHit, ref sampleR);
    }

    void MarkOverhangIfNeeded(float supportedFloorY, ref GroundSample sample)
    {
        if (!sample.hasHit) return;

        float drop = supportedFloorY - sample.hit.point.y;
        bool isFlatWalkableLowerFloor = !sample.usedSphereFallback && sample.hit.normal.y >= 0.85f;
        float dropLimit = isFlatWalkableLowerFloor ? maxPelvisDrop : cornerEdgeDropThreshold;
        if (drop > dropLimit)
            sample.isOverhanging = true;
    }

    void FlattenCornerBevelNormal(bool hasCenterFloor, RaycastHit centerHit, ref GroundSample sample)
    {
        if (!sample.hasHit) return;

        // İnce ışınlar ıskalayıp yalnızca küre kenarı pah yakaladıysa dik açı uydurmasını önle
        if (sample.usedSphereFallback && sample.hit.normal.y < edgeBevelNormalThreshold)
        {
            sample.hit.normal = Vector3.up;
            return;
        }

        // Karakter düz bir blok üstündeyken (center normal ~1) aynı bloğun kenar pahı ayağı yana bükmesin
        if (hasCenterFloor
            && centerHit.normal.y >= 0.96f
            && sample.hit.collider == centerHit.collider
            && sample.hit.point.y < centerHit.point.y - 0.004f
            && sample.hit.normal.y < 0.88f)
        {
            sample.hit.normal = Vector3.up;
        }
    }

    void FinalizeGroundDelta(float referenceFloorY, ref GroundSample sample)
    {
        if (!sample.hasHit)
        {
            sample.groundDelta = 0f;
            return;
        }

        float rawDelta = sample.hit.point.y - referenceFloorY;
        if (Mathf.Abs(rawDelta) < flatDeadzoneHeight && sample.hit.normal.y >= flatDeadzoneNormalY)
        {
            rawDelta = 0f;
            sample.hit.normal = Vector3.up;
        }

        sample.groundDelta = Mathf.Clamp(rawDelta, -maxPelvisDrop, maxFootRise);
    }

    float ComputeTargetPelvisOffset(
        float animLiftL,
        GroundSample sampleL,
        float animLiftR,
        GroundSample sampleR)
    {
        bool hasAnyDiff = false;
        float minDiff = 0f;

        AccumulatePelvisDiff(sampleL, animLiftL, ref hasAnyDiff, ref minDiff);
        AccumulatePelvisDiff(sampleR, animLiftR, ref hasAnyDiff, ref minDiff);

        if (!hasAnyDiff) return 0f;
        return Mathf.Clamp(minDiff, -maxPelvisDrop, 0f);
    }

    void AccumulatePelvisDiff(
        GroundSample sample,
        float animLift,
        ref bool hasAnyDiff,
        ref float minDiff)
    {
        // Havadaki adım ayağı, köşeden dışarı taşan ayak veya düz/yüksek zemin kalçayı aşağı çekmez
        if (!sample.hasHit || sample.isOverhanging || animLift > 0.045f) return;

        float d = sample.groundDelta;
        if (d >= -flatDeadzoneHeight) return;

        minDiff = hasAnyDiff ? Mathf.Min(minDiff, d) : d;
        hasAnyDiff = true;
    }

    void ApplyFootGoal(
        AvatarIKGoal goal,
        AvatarIKHint kneeHint,
        Transform upperLeg,
        bool isLeft,
        Vector3 animFootPos,
        Quaternion animFootRot,
        float soleHeight,
        float animLift,
        GroundSample sample,
        float speed,
        Vector3 refFwd,
        Vector3 refRight,
        float dt,
        ref FootSideState state)
    {
        bool validSupport = sample.hasHit && !sample.isOverhanging;
        if (validSupport)
        {
            Vector3 targetNormal = ClampGroundNormal(sample.hit.normal, refRight);
            bool needsIK = Mathf.Abs(sample.groundDelta) >= 0.001f
                || targetNormal.y < 0.9995f
                || Mathf.Abs(currentPelvisOffset) > 0.002f
                || Mathf.Abs(state.smoothedHeightOffset) > 0.002f;
            float swingFade = 1f - Mathf.InverseLerp(0.05f, 0.15f, animLift);
            float targetWeight = needsIK ? ikBlend * Mathf.Clamp01(swingFade) : 0f;
            AdvanceFootState(true, sample.groundDelta, targetNormal, targetWeight, ikWeightLerpSpeed * 0.75f, dt, ref state);
        }
        else
        {
            AdvanceFootState(false, 0f, Vector3.up, 0f, ikWeightLerpSpeed * 0.85f, dt, ref state);
        }

        anim.SetIKPositionWeight(goal, state.smoothWeight);
        anim.SetIKRotationWeight(goal, state.smoothWeight);

        if (state.smoothWeight <= 0.001f)
        {
            anim.SetIKHintPositionWeight(kneeHint, 0f);
            return;
        }

        Vector3 targetPos = animFootPos + Vector3.up * state.smoothedHeightOffset;
        if (validSupport && state.smoothedHeightOffset > 0.005f)
        {
            float minStepY = sample.hit.point.y + soleHeight;
            float stepBlend = Mathf.InverseLerp(0.08f, 0.01f, animLift);
            targetPos.y = Mathf.Max(targetPos.y, Mathf.Lerp(targetPos.y, minStepY, stepBlend));
        }

        Quaternion slopeTilt = Quaternion.FromToRotation(Vector3.up, state.smoothedNormal);
        Quaternion targetRot = slopeTilt * animFootRot;

        anim.SetIKPosition(goal, targetPos);
        anim.SetIKRotation(goal, targetRot);

        // Düz veya alçak zeminde animasyonun kendi diz yönünü koru; yalnızca yüksek basamağa çıkarken öne diz ipucu ver
        if (state.smoothedHeightOffset > 0.03f && kneeHintWeight > 0.001f)
        {
            float speedFade = 1f - Mathf.InverseLerp(0.45f, Mathf.Max(0.8f, kneeHintFadeEnd), speed);
            float hintAlpha = Mathf.InverseLerp(0.03f, 0.14f, state.smoothedHeightOffset)
                * state.smoothWeight
                * kneeHintWeight
                * speedFade;

            anim.SetIKHintPositionWeight(kneeHint, hintAlpha);
            if (hintAlpha > 0.001f)
            {
                Vector3 hipPos = upperLeg != null ? upperLeg.position : targetPos + Vector3.up * 0.55f;
                Vector3 midPoint = Vector3.Lerp(hipPos, targetPos, 0.5f);
                Vector3 footFwd = GetPlanarFootForward(animFootRot, refFwd);
                Vector3 kneeHintPos = midPoint + footFwd * kneeForwardOffset + refRight * (isLeft ? -kneeOutwardOffset : kneeOutwardOffset);
                anim.SetIKHintPosition(kneeHint, kneeHintPos);
            }
        }
        else
        {
            anim.SetIKHintPositionWeight(kneeHint, 0f);
        }
    }

    Vector3 ClampGroundNormal(Vector3 rawNormal, Vector3 refRight)
    {
        if (rawNormal.sqrMagnitude < 0.0001f) return Vector3.up;
        Vector3 normal = Vector3.RotateTowards(Vector3.up, rawNormal.normalized, maxSlopeAlignAngle * Mathf.Deg2Rad, 0f);

        float maxSide = Mathf.Sin(maxSideRollAngle * Mathf.Deg2Rad);
        float side = Vector3.Dot(normal, refRight);
        if (Mathf.Abs(side) > maxSide)
        {
            normal = (normal - refRight * side + refRight * Mathf.Clamp(side, -maxSide, maxSide)).normalized;
            normal = Vector3.RotateTowards(Vector3.up, normal, maxSlopeAlignAngle * Mathf.Deg2Rad, 0f);
        }

        return normal;
    }

    Vector3 GetPlanarFootForward(Quaternion footRot, Vector3 fallbackForward)
    {
        Vector3 fwd = Vector3.ProjectOnPlane(footRot * Vector3.forward, Vector3.up);
        if (fwd.sqrMagnitude > 0.001f) return fwd.normalized;
        Vector3 rootPlanar = Vector3.ProjectOnPlane(fallbackForward, Vector3.up);
        return rootPlanar.sqrMagnitude > 0.001f ? rootPlanar.normalized : Vector3.forward;
    }

    bool IsFootBlockedByWall(Transform upperLeg, Vector3 groundPoint, float soleHeight, float modelFloorY)
    {
        if (!checkShinWallClearance) return false;

        Vector3 pelvisRef = upperLeg != null
            ? upperLeg.position
            : (rootTransform != null ? rootTransform.position : transform.position) + Vector3.up * 0.55f;

        float shinCheckY = Mathf.Max(modelFloorY + 0.14f, groundPoint.y + soleHeight + 0.14f);
        Vector3 start = new Vector3(pelvisRef.x, shinCheckY, pelvisRef.z);
        Vector3 end = new Vector3(groundPoint.x, shinCheckY, groundPoint.z);
        Vector3 delta = end - start;
        float dist = delta.magnitude;
        if (dist <= 0.06f) return false;

        Vector3 dir = delta / dist;
        float checkLen = Mathf.Max(0.01f, dist - 0.04f);
        int count = Physics.SphereCastNonAlloc(start, 0.045f, dir, hitBuffer, checkLen, groundMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (h.collider == null || h.collider.isTrigger) continue;
            if (rootTransform != null && h.collider.transform.IsChildOf(rootTransform)) continue;
            if (Mathf.Abs(h.normal.y) < 0.45f)
                return true;
        }
        return false;
    }

    bool TryFindGroundUnderFoot(
        Vector3 footWorldPos,
        Vector3 footForward,
        float modelFloorY,
        out RaycastHit bestHit,
        out bool usedSphereFallback)
    {
        bestHit = default;
        usedSphereFallback = false;

        float startY = Mathf.Max(footWorldPos.y, modelFloorY) + raycastUpOffset;
        float maxDist = raycastUpOffset + raycastDownDistance;
        float minY = modelFloorY - maxPelvisDrop - 0.12f;
        float maxY = modelFloorY + maxFootRise;

        // 1. Öncelik: Ayak bileği ve tarak altında ince dikey ışınlar (düz blok köşesinde sahte sphere normali üretmez)
        Vector3 ankleOrigin = new Vector3(footWorldPos.x, startY, footWorldPos.z);
        bool hasAnkle = TryRaycastBestGround(ankleOrigin, maxDist, minY, maxY, out RaycastHit ankleHit);

        Vector3 ballPos = footWorldPos + footForward * 0.055f;
        Vector3 ballOrigin = new Vector3(ballPos.x, startY, ballPos.z);
        bool hasBall = TryRaycastBestGround(ballOrigin, maxDist, minY, maxY, out RaycastHit ballHit);

        if (hasAnkle && hasBall)
        {
            // Biri köşede aşağı düşüp diğeri üst yüzeydeyse destekli üst yüzeyi koru; aynı seviyedeyken düz normali seç
            if (ballHit.point.y > ankleHit.point.y + 0.035f)
                bestHit = ballHit;
            else if (ankleHit.point.y > ballHit.point.y + 0.035f)
                bestHit = ankleHit;
            else
                bestHit = ballHit.normal.y > ankleHit.normal.y + 0.05f ? ballHit : ankleHit;
            return true;
        }

        if (hasAnkle)
        {
            bestHit = ankleHit;
            return true;
        }

        if (hasBall)
        {
            bestHit = ballHit;
            return true;
        }

        // 2. Yedek: İnce korkuluk/silindir kenarları için dar SphereCast
        int count = Physics.SphereCastNonAlloc(
            ankleOrigin,
            footCastRadius,
            Vector3.down,
            hitBuffer,
            maxDist,
            groundMask,
            QueryTriggerInteraction.Ignore);

        float bestDist = float.MaxValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (!IsValidGroundHit(h, minY, maxY, true)) continue;
            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                bestHit = h;
                found = true;
            }
        }

        usedSphereFallback = found;
        return found;
    }

    bool TryRaycastBestGround(Vector3 origin, float maxDist, float minY, float maxY, out RaycastHit bestHit)
    {
        bestHit = default;
        int count = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            hitBuffer,
            maxDist,
            groundMask,
            QueryTriggerInteraction.Ignore);

        float bestDist = float.MaxValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (!IsValidGroundHit(h, minY, maxY, false)) continue;
            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                bestHit = h;
                found = true;
            }
        }

        return found;
    }

    bool IsValidGroundHit(RaycastHit h, float minY, float maxY, bool isSphereCast)
    {
        if (h.collider == null || h.collider.isTrigger) return false;
        if (rootTransform != null && h.collider.transform.IsChildOf(rootTransform)) return false;
        if (isSphereCast && h.distance <= 0.001f && h.point == Vector3.zero) return false;
        if (h.normal.y < minWalkableNormalY) return false;
        if (h.point.y > maxY || h.point.y < minY) return false;
        return true;
    }
}
