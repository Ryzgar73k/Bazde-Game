using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Kürdistan doğa, dağ, vadi, su/sis/bulut, uzak kale/yayla köyü, ada kaya kökleri,
/// Newroz ateş çanakları, başlangıç/checkpoint meşaleleri ve hafif URP dalgalanan bayrağı
/// mevcut parkura dokunmadan sahneye kuran modüler Editör inşa aracı.
/// </summary>
public static class KurdistanEnvironmentBuilder
{
    public const string EnvRootName = "--- ENVIRONMENT & LIGHTING ---";

    public static bool EnableDrapedMountainFlag = true;
    public static bool EnablePaintedMountainFlag = true;
    public static float DrapedFlagWidthMeters = 78f;
    public static float PaintedFlagWidthMeters = 58f;

    struct BiomeProfile
    {
        public int LevelNumber;
        public string Name;
        public Vector3 SunEuler;
        public Color SunColor;
        public float SunIntensity;
        public Color FogColor;
        public float FogDensity;
        public float AmbientIntensity;
        public float DefaultFallY;
        public Vector2 SurfaceScrollSpeed;
        public float SurfaceBobAmp;
        public float SurfaceBobSpeed;
        public Vector3 CloudDrift;
        public int TreeCount;
        public int RockCount;
        public int CloudCount;
        public int EagleCount;
        public bool AddDeepValleyUnderSurface;
        public bool AddCloudSeaClusters;
        public float DrapedFlagAngleDeg;
        public float PaintedFlagAngleDeg;
    }

    [MenuItem("Tools/Parkur/Aktif Sahnenin Cevresini Yenile")]
    public static void RebuildActiveSceneEnvironment()
    {
        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid())
        {
            Debug.LogError("[KurdistanEnvironmentBuilder] Geçerli aktif sahne bulunamadı.");
            return;
        }

        int levelNum = InferLevelNumber(active.path, active.name);
        BuildEnvironmentForScene(active, levelNum);
        if (!string.IsNullOrEmpty(active.path))
            EditorSceneManager.SaveScene(active);
        Debug.Log($"[KurdistanEnvironmentBuilder] Aktif sahne çevresi yenilendi: {active.name} (Biyom L{levelNum})");
    }

    [MenuItem("Tools/Parkur/Cevreyi Yenile (Tum Leveller 1-4)")]
    public static void RebuildAllLevelsEnvironment()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        RebuildAllLevelsInternal();
    }

    /// <summary>
    /// Unity batch mode (-executeMethod) üzerinden tüm 4 standart sahnenin çevresini yeniler.
    /// Mevcut parkur geometrisine ve oynanış objelerine dokunmaz.
    /// </summary>
    public static void RebuildAllLevelsEnvironmentBatch()
    {
        RebuildAllLevelsInternal();
    }

    static void RebuildAllLevelsInternal()
    {
        string originalScenePath = SceneManager.GetActiveScene().path;
        string[] paths = KayKitLevelKit.BuildScenePaths;
        for (int i = 0; i < paths.Length; i++)
        {
            string scenePath = paths[i];
            if (!System.IO.File.Exists(scenePath))
            {
                Debug.LogWarning($"[KurdistanEnvironmentBuilder] Sahne bulunamadı, atlanıyor: {scenePath}");
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int levelNum = InferLevelNumber(scenePath, scene.name);
            BuildEnvironmentForScene(scene, levelNum);
            bool saved = EditorSceneManager.SaveScene(scene);
            if (!saved)
                Debug.LogError($"[KurdistanEnvironmentBuilder] Sahne kaydedilemedi: {scenePath}");
            else
                Debug.Log($"[KurdistanEnvironmentBuilder] Çevre başarıyla yenilendi: {scenePath} (Biyom L{levelNum})");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!string.IsNullOrEmpty(originalScenePath) && System.IO.File.Exists(originalScenePath))
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
    }

    public static int InferLevelNumber(string scenePath, string sceneName = null)
    {
        string combined = ((scenePath ?? "") + " " + (sceneName ?? "")).ToLowerInvariant();
        if (combined.Contains("level4")) return 4;
        if (combined.Contains("level3")) return 3;
        if (combined.Contains("level2")) return 2;
        return 1;
    }

    public static void BuildEnvironmentForScene(Scene scene, int levelNumber)
    {
        if (!scene.IsValid()) return;
        levelNumber = Mathf.Clamp(levelNumber, 1, 4);
        BiomeProfile profile = GetProfile(levelNumber);

        // 1. Sahnedeki mevcut parkur sınırlarını ve düşme kotunu oku
        Bounds courseBounds = ComputeCourseBounds(scene);
        float fallThresholdY = ResolveFallThresholdY(scene, profile.DefaultFallY);
        Vector2 courseCenterXZ = new Vector2(courseBounds.center.x, courseBounds.center.z);
        float maxHalfExtent = Mathf.Max(courseBounds.extents.x, courseBounds.extents.z);
        float innerValleyRadius = Mathf.Max(54f, maxHalfExtent + 28f);

        // 2. Kalıcı varlıkları (doku, materyal, mesh) üret/güncelle
        var shared = KurdistanEnvironmentAssets.EnsureSharedAssets();
        var biome = KurdistanEnvironmentAssets.EnsureBiomeAssets(
            levelNumber,
            courseCenterXZ,
            innerValleyRadius,
            profile.DrapedFlagAngleDeg,
            profile.PaintedFlagAngleDeg,
            DrapedFlagWidthMeters,
            PaintedFlagWidthMeters);

        // 3. Yalnızca --- ENVIRONMENT & LIGHTING --- kökünü temizle ve kur (parkura dokunma!)
        GameObject envRootGO = FindOrCreateEnvRoot(scene);
        Transform envRoot = envRootGO.transform;
        ClearChildrenImmediate(envRoot);

        // 4. Işık, Skybox, Sis ve Ana Kamera farClipPlane
        SetupLightingAndAtmosphere(envRoot, profile, biome);
        ConfigureSceneCameras(scene);

        // 5. Dağ & Vadi halkaları + Yamaca Serili / Kayaya İşlenmiş Dağ Bayrakları
        CreateMountainLayer(envRoot, courseCenterXZ, biome, shared);

        // 6. Parkur altı biyom yüzeyi (Göl / Nehir / Altın Sis / Bulut Denizi)
        var motion = envRootGO.GetComponent<AmbientEnvironmentMotion>();
        if (motion == null) motion = envRootGO.AddComponent<AmbientEnvironmentMotion>();
        motion.clouds.Clear();
        motion.eagles.Clear();

        Renderer surfaceRenderer = CreateBottomSurface(envRoot, courseCenterXZ, fallThresholdY, profile, biome, shared);
        motion.animatedSurfaceRenderer = surfaceRenderer;
        motion.uvScrollSpeed = profile.SurfaceScrollSpeed;
        motion.verticalBobAmplitude = profile.SurfaceBobAmp;
        motion.verticalBobSpeed = profile.SurfaceBobSpeed;

        // 7. BOXOPHOBIC Low-Poly ağaçlar, kayalar ve uzak kale/yayla köyü siluetleri
        PopulateNatureAndDistantLandmarks(envRoot, courseCenterXZ, innerValleyRadius, fallThresholdY, profile, biome, shared);

        // 8. Büyük sabit adalara görsel kaya/taş kökleri (collider yok)
        CreateIslandRockRoots(scene, envRoot, fallThresholdY, shared);

        // 9. Kültürel & sembolik noktalar: Başlangıç meşaleleri, Checkpoint meşaleleri, Zirve Bayrağı + Newroz Çanakları
        PlaceCulturalLandmarks(scene, envRoot, shared);

        // 10. Hareketli bulutlar ve dağ kartalları
        PopulateCloudsAndEagles(envRoot, courseBounds, innerValleyRadius, profile, shared, motion);

        EditorSceneManager.MarkSceneDirty(scene);
    }

    static BiomeProfile GetProfile(int levelNumber)
    {
        switch (levelNumber)
        {
            case 2:
                return new BiomeProfile
                {
                    LevelNumber = 2,
                    Name = "Kanyon ve Nehir Vadisi",
                    SunEuler = new Vector3(56f, 28f, 0f),
                    SunColor = new Color(1.0f, 0.97f, 0.92f),
                    SunIntensity = 1.42f,
                    FogColor = new Color(0.68f, 0.79f, 0.87f),
                    FogDensity = 0.0024f,
                    AmbientIntensity = 1.15f,
                    DefaultFallY = -12f,
                    SurfaceScrollSpeed = new Vector2(0.0f, -0.055f),
                    SurfaceBobAmp = 0.05f,
                    SurfaceBobSpeed = 1.4f,
                    CloudDrift = new Vector3(2.1f, 0f, 0.7f),
                    TreeCount = 34,
                    RockCount = 26,
                    CloudCount = 14,
                    EagleCount = 3,
                    AddDeepValleyUnderSurface = false,
                    AddCloudSeaClusters = false,
                    DrapedFlagAngleDeg = 80f,
                    PaintedFlagAngleDeg = 36f
                };

            case 3:
                return new BiomeProfile
                {
                    LevelNumber = 3,
                    Name = "Yüksek Dağ Geçidi (Gün Batımı)",
                    SunEuler = new Vector3(20f, -128f, 0f),
                    SunColor = new Color(1.0f, 0.76f, 0.48f),
                    SunIntensity = 1.38f,
                    FogColor = new Color(0.84f, 0.64f, 0.49f),
                    FogDensity = 0.0031f,
                    AmbientIntensity = 1.05f,
                    DefaultFallY = -14f,
                    SurfaceScrollSpeed = new Vector2(0.015f, 0.010f),
                    SurfaceBobAmp = 0.14f,
                    SurfaceBobSpeed = 0.65f,
                    CloudDrift = new Vector3(1.5f, 0f, -0.6f),
                    TreeCount = 40,
                    RockCount = 24,
                    CloudCount = 14,
                    EagleCount = 4,
                    AddDeepValleyUnderSurface = true,
                    AddCloudSeaClusters = false,
                    DrapedFlagAngleDeg = 92f,
                    PaintedFlagAngleDeg = 48f
                };

            case 4:
                return new BiomeProfile
                {
                    LevelNumber = 4,
                    Name = "Karlı Zirveler (Bulutların Üstü)",
                    SunEuler = new Vector3(44f, 42f, 0f),
                    SunColor = new Color(0.95f, 0.97f, 1.0f),
                    SunIntensity = 1.36f,
                    FogColor = new Color(0.76f, 0.85f, 0.94f),
                    FogDensity = 0.0023f,
                    AmbientIntensity = 1.22f,
                    DefaultFallY = -14f,
                    SurfaceScrollSpeed = new Vector2(0.008f, 0.006f),
                    SurfaceBobAmp = 0.12f,
                    SurfaceBobSpeed = 0.55f,
                    CloudDrift = new Vector3(2.4f, 0f, 0.8f),
                    TreeCount = 22,
                    RockCount = 24,
                    CloudCount = 16,
                    EagleCount = 4,
                    AddDeepValleyUnderSurface = false,
                    AddCloudSeaClusters = true,
                    DrapedFlagAngleDeg = 86f,
                    PaintedFlagAngleDeg = 142f
                };

            default:
                return new BiomeProfile
                {
                    LevelNumber = 1,
                    Name = "Bahar Yaylası (Sabah)",
                    SunEuler = new Vector3(38f, -32f, 0f),
                    SunColor = new Color(1.0f, 0.95f, 0.86f),
                    SunIntensity = 1.34f,
                    FogColor = new Color(0.67f, 0.82f, 0.92f),
                    FogDensity = 0.0021f,
                    AmbientIntensity = 1.18f,
                    DefaultFallY = -12f,
                    SurfaceScrollSpeed = new Vector2(0.012f, 0.018f),
                    SurfaceBobAmp = 0.06f,
                    SurfaceBobSpeed = 0.95f,
                    CloudDrift = new Vector3(1.6f, 0f, 0.45f),
                    TreeCount = 46,
                    RockCount = 20,
                    CloudCount = 14,
                    EagleCount = 3,
                    AddDeepValleyUnderSurface = false,
                    AddCloudSeaClusters = false,
                    DrapedFlagAngleDeg = 84f,
                    PaintedFlagAngleDeg = 134f
                };
        }
    }

    static GameObject FindOrCreateEnvRoot(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == EnvRootName)
                return root;
        }
        var go = new GameObject(EnvRootName);
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.SetAsFirstSibling();
        return go;
    }

    static void ClearChildrenImmediate(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }

    static Bounds ComputeCourseBounds(Scene scene)
    {
        bool initialized = false;
        Bounds b = new Bounds(Vector3.zero, new Vector3(40f, 20f, 40f));

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == EnvRootName || root.name == "--- GAMEPLAY & MANAGERS ---")
                continue;

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!initialized) { b = r.bounds; initialized = true; }
                else b.Encapsulate(r.bounds);
            }
        }

        return b;
    }

    static float ResolveFallThresholdY(Scene scene, float fallback)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var rm = root.GetComponentInChildren<RespawnManager>(true);
            if (rm != null)
                return rm.fallThresholdY;
        }
        return fallback;
    }

    static void SetupLightingAndAtmosphere(Transform envRoot, BiomeProfile profile, KurdistanEnvironmentAssets.BiomeAssetSet biome)
    {
        var lightGO = new GameObject("Directional Light", typeof(Light));
        lightGO.transform.SetParent(envRoot, false);
        lightGO.transform.rotation = Quaternion.Euler(profile.SunEuler);

        var light = lightGO.GetComponent<Light>();
        light.type = LightType.Directional;
        light.color = profile.SunColor;
        light.intensity = profile.SunIntensity;
        light.shadows = LightShadows.Soft;

        RenderSettings.sun = light;
        RenderSettings.skybox = biome.SkyboxMat;
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = profile.AmbientIntensity;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = profile.FogColor;
        RenderSettings.fogDensity = profile.FogDensity;

        DynamicGI.UpdateEnvironment();
    }

    static void ConfigureSceneCameras(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var cam in root.GetComponentsInChildren<Camera>(true))
            {
                if (cam.farClipPlane < 650f)
                {
                    cam.farClipPlane = 650f;
                    EditorUtility.SetDirty(cam);
                }
            }
        }
    }

    static void CreateMountainLayer(
        Transform envRoot,
        Vector2 centerXZ,
        KurdistanEnvironmentAssets.BiomeAssetSet biome,
        KurdistanEnvironmentAssets.SharedAssetSet shared)
    {
        var mountainGroup = new GameObject("ZagrosMountains").transform;
        mountainGroup.SetParent(envRoot, false);
        mountainGroup.position = new Vector3(centerXZ.x, 0f, centerXZ.y);

        Material[] triMats = { biome.FoothillMat, biome.CliffRockMat, biome.SnowCapMat };

        CreateStaticMeshObject("ValleyRim", mountainGroup, Vector3.zero, Quaternion.identity, Vector3.one, biome.ValleyRimMesh, useLocalSpace: true, triMats);
        CreateStaticMeshObject("HorizonMountainRange", mountainGroup, Vector3.zero, Quaternion.identity, Vector3.one, biome.MountainRingMesh, useLocalSpace: true, triMats);

        // A: Dağ yamacına serili dev dalgalanan kumaş Kürdistan bayrağı
        if (EnableDrapedMountainFlag && biome.DrapedMountainFlagMesh != null && shared.FlagMat != null)
        {
            var drapedGO = CreateStaticMeshObject(
                "HorizonMountainFlag_Draped",
                mountainGroup,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one,
                biome.DrapedMountainFlagMesh,
                useLocalSpace: true,
                shared.FlagMat);
            var wave = drapedGO.AddComponent<MountainDrapedFlag>();
            wave.waveAmplitude = 0.65f;
            wave.waveFrequencyX = 2.2f;
            wave.waveFrequencyY = 1.4f;
            wave.waveSpeed = 1.05f;
            wave.secondaryAmplitude = 0.22f;
        }

        // B: Vadi/dağ kayasına işlenmiş anıtsal Kürdistan bayrağı
        if (EnablePaintedMountainFlag && biome.PaintedMountainFlagMesh != null && shared.MountainPaintFlagMat != null)
        {
            CreateStaticMeshObject(
                "ValleyMountainFlag_Painted",
                mountainGroup,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one,
                biome.PaintedMountainFlagMesh,
                useLocalSpace: true,
                shared.MountainPaintFlagMat);
        }
    }

    static Renderer CreateBottomSurface(
        Transform envRoot,
        Vector2 centerXZ,
        float fallThresholdY,
        BiomeProfile profile,
        KurdistanEnvironmentAssets.BiomeAssetSet biome,
        KurdistanEnvironmentAssets.SharedAssetSet shared)
    {
        var surfaceGroup = new GameObject("ValleyFloorSurface").transform;
        surfaceGroup.SetParent(envRoot, false);

        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = profile.LevelNumber == 1 ? "MountainLakeSurface"
                   : profile.LevelNumber == 2 ? "CanyonRiverSurface"
                   : profile.LevelNumber == 3 ? "SunsetMistSurface"
                   : "CloudSeaFloor";
        plane.transform.SetParent(surfaceGroup, false);
        plane.transform.position = new Vector3(centerXZ.x, fallThresholdY, centerXZ.y);
        plane.transform.localScale = new Vector3(110f, 1f, 110f); // 1100 x 1100 m

        var col = plane.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        var mr = plane.GetComponent<MeshRenderer>();
        mr.sharedMaterial = biome.SurfaceMat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;

        if (profile.AddDeepValleyUnderSurface)
        {
            var deep = GameObject.CreatePrimitive(PrimitiveType.Plane);
            deep.name = "DeepValleyFloor";
            deep.transform.SetParent(surfaceGroup, false);
            deep.transform.position = new Vector3(centerXZ.x, fallThresholdY - 14f, centerXZ.y);
            deep.transform.localScale = new Vector3(110f, 1f, 110f);
            var dCol = deep.GetComponent<Collider>();
            if (dCol != null) Object.DestroyImmediate(dCol);
            var dMr = deep.GetComponent<MeshRenderer>();
            dMr.sharedMaterial = biome.FoothillMat;
            dMr.shadowCastingMode = ShadowCastingMode.Off;
            dMr.receiveShadows = false;
        }

        if (profile.AddCloudSeaClusters && shared.CloudMesh != null)
        {
            var rng = new System.Random(4400 + profile.LevelNumber);
            for (int i = 0; i < 24; i++)
            {
                float angle = ((float)i / 24f) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.25f;
                float rad = Mathf.Lerp(25f, 165f, (float)rng.NextDouble());
                float x = centerXZ.x + Mathf.Cos(angle) * rad;
                float z = centerXZ.y + Mathf.Sin(angle) * rad;
                float y = fallThresholdY + Mathf.Lerp(-0.7f, 0.75f, (float)rng.NextDouble());
                float sx = Mathf.Lerp(5.5f, 12.5f, (float)rng.NextDouble());
                float sy = Mathf.Lerp(1.1f, 2.2f, (float)rng.NextDouble());
                float sz = Mathf.Lerp(4.5f, 10.5f, (float)rng.NextDouble());

                CreateStaticMeshObject(
                    $"SeaCloud_{i + 1}",
                    surfaceGroup,
                    new Vector3(x, y, z),
                    Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f),
                    new Vector3(sx, sy, sz),
                    shared.CloudMesh,
                    shared.CloudMat);
            }
        }

        return mr;
    }

    static void PopulateNatureAndDistantLandmarks(
        Transform envRoot,
        Vector2 centerXZ,
        float innerValleyRadius,
        float fallThresholdY,
        BiomeProfile profile,
        KurdistanEnvironmentAssets.BiomeAssetSet biome,
        KurdistanEnvironmentAssets.SharedAssetSet shared)
    {
        var natureGroup = new GameObject("NatureAndLandmarks").transform;
        natureGroup.SetParent(envRoot, false);

        Mesh firMeshA = LoadFirstMeshFromPath("Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Meshes/LowPoly - FirTree A.FBX");
        Mesh firMeshB = LoadFirstMeshFromPath("Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Meshes/LowPoly - FirTree B.FBX");
        Mesh rockMeshA = LoadFirstMeshFromPath("Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Meshes/LowPoly - Rock A.FBX");
        Mesh rockMeshB = LoadFirstMeshFromPath("Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Meshes/LowPoly - Rock B.FBX");

        var rng = new System.Random(1000 + profile.LevelNumber * 77);
        Material[] firMats = { shared.TreeTrunkMat, biome.PineFoliageMat };

        // Çam ağaçları: Vadi yamaçlarına
        for (int i = 0; i < profile.TreeCount; i++)
        {
            Mesh chosen = (i % 2 == 0 || firMeshB == null) ? firMeshA : firMeshB;
            if (chosen == null) continue;

            float angle = ((float)i / profile.TreeCount) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.22f;
            float dist = innerValleyRadius + Mathf.Lerp(12f, 78f, (float)rng.NextDouble());
            float x = centerXZ.x + Mathf.Cos(angle) * dist;
            float z = centerXZ.y + Mathf.Sin(angle) * dist;
            float y = fallThresholdY + Mathf.Lerp(-1.5f, 11f, (float)rng.NextDouble());
            float s = Mathf.Lerp(2.2f, 4.4f, (float)rng.NextDouble());

            CreateStaticMeshObject(
                $"FirTree_{i + 1}",
                natureGroup,
                new Vector3(x, y, z),
                Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f),
                Vector3.one * s,
                chosen,
                firMats);
        }

        // Low-Poly Kayalar: Vadi kenarları ve göl/kanyon kıyısı
        for (int i = 0; i < profile.RockCount; i++)
        {
            Mesh chosen = (i % 2 == 0 || rockMeshB == null) ? rockMeshA : rockMeshB;
            if (chosen == null) continue;

            float angle = ((float)i / profile.RockCount) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.35f;
            float dist = innerValleyRadius + Mathf.Lerp(4f, 68f, (float)rng.NextDouble());
            float x = centerXZ.x + Mathf.Cos(angle) * dist;
            float z = centerXZ.y + Mathf.Sin(angle) * dist;
            float y = fallThresholdY + Mathf.Lerp(-2.5f, 5.5f, (float)rng.NextDouble());
            float s = Mathf.Lerp(2.4f, 6.2f, (float)rng.NextDouble());

            CreateStaticMeshObject(
                $"ValleyRock_{i + 1}",
                natureGroup,
                new Vector3(x, y, z),
                Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f),
                new Vector3(s, s * Mathf.Lerp(0.75f, 1.35f, (float)rng.NextDouble()), s),
                chosen,
                biome.CliffRockMat);
        }

        // Uzak Taş Kale/Burç Siluetleri (Erbil / Amed / Hasankeyf esintisi)
        if (shared.StoneCastleMesh != null)
        {
            float[] castleAnglesDeg = { 18f, 108f, -62f };
            for (int i = 0; i < castleAnglesDeg.Length; i++)
            {
                float rad = castleAnglesDeg[i] * Mathf.Deg2Rad;
                float dist = innerValleyRadius + 58f + (i * 18f);
                Vector3 pos = new Vector3(
                    centerXZ.x + Mathf.Cos(rad) * dist,
                    fallThresholdY + 12f + (i % 2) * 5f,
                    centerXZ.y + Mathf.Sin(rad) * dist);
                Quaternion rot = Quaternion.LookRotation(new Vector3(centerXZ.x - pos.x, 0f, centerXZ.y - pos.z), Vector3.up);
                CreateStaticMeshObject($"DistantStoneCastle_{i + 1}", natureGroup, pos, rot, Vector3.one * 1.45f, shared.StoneCastleMesh, shared.StoneArchMat);
            }
        }

        // Uzak Taş Yayla Köyü Kümeleri
        if (shared.HighlandVillageMesh != null)
        {
            float[] villageAnglesDeg = { -28f, 64f, 165f };
            for (int i = 0; i < villageAnglesDeg.Length; i++)
            {
                float rad = villageAnglesDeg[i] * Mathf.Deg2Rad;
                float dist = innerValleyRadius + 36f + (i * 12f);
                Vector3 pos = new Vector3(
                    centerXZ.x + Mathf.Cos(rad) * dist,
                    fallThresholdY + 5f + (i % 2) * 3f,
                    centerXZ.y + Mathf.Sin(rad) * dist);
                Quaternion rot = Quaternion.Euler(0f, (i * 55f) + 20f, 0f);
                CreateStaticMeshObject($"HighlandVillage_{i + 1}", natureGroup, pos, rot, Vector3.one * 1.3f, shared.HighlandVillageMesh, shared.StoneArchMat);
            }
        }
    }

    static void CreateIslandRockRoots(Scene scene, Transform envRoot, float fallThresholdY, KurdistanEnvironmentAssets.SharedAssetSet shared)
    {
        if (shared.RockPillarRootMesh == null) return;

        var rootsGroup = new GameObject("IslandRockRoots").transform;
        rootsGroup.SetParent(envRoot, false);

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == EnvRootName || root.name == "--- GAMEPLAY & MANAGERS ---")
                continue;

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                bool isMajorIsland = t.name == "StartIsland"
                    || t.name == "VictorySummitArena"
                    || (t.name.StartsWith("Checkpoint") && t.name.EndsWith("_Island"));
                if (!isMajorIsland) continue;

                if (!TryGetTransformBounds(t, out Bounds b)) continue;

                float topY = b.min.y + 0.25f;
                float bottomY = fallThresholdY - 6f;
                float rootHeight = Mathf.Max(4f, topY - bottomY);
                float radiusScale = Mathf.Clamp(Mathf.Min(b.extents.x, b.extents.z) * 0.90f, 2.2f, 5.5f);

                CreateStaticMeshObject(
                    $"{t.name}_RockRoot",
                    rootsGroup,
                    new Vector3(b.center.x, topY, b.center.z),
                    Quaternion.Euler(0f, (t.position.x + t.position.z) * 17f, 0f),
                    new Vector3(radiusScale, rootHeight, radiusScale),
                    shared.RockPillarRootMesh,
                    shared.RockRootMat);
            }
        }
    }

    static void PlaceCulturalLandmarks(Scene scene, Transform envRoot, KurdistanEnvironmentAssets.SharedAssetSet shared)
    {
        var landmarksGroup = new GameObject("CulturalLandmarks").transform;
        landmarksGroup.SetParent(envRoot, false);

        // 1. Başlangıç Kemeri (WelcomeArch): İki yana taş sütun + sıcak meşaleler (StartIsland zemin sınırları içinde)
        Transform startIsland = FindTransformByName(scene, "StartIsland");
        Transform welcomeArch = FindTransformByName(scene, "WelcomeArch");
        if (startIsland != null && TryGetTransformBounds(startIsland, out Bounds startBounds))
        {
            float torchY = startBounds.max.y;
            float torchZ = welcomeArch != null
                ? Mathf.Clamp(welcomeArch.position.z - 0.85f, startBounds.min.z + 0.55f, startBounds.max.z - 0.55f)
                : startBounds.max.z - 1.35f;
            Vector3 leftPos = new Vector3(startBounds.min.x + 0.55f, torchY, torchZ);
            Vector3 rightPos = new Vector3(startBounds.max.x - 0.55f, torchY, torchZ);
            CreateTorchAt("StartTorch_L", landmarksGroup, leftPos, 1.0f, 1.85f, shared);
            CreateTorchAt("StartTorch_R", landmarksGroup, rightPos, 1.0f, 1.85f, shared);
        }
        else if (welcomeArch != null)
        {
            Vector3 leftPos = welcomeArch.TransformPoint(new Vector3(-2.45f, 0f, -0.85f));
            Vector3 rightPos = welcomeArch.TransformPoint(new Vector3(2.45f, 0f, -0.85f));
            CreateTorchAt("StartTorch_L", landmarksGroup, leftPos, 1.0f, 1.85f, shared);
            CreateTorchAt("StartTorch_R", landmarksGroup, rightPos, 1.0f, 1.85f, shared);
        }

        // 2. Checkpoint Adaları: Güvenli köşeye taş meşale + ölçülü kırmızı-sarı-yeşil-beyaz aksan
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == EnvRootName || root.name == "--- GAMEPLAY & MANAGERS ---")
                continue;

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!(t.name.StartsWith("Checkpoint") && t.name.EndsWith("_Island")))
                    continue;

                if (!TryGetTransformBounds(t, out Bounds b)) continue;
                Vector3 torchPos = new Vector3(b.min.x + 0.72f, b.max.y, b.max.z - 0.72f);
                Transform torch = CreateTorchAt($"{t.name}_Torch", landmarksGroup, torchPos, 0.82f, 1.45f, shared);
                if (torch != null)
                    AddSubtleTricolorAccent(torch, new Vector3(0f, 0.75f, 0f), 0.22f, shared);
            }
        }

        // 3. Zafer Zirvesi (VictorySummitArena): Dalgalanan Kürdistan Bayrağı + 2 adet Newroz Ateş Çanağı
        Transform summitArena = FindTransformByName(scene, "VictorySummitArena");
        if (summitArena != null && TryGetTransformBounds(summitArena, out Bounds summitBounds))
        {
            Transform goal = FindTransformByName(scene, "LevelGoal");
            Quaternion facing = goal != null ? goal.rotation : Quaternion.identity;
            Vector3 arenaTopCenter = new Vector3(summitBounds.center.x, summitBounds.max.y, summitBounds.center.z);

            Vector3 forward = facing * Vector3.forward;
            Vector3 right = facing * Vector3.right;
            float backDist = Mathf.Max(2.8f, Mathf.Min(summitBounds.extents.x, summitBounds.extents.z) - 1.15f);
            float sideDist = Mathf.Max(2.6f, Mathf.Min(summitBounds.extents.x, summitBounds.extents.z) - 1.35f);

            // Zirvenin arka merkezinde dalgalanan 21 ışınlı Kürdistan bayrağı
            Vector3 polePos = arenaTopCenter + forward * backDist;
            CreateKurdistanFlagAssembly(landmarksGroup, polePos, facing, shared);

            // İki arka köşede Newroz ateş çanakları
            CreateNewrozBrazierAt("NewrozBrazier_L", landmarksGroup, arenaTopCenter + forward * (backDist - 0.45f) - right * sideDist, shared);
            CreateNewrozBrazierAt("NewrozBrazier_R", landmarksGroup, arenaTopCenter + forward * (backDist - 0.45f) + right * sideDist, shared);
        }
    }

    static void CreateKurdistanFlagAssembly(Transform parent, Vector3 basePos, Quaternion facing, KurdistanEnvironmentAssets.SharedAssetSet shared)
    {
        var flagRoot = new GameObject("KurdistanSummitFlag");
        flagRoot.transform.SetParent(parent, false);
        flagRoot.transform.SetPositionAndRotation(basePos, facing);

        var flagCol = flagRoot.AddComponent<CapsuleCollider>();
        flagCol.isTrigger = false;
        flagCol.center = new Vector3(0f, 3.1f, 0f);
        flagCol.radius = 0.38f;
        flagCol.height = 6.2f;

        // Taş kaide
        var basePedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        basePedestal.name = "StoneBase";
        basePedestal.transform.SetParent(flagRoot.transform, false);
        basePedestal.transform.localPosition = new Vector3(0f, 0.16f, 0f);
        basePedestal.transform.localScale = new Vector3(0.85f, 0.16f, 0.85f);
        Object.DestroyImmediate(basePedestal.GetComponent<Collider>());
        var baseMr = basePedestal.GetComponent<MeshRenderer>();
        baseMr.sharedMaterial = shared.StoneArchMat;
        baseMr.shadowCastingMode = ShadowCastingMode.Off;

        // Direk
        const float poleHeight = 6.2f;
        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "FlagPole";
        pole.transform.SetParent(flagRoot.transform, false);
        pole.transform.localPosition = new Vector3(0f, poleHeight * 0.5f, 0f);
        pole.transform.localScale = new Vector3(0.13f, poleHeight * 0.5f, 0.13f);
        Object.DestroyImmediate(pole.GetComponent<Collider>());
        var poleMr = pole.GetComponent<MeshRenderer>();
        poleMr.sharedMaterial = shared.AccentWhiteMat;
        poleMr.shadowCastingMode = ShadowCastingMode.Off;

        // Altın tepe alemi
        var finial = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        finial.name = "PoleFinial";
        finial.transform.SetParent(flagRoot.transform, false);
        finial.transform.localPosition = new Vector3(0f, poleHeight + 0.14f, 0f);
        finial.transform.localScale = Vector3.one * 0.28f;
        Object.DestroyImmediate(finial.GetComponent<Collider>());
        var finMr = finial.GetComponent<MeshRenderer>();
        finMr.sharedMaterial = shared.AccentGoldMat;
        finMr.shadowCastingMode = ShadowCastingMode.Off;

        // Hafif URP Dalgalanan Kumaş
        var cloth = new GameObject("FlagCloth", typeof(MeshFilter), typeof(MeshRenderer), typeof(WavingFlagMesh));
        cloth.transform.SetParent(flagRoot.transform, false);
        cloth.transform.localPosition = new Vector3(0.06f, poleHeight - 1.10f, 0f);
        cloth.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);

        var mr = cloth.GetComponent<MeshRenderer>();
        mr.sharedMaterial = shared.FlagMat;
        mr.shadowCastingMode = ShadowCastingMode.Off;

        var wave = cloth.GetComponent<WavingFlagMesh>();
        wave.width = 3.35f;
        wave.height = 2.15f;
        wave.segmentsX = 14;
        wave.segmentsY = 8;
        wave.waveAmplitude = 0.24f;
        wave.waveFrequency = 2.4f;
        wave.waveSpeed = 3.2f;
    }

    static void CreateNewrozBrazierAt(string name, Transform parent, Vector3 pos, KurdistanEnvironmentAssets.SharedAssetSet shared)
    {
        if (shared.NewrozBrazierMesh == null) return;

        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = pos;

        var brazierCol = root.AddComponent<CapsuleCollider>();
        brazierCol.isTrigger = false;
        brazierCol.center = new Vector3(0f, 0.95f, 0f);
        brazierCol.radius = 0.82f;
        brazierCol.height = 1.95f;

        CreateStaticMeshObject("BrazierStone", root.transform, Vector3.zero, Quaternion.identity, Vector3.one, shared.NewrozBrazierMesh, useLocalSpace: true, shared.StoneArchMat);

        Transform flame = null;
        if (shared.FlameCoreMesh != null)
        {
            var flameGO = CreateStaticMeshObject("NewrozFlame", root.transform, new Vector3(0f, 1.32f, 0f), Quaternion.identity, new Vector3(1.55f, 1.65f, 1.55f), shared.FlameCoreMesh, useLocalSpace: true, shared.EmberCoreMat);
            flame = flameGO.transform;
        }

        // Hafif mobil dostu ateş/kor partikülü (maks 22 partikül)
        var psGO = new GameObject("NewrozSparks", typeof(ParticleSystem));
        psGO.transform.SetParent(root.transform, false);
        psGO.transform.localPosition = new Vector3(0f, 1.48f, 0f);
        ConfigureFireParticles(psGO.GetComponent<ParticleSystem>(), shared.FlameParticleMat, maxParticles: 22, rate: 10f, startSize: 0.42f, speed: 1.55f);

        var lightGO = new GameObject("NewrozLight", typeof(Light));
        lightGO.transform.SetParent(root.transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 2.05f, 0f);
        var pl = lightGO.GetComponent<Light>();
        pl.type = LightType.Point;
        pl.color = new Color(1.0f, 0.58f, 0.18f);
        pl.range = 8.5f;
        pl.intensity = 2.6f;
        pl.shadows = LightShadows.None;

        var flicker = root.AddComponent<FlickeringFlameLight>();
        flicker.targetLight = pl;
        flicker.flameVisual = flame;
        flicker.baseIntensity = 2.6f;
        flicker.intensityVariance = 0.55f;
        flicker.flickerSpeed = 8.0f;
    }

    static Transform CreateTorchAt(
        string name,
        Transform parent,
        Vector3 pos,
        float scale,
        float lightIntensity,
        KurdistanEnvironmentAssets.SharedAssetSet shared)
    {
        if (shared.StoneTorchPedestalMesh == null) return null;

        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = pos;

        root.transform.localScale = Vector3.one * scale;

        var torchCol = root.AddComponent<CapsuleCollider>();
        torchCol.isTrigger = false;
        torchCol.center = new Vector3(0f, 1.05f, 0f);
        torchCol.radius = 0.34f;
        torchCol.height = 2.15f;

        CreateStaticMeshObject("TorchPedestal", root.transform, Vector3.zero, Quaternion.identity, Vector3.one, shared.StoneTorchPedestalMesh, useLocalSpace: true, shared.StoneArchMat);

        Transform flame = null;
        if (shared.FlameCoreMesh != null)
        {
            var flameGO = CreateStaticMeshObject("TorchFlame", root.transform, new Vector3(0f, 1.84f, 0f), Quaternion.identity, new Vector3(0.85f, 0.95f, 0.85f), shared.FlameCoreMesh, useLocalSpace: true, shared.EmberCoreMat);
            flame = flameGO.transform;
        }

        var psGO = new GameObject("TorchSparks", typeof(ParticleSystem));
        psGO.transform.SetParent(root.transform, false);
        psGO.transform.localPosition = new Vector3(0f, 1.92f, 0f);
        ConfigureFireParticles(psGO.GetComponent<ParticleSystem>(), shared.FlameParticleMat, maxParticles: 12, rate: 6f, startSize: 0.25f, speed: 1.05f);

        var lightGO = new GameObject("TorchLight", typeof(Light));
        lightGO.transform.SetParent(root.transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 2.25f, 0f);
        var pl = lightGO.GetComponent<Light>();
        pl.type = LightType.Point;
        pl.color = new Color(1.0f, 0.64f, 0.24f);
        pl.range = 6.0f;
        pl.intensity = lightIntensity;
        pl.shadows = LightShadows.None;

        var flicker = root.AddComponent<FlickeringFlameLight>();
        flicker.targetLight = pl;
        flicker.flameVisual = flame;
        flicker.baseIntensity = lightIntensity;
        flicker.intensityVariance = 0.35f;
        flicker.flickerSpeed = 7.2f;

        return root.transform;
    }

    static void AddSubtleTricolorAccent(Transform parent, Vector3 localPos, float radius, KurdistanEnvironmentAssets.SharedAssetSet shared)
    {
        Material[] mats = { shared.AccentRedMat, shared.AccentWhiteMat, shared.AccentGreenMat };
        for (int i = 0; i < 3; i++)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = $"AccentBand_{i}";
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = localPos + new Vector3(0f, (1 - i) * 0.08f, 0f);
            ring.transform.localScale = new Vector3(radius * 2f, 0.038f, radius * 2f);
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            var mr = ring.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mats[i];
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    static void ConfigureFireParticles(ParticleSystem ps, Material mat, int maxParticles, float rate, float startSize, float speed)
    {
        var main = ps.main;
        main.duration = 2.0f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.95f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.6f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(startSize * 0.6f, startSize);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1.0f, 0.85f, 0.3f, 0.9f), new Color(1.0f, 0.35f, 0.08f, 0.85f));
        main.maxParticles = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var emission = ps.emission;
        emission.rateOverTime = rate;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 14f;
        shape.radius = 0.22f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    static void PopulateCloudsAndEagles(
        Transform envRoot,
        Bounds courseBounds,
        float innerValleyRadius,
        BiomeProfile profile,
        KurdistanEnvironmentAssets.SharedAssetSet shared,
        AmbientEnvironmentMotion motion)
    {
        var skyGroup = new GameObject("SkyLife").transform;
        skyGroup.SetParent(envRoot, false);

        Vector2 centerXZ = new Vector2(courseBounds.center.x, courseBounds.center.z);
        float wrapExt = Mathf.Max(220f, innerValleyRadius + 150f);
        motion.cloudDriftVelocity = profile.CloudDrift;
        motion.wrapCenter = new Vector3(centerXZ.x, 0f, centerXZ.y);
        motion.wrapHalfExtents = new Vector2(wrapExt, wrapExt);

        var rng = new System.Random(5500 + profile.LevelNumber * 31);

        if (shared.CloudMesh != null)
        {
            float baseCloudY = courseBounds.max.y + 20f;
            for (int i = 0; i < profile.CloudCount; i++)
            {
                float x = centerXZ.x + Mathf.Lerp(-wrapExt * 0.85f, wrapExt * 0.85f, (float)rng.NextDouble());
                float z = centerXZ.y + Mathf.Lerp(-wrapExt * 0.85f, wrapExt * 0.85f, (float)rng.NextDouble());
                float y = baseCloudY + Mathf.Lerp(0f, 24f, (float)rng.NextDouble());
                float s = Mathf.Lerp(3.2f, 7.2f, (float)rng.NextDouble());

                var cloudGO = CreateStaticMeshObject(
                    $"Cloud_{i + 1}",
                    skyGroup,
                    new Vector3(x, y, z),
                    Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f),
                    new Vector3(s * 1.25f, s * 0.78f, s),
                    shared.CloudMesh,
                    shared.CloudMat);
                motion.clouds.Add(cloudGO.transform);
            }
        }

        if (shared.EagleMesh != null)
        {
            float baseEagleY = courseBounds.max.y + 24f;
            for (int i = 0; i < profile.EagleCount; i++)
            {
                float angle = ((float)i / Mathf.Max(1, profile.EagleCount)) * Mathf.PI * 2f;
                Vector3 orbitCenter = new Vector3(
                    centerXZ.x + Mathf.Cos(angle) * 35f,
                    baseEagleY + i * 3.5f,
                    centerXZ.y + Mathf.Sin(angle) * 35f);

                var eagleGO = CreateStaticMeshObject(
                    $"MountainEagle_{i + 1}",
                    skyGroup,
                    orbitCenter,
                    Quaternion.identity,
                    Vector3.one * Mathf.Lerp(1.1f, 1.45f, (float)rng.NextDouble()),
                    shared.EagleMesh,
                    shared.EagleMat);

                motion.eagles.Add(new AmbientEnvironmentMotion.EagleOrbit
                {
                    transform = eagleGO.transform,
                    center = orbitCenter,
                    radius = Mathf.Lerp(26f, 48f, (float)rng.NextDouble()),
                    angularSpeed = (i % 2 == 0 ? 1f : -1f) * Mathf.Lerp(0.18f, 0.28f, (float)rng.NextDouble()),
                    bobAmplitude = 1.4f,
                    phase = i * 1.7f
                });
            }
        }
    }

    // -------------------------------------------------------------------------
    // SAHNE VE MESH YARDIMCILARI
    // -------------------------------------------------------------------------

    static GameObject CreateStaticMeshObject(
        string name,
        Transform parent,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        Mesh mesh,
        params Material[] materials)
    {
        return CreateStaticMeshObject(name, parent, position, rotation, scale, mesh, useLocalSpace: false, materials);
    }

    static GameObject CreateStaticMeshObject(
        string name,
        Transform parent,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        Mesh mesh,
        bool useLocalSpace,
        params Material[] materials)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        if (useLocalSpace)
            go.transform.SetLocalPositionAndRotation(position, rotation);
        else
            go.transform.SetPositionAndRotation(position, rotation);
        go.transform.localScale = scale;

        var mf = go.GetComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterials = materials;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = true;
        return go;
    }

    static Mesh LoadFirstMeshFromPath(string assetPath)
    {
        var direct = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        if (direct != null) return direct;

        var all = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        foreach (var obj in all)
        {
            if (obj is Mesh m) return m;
        }
        return null;
    }

    static Transform FindTransformByName(Scene scene, string targetName)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == targetName) return t;
            }
        }
        return null;
    }

    static bool TryGetTransformBounds(Transform target, out Bounds bounds)
    {
        bounds = default;
        bool hasAny = false;
        foreach (var r in target.GetComponentsInChildren<Renderer>(true))
        {
            if (!hasAny) { bounds = r.bounds; hasAny = true; }
            else bounds.Encapsulate(r.bounds);
        }
        if (hasAny) return true;

        foreach (var c in target.GetComponentsInChildren<Collider>(true))
        {
            if (!hasAny) { bounds = c.bounds; hasAny = true; }
            else bounds.Encapsulate(c.bounds);
        }
        return hasAny;
    }
}
