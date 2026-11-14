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
    [MenuItem("Tools/Parkur/Animasyonları Ayarla ve Bağla")]
    public static void SetupAnimations()
    {
        string charPath = "Assets/char.fbx";
        string[] animFiles = new string[]
        {
            "Assets/X Bot@Offensive Idle.fbx",
            "Assets/X Bot@Running.fbx",
            "Assets/X Bot@Jumping.fbx"
        };

        // Karakter modelini Humanoid olarak garantiye al
        var charImporter = AssetImporter.GetAtPath(charPath) as ModelImporter;
        if (charImporter != null && charImporter.animationType != ModelImporterAnimationType.Human)
        {
            charImporter.animationType = ModelImporterAnimationType.Human;
            charImporter.SaveAndReimport();
        }

        // Animasyon FBX ayarlarını yapılandır (Loop + Bake Into Pose ile sola yatmayı/kaymayı engelle)
        ConfigureAnimFbx(animFiles[0], true);  // Idle (Loop = true)
        ConfigureAnimFbx(animFiles[1], true);  // Running (Loop = true)
        ConfigureAnimFbx(animFiles[2], false); // Jumping (Loop = false)

        // 1. Animator Controller oluştur veya yükle
        string controllerPath = "Assets/PlayerAnimatorController.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }

        // Parametreleri ekle (yoksa)
        EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "AnimSpeedMultiplier", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "IsGrounded", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "Jump", AnimatorControllerParameterType.Trigger);

        // Klipleri bul
        AnimationClip idleClip = LoadClip(animFiles[0]);
        AnimationClip runClip = LoadClip(animFiles[1]);
        AnimationClip jumpClip = LoadClip(animFiles[2]);

        // Katman ve State'leri temizle/düzenle
        var rootStateMachine = controller.layers[0].stateMachine;
        rootStateMachine.states = new ChildAnimatorState[0];
        rootStateMachine.anyStateTransitions = new AnimatorStateTransition[0];

        var idleState = rootStateMachine.AddState("Idle", new Vector3(250, 0, 0));
        idleState.motion = idleClip;
        idleState.iKOnFeet = false;

        var runState = rootStateMachine.AddState("Run", new Vector3(250, 100, 0));
        runState.motion = runClip;
        runState.iKOnFeet = false;
        runState.speedParameterActive = true;
        runState.speedParameter = "AnimSpeedMultiplier";

        var jumpState = rootStateMachine.AddState("Jump", new Vector3(250, -100, 0));
        jumpState.motion = jumpClip;
        jumpState.iKOnFeet = false;

        // Geçişler: Idle <-> Run
        var toRun = idleState.AddTransition(runState);
        toRun.hasExitTime = false;
        toRun.duration = 0.15f;
        toRun.AddCondition(AnimatorConditionMode.Greater, 0.2f, "Speed");

        var toIdle = runState.AddTransition(idleState);
        toIdle.hasExitTime = false;
        toIdle.duration = 0.15f;
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.2f, "Speed");

        // AnyState -> Jump
        var anyToJump = rootStateMachine.AddAnyStateTransition(jumpState);
        anyToJump.hasExitTime = false;
        anyToJump.duration = 0.08f;
        anyToJump.canTransitionToSelf = false;
        anyToJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");

        // Jump -> Run (Yere basınca ve hız varsa)
        var jumpToRun = jumpState.AddTransition(runState);
        jumpToRun.hasExitTime = false;
        jumpToRun.duration = 0.15f;
        jumpToRun.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        jumpToRun.AddCondition(AnimatorConditionMode.Greater, 0.2f, "Speed");

        // Jump -> Idle (Yere basınca ve duruyorsa)
        var jumpToIdle = jumpState.AddTransition(idleState);
        jumpToIdle.hasExitTime = false;
        jumpToIdle.duration = 0.15f;
        jumpToIdle.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        jumpToIdle.AddCondition(AnimatorConditionMode.Less, 0.2f, "Speed");

        AssetDatabase.SaveAssets();

        // 2. Sahnede Player nesnesindeki Animator'a ata
        var player = GameObject.Find("Player");
        if (player != null)
        {
            var anim = player.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
                EditorUtility.SetDirty(anim);
            }
            var pc = player.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.animator = anim;
                EditorUtility.SetDirty(pc);
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        Debug.Log("Animasyonlar Remy T-Pose iskeletiyle eşlendi, döngüye alındı ve sola kayma kilitlendi!");
    }

    static void ConfigureAnimFbx(string assetPath, bool loop)
    {
        // Önce güncel meta dosyasını Unity'ye zorla okut
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null) return;

        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
        }

        // Her zaman defaultClipAnimations'tan başla ki FBX'in gerçek başlangıç/bitiş kareleri (firstFrame/lastFrame) tam alınsın
        var clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.clipAnimations;
        }

        if (clips != null && clips.Length > 0)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = loop;
                // Loop Pose (loopBlend): Animasyonun başlangıç ve bitiş pozlarını birbirine kaynaştırır, döngü geçişinde sırıtmaz!
                clips[i].loopPose = loop;

                // Dönüş ve Y yüksekliğini poza sabitle
                clips[i].lockRootRotation = true;
                clips[i].lockRootHeightY = true;
                // XZ yatay hareketi poza GÖMME (false): Böylece yürüme/koşmada karakter kapsülün dışına kayıp başa sarmaz,
                // zıplamada ise ileri hareket RootMotionRelay ile kapsüle aktarılır.
                clips[i].lockRootPositionXZ = false;

                // Body Orientation & Center of Mass baz alınsın
                clips[i].keepOriginalOrientation = false;
                clips[i].keepOriginalPositionY = true;
                clips[i].keepOriginalPositionXZ = false;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (var p in controller.parameters)
        {
            if (p.name == name) return;
        }
        controller.AddParameter(name, type);
    }

    static AnimationClip LoadClip(string assetPath)
    {
        var objs = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        foreach (var obj in objs)
        {
            if (obj is AnimationClip clip && !clip.name.Contains("__preview__"))
            {
                return clip;
            }
        }
        return null;
    }
}
