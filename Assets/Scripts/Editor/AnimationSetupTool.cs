using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Parkur > Animasyonları Ayarla ve Bağla
/// FBX kliplerinin döngüsünü (Loop) ve Root Transform (Bake Into Pose) ayarlarını yapar,
/// sola kaymayı ve ayak burkulmasını önler, hız çarpanını bağlar ve karaktere atar.
/// </summary>
public static class AnimationSetupTool
{
    const string IdleAnimPath = "Assets/char@Bored.fbx";
    const string RunAnimPath = "Assets/X Bot@Running.fbx";
    const string JumpAnimPath = "Assets/X Bot@Jumping.fbx";

    [MenuItem("Tools/Parkur/Animasyonları Ayarla ve Bağla")]
    public static void SetupAnimations()
    {
        string charPath = "Assets/char.fbx";

        // Karakter modelini Humanoid olarak garantiye al
        var charImporter = AssetImporter.GetAtPath(charPath) as ModelImporter;
        if (charImporter != null && charImporter.animationType != ModelImporterAnimationType.Human)
        {
            charImporter.animationType = ModelImporterAnimationType.Human;
            charImporter.SaveAndReimport();
        }

        // Animasyon FBX ayarlarını yapılandır (Loop + Bake Into Pose ile sola yatmayı/kaymayı engelle)
        ConfigureAnimFbx(IdleAnimPath, groundFeet: true, loop: true);
        ConfigureAnimFbx(RunAnimPath, groundFeet: true, loop: true);
        ConfigureJumpSubClips(JumpAnimPath);

        RebuildController();
    }

    /// <summary>
    /// Controller eski sürümden kalınsa veya Locomotion BlendTree / VerticalVelocity yoksa günceller.
    /// </summary>
    public static void EnsureUpToDateController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/PlayerAnimatorController.controller");
        if (controller == null) return;

        bool hasVerticalVel = false;
        foreach (var p in controller.parameters)
        {
            if (p.name == "VerticalVelocity") { hasVerticalVel = true; break; }
        }

        bool hasExpectedIdleMotion = false;
        bool hasUpdatedFallTransition = false;
        bool hasJumpBlendTree = false;
        AnimationClip expectedIdle = LoadClip(IdleAnimPath);
        var layers = controller.layers;
        if (layers != null && layers.Length > 0 && layers[0].stateMachine != null)
        {
            var sm = layers[0].stateMachine;
            foreach (var cs in sm.states)
            {
                if (cs.state != null && cs.state.name == "Jump")
                {
                    hasJumpBlendTree = cs.state.motion is BlendTree && !cs.state.speedParameterActive;
                    break;
                }
            }
            if (sm.defaultState != null && sm.defaultState.motion is BlendTree tree)
            {
                var children = tree.children;
                hasExpectedIdleMotion = children.Length > 0 && (expectedIdle == null || children[0].motion == expectedIdle);
                foreach (var t in sm.defaultState.transitions)
                {
                    if (t.destinationState != null
                        && t.destinationState.name == "Jump"
                        && t.interruptionSource == TransitionInterruptionSource.Destination)
                    {
                        foreach (var c in t.conditions)
                        {
                            if (c.parameter == "VerticalVelocity" && Mathf.Approximately(c.threshold, -3.5f))
                            {
                                hasUpdatedFallTransition = true;
                                break;
                            }
                        }
                    }
                    if (hasUpdatedFallTransition) break;
                }
            }
        }

        if (hasVerticalVel && hasExpectedIdleMotion && hasUpdatedFallTransition && hasJumpBlendTree) return;

        ConfigureAnimFbx(IdleAnimPath, groundFeet: true, loop: true);
        ConfigureJumpSubClips(JumpAnimPath);
        RebuildController();
    }

    static void RebuildController()
    {
        string controllerPath = "Assets/PlayerAnimatorController.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }

        EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "AnimSpeedMultiplier", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "IsGrounded", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "VerticalVelocity", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "Jump", AnimatorControllerParameterType.Trigger);

        AnimationClip idleClip = LoadClip(IdleAnimPath);
        AnimationClip runClip = LoadClip(RunAnimPath);
        AnimationClip jumpRiseClip = LoadClip(JumpAnimPath, "JumpRise") ?? LoadClip(JumpAnimPath);
        AnimationClip jumpFallClip = LoadClip(JumpAnimPath, "JumpFall") ?? jumpRiseClip;

        // Base Layer IK Pass açık (FootIK için zorunlu)
        var layers = controller.layers;
        if (layers != null && layers.Length > 0)
        {
            layers[0].iKPass = true;
            controller.layers = layers;
        }
        var rootStateMachine = controller.layers[0].stateMachine;
        rootStateMachine.states = new ChildAnimatorState[0];
        rootStateMachine.anyStateTransitions = new AnimatorStateTransition[0];

        // Eski sub-asset BlendTree, AnimatorState ve AnimatorStateTransition artıkları birikmesin diye temizle
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(controllerPath))
        {
            if (obj is BlendTree || obj is AnimatorState || obj is AnimatorStateTransition)
                Object.DestroyImmediate(obj, true);
        }

        // 1D Blend Tree: Idle (0) -> Run (4.2) yumuşak hareket geçişi
        AnimatorState locomotionState = controller.CreateBlendTreeInController("Locomotion", out BlendTree locomotionTree, 0);
        locomotionTree.name = "Locomotion";
        locomotionTree.blendType = BlendTreeType.Simple1D;
        locomotionTree.blendParameter = "Speed";
        locomotionTree.useAutomaticThresholds = false;
        if (idleClip != null) locomotionTree.AddChild(idleClip, 0f);
        if (runClip != null) locomotionTree.AddChild(runClip, 4.2f);
        locomotionState.iKOnFeet = false;
        locomotionState.speedParameterActive = true;
        locomotionState.speedParameter = "AnimSpeedMultiplier";

        // 1D Blend Tree (1.0x doğal hız): VerticalVelocity düşerken hemen iniş pozuna geçer
        AnimatorState jumpState = controller.CreateBlendTreeInController("Jump", out BlendTree jumpTree, 0);
        jumpTree.name = "Jump";
        jumpTree.blendType = BlendTreeType.Simple1D;
        jumpTree.blendParameter = "VerticalVelocity";
        jumpTree.useAutomaticThresholds = false;
        if (jumpFallClip != null) jumpTree.AddChild(jumpFallClip, -4.0f);
        if (jumpRiseClip != null) jumpTree.AddChild(jumpRiseClip, 4.5f);
        jumpState.iKOnFeet = false;
        jumpState.speedParameterActive = false;

        var states = rootStateMachine.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state == jumpState)
            {
                states[i].position = new Vector3(330, -120, 0);
                rootStateMachine.states = states;
                break;
            }
        }

        rootStateMachine.defaultState = locomotionState;

        // Düşme geçişi (yalnızca gerçek yüksek düşüşte; yere inilirse geçiş anında kesilebilir)
        var fallTransition = locomotionState.AddTransition(jumpState);
        fallTransition.hasExitTime = false;
        fallTransition.duration = 0.14f;
        fallTransition.interruptionSource = TransitionInterruptionSource.Destination;
        fallTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
        fallTransition.AddCondition(AnimatorConditionMode.Less, -3.5f, "VerticalVelocity");

        // Yere iniş geçişi (kısa zıplamalarda hızlı toparlanma ve anında tekrar zıplayabilme)
        var jumpToLocomotion = jumpState.AddTransition(locomotionState);
        jumpToLocomotion.hasExitTime = false;
        jumpToLocomotion.duration = 0.08f;
        jumpToLocomotion.interruptionSource = TransitionInterruptionSource.Destination;
        jumpToLocomotion.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        jumpToLocomotion.AddCondition(AnimatorConditionMode.Less, 0.5f, "VerticalVelocity");

        // AnyState -> Jump (trambolinde üst üste sıçrayışta da yeniden başlar)
        var anyToJump = rootStateMachine.AddAnyStateTransition(jumpState);
        anyToJump.hasExitTime = false;
        anyToJump.duration = 0.06f;
        anyToJump.canTransitionToSelf = true;
        anyToJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");

        AssetDatabase.SaveAssets();

        // Sahnede Player nesnesindeki Animator'a gerekiyorsa ata
        var player = GameObject.Find("Player");
        if (player != null)
        {
            var anim = player.GetComponentInChildren<Animator>();
            if (anim != null && (anim.runtimeAnimatorController != controller || anim.applyRootMotion))
            {
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
                EditorUtility.SetDirty(anim);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            var pc = player.GetComponent<PlayerController>();
            if (pc != null && pc.animator != anim)
            {
                pc.animator = anim;
                EditorUtility.SetDirty(pc);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
        }

        Debug.Log("Animasyonlar Remy T-Pose iskeletiyle eşlendi, döngüye alındı ve sola kayma kilitlendi!");
    }

    static ModelImporter EnsureHumanImporter(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        }
        if (importer == null) return null;
        if (importer.animationType != ModelImporterAnimationType.Human)
            importer.animationType = ModelImporterAnimationType.Human;
        if (importer.avatarSetup == ModelImporterAvatarSetup.NoAvatar)
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        return importer;
    }

    static ModelImporterClipAnimation BuildClipWithPoseSettings(ModelImporterClipAnimation src, bool groundFeet, bool loop, bool clone = false)
    {
        var clip = clone ? new ModelImporterClipAnimation { takeName = src.takeName } : src;
        clip.loopTime = loop;
        clip.loopPose = loop;
        clip.lockRootRotation = true;
        clip.lockRootHeightY = true;
        clip.lockRootPositionXZ = groundFeet;
        clip.keepOriginalOrientation = false;
        clip.keepOriginalPositionY = !groundFeet;
        clip.keepOriginalPositionXZ = false;
        clip.heightFromFeet = groundFeet;
        return clip;
    }

    static void ConfigureAnimFbx(string assetPath, bool groundFeet, bool loop)
    {
        var importer = EnsureHumanImporter(assetPath);
        if (importer == null) return;

        var clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) return;

        for (int i = 0; i < clips.Length; i++)
            clips[i] = BuildClipWithPoseSettings(clips[i], groundFeet, loop);
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    static void ConfigureJumpSubClips(string assetPath)
    {
        var importer = EnsureHumanImporter(assetPath);
        if (importer == null) return;

        var def = importer.defaultClipAnimations;
        if (def == null || def.Length == 0) return;
        var baseClip = def[0];

        var rise = BuildClipWithPoseSettings(baseClip, groundFeet: false, loop: false, clone: true);
        rise.name = "JumpRise";
        rise.firstFrame = Mathf.Min(1f, baseClip.lastFrame);
        rise.lastFrame = Mathf.Min(10f, baseClip.lastFrame);

        var fall = BuildClipWithPoseSettings(baseClip, groundFeet: false, loop: false, clone: true);
        fall.name = "JumpFall";
        fall.firstFrame = Mathf.Min(13f, baseClip.lastFrame);
        fall.lastFrame = Mathf.Min(21f, baseClip.lastFrame);

        importer.clipAnimations = new[] { rise, fall };
        importer.SaveAndReimport();
    }

    static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (var p in controller.parameters)
        {
            if (p.name == name) return;
        }
        controller.AddParameter(name, type);
    }

    static AnimationClip LoadClip(string assetPath, string exactName = null)
    {
        var objs = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        foreach (var obj in objs)
        {
            if (obj is AnimationClip clip && !clip.name.Contains("__preview__")
                && (exactName == null || clip.name == exactName))
            {
                return clip;
            }
        }
        return null;
    }
}
