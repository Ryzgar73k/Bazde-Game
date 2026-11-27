using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// KayKit - Platformer Pack varlıklarıyla standart, URP uyumlu ve tekrar üretilebilir
/// parkur levelleri kurmak için ortak sahne ve engel yerleştirme yardımcıları.
/// </summary>
public static class KayKitLevelKit
{
    public const string KayKitPrefabs = "Assets/KayKit/Packs/KayKit - Platformer Pack (for Unity)/Prefabs";
    public const string SkyboxPath = "Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Materials/Skybox Cubemap Extended Day.mat";
    public const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    public const string AnimatorControllerPath = "Assets/PlayerAnimatorController.controller";
    public const string BeaconMatFolder = "Assets/Materials/Level4";

    public static readonly string[] BuildScenePaths =
    {
        "Assets/Scenes/Level1.unity",
        "Assets/Scenes/Level2.unity",
        "Assets/Scenes/Level3.unity",
        "Assets/Scenes/Level4.unity"
    };

    public class SceneContext
    {
        public Scene Scene;
        public string ScenePath;
        public Transform EnvRoot;
        public Transform GameplayRoot;
        public Transform LevelRoot;
        public Material BeaconMat;
        public GameObject Player;
    }

    public static SceneContext CreateSceneContext(
        string scenePath,
        string levelLabel,
        Vector3 spawnPos,
        Quaternion spawnRot,
        float fallThresholdY,
        Vector3 killZoneCenter,
        Vector3 killZoneSize)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var envRoot = new GameObject("--- ENVIRONMENT & LIGHTING ---").transform;
        var gameplayRoot = new GameObject("--- GAMEPLAY & MANAGERS ---").transform;
        var levelRoot = new GameObject($"--- {levelLabel} KAYKIT COURSE ---").transform;

        SetupEnvironment(envRoot);

        var spawnPoint = new GameObject("InitialSpawnPoint");
        spawnPoint.transform.SetParent(gameplayRoot, false);
        spawnPoint.transform.SetPositionAndRotation(spawnPos, spawnRot);

        var killZoneGO = new GameObject("KillZone", typeof(BoxCollider), typeof(KillZone));
        killZoneGO.transform.SetParent(gameplayRoot, false);
        killZoneGO.transform.position = killZoneCenter;
        var kzCol = killZoneGO.GetComponent<BoxCollider>();
        kzCol.isTrigger = true;
        kzCol.size = killZoneSize;

        var respawnGO = new GameObject("RespawnManager", typeof(RespawnManager));
        respawnGO.transform.SetParent(gameplayRoot, false);
        var respawn = respawnGO.GetComponent<RespawnManager>();
        respawn.defaultSpawnPoint = spawnPoint.transform;
        respawn.defaultSpawnOffset = new Vector3(0f, 0.15f, 0f);
        respawn.fallThresholdY = fallThresholdY;
        respawn.autoCheckFall = true;
        respawn.respawnDelay = 0.05f;

        var player = SetupPlayerAndCamera(spawnPoint.transform, gameplayRoot);
        respawn.player = player.GetComponent<PlayerController>();

        var hudGO = new GameObject("LevelHUD", typeof(LevelHUD));
        hudGO.transform.SetParent(gameplayRoot, false);
        hudGO.GetComponent<LevelHUD>().levelLabel = levelLabel;

        return new SceneContext
        {
            Scene = scene,
            ScenePath = scenePath,
            EnvRoot = envRoot,
            GameplayRoot = gameplayRoot,
            LevelRoot = levelRoot,
            BeaconMat = CreateOrLoadBeaconMaterial(),
            Player = player
        };
    }

    public static bool SaveSceneAndRegisterBuild(SceneContext ctx)
    {
        string dir = Path.GetDirectoryName(ctx.ScenePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        bool saved = EditorSceneManager.SaveScene(ctx.Scene, ctx.ScenePath);
        if (!saved)
        {
            Debug.LogError($"[KayKitLevelKit] Sahne kaydedilemedi: {ctx.ScenePath}");
            return false;
        }

        KurdistanEnvironmentBuilder.BuildEnvironmentForScene(
            ctx.Scene,
            KurdistanEnvironmentBuilder.InferLevelNumber(ctx.ScenePath, ctx.Scene.name));
        EditorSceneManager.SaveScene(ctx.Scene);

        RegisterAllStandardBuildScenes();
        AssetDatabase.SaveAssets();
        return true;
    }

    public static void RegisterAllStandardBuildScenes()
    {
        var list = new List<EditorBuildSettingsScene>();
        var added = new HashSet<string>();

        foreach (string p in BuildScenePaths)
        {
            if (File.Exists(p) && added.Add(p))
                list.Add(new EditorBuildSettingsScene(p, true));
        }

        foreach (var existing in EditorBuildSettings.scenes)
        {
            if (!string.IsNullOrEmpty(existing.path) && File.Exists(existing.path) && added.Add(existing.path))
                list.Add(existing);
        }

        EditorBuildSettings.scenes = list.ToArray();
    }

    static void SetupEnvironment(Transform parent)
    {
        var lightGO = new GameObject("Directional Light", typeof(Light));
        lightGO.transform.SetParent(parent, false);
        var light = lightGO.GetComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1.0f, 0.96f, 0.90f);
        light.intensity = 1.3f;
        light.shadows = LightShadows.Soft;
        lightGO.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

        var skyboxMat = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
        if (skyboxMat != null)
        {
            RenderSettings.skybox = skyboxMat;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.15f;
            DynamicGI.UpdateEnvironment();
        }
    }

    static GameObject SetupPlayerAndCamera(Transform spawnPoint, Transform parent)
    {
        GameObject player;
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab != null)
        {
            player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, parent);
            player.name = "Player";
        }
        else
        {
            player = new GameObject("Player", typeof(CharacterController), typeof(PlayerController));
            player.transform.SetParent(parent, false);
        }

        player.tag = "Player";
        player.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        var cc = player.GetComponent<CharacterController>();
        cc.height = 1.08f;
        cc.radius = 0.16f;
        cc.skinWidth = 0.025f;
        cc.center = new Vector3(0f, 0.565f, 0f);
        cc.stepOffset = 0.24f;
        cc.slopeLimit = 55f;

        var pc = player.GetComponent<PlayerController>();
        pc.configVersion = 0;
        pc.ApplyConfigMigration();
        pc.canControl = true;
        pc.maxStepHeight = 0.24f;
        pc.maxSlopeAngle = 55f;

        if (player.transform.childCount > 0)
        {
            var model = player.transform.GetChild(0);
            model.localPosition = Vector3.zero;
            var anim = model.GetComponent<Animator>();
            if (anim != null)
            {
                var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorControllerPath);
                if (ctrl != null) anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
                if (anim.GetComponent<FootIK>() == null)
                    anim.gameObject.AddComponent<FootIK>();
                pc.animator = anim;
            }
        }

        var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGO.tag = "MainCamera";
        camGO.transform.SetParent(parent, false);
        camGO.GetComponent<Camera>().farClipPlane = 650f;
        Vector3 backOffset = spawnPoint.rotation * new Vector3(0f, 2.0f, -3.4f);
        camGO.transform.position = spawnPoint.position + backOffset;
        camGO.transform.rotation = Quaternion.Euler(18f, spawnPoint.eulerAngles.y, 0f);

        var tpc = camGO.AddComponent<ThirdPersonCamera>();
        tpc.target = player.transform;
        tpc.distance = 3.4f;
        tpc.height = 0.95f;
        tpc.configVersion = ThirdPersonCamera.LatestConfigVersion;
        tpc.lockInstant = false;

        pc.cameraTransform = camGO.transform;

        var mobileUI = new GameObject("MobileControls", typeof(MobileControlsUI));
        mobileUI.transform.SetParent(parent, false);

        return player;
    }

    public static GameObject LoadKayKitPrefab(string relPath)
    {
        string full = $"{KayKitPrefabs}/{relPath}.prefab";
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(full);
        if (asset == null) Debug.LogError($"[KayKitLevelKit] Prefab bulunamadı: {full}");
        return asset;
    }

    public static GameObject PlacePlatform(string relPath, string name, Vector3 pos, Vector3 scale, Transform parent, Quaternion? rot = null)
    {
        var prefab = LoadKayKitPrefab(relPath);
        if (prefab == null) return null;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.SetPositionAndRotation(pos, rot ?? Quaternion.identity);
        go.transform.localScale = scale;

        AddFitBoxCollider(go, isTrigger: false);
        return go;
    }

    public static GameObject PlaceSlope(string relPath, string name, Vector3 pos, Quaternion rot, Vector3 scale, Transform parent)
    {
        var prefab = LoadKayKitPrefab(relPath);
        if (prefab == null) return null;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = scale;

        var mf = go.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = true;
        }
        return go;
    }

    public static GameObject PlaceProp(string relPath, string name, Vector3 pos, Quaternion rot, Vector3 scale, Transform parent, bool addCollider = false)
    {
        var prefab = LoadKayKitPrefab(relPath);
        if (prefab == null) return null;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = scale;
        if (addCollider) AddFitBoxCollider(go, isTrigger: false);
        return go;
    }

    public static GameObject PlaceArch(string relPath, string name, Vector3 pos, Quaternion rot, Vector3 scale, Transform parent)
    {
        var go = PlaceProp(relPath, name, pos, rot, scale, parent, addCollider: false);
        if (go != null) AddArchSideColliders(go, halfSpanX: 2.50f, pillarHeight: 3.80f, pillarWidth: 0.70f, pillarDepth: 0.75f);
        return go;
    }

    static void AddArchSideColliders(GameObject go, float halfSpanX, float pillarHeight, float pillarWidth, float pillarDepth)
    {
        for (int s = -1; s <= 1; s += 2)
        {
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = false;
            col.center = new Vector3(s * halfSpanX, pillarHeight * 0.5f, 0f);
            col.size = new Vector3(pillarWidth, pillarHeight, pillarDepth);
        }
    }

    public static GameObject PlaceHurdleBar(string relPath, string name, Vector3 pos, Quaternion rot, Vector3 scale, Transform parent)
    {
        var go = PlaceProp(relPath, name, pos, rot, scale, parent);
        if (go != null) AddFitBoxCollider(go, isTrigger: false);
        return go;
    }

    public static GameObject PlaceMovingPlatform(string relPath, string name, Vector3 startPos, Vector3 offset,
        float speed, float wait, Transform parent, Quaternion? rot = null, Vector3? scale = null)
    {
        var go = PlacePlatform(relPath, name, startPos, scale ?? Vector3.one, parent, rot);
        if (go == null) return null;

        var mp = go.AddComponent<MovingPlatform>();
        mp.moveOffset = offset;
        mp.speed = speed;
        mp.waitTimeAtEnds = wait;
        mp.smoothDamping = true;
        mp.autoCreatePassengerTrigger = true;
        return go;
    }

    public static void PlaceSpringPad(string name, Vector3 pos, float bounceHeight, Transform parent, float scaleXZ = 1.4f)
    {
        var prefab = LoadKayKitPrefab("yellow/spring_pad_yellow");
        if (prefab == null) return;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = new Vector3(scaleXZ, 1.0f, scaleXZ);

        var box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.5f, 0f);
        box.size = new Vector3(1.5f, 1.0f, 1.5f);

        var pad = go.AddComponent<TrampolinePad>();
        pad.bounceHeight = bounceHeight;
        pad.bounceInterval = 0.25f;
        pad.bounceImmediatelyOnStep = true;
        pad.detectionPadding = 0.35f;
        pad.configVersion = TrampolinePad.LatestConfigVersion;

        var lightGO = new GameObject("PadGlow", typeof(Light));
        lightGO.transform.SetParent(go.transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        var pl = lightGO.GetComponent<Light>();
        pl.type = LightType.Point;
        pl.color = new Color(1f, 0.85f, 0.2f);
        pl.range = 5f;
        pl.intensity = 2f;
    }

    /// <summary>
    /// Üzerinde yürünebilen KayKit dönen çark platformu (ContinuousRotator + 4 korkuluksuz/kaymaz kol).
    /// </summary>
    public static GameObject PlaceSpinningWheelDeck(string name, Vector3 pos, float radiusScale, float speedDeg, Transform parent)
    {
        var wheelRoot = new GameObject(name);
        wheelRoot.transform.SetParent(parent, false);
        wheelRoot.transform.position = pos;

        // Merkez göbek platformu (6x6x1)
        PlacePlatform("yellow/platform_6x6x1_yellow", name + "_Hub", pos, new Vector3(0.7f * radiusScale, 1f, 0.7f * radiusScale), wheelRoot.transform);
        // X ve Z yönünde 2 kesişen uzun kol (platform_4x2x1) -> gerçek çark şekli
        PlacePlatform("blue/platform_4x2x1_blue", name + "_ArmX", pos, new Vector3(2.2f * radiusScale, 0.9f, 0.9f), wheelRoot.transform);
        PlacePlatform("blue/platform_4x2x1_blue", name + "_ArmZ", pos, new Vector3(2.2f * radiusScale, 0.9f, 0.9f), wheelRoot.transform, Quaternion.Euler(0f, 90f, 0f));

        var rot = wheelRoot.AddComponent<ContinuousRotator>();
        rot.rotationAxis = ContinuousRotator.Axis.Y_YatayDonus;
        rot.rotationSpeed = speedDeg;
        rot.configVersion = 3;
        rot.useExplicitSetup = true;
        return wheelRoot;
    }

    /// <summary>
    /// Yürünebilen döner boru silindir köprüsü (KayKit pipe_straight_A segmentleri + pürüzsüz CapsuleCollider).
    /// </summary>
    public static GameObject PlaceRollingCylinder(string name, Vector3 axisCenterPos, int segments, float radiusScale, float rotSpeedDeg, Transform parent, bool alongZ = true, float pushMultiplier = 0.62f)
    {
        var roller = new GameObject(name);
        roller.transform.SetParent(parent, false);
        roller.transform.position = axisCenterPos;
        roller.transform.rotation = alongZ ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);

        float segLen = 2.0f;
        float totalLen = segments * segLen;
        float startZ = -totalLen * 0.5f + segLen * 0.5f;

        for (int i = 0; i < segments; i++)
        {
            string prefabPath = (i % 2 == 0) ? "blue/pipe_straight_A_blue" : "yellow/pipe_straight_A_yellow";
            var prefab = LoadKayKitPrefab(prefabPath);
            if (prefab == null) continue;

            // pipe_straight_A dikey eksenli ve Y=[0..2]; (90,0,0) döndürüp yerel merkezini (0, 1, 0) dengeleyince Z boyunca oturur
            var holder = new GameObject($"Seg_{i}");
            holder.transform.SetParent(roller.transform, false);
            holder.transform.localPosition = new Vector3(0f, 0f, startZ + i * segLen);
            holder.transform.localScale = new Vector3(radiusScale, radiusScale, 1f);

            var meshGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
            meshGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            meshGo.transform.localPosition = new Vector3(0f, 0f, -1.0f);
        }

        var cap = roller.AddComponent<CapsuleCollider>();
        cap.direction = 2; // Z ekseni boyunca silindir kapsül
        cap.radius = 0.96f * radiusScale;
        cap.height = totalLen;

        var cyl = roller.AddComponent<RotatingCylinderObstacle>();
        cyl.rotationAxis = alongZ ? RotatingCylinderObstacle.Axis.Z_DikeyTekerlek : RotatingCylinderObstacle.Axis.X_TaklaEksen;
        cyl.rotationSpeed = rotSpeedDeg;
        cyl.playerPushMultiplier = pushMultiplier;
        cyl.configVersion = 3;
        cyl.standalonePivot = true;
        return roller;
    }

    public static void PlaceHighSweeper(string relPath, string name, Vector3 pos, Vector3 scale, float speedDeg, bool isQuad, Transform parent)
    {
        var prefab = LoadKayKitPrefab(relPath);
        if (prefab == null) return;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = scale;

        if (isQuad)
        {
            var b1 = go.AddComponent<BoxCollider>();
            b1.isTrigger = true;
            b1.center = new Vector3(0f, 0.78f, 0f);
            b1.size = new Vector3(5.2f, 1.45f, 0.65f);

            var b2 = go.AddComponent<BoxCollider>();
            b2.isTrigger = true;
            b2.center = new Vector3(0f, 0.78f, 0f);
            b2.size = new Vector3(0.65f, 1.45f, 5.2f);
        }
        else
        {
            var b = go.AddComponent<BoxCollider>();
            b.isTrigger = true;
            b.center = new Vector3(0f, 0.78f, 0f);
            b.size = new Vector3(8.5f, 1.45f, 0.75f);
        }

        // Ortadaki direğe / mekanizma merkezine zıplanmasını engelleyen katı merkez çarpıştırıcısı
        var hubGO = new GameObject("CenterHubBlocker");
        hubGO.transform.SetParent(go.transform, false);
        var hubCap = hubGO.AddComponent<CapsuleCollider>();
        hubCap.isTrigger = false;
        hubCap.center = new Vector3(0f, 1.55f, 0f);
        hubCap.radius = 0.72f;
        hubCap.height = 3.4f;

        var haz = go.AddComponent<RotatingHazard>();
        haz.motionType = RotatingHazard.MotionType.ContinuousSpin;
        haz.localAxis = Vector3.up;
        haz.speed = speedDeg;
        haz.hitBehavior = RotatingHazard.HitBehavior.Knockback;
        haz.knockbackSpeed = 11.5f;
        haz.knockbackUpSpeed = 3.0f;
        haz.hitCooldown = 0.22f;
    }

    public static void PlaceTimedSpikeTrap(string name, Vector3 pos, float startDelay, Transform parent)
    {
        var prefab = LoadKayKitPrefab("red/floor_spikes_trap_4x4x1_red");
        if (prefab == null) return;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.position = pos;

        var baseCol = go.AddComponent<BoxCollider>();
        baseCol.center = new Vector3(0f, 0.75f, 0f);
        baseCol.size = new Vector3(4f, 0.5f, 4f);

        Collider spikeTrigger = null;
        Transform spikesChild = go.transform.childCount > 0 ? go.transform.GetChild(0) : null;
        if (spikesChild != null)
        {
            var trig = spikesChild.gameObject.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            trig.center = new Vector3(0f, 1.25f, 0f);
            trig.size = new Vector3(3.4f, 0.55f, 3.4f);
            spikeTrigger = trig;
        }

        var trap = go.AddComponent<TimedSpikeTrap>();
        trap.spikesVisual = spikesChild;
        trap.dangerTrigger = spikeTrigger;
        trap.startDelay = startDelay;
    }

    public static void PlaceSwingingHammer(string name, Vector3 bridgeFloorCenter, Transform parent, float speedRad = 2.15f)
    {
        var frameRoot = new GameObject(name + "_Gantry");
        frameRoot.transform.SetParent(parent, false);
        frameRoot.transform.position = bridgeFloorCenter;

        PlaceProp("neutral/pillar_2x2x8", "Pillar_Front", bridgeFloorCenter + new Vector3(0f, 0f, -3.6f), Quaternion.identity, new Vector3(0.75f, 1.0f, 0.75f), frameRoot.transform);
        PlaceProp("neutral/pillar_2x2x8", "Pillar_Back", bridgeFloorCenter + new Vector3(0f, 0f, 3.6f), Quaternion.identity, new Vector3(0.75f, 1.0f, 0.75f), frameRoot.transform);
        PlaceProp("neutral/strut_horizontal", "OverheadBeam", bridgeFloorCenter + new Vector3(0f, 7.5f, 0f), Quaternion.Euler(0f, 90f, 0f), new Vector3(4.0f, 1.2f, 1.2f), frameRoot.transform);

        var hammerPrefab = LoadKayKitPrefab("red/hammer_spikes_red");
        if (hammerPrefab == null) return;

        var hammer = (GameObject)PrefabUtility.InstantiatePrefab(hammerPrefab, frameRoot.transform);
        hammer.name = name;
        hammer.transform.position = bridgeFloorCenter + new Vector3(0f, 7.25f, 0f);
        hammer.transform.rotation = Quaternion.Euler(0f, 90f, 180f);
        hammer.transform.localScale = new Vector3(0.9f, 0.95f, 0.9f);

        var headTrigger = hammer.AddComponent<BoxCollider>();
        headTrigger.isTrigger = true;
        headTrigger.center = new Vector3(0f, 5.9f, 0f);
        headTrigger.size = new Vector3(5.2f, 1.8f, 2.2f);

        var haz = hammer.AddComponent<RotatingHazard>();
        haz.motionType = RotatingHazard.MotionType.PendulumSwing;
        haz.localAxis = Vector3.right;
        haz.speed = speedRad;
        haz.swingAngle = 52f;
        haz.hitBehavior = RotatingHazard.HitBehavior.InstantRespawn;
    }

    public static void PlaceSlidingSawTrap(string name, Vector3 startPos, Vector3 slideOffset, Transform parent, float slideSpeed = 3.2f)
    {
        var prefab = LoadKayKitPrefab("red/saw_trap_red");
        if (prefab == null) return;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.position = startPos;
        go.transform.localScale = Vector3.one * 0.65f;

        var mp = go.AddComponent<MovingPlatform>();
        mp.moveOffset = slideOffset;
        mp.speed = slideSpeed;
        mp.waitTimeAtEnds = 0.25f;
        mp.smoothDamping = true;
        mp.autoCreatePassengerTrigger = false;

        if (go.transform.childCount > 0)
        {
            var blade = go.transform.GetChild(0).gameObject;
            var bc = blade.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.center = new Vector3(0f, 0.15f, 0f);
            bc.size = new Vector3(5.2f, 0.75f, 5.2f);

            var haz = blade.AddComponent<RotatingHazard>();
            haz.motionType = RotatingHazard.MotionType.ContinuousSpin;
            haz.localAxis = Vector3.up;
            haz.speed = 210f;
            haz.hitBehavior = RotatingHazard.HitBehavior.InstantRespawn;
        }
    }

    public static ActivatedBridge PlaceActivatedBridge(string relPath, string name, Vector3 startPos, Vector3 activeOffset,
        float moveSpeed, Transform parent, Quaternion? rot = null, Vector3? scale = null)
    {
        var go = PlacePlatform(relPath, name, startPos, scale ?? Vector3.one, parent, rot);
        if (go == null) return null;

        var bridge = go.AddComponent<ActivatedBridge>();
        bridge.activeOffset = activeOffset;
        bridge.moveSpeed = moveSpeed;
        return bridge;
    }

    public static void PlacePressureButton(string name, Vector3 pos, List<ActivatedBridge> targets, Transform parent)
    {
        var prefab = LoadKayKitPrefab("green/button_base_green");
        if (prefab == null) return;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.position = pos;

        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, 0.6f, 0f);
        box.size = new Vector3(1.6f, 1.2f, 1.6f);

        var lightGO = new GameObject("ButtonLight", typeof(Light));
        lightGO.transform.SetParent(go.transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        var pl = lightGO.GetComponent<Light>();
        pl.type = LightType.Point;
        pl.color = new Color(1f, 0.85f, 0.2f);
        pl.range = 6f;
        pl.intensity = 2f;

        var btn = go.AddComponent<PressureButton>();
        btn.buttonCap = go.transform.childCount > 0 ? go.transform.GetChild(0) : null;
        btn.indicatorLight = pl;
        btn.targets = targets;
        EditorUtility.SetDirty(btn);
    }

    public static void PlaceCheckpoint(int index, Vector3 floorPos, Quaternion spawnFacing, Material beaconMat, Transform parent, Vector3? flagWorldOffset = null)
    {
        var cpRoot = new GameObject($"Checkpoint_{index}");
        cpRoot.transform.SetParent(parent, false);
        cpRoot.transform.SetPositionAndRotation(floorPos, spawnFacing);

        var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pad.name = "CheckpointBeacon";
        pad.transform.SetParent(cpRoot.transform, false);
        pad.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        pad.transform.localScale = new Vector3(2.4f, 0.04f, 2.4f);
        Object.DestroyImmediate(pad.GetComponent<Collider>());
        var padBox = pad.AddComponent<BoxCollider>();
        padBox.isTrigger = false;
        padBox.size = new Vector3(0.85f, 1.0f, 0.85f);
        if (beaconMat != null) pad.GetComponent<MeshRenderer>().sharedMaterial = beaconMat;

        Vector3 flagPos = flagWorldOffset.HasValue
            ? floorPos + flagWorldOffset.Value
            : cpRoot.transform.TransformPoint(new Vector3(1.8f, 0f, 0f));
        HoistableFlag hoistable = PlaceHoistableKayKitFlag(
            "CheckpointFlag",
            flagPos,
            spawnFacing,
            Vector3.one,
            cpRoot.transform,
            triggerOnVictory: false,
            addPoleCollider: true);

        var lightGO = new GameObject("CheckpointLight", typeof(Light));
        lightGO.transform.SetParent(cpRoot.transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        var pl = lightGO.GetComponent<Light>();
        pl.type = LightType.Point;
        pl.range = 6f;
        pl.intensity = 2.0f;

        var col = cpRoot.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0f, 1.4f, 0f);
        col.size = new Vector3(4.5f, 2.8f, 4.5f);

        var cp = cpRoot.AddComponent<Checkpoint>();
        cp.checkpointIndex = index;
        cp.spawnOffset = new Vector3(0f, 0.15f, 0f);
        cp.faceForwardOnRespawn = true;
        cp.visualRenderer = pad.GetComponent<MeshRenderer>();
        cp.checkpointLight = pl;
        cp.checkpointFlag = hoistable;
        cp.inactiveColor = new Color(0.85f, 0.78f, 0.25f, 1f);
        cp.activeColor = new Color(0.15f, 0.95f, 0.35f, 1f);
        cp.activeEmissionColor = new Color(0.2f, 1.2f, 0.45f, 1f);
    }

    public static void PlaceCollectible(CollectibleItem.CollectibleType type, string name, Vector3 pos, Transform parent)
    {
        string path = type == CollectibleItem.CollectibleType.Diamond ? "blue/diamond_blue" : "yellow/star_yellow";
        var prefab = LoadKayKitPrefab(path);
        if (prefab == null) return;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = type == CollectibleItem.CollectibleType.Diamond ? Vector3.one * 0.95f : Vector3.one * 0.85f;

        var sc = go.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 0.85f;

        var item = go.AddComponent<CollectibleItem>();
        item.itemType = type;
    }

    public static void PlaceVictoryZone(
        Vector3 floorCenter,
        Quaternion facing,
        string nextSceneName,
        Transform parent)
    {
        var finishArch = PlaceProp("neutral/signage_finish", "FinishArch", floorCenter + facing * new Vector3(0f, 0f, -2.5f), facing * Quaternion.Euler(0f, 180f, 0f), Vector3.one, parent);
        if (finishArch != null) AddArchSideColliders(finishArch, halfSpanX: 2.45f, pillarHeight: 4.20f, pillarWidth: 0.55f, pillarDepth: 0.55f);
        PlaceHoistableKayKitFlag("VictoryFlag_L", floorCenter + facing * new Vector3(-3.2f, 0f, 2.0f), facing * Quaternion.Euler(0f, 20f, 0f), Vector3.one * 1.25f, parent, triggerOnVictory: true, addPoleCollider: false);
        PlaceHoistableKayKitFlag("VictoryFlag_R", floorCenter + facing * new Vector3(3.2f, 0f, 2.0f), facing * Quaternion.Euler(0f, -20f, 0f), Vector3.one * 1.25f, parent, triggerOnVictory: true, addPoleCollider: false);

        var goalGO = new GameObject("LevelGoal", typeof(BoxCollider), typeof(LevelGoal));
        goalGO.transform.SetParent(parent, false);
        goalGO.transform.SetPositionAndRotation(floorCenter + facing * new Vector3(0f, 1.5f, 0.5f), facing);
        var box = goalGO.GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(5.5f, 3.5f, 5.5f);

        var goal = goalGO.GetComponent<LevelGoal>();
        goal.playerTag = "Player";
        goal.stopPlayerOnFinish = true;
        goal.uiDelay = 1.25f;
        goal.nextSceneName = nextSceneName;
        goal.mainMenuSceneName = "Level1";

        new GameObject("VictoryUI", typeof(VictoryUI)).transform.SetParent(parent, false);
    }

    static HoistableFlag PlaceHoistableKayKitFlag(
        string name,
        Vector3 pos,
        Quaternion rot,
        Vector3 scale,
        Transform parent,
        bool triggerOnVictory,
        bool addPoleCollider)
    {
        KurdistanEnvironmentAssets.EnsureSplitKayKitFlagMeshes(
            out Mesh poleYellow,
            out Mesh poleGreen,
            out Mesh clothYellow,
            out Mesh clothGreen,
            out Material flagMat);

        var root = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(pos, rot);
        root.transform.localScale = scale;

        var poleMF = root.GetComponent<MeshFilter>();
        poleMF.sharedMesh = poleYellow;
        var poleMR = root.GetComponent<MeshRenderer>();
        poleMR.sharedMaterial = flagMat;

        if (addPoleCollider)
        {
            var flagCap = root.AddComponent<CapsuleCollider>();
            flagCap.isTrigger = false;
            flagCap.center = new Vector3(0f, 1.03f, 0f);
            flagCap.radius = 0.16f;
            flagCap.height = 2.06f;
        }

        var clothGO = new GameObject("FlagCloth", typeof(MeshFilter), typeof(MeshRenderer));
        clothGO.transform.SetParent(root.transform, false);
        clothGO.transform.localPosition = new Vector3(0f, -0.76f, 0f);
        clothGO.transform.localRotation = Quaternion.identity;
        clothGO.transform.localScale = Vector3.one;

        var clothMF = clothGO.GetComponent<MeshFilter>();
        clothMF.sharedMesh = clothYellow;
        var clothMR = clothGO.GetComponent<MeshRenderer>();
        clothMR.sharedMaterial = flagMat;

        var hoist = root.AddComponent<HoistableFlag>();
        hoist.triggerOnVictory = triggerOnVictory;
        hoist.hoistDuration = 0.55f;
        hoist.useBackOvershoot = true;
        hoist.movingTarget = clothGO.transform;
        hoist.loweredLocalY = -0.76f;
        hoist.raisedLocalY = 0f;
        hoist.poleMeshFilter = poleMF;
        hoist.inactivePoleMesh = poleYellow;
        hoist.activePoleMesh = poleGreen;
        hoist.clothMeshFilter = clothMF;
        hoist.inactiveClothMesh = clothYellow;
        hoist.activeClothMesh = clothGreen;
        return hoist;
    }

    static void AddFitBoxCollider(GameObject go, bool isTrigger)
    {
        var mf = go.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            var b = mf.sharedMesh.bounds;
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = isTrigger;
            col.center = b.center;
            col.size = b.size;
            return;
        }

        var fallback = go.AddComponent<BoxCollider>();
        fallback.isTrigger = isTrigger;
    }

    static Material CreateOrLoadBeaconMaterial()
    {
        if (!Directory.Exists(BeaconMatFolder)) Directory.CreateDirectory(BeaconMatFolder);
        string path = $"{BeaconMatFolder}/CheckpointBeacon.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(0.85f, 0.78f, 0.25f, 1f));
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
