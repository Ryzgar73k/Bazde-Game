using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Parkur > Animasyonları Ayarla ve Bağla
/// Assets/Animations/ altındaki Mixamo FBX kliplerini otomatik tanır (yoksa varsayılan FBX'lere düşer),
/// Humanoid + Loop + Pose ayarlarını yapıp 6+ durumlu PlayerAnimatorController'ı kurar.
/// </summary>
public static class AnimationSetupTool
{
    const string AnimationsFolder = "Assets/Animations";
    const string BasicMotionsMaleFolder = "Assets/Kevin Iglesias/Human Animations/Animations/Male";
    const string FallbackIdlePath = "Assets/char@Bored.fbx";
    const string FallbackRunPath = "Assets/X Bot@Running.fbx";
    const string FallbackJumpPath = "Assets/X Bot@Jumping.fbx";
    const string ControllerPath = "Assets/PlayerAnimatorController.controller";
    const string CleanJumpAirAssetPath = "Assets/Animations/BM_JumpAir_Clean.anim";
    const string CleanJumpLandAssetPath = "Assets/Animations/BM_JumpLand_Clean.anim";

    struct ClipSet
    {
        public AnimationClip idle;
        public AnimationClip walk;
        public AnimationClip run;
        public AnimationClip sprint;
        public AnimationClip jumpUp;
        public AnimationClip runJump;
        public AnimationClip inAir;
        public AnimationClip fallLoop;
        public AnimationClip land;
        public AnimationClip bounce;
        public AnimationClip bounce2;
        public AnimationClip mantle;
    }

    [MenuItem("Tools/Parkur/Animasyonları Ayarla ve Bağla")]
    public static void SetupAnimations()
    {
        EnsureCharacterHumanoid();
        ClipSet clips = PrepareAllClips();
        RebuildController(clips);
    }

    /// <summary>
    /// Controller eski sürümden kalınsa (6+ durum / JumpVariant / BounceVariant / BounceSpeed / InAir eksikse) günceller.
    /// </summary>
    public static void EnsureUpToDateController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) return;

        bool hasVerticalVel = HasParam(controller, "VerticalVelocity");
        bool hasBounce = HasParam(controller, "Bounce");
        bool hasBounceSpeed = HasParam(controller, "BounceSpeed");
        bool hasLand = HasParam(controller, "Land");
        bool hasMantle = HasParam(controller, "Mantle");
        bool hasJumpVariant = HasParam(controller, "JumpVariant");
        bool hasBounceVariant = HasParam(controller, "BounceVariant");
        bool hasJumpUpState = false;
        bool hasInAirState = false;
        bool hasFallLoopState = false;

        var layers = controller.layers;
        if (layers != null && layers.Length > 0 && layers[0].stateMachine != null)
        {
            foreach (var cs in layers[0].stateMachine.states)
            {
                if (cs.state == null) continue;
                if (cs.state.name == "JumpUp") hasJumpUpState = true;
                else if (cs.state.name == "InAir") hasInAirState = true;
                else if (cs.state.name == "FallLoop") hasFallLoopState = true;
            }
        }

        if (hasVerticalVel && hasBounce && hasBounceSpeed && hasLand && hasMantle && hasJumpVariant && hasBounceVariant
            && hasJumpUpState && hasInAirState && hasFallLoopState)
            return;

        EnsureCharacterHumanoid();
        ClipSet clips = PrepareAllClips();
        RebuildController(clips);
    }

    static bool HasParam(AnimatorController controller, string name)
    {
        foreach (var p in controller.parameters)
        {
            if (p.name == name) return true;
        }
        return false;
    }

    static void EnsureCharacterHumanoid()
    {
        var charImporter = AssetImporter.GetAtPath("Assets/char.fbx") as ModelImporter;
        if (charImporter != null && charImporter.animationType != ModelImporterAnimationType.Human)
        {
            charImporter.animationType = ModelImporterAnimationType.Human;
            charImporter.SaveAndReimport();
        }
    }

    static ClipSet PrepareAllClips()
    {
        // Kevin Iglesias Basic Motions (Male) klipleri zaten Humanoid + CopyFromOther + Loop ayarlidir; ModelImporter degistirilmez.
        AnimationClip bmIdle = LoadBasicMotionClip("Idles/HumanM@Idle01.fbx");
        AnimationClip bmWalk = LoadBasicMotionClip("Movement/Walk/HumanM@Walk01_Forward.fbx");
        AnimationClip bmRun = LoadBasicMotionClip("Movement/Run/HumanM@Run01_Forward.fbx");
        AnimationClip bmSprint = LoadBasicMotionClip("Movement/Sprint/HumanM@Sprint01_Forward.fbx");
        AnimationClip bmJumpBegin = LoadBasicMotionClip("Movement/Jump/HumanM@Jump01 - Begin.fbx");
        AnimationClip bmInAir = LoadBasicMotionClip("Movement/Jump/HumanM@Fall01.fbx");
        AnimationClip bmJumpLand = LoadBasicMotionClip("Movement/Jump/HumanM@Jump01 - Land.fbx");

        AnimationClip bmJumpClean = bmJumpBegin != null
            ? BuildTrimmedAnimAsset(bmJumpBegin, CleanJumpAirAssetPath, 0.00f, 0.44f, loop: false)
            : null;
        AnimationClip bmLandClean = bmJumpLand != null
            ? BuildTrimmedAnimAsset(bmJumpLand, CleanJumpLandAssetPath, 0.17f, 0.56f, loop: false)
            : null;
        if (bmJumpClean != null || bmLandClean != null)
            AssetDatabase.SaveAssets();

        string idlePath = FindCustomAssetPath(new[] { "Anim_Idle", "Breathing Idle", "Idle" }, exclude: new[] { "Offensive" }) ?? FallbackIdlePath;
        string walkPath = FindCustomAssetPath(new[] { "Anim_Walk", "Walking", "Walk" }, exclude: new[] { "Start", "Turn" });
        string runPath = FindCustomAssetPath(new[] { "Anim_Run", "Fast Run", "Running", "Run" }, exclude: new[] { "Jump" }) ?? FallbackRunPath;
        string jumpUpPath = FindCustomAssetPath(new[] { "Anim_JumpUp", "JumpUp", "Jumping Up", "Jumping", "Jump" }, exclude: new[] { "Fall", "Land", "Bounce", "Flip", "RunJump", "Running Jump" });
        string runJumpPath = FindCustomAssetPath(new[] { "Anim_RunJump", "Running Jump", "RunJump" });
        string fallPath = FindCustomAssetPath(new[] { "Anim_FallLoop", "FallLoop", "Falling Idle", "Fall" }, exclude: new[] { "Landing", "Land" });
        string landPath = FindCustomAssetPath(new[] { "Anim_Land", "Falling To Landing", "Landing", "Land" });
        string bouncePath = FindCustomAssetPath(new[] { "Anim_Bounce_1", "Anim_Bounce", "Front Flip" }, exclude: new[] { "Twist", "Bounce_2" });
        string bounce2Path = FindCustomAssetPath(new[] { "Anim_Bounce_2", "Front Twist Flip", "Twist Flip" });
        string mantlePath = FindCustomAssetPath(new[] { "Anim_Mantle", "Braced Hang To Crouch", "Mantle", "Braced Hang" });

        if (bmIdle == null) ConfigureAnimFbx(idlePath, groundFeet: true, loop: true, lockRootY: true);
        if (bmWalk == null && walkPath != null) ConfigureAnimFbx(walkPath, groundFeet: true, loop: true, lockRootY: true);
        if (bmRun == null) ConfigureAnimFbx(runPath, groundFeet: true, loop: true, lockRootY: true);

        if (bmJumpBegin == null && jumpUpPath != null) ConfigureTrimmedFbx(jumpUpPath, "JumpUp_Trim", 12f, 32f, groundFeet: false, loop: false, lockRootY: false, lockRootXZ: true);
        if (bmJumpBegin == null && runJumpPath != null) ConfigureTrimmedFbx(runJumpPath, "RunJump_Trim", 0f, 20f, groundFeet: false, loop: false, lockRootY: false, lockRootXZ: false);

        if (fallPath != null) ConfigureAnimFbx(fallPath, groundFeet: false, loop: true, lockRootY: false, lockRootXZ: true);
        if ((bmJumpBegin == null && jumpUpPath == null) || fallPath == null)
            ConfigureJumpSubClips(FallbackJumpPath);

        if (bmJumpLand == null && landPath != null) ConfigureTrimmedFbx(landPath, "Land_Trim", 8f, 26f, groundFeet: true, loop: false, lockRootY: true, lockRootXZ: true);
        if (bouncePath != null) ConfigureTrimmedFbx(bouncePath, "Bounce1_Trim", 13f, 45f, groundFeet: false, loop: false, lockRootY: false, lockRootXZ: false);
        if (bounce2Path != null) ConfigureTrimmedFbx(bounce2Path, "Bounce2_Trim", 15f, 44f, groundFeet: false, loop: false, lockRootY: false, lockRootXZ: false);
        if (mantlePath != null) ConfigureTrimmedFbx(mantlePath, "Mantle_Trim", 3f, 29f, groundFeet: false, loop: false, lockRootY: false, lockRootXZ: false);

        return new ClipSet
        {
            idle = bmIdle ?? LoadClip(idlePath),
            walk = bmWalk ?? (walkPath != null ? LoadClip(walkPath) : null),
            run = bmRun ?? LoadClip(runPath),
            sprint = bmSprint,
            jumpUp = bmJumpClean ?? bmJumpBegin ?? (jumpUpPath != null ? LoadClip(jumpUpPath) : (LoadClip(FallbackJumpPath, "JumpRise") ?? LoadClip(FallbackJumpPath))),
            runJump = bmJumpClean ?? bmJumpBegin ?? (runJumpPath != null ? LoadClip(runJumpPath) : null),
            inAir = bmInAir,
            fallLoop = fallPath != null ? LoadClip(fallPath) : (LoadClip(FallbackJumpPath, "JumpFall") ?? LoadClip(FallbackJumpPath, "JumpRise")),
            land = bmLandClean ?? bmJumpLand ?? (landPath != null ? LoadClip(landPath) : null),
            bounce = bouncePath != null ? LoadClip(bouncePath) : null,
            bounce2 = bounce2Path != null ? LoadClip(bounce2Path) : null,
            mantle = mantlePath != null ? LoadClip(mantlePath) : null
        };
    }

    static AnimationClip BuildTrimmedAnimAsset(AnimationClip srcClip, string assetPath, float startTime, float endTime, bool loop)
    {
        if (srcClip == null) return null;

        startTime = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, srcClip.length - 0.02f));
        endTime = Mathf.Clamp(endTime, startTime + 0.02f, srcClip.length);
        float duration = endTime - startTime;

        Directory.CreateDirectory(AnimationsFolder);
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        AnimationClip dstClip = existing != null ? existing : new AnimationClip();
        if (existing != null) dstClip.ClearCurves();

        dstClip.frameRate = srcClip.frameRate > 0f ? srcClip.frameRate : 30f;

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(srcClip))
        {
            AnimationCurve src = AnimationUtility.GetEditorCurve(srcClip, binding);
            if (src == null || src.length == 0) continue;

            AnimationCurve dst = new AnimationCurve();
            float startNextT = Mathf.Min(endTime, startTime + 0.005f);
            float startVal = src.Evaluate(startTime);
            float startTan = (src.Evaluate(startNextT) - startVal) / Mathf.Max(0.0001f, startNextT - startTime);
            dst.AddKey(new Keyframe(0f, startVal, startTan, startTan));

            foreach (Keyframe k in src.keys)
            {
                if (k.time > startTime + 0.001f && k.time < endTime - 0.001f)
                {
                    Keyframe shifted = k;
                    shifted.time = k.time - startTime;
                    dst.AddKey(shifted);
                }
            }

            float endPrevT = Mathf.Max(startTime, endTime - 0.005f);
            float endVal = src.Evaluate(endTime);
            float endTan = (endVal - src.Evaluate(endPrevT)) / Mathf.Max(0.0001f, endTime - endPrevT);
            dst.AddKey(new Keyframe(duration, endVal, endTan, endTan));

            dst.preWrapMode = src.preWrapMode;
            dst.postWrapMode = src.postWrapMode;
            AnimationUtility.SetEditorCurve(dstClip, binding, dst);
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(srcClip);
        settings.startTime = 0f;
        settings.stopTime = duration;
        settings.loopTime = loop;
        settings.loopBlend = loop;
        AnimationUtility.SetAnimationClipSettings(dstClip, settings);

        if (existing == null) AssetDatabase.CreateAsset(dstClip, assetPath);
        else EditorUtility.SetDirty(dstClip);
        return dstClip;
    }

    static AnimationClip LoadBasicMotionClip(string relativeSubPath)
    {
        if (!AssetDatabase.IsValidFolder(BasicMotionsMaleFolder)) return null;
        return LoadClip($"{BasicMotionsMaleFolder}/{relativeSubPath}");
    }

    static string FindCustomAssetPath(string[] keywords, string[] exclude = null)
    {
        if (!AssetDatabase.IsValidFolder(AnimationsFolder)) return null;

        List<string> candidates = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("", new[] { AnimationsFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(path)) continue;
            string ext = Path.GetExtension(path);
            if (!ext.Equals(".fbx", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".anim", StringComparison.OrdinalIgnoreCase))
                continue;
            candidates.Add(path);
        }

        foreach (string kw in keywords)
        {
            foreach (string path in candidates)
            {
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (fileName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) < 0) continue;

                bool excluded = false;
                if (exclude != null)
                {
                    foreach (string ex in exclude)
                    {
                        if (fileName.IndexOf(ex, StringComparison.OrdinalIgnoreCase) >= 0) { excluded = true; break; }
                    }
                }
                if (!excluded) return path;
            }
        }
        return null;
    }

    static void RebuildController(ClipSet clips)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "AnimSpeedMultiplier", AnimatorControllerParameterType.Float, 1.0f);
        EnsureParameter(controller, "BounceSpeed", AnimatorControllerParameterType.Float, 1.0f);
        EnsureParameter(controller, "IsGrounded", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "VerticalVelocity", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "Jump", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Bounce", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Land", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Mantle", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "JumpVariant", AnimatorControllerParameterType.Int);
        EnsureParameter(controller, "BounceVariant", AnimatorControllerParameterType.Int);

        var layers = controller.layers;
        if (layers != null && layers.Length > 0)
        {
            layers[0].iKPass = true;
            controller.layers = layers;
        }

        var rootStateMachine = controller.layers[0].stateMachine;
        rootStateMachine.states = new ChildAnimatorState[0];
        rootStateMachine.anyStateTransitions = new AnimatorStateTransition[0];

        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
        {
            if (obj is BlendTree || obj is AnimatorState || obj is AnimatorStateTransition)
                UnityEngine.Object.DestroyImmediate(obj, true);
        }

        // 1. Locomotion BlendTree: Idle -> (Walk) -> Run -> (Sprint)
        AnimatorState locomotionState = controller.CreateBlendTreeInController("Locomotion", out BlendTree locomotionTree, 0);
        locomotionTree.name = "Locomotion";
        locomotionTree.blendType = BlendTreeType.Simple1D;
        locomotionTree.blendParameter = "Speed";
        locomotionTree.useAutomaticThresholds = false;
        if (clips.idle != null) locomotionTree.AddChild(clips.idle, 0f);
        if (clips.walk != null && clips.run != null && clips.sprint != null)
        {
            locomotionTree.AddChild(clips.walk, 1.4f);
            locomotionTree.AddChild(clips.run, 2.8f);
            locomotionTree.AddChild(clips.sprint, 4.5f);
            var children = locomotionTree.children;
            if (children.Length == 4)
            {
                children[0].timeScale = 1.0f;
                children[1].timeScale = 1.02f;
                children[2].timeScale = 1.06f;
                children[3].timeScale = 1.10f;
                locomotionTree.children = children;
            }
        }
        else if (clips.walk != null)
        {
            locomotionTree.AddChild(clips.walk, 4.0f);
            if (clips.run != null) locomotionTree.AddChild(clips.run, 7.2f);
        }
        else if (clips.run != null)
        {
            locomotionTree.AddChild(clips.run, 4.0f);
            locomotionTree.AddChild(clips.run, 7.2f);
            var treeChildren = locomotionTree.children;
            if (treeChildren.Length >= 2)
            {
                treeChildren[treeChildren.Length - 2].timeScale = 0.72f;
                treeChildren[treeChildren.Length - 1].timeScale = 1.05f;
                locomotionTree.children = treeChildren;
            }
        }
        locomotionState.iKOnFeet = false;
        locomotionState.speedParameterActive = true;
        locomotionState.speedParameter = "AnimSpeedMultiplier";

        // 2. JumpUp, (RunJump), 3. InAir, FallLoop, 4. Bounce, (BounceTwist)
        AnimatorState jumpUpState = rootStateMachine.AddState("JumpUp", new Vector3(320, -140, 0));
        jumpUpState.motion = clips.jumpUp;
        jumpUpState.speed = 0.68f;

        AnimatorState runJumpState = null;
        if (clips.runJump != null)
        {
            runJumpState = rootStateMachine.AddState("RunJump", new Vector3(320, -70, 0));
            runJumpState.motion = clips.runJump;
            runJumpState.speed = 0.72f;
        }

        AnimatorState inAirState = rootStateMachine.AddState("InAir", new Vector3(560, -160, 0));
        inAirState.motion = clips.inAir ?? clips.jumpUp;
        if (clips.inAir != null)
        {
            inAirState.speed = 1.0f;
            inAirState.cycleOffset = 0f;
        }
        else
        {
            inAirState.speed = 0.35f;
            inAirState.cycleOffset = 0.55f;
        }

        AnimatorState fallLoopState = rootStateMachine.AddState("FallLoop", new Vector3(560, -40, 0));
        fallLoopState.motion = clips.fallLoop ?? clips.jumpUp;

        AnimatorState bounceState = rootStateMachine.AddState("Bounce", new Vector3(320, -240, 0));
        bounceState.motion = clips.bounce ?? clips.jumpUp;
        bounceState.speed = 1.0f;
        bounceState.speedParameterActive = true;
        bounceState.speedParameter = "BounceSpeed";

        AnimatorState bounceTwistState = null;
        if (clips.bounce2 != null)
        {
            bounceTwistState = rootStateMachine.AddState("BounceTwist", new Vector3(320, -310, 0));
            bounceTwistState.motion = clips.bounce2;
            bounceTwistState.speed = 1.0f;
            bounceTwistState.speedParameterActive = true;
            bounceTwistState.speedParameter = "BounceSpeed";
        }

        rootStateMachine.defaultState = locomotionState;

        // Land (yalnızca özel iniş klibi varsa ayrı durum; yoksa FallLoop doğrudan Locomotion'a döner)
        AnimatorState landTargetState = locomotionState;
        if (clips.land != null)
        {
            AnimatorState landState = rootStateMachine.AddState("Land", new Vector3(320, 90, 0));
            landState.motion = clips.land;
            landState.speed = 1.25f;
            landTargetState = landState;

            var landMoveExit = landState.AddTransition(locomotionState);
            landMoveExit.hasExitTime = false;
            landMoveExit.duration = 0.08f;
            landMoveExit.interruptionSource = TransitionInterruptionSource.Destination;
            landMoveExit.AddCondition(AnimatorConditionMode.Greater, 0.35f, "Speed");

            var landIdleExit = landState.AddTransition(locomotionState);
            landIdleExit.hasExitTime = true;
            landIdleExit.exitTime = 0.80f;
            landIdleExit.duration = 0.12f;
        }

        // Mantle (özel tırmanma klibi varsa ayrı durum)
        if (clips.mantle != null)
        {
            AnimatorState mantleState = rootStateMachine.AddState("Mantle", new Vector3(40, -200, 0));
            mantleState.motion = clips.mantle;
            mantleState.speed = 1.28f;

            var anyToMantle = rootStateMachine.AddAnyStateTransition(mantleState);
            anyToMantle.hasExitTime = false;
            anyToMantle.duration = 0.06f;
            anyToMantle.canTransitionToSelf = false;
            anyToMantle.AddCondition(AnimatorConditionMode.If, 0, "Mantle");

            var mantleDone = mantleState.AddTransition(locomotionState);
            mantleDone.hasExitTime = true;
            mantleDone.exitTime = 0.88f;
            mantleDone.duration = 0.1f;
        }

        // Locomotion -> FallLoop (yalnızca ciddi yüksekten düşüşte devreye gir)
        var locoToFall = locomotionState.AddTransition(fallLoopState);
        locoToFall.hasExitTime = false;
        locoToFall.duration = 0.16f;
        locoToFall.interruptionSource = TransitionInterruptionSource.Destination;
        locoToFall.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
        locoToFall.AddCondition(AnimatorConditionMode.Less, -14.0f, "VerticalVelocity");

        // Locomotion -> InAir (platform kenarından normal adım inişinde sakin havada pozisyon)
        var locoToInAir = locomotionState.AddTransition(inAirState);
        locoToInAir.hasExitTime = false;
        locoToInAir.duration = 0.14f;
        locoToInAir.interruptionSource = TransitionInterruptionSource.Destination;
        locoToInAir.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
        locoToInAir.AddCondition(AnimatorConditionMode.Less, -6.5f, "VerticalVelocity");

        // JumpUp / RunJump -> yalnizca derin dususte InAir (-16.0) / FallLoop (-19.5); normal ziplamada Jump01 -> Land
        WireJumpState(jumpUpState, inAirState, fallLoopState, landTargetState);
        if (runJumpState != null)
            WireJumpState(runJumpState, inAirState, fallLoopState, landTargetState);

        // Bounce / BounceTwist -> inise kadar taklada kalir; yalnizca cok derin boslukta FallLoop (-23.0)
        WireBounceState(bounceState, fallLoopState, landTargetState);
        if (bounceTwistState != null)
            WireBounceState(bounceTwistState, fallLoopState, landTargetState);

        // InAir -> FallLoop (derin düşüş) & Ground
        WireAirState(inAirState, fallLoopState, landTargetState, fallVyThreshold: -19.5f, fallTransitionDur: 0.20f);

        // FallLoop -> Land/Locomotion
        var fallToGround = fallLoopState.AddTransition(landTargetState);
        fallToGround.hasExitTime = false;
        fallToGround.duration = 0.08f;
        fallToGround.interruptionSource = TransitionInterruptionSource.Destination;
        fallToGround.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");

        // AnyState -> JumpUp / RunJump
        var anyToJump = rootStateMachine.AddAnyStateTransition(jumpUpState);
        anyToJump.hasExitTime = false;
        anyToJump.duration = 0.06f;
        anyToJump.canTransitionToSelf = true;
        anyToJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
        if (runJumpState != null)
        {
            anyToJump.AddCondition(AnimatorConditionMode.NotEqual, 1, "JumpVariant");

            var anyToRunJump = rootStateMachine.AddAnyStateTransition(runJumpState);
            anyToRunJump.hasExitTime = false;
            anyToRunJump.duration = 0.06f;
            anyToRunJump.canTransitionToSelf = true;
            anyToRunJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            anyToRunJump.AddCondition(AnimatorConditionMode.Equals, 1, "JumpVariant");
        }

        // AnyState -> Bounce / BounceTwist
        var anyToBounce = rootStateMachine.AddAnyStateTransition(bounceState);
        anyToBounce.hasExitTime = false;
        anyToBounce.duration = 0.06f;
        anyToBounce.canTransitionToSelf = true;
        anyToBounce.AddCondition(AnimatorConditionMode.If, 0, "Bounce");
        if (bounceTwistState != null)
        {
            anyToBounce.AddCondition(AnimatorConditionMode.NotEqual, 1, "BounceVariant");

            var anyToBounceTwist = rootStateMachine.AddAnyStateTransition(bounceTwistState);
            anyToBounceTwist.hasExitTime = false;
            anyToBounceTwist.duration = 0.06f;
            anyToBounceTwist.canTransitionToSelf = true;
            anyToBounceTwist.AddCondition(AnimatorConditionMode.If, 0, "Bounce");
            anyToBounceTwist.AddCondition(AnimatorConditionMode.Equals, 1, "BounceVariant");
        }

        AssetDatabase.SaveAssets();

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

        Debug.Log("[AnimationSetupTool] PlayerAnimatorController (JumpUp/RunJump/FallLoop/Bounce/BounceTwist/Land/Mantle) ile güncellendi.");
    }

    static void WireJumpState(AnimatorState state, AnimatorState inAirState, AnimatorState fallLoopState, AnimatorState landTargetState)
    {
        var toGround = state.AddTransition(landTargetState);
        toGround.hasExitTime = false;
        toGround.duration = 0.08f;
        toGround.interruptionSource = TransitionInterruptionSource.Destination;
        toGround.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        toGround.AddCondition(AnimatorConditionMode.Less, 0.5f, "VerticalVelocity");

        var toFallLoop = state.AddTransition(fallLoopState);
        toFallLoop.hasExitTime = false;
        toFallLoop.duration = 0.18f;
        toFallLoop.interruptionSource = TransitionInterruptionSource.Destination;
        toFallLoop.AddCondition(AnimatorConditionMode.Less, -19.5f, "VerticalVelocity");

        var toInAir = state.AddTransition(inAirState);
        toInAir.hasExitTime = false;
        toInAir.duration = 0.16f;
        toInAir.interruptionSource = TransitionInterruptionSource.Destination;
        toInAir.AddCondition(AnimatorConditionMode.Less, -16.0f, "VerticalVelocity");
    }

    static void WireBounceState(AnimatorState state, AnimatorState fallLoopState, AnimatorState landTargetState)
    {
        var toGround = state.AddTransition(landTargetState);
        toGround.hasExitTime = false;
        toGround.duration = 0.08f;
        toGround.interruptionSource = TransitionInterruptionSource.Destination;
        toGround.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        toGround.AddCondition(AnimatorConditionMode.Less, 0.5f, "VerticalVelocity");

        var toFall = state.AddTransition(fallLoopState);
        toFall.hasExitTime = false;
        toFall.duration = 0.20f;
        toFall.interruptionSource = TransitionInterruptionSource.Destination;
        toFall.AddCondition(AnimatorConditionMode.Less, -23.0f, "VerticalVelocity");
    }

    static void WireAirState(AnimatorState state, AnimatorState fallLoopState, AnimatorState landTargetState, float fallVyThreshold, float fallTransitionDur)
    {
        var toFall = state.AddTransition(fallLoopState);
        toFall.hasExitTime = false;
        toFall.duration = fallTransitionDur;
        toFall.interruptionSource = TransitionInterruptionSource.Destination;
        toFall.AddCondition(AnimatorConditionMode.Less, fallVyThreshold, "VerticalVelocity");

        var toGround = state.AddTransition(landTargetState);
        toGround.hasExitTime = false;
        toGround.duration = 0.08f;
        toGround.interruptionSource = TransitionInterruptionSource.Destination;
        toGround.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        toGround.AddCondition(AnimatorConditionMode.Less, 0.5f, "VerticalVelocity");
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

    static ModelImporterClipAnimation BuildClipWithPoseSettings(ModelImporterClipAnimation src, bool groundFeet, bool loop, bool lockRootY, bool lockRootXZ = true, bool clone = false)
    {
        var clip = clone ? new ModelImporterClipAnimation { takeName = src.takeName } : src;
        clip.loopTime = loop;
        clip.loopPose = loop;
        clip.lockRootRotation = true;
        clip.lockRootHeightY = lockRootY;
        clip.lockRootPositionXZ = lockRootXZ;
        clip.keepOriginalOrientation = false;
        clip.keepOriginalPositionY = false;
        clip.keepOriginalPositionXZ = false;
        clip.heightFromFeet = groundFeet || !lockRootY;
        return clip;
    }

    static void ConfigureAnimFbx(string assetPath, bool groundFeet, bool loop, bool lockRootY = true, bool lockRootXZ = true)
    {
        if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            return;

        var importer = EnsureHumanImporter(assetPath);
        if (importer == null) return;

        var clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) return;

        for (int i = 0; i < clips.Length; i++)
            clips[i] = BuildClipWithPoseSettings(clips[i], groundFeet, loop, lockRootY, lockRootXZ);
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    static void ConfigureTrimmedFbx(string assetPath, string clipName, float firstFrame, float lastFrame, bool groundFeet, bool loop, bool lockRootY, bool lockRootXZ = true)
    {
        if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            return;

        var importer = EnsureHumanImporter(assetPath);
        if (importer == null) return;

        var def = importer.defaultClipAnimations;
        if (def == null || def.Length == 0) return;
        var baseClip = def[0];

        var clip = BuildClipWithPoseSettings(baseClip, groundFeet, loop, lockRootY, lockRootXZ, clone: true);
        clip.name = clipName;
        clip.firstFrame = Mathf.Clamp(firstFrame, baseClip.firstFrame, baseClip.lastFrame);
        clip.lastFrame = Mathf.Clamp(lastFrame, clip.firstFrame + 1f, baseClip.lastFrame);

        importer.clipAnimations = new[] { clip };
        importer.SaveAndReimport();
    }

    static void ConfigureJumpSubClips(string assetPath)
    {
        var importer = EnsureHumanImporter(assetPath);
        if (importer == null) return;

        var def = importer.defaultClipAnimations;
        if (def == null || def.Length == 0) return;
        var baseClip = def[0];

        var rise = BuildClipWithPoseSettings(baseClip, groundFeet: false, loop: false, lockRootY: true, clone: true);
        rise.name = "JumpRise";
        rise.firstFrame = Mathf.Min(1f, baseClip.lastFrame);
        rise.lastFrame = Mathf.Min(10f, baseClip.lastFrame);

        // Havada kalındığında takılmaması için FallLoop varsayılan alt klibi döngüsel tutulur
        var fall = BuildClipWithPoseSettings(baseClip, groundFeet: false, loop: true, lockRootY: true, clone: true);
        fall.name = "JumpFall";
        fall.firstFrame = Mathf.Min(13f, baseClip.lastFrame);
        fall.lastFrame = Mathf.Min(21f, baseClip.lastFrame);

        importer.clipAnimations = new[] { rise, fall };
        importer.SaveAndReimport();
    }

    static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type, float defaultFloat = 0f)
    {
        var ps = controller.parameters;
        for (int i = 0; i < ps.Length; i++)
        {
            if (ps[i].name == name)
            {
                if (type == AnimatorControllerParameterType.Float && !Mathf.Approximately(ps[i].defaultFloat, defaultFloat))
                {
                    ps[i].defaultFloat = defaultFloat;
                    controller.parameters = ps;
                }
                return;
            }
        }
        controller.AddParameter(new AnimatorControllerParameter
        {
            name = name,
            type = type,
            defaultFloat = defaultFloat
        });
    }

    static AnimationClip LoadClip(string assetPath, string exactName = null)
    {
        if (string.IsNullOrEmpty(assetPath)) return null;
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
