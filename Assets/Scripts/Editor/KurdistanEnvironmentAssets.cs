using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Kürdistan doğa çevresi için kalıcı doku, URP materyal ve flat-shaded low-poly mesh varlıklarını
/// Assets/Environment/Generated/ altında üretir ve günceller.
/// </summary>
public static class KurdistanEnvironmentAssets
{
    public const string GeneratedRoot = "Assets/Environment/Generated";
    public const string TexturesDir = GeneratedRoot + "/Textures";
    public const string MaterialsDir = GeneratedRoot + "/Materials";
    public const string SkyboxesDir = MaterialsDir + "/Skyboxes";
    public const string MeshesDir = GeneratedRoot + "/Meshes";

    public const string BaseSkyboxPath = "Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Materials/Skybox Cubemap Extended Day.mat";

    public struct BiomeAssetSet
    {
        public Material FoothillMat;
        public Material CliffRockMat;
        public Material SnowCapMat;
        public Material SurfaceMat;
        public Material PineFoliageMat;
        public Material SkyboxMat;
        public Mesh MountainRingMesh;
        public Mesh ValleyRimMesh;
        public Mesh DrapedMountainFlagMesh;
        public Mesh PaintedMountainFlagMesh;
    }

    public struct SharedAssetSet
    {
        public Material FlagMat;
        public Material MountainPaintFlagMat;
        public Material FlameParticleMat;
        public Material StoneArchMat;
        public Material WoodAccentMat;
        public Material RockRootMat;
        public Material CloudMat;
        public Material EagleMat;
        public Material EmberCoreMat;
        public Material AccentRedMat;
        public Material AccentGoldMat;
        public Material AccentGreenMat;
        public Material AccentWhiteMat;
        public Material TreeTrunkMat;

        public Mesh RockPillarRootMesh;
        public Mesh CloudMesh;
        public Mesh EagleMesh;
        public Mesh StoneCastleMesh;
        public Mesh HighlandVillageMesh;
        public Mesh NewrozBrazierMesh;
        public Mesh StoneTorchPedestalMesh;
        public Mesh FlameCoreMesh;
    }

    public static void EnsureFolders()
    {
        CreateFolderIfMissing("Assets", "Environment");
        CreateFolderIfMissing("Assets/Environment", "Generated");
        CreateFolderIfMissing(GeneratedRoot, "Textures");
        CreateFolderIfMissing(GeneratedRoot, "Materials");
        CreateFolderIfMissing(MaterialsDir, "Skyboxes");
        CreateFolderIfMissing(GeneratedRoot, "Meshes");
    }

    static void CreateFolderIfMissing(string parent, string folderName)
    {
        string combined = parent + "/" + folderName;
        if (!AssetDatabase.IsValidFolder(combined))
            AssetDatabase.CreateFolder(parent, folderName);
    }

    public static SharedAssetSet EnsureSharedAssets()
    {
        EnsureFolders();

        Texture2D flagTex = EnsureKurdistanFlagTexture();
        Texture2D paintFlagTex = EnsureKurdistanMountainPaintTexture();
        Texture2D particleTex = EnsureSoftGlowParticleTexture();
        Texture2D waterTex = EnsureStylizedWaterTexture();
        _ = waterTex;

        var set = new SharedAssetSet
        {
            FlagMat = CreateOrUpdateFlagMaterial(flagTex),
            MountainPaintFlagMat = CreateOrUpdateMountainPaintFlagMaterial(paintFlagTex),
            FlameParticleMat = CreateOrUpdateFlameParticleMaterial(particleTex),
            StoneArchMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_StoneArchitecture.mat", new Color(0.57f, 0.53f, 0.47f), 0.12f, 0f),
            WoodAccentMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_WoodAccent.mat", new Color(0.36f, 0.24f, 0.14f), 0.15f, 0f),
            RockRootMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_RockRoot.mat", new Color(0.39f, 0.37f, 0.35f), 0.08f, 0f),
            CloudMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_CloudLowPoly.mat", new Color(0.97f, 0.98f, 1.0f), 0.04f, 0f),
            EagleMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_EagleDark.mat", new Color(0.20f, 0.15f, 0.12f), 0.1f, 0f),
            EmberCoreMat = CreateOrUpdateEmissiveMaterial($"{MaterialsDir}/Mat_EmberCore.mat", new Color(1.0f, 0.56f, 0.12f), new Color(2.6f, 1.15f, 0.22f)),
            AccentRedMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_AccentRed.mat", new Color(0.84f, 0.14f, 0.16f), 0.15f, 0f),
            AccentGoldMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_AccentGold.mat", new Color(0.95f, 0.75f, 0.12f), 0.25f, 0.15f),
            AccentGreenMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_AccentGreen.mat", new Color(0.14f, 0.55f, 0.24f), 0.15f, 0f),
            AccentWhiteMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_AccentWhite.mat", new Color(0.94f, 0.94f, 0.93f), 0.15f, 0f),
            TreeTrunkMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_TreeTrunk.mat", new Color(0.34f, 0.22f, 0.14f), 0.08f, 0f),

            RockPillarRootMesh = CreateOrUpdateMeshAsset($"{MeshesDir}/RockPillarRoot.asset", BuildRockPillarRootMesh()),
            CloudMesh = CreateOrUpdateMeshAsset($"{MeshesDir}/LowPolyCloud.asset", BuildCloudMesh()),
            EagleMesh = CreateOrUpdateMeshAsset($"{MeshesDir}/MountainEagle.asset", BuildEagleMesh()),
            StoneCastleMesh = CreateOrUpdateMeshAsset($"{MeshesDir}/StoneCastleSilhouette.asset", BuildStoneCastleMesh()),
            HighlandVillageMesh = CreateOrUpdateMeshAsset($"{MeshesDir}/HighlandVillageCluster.asset", BuildHighlandVillageMesh()),
            NewrozBrazierMesh = CreateOrUpdateMeshAsset($"{MeshesDir}/NewrozBrazier.asset", BuildNewrozBrazierMesh()),
            StoneTorchPedestalMesh = CreateOrUpdateMeshAsset($"{MeshesDir}/StoneTorchPedestal.asset", BuildStoneTorchPedestalMesh()),
            FlameCoreMesh = CreateOrUpdateMeshAsset($"{MeshesDir}/FlameCore.asset", BuildFlameCoreMesh())
        };

        AssetDatabase.SaveAssets();
        return set;
    }

    public static BiomeAssetSet EnsureBiomeAssets(
        int levelNumber,
        Vector2 courseCenterXZ,
        float innerValleyRadius,
        float drapedFlagAngleDeg = 84f,
        float paintedFlagAngleDeg = 134f,
        float drapedFlagWidthMeters = 78f,
        float paintedFlagWidthMeters = 58f)
    {
        EnsureFolders();
        Texture2D waterTex = EnsureStylizedWaterTexture();

        Color foothill;
        Color cliff;
        Color snow;
        Color surfaceColor;
        float surfaceSmoothness;
        bool surfaceTransparent;
        Vector2 surfaceTiling;
        Color pineColor;
        Color skyTint;
        float skyExposure;
        float valleyMinHeight;
        float valleyMaxHeight;
        float mountainMinHeight;
        float mountainMaxHeight;
        float snowThreshold;
        int seed;

        switch (levelNumber)
        {
            case 2:
                // Level 2: Kanyon & Nehir Vadisi (Öğle)
                foothill = new Color(0.34f, 0.52f, 0.28f);
                cliff = new Color(0.56f, 0.42f, 0.34f);
                snow = new Color(0.88f, 0.90f, 0.94f);
                surfaceColor = new Color(0.11f, 0.54f, 0.68f, 1f);
                surfaceSmoothness = 0.78f;
                surfaceTransparent = false;
                surfaceTiling = new Vector2(22f, 22f);
                pineColor = new Color(0.18f, 0.43f, 0.22f);
                skyTint = new Color(0.52f, 0.54f, 0.56f, 1f);
                skyExposure = 1.05f;
                valleyMinHeight = 14f;
                valleyMaxHeight = 46f;
                mountainMinHeight = 52f;
                mountainMaxHeight = 132f;
                snowThreshold = 0.78f;
                seed = 2027;
                break;

            case 3:
                // Level 3: Yüksek Dağ Geçidi (Altın Saat / Gün Batımı)
                foothill = new Color(0.32f, 0.44f, 0.24f);
                cliff = new Color(0.45f, 0.36f, 0.34f);
                snow = new Color(0.98f, 0.89f, 0.82f);
                surfaceColor = new Color(0.95f, 0.68f, 0.42f, 0.68f);
                surfaceSmoothness = 0.15f;
                surfaceTransparent = true;
                surfaceTiling = new Vector2(10f, 10f);
                pineColor = new Color(0.16f, 0.34f, 0.19f);
                skyTint = new Color(0.66f, 0.47f, 0.36f, 1f);
                skyExposure = 0.95f;
                valleyMinHeight = 16f;
                valleyMaxHeight = 52f;
                mountainMinHeight = 62f;
                mountainMaxHeight = 152f;
                snowThreshold = 0.64f;
                seed = 3039;
                break;

            case 4:
                // Level 4: Karlı Zirveler (Bulutların Üstü)
                foothill = new Color(0.38f, 0.47f, 0.35f);
                cliff = new Color(0.42f, 0.44f, 0.49f);
                snow = new Color(0.95f, 0.97f, 1.0f);
                surfaceColor = new Color(0.93f, 0.96f, 1.0f, 1f);
                surfaceSmoothness = 0.10f;
                surfaceTransparent = false;
                surfaceTiling = new Vector2(12f, 12f);
                pineColor = new Color(0.24f, 0.42f, 0.33f);
                skyTint = new Color(0.48f, 0.54f, 0.62f, 1f);
                skyExposure = 1.10f;
                valleyMinHeight = 18f;
                valleyMaxHeight = 56f;
                mountainMinHeight = 70f;
                mountainMaxHeight = 168f;
                snowThreshold = 0.46f;
                seed = 4049;
                break;

            default:
                // Level 1: Bahar Yaylası (Sabah)
                foothill = new Color(0.29f, 0.60f, 0.25f);
                cliff = new Color(0.49f, 0.45f, 0.40f);
                snow = new Color(0.93f, 0.96f, 0.99f);
                surfaceColor = new Color(0.12f, 0.59f, 0.64f, 1f);
                surfaceSmoothness = 0.74f;
                surfaceTransparent = false;
                surfaceTiling = new Vector2(18f, 18f);
                pineColor = new Color(0.18f, 0.49f, 0.22f);
                skyTint = new Color(0.50f, 0.54f, 0.58f, 1f);
                skyExposure = 1.05f;
                valleyMinHeight = 10f;
                valleyMaxHeight = 36f;
                mountainMinHeight = 46f;
                mountainMaxHeight = 122f;
                snowThreshold = 0.72f;
                seed = 1013;
                break;
        }

        string prefix = $"{MaterialsDir}/Biome_L{levelNumber}";
        Material surfaceMat = CreateOrUpdateSurfaceMaterial(
            $"{prefix}_Surface.mat",
            surfaceColor,
            surfaceSmoothness,
            surfaceTransparent,
            waterTex,
            surfaceTiling);

        float safeInner = Mathf.Max(42f, innerValleyRadius);
        Mesh valleyMesh = BuildMountainRingMesh(
            $"ValleyRim_L{levelNumber}",
            seed + 17,
            safeInner,
            safeInner + 115f,
            radialSteps: 5,
            angularSegments: 44,
            minPeakHeight: valleyMinHeight,
            maxPeakHeight: valleyMaxHeight,
            snowThreshold: Mathf.Min(0.92f, snowThreshold + 0.18f),
            cliffSlopeThreshold: 0.76f,
            baseElevation: -18f,
            highlightAngleDeg: paintedFlagAngleDeg,
            out Vector3[,] valleyGrid);

        Mesh mountainMesh = BuildMountainRingMesh(
            $"MountainRing_L{levelNumber}",
            seed,
            safeInner + 95f,
            safeInner + 335f,
            radialSteps: 6,
            angularSegments: 56,
            minPeakHeight: mountainMinHeight,
            maxPeakHeight: mountainMaxHeight,
            snowThreshold: snowThreshold,
            cliffSlopeThreshold: 0.78f,
            baseElevation: -20f,
            highlightAngleDeg: drapedFlagAngleDeg,
            out Vector3[,] mountainGrid);

        Mesh drapedFlagMesh = BuildMountainFlagPatchMesh(
            $"MountainFlagDrape_L{levelNumber}",
            mountainGrid,
            drapedFlagAngleDeg,
            drapedFlagWidthMeters,
            rtMin: 0.14f,
            rtMax: 0.47f,
            segU: 28,
            segV: 18,
            normalOffset: 1.35f,
            flatShaded: false);

        Mesh paintedFlagMesh = BuildMountainFlagPatchMesh(
            $"MountainFlagPaint_L{levelNumber}",
            valleyGrid,
            paintedFlagAngleDeg,
            paintedFlagWidthMeters,
            rtMin: 0.13f,
            rtMax: 0.46f,
            segU: 24,
            segV: 16,
            normalOffset: 0.55f,
            flatShaded: true);

        _ = courseCenterXZ;

        var result = new BiomeAssetSet
        {
            FoothillMat = CreateOrUpdateLitMaterial($"{prefix}_Foothill.mat", foothill, 0.06f, 0f),
            CliffRockMat = CreateOrUpdateLitMaterial($"{prefix}_CliffRock.mat", cliff, 0.10f, 0f),
            SnowCapMat = CreateOrUpdateLitMaterial($"{prefix}_SnowCap.mat", snow, 0.18f, 0f),
            SurfaceMat = surfaceMat,
            PineFoliageMat = CreateOrUpdateLitMaterial($"{prefix}_Pine.mat", pineColor, 0.08f, 0f),
            SkyboxMat = CreateOrUpdateSkyboxMaterial($"{SkyboxesDir}/Skybox_L{levelNumber}.mat", skyTint, skyExposure),
            ValleyRimMesh = CreateOrUpdateMeshAsset($"{MeshesDir}/ValleyRim_L{levelNumber}.asset", valleyMesh),
            MountainRingMesh = CreateOrUpdateMeshAsset($"{MeshesDir}/MountainRing_L{levelNumber}.asset", mountainMesh),
            DrapedMountainFlagMesh = CreateOrUpdateMeshAsset($"{MeshesDir}/MountainFlagDrape_L{levelNumber}.asset", drapedFlagMesh),
            PaintedMountainFlagMesh = CreateOrUpdateMeshAsset($"{MeshesDir}/MountainFlagPaint_L{levelNumber}.asset", paintedFlagMesh)
        };

        AssetDatabase.SaveAssets();
        return result;
    }

    // -------------------------------------------------------------------------
    // DOKU ÜRETİCİLERİ
    // -------------------------------------------------------------------------

    static Color EvaluateKurdistanFlagColor(int x, int y, int w, int h)
    {
        Color red = new Color(0.929f, 0.161f, 0.224f, 1f);
        Color white = Color.white;
        Color green = new Color(0.153f, 0.557f, 0.263f, 1f);
        Color gold = new Color(0.996f, 0.741f, 0.067f, 1f);

        float cx = (w - 1) * 0.5f;
        float cy = (h - 1) * 0.5f;
        float innerR = h * 0.155f;
        float outerR = h * 0.345f;
        const int rayCount = 21;
        float sector = (Mathf.PI * 2f) / rayCount;

        // Texture2D y=0 alttır -> alt yeşil, orta beyaz, üst kırmızı
        float ny = (float)y / h;
        Color band = ny < (1f / 3f) ? green : (ny < (2f / 3f) ? white : red);

        float dx = x - cx;
        float dy = y - cy;
        float r = Mathf.Sqrt(dx * dx + dy * dy);
        Color col = band;

        if (r <= outerR + 1.5f)
        {
            float sunAlpha;
            if (r <= innerR)
            {
                sunAlpha = 1f;
            }
            else
            {
                float angle = Mathf.Atan2(dy, dx);
                if (angle < 0f) angle += Mathf.PI * 2f;
                // En yukarıdan başlat
                float shifted = Mathf.Repeat(angle - Mathf.PI * 0.5f, sector);
                float distFromRayCenter = Mathf.Abs(shifted - sector * 0.5f);
                float t = Mathf.Clamp01((r - innerR) / (outerR - innerR));
                float allowedHalfAngle = (sector * 0.43f) * (1f - t);
                float edgeDistPx = (allowedHalfAngle - distFromRayCenter) * r;
                float tipEdgePx = outerR - r;
                float signedEdge = Mathf.Min(edgeDistPx, tipEdgePx);
                sunAlpha = Mathf.Clamp01(signedEdge * 0.85f + 0.5f);
            }

            if (sunAlpha > 0.001f)
                col = Color.Lerp(band, gold, sunAlpha);
        }

        return col;
    }

    static Texture2D EnsureKurdistanFlagTexture()
    {
        string path = $"{TexturesDir}/KurdistanFlag.png";
        const int w = 512;
        const int h = 340;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                pixels[y * w + x] = EvaluateKurdistanFlagColor(x, y, w, h);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Texture2D EnsureKurdistanMountainPaintTexture()
    {
        string path = $"{TexturesDir}/KurdistanMountainPaint.png";
        const int w = 512;
        const int h = 340;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            float v = (float)y / (h - 1);
            for (int x = 0; x < w; x++)
            {
                float u = (float)x / (w - 1);
                Color col = EvaluateKurdistanFlagColor(x, y, w, h);

                // Doğal kaya mineral dokusu ve hafif matlaştırma (neon durmaması için)
                float rockGrain1 = Mathf.PerlinNoise(u * 16f + 4.2f, v * 11f + 9.7f);
                float rockGrain2 = Mathf.PerlinNoise(u * 42f + 13.5f, v * 28f + 2.1f);
                float mineralShade = Mathf.Lerp(0.84f, 1.04f, rockGrain1 * 0.65f + rockGrain2 * 0.35f);

                float lum = col.r * 0.299f + col.g * 0.587f + col.b * 0.114f;
                col = Color.Lerp(col, new Color(lum, lum, lum, 1f), 0.14f);
                col.r = Mathf.Clamp01(col.r * mineralShade);
                col.g = Mathf.Clamp01(col.g * mineralShade);
                col.b = Mathf.Clamp01(col.b * mineralShade);

                // Kaya üzerine boyanmış yıpranmış kenar maskesi
                float edgeX = Mathf.Min(u, 1f - u) * 1.35f;
                float edgeY = Mathf.Min(v, 1f - v);
                float edgeDist = Mathf.Min(edgeX, edgeY);
                float borderNoise = (Mathf.PerlinNoise(u * 14f + 3.1f, v * 9f + 7.4f) - 0.5f) * 0.09f;
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.015f, 0.065f, edgeDist + borderNoise));

                // Kenarlara yakın küçük taş aşınma noktaları
                if (edgeDist < 0.085f && rockGrain2 < 0.24f)
                    alpha *= 0.2f;

                col.a = alpha;
                pixels[y * w + x] = col;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Texture2D EnsureSoftGlowParticleTexture()
    {
        string path = $"{TexturesDir}/SoftGlowParticle.png";

        const int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        float c = (s - 1) * 0.5f;
        var pixels = new Color[s * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                // Premultiplied alpha: kenarlar (0,0,0,0) olur, kare izi kalmaz
                pixels[y * s + x] = new Color(a, a, a, a);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Texture2D EnsureStylizedWaterTexture()
    {
        string path = $"{TexturesDir}/StylizedWaterPattern.png";
        if (File.Exists(path))
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        const int s = 256;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var pixels = new Color[s * s];
        for (int y = 0; y < s; y++)
        {
            float v = (float)y / s;
            for (int x = 0; x < s; x++)
            {
                float u = (float)x / s;
                // Kesintisiz (seamless) periyodik dalga deseni
                float w1 = Mathf.Sin((u * 3f + v * 2f) * Mathf.PI * 2f);
                float w2 = Mathf.Cos((u * 5f - v * 4f) * Mathf.PI * 2f);
                float w3 = Mathf.Sin((u * 8f + v * 7f) * Mathf.PI * 2f) * 0.4f;
                float foam = Mathf.SmoothStep(0.55f, 0.95f, (w1 * 0.5f + w2 * 0.35f + w3 * 0.15f) * 0.5f + 0.5f);
                float shade = Mathf.Lerp(0.84f, 1.0f, foam);
                pixels[y * s + x] = new Color(shade, Mathf.Lerp(0.90f, 1.0f, foam), 1f, 1f);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // -------------------------------------------------------------------------
    // MATERYAL ÜRETİCİLERİ
    // -------------------------------------------------------------------------

    static Shader GetUrpLitShader()
    {
        return Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Universal Render Pipeline/Simple Lit")
            ?? Shader.Find("Standard");
    }

    static Material CreateOrUpdateMaterialAsset(string path, Shader shader)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }
        return mat;
    }

    static Material CreateOrUpdateLitMaterial(string path, Color color, float smoothness, float metallic)
    {
        var mat = CreateOrUpdateMaterialAsset(path, GetUrpLitShader());
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material CreateOrUpdateEmissiveMaterial(string path, Color baseColor, Color emissionColor)
    {
        var mat = CreateOrUpdateLitMaterial(path, baseColor, 0.2f, 0f);
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.SetColor("_EmissionColor", emissionColor);
            mat.EnableKeyword("_EMISSION");
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material CreateOrUpdateFlagMaterial(Texture2D flagTex)
    {
        var mat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_KurdistanFlag.mat", Color.white, 0.14f, 0f);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", flagTex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", flagTex);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f); // Çift taraflı görünüm
        mat.doubleSidedGI = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material CreateOrUpdateMountainPaintFlagMaterial(Texture2D paintTex)
    {
        var mat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_KurdistanMountainPaint.mat", new Color(0.96f, 0.95f, 0.93f, 1f), 0.04f, 0f);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", paintTex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", paintTex);
        if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
        if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.42f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 2f); // İç yamaçtan bakan ön yüz
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 1f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material CreateOrUpdateFlameParticleMaterial(Texture2D particleTex)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Particles/Standard Unlit");

        var mat = CreateOrUpdateMaterialAsset($"{MaterialsDir}/Mat_FlameParticle.mat", shader);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", particleTex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", particleTex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 1f); // Additive
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material CreateOrUpdateSurfaceMaterial(
        string path,
        Color color,
        float smoothness,
        bool transparent,
        Texture2D patternTex,
        Vector2 tiling)
    {
        var mat = CreateOrUpdateLitMaterial(path, color, smoothness, 0f);
        if (mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", patternTex);
            mat.SetTextureScale("_BaseMap", tiling);
        }
        if (mat.HasProperty("_MainTex"))
        {
            mat.SetTexture("_MainTex", patternTex);
            mat.SetTextureScale("_MainTex", tiling);
        }

        if (transparent)
        {
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else
        {
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 1f);
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material CreateOrUpdateSkyboxMaterial(string path, Color tint, float exposure)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var baseSky = AssetDatabase.LoadAssetAtPath<Material>(BaseSkyboxPath);
        if (mat == null)
        {
            if (baseSky != null)
            {
                mat = new Material(baseSky);
            }
            else
            {
                mat = new Material(Shader.Find("Skybox/Procedural") ?? GetUrpLitShader());
            }
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (baseSky != null && mat.shader != baseSky.shader)
        {
            mat.CopyPropertiesFromMaterial(baseSky);
        }

        if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", tint);
        if (mat.HasProperty("_SkyTint")) mat.SetColor("_SkyTint", tint);
        if (mat.HasProperty("_Exposure")) mat.SetFloat("_Exposure", exposure);
        if (mat.HasProperty("_FogIntensity")) mat.SetFloat("_FogIntensity", 1f);
        if (mat.HasProperty("_FogHeight")) mat.SetFloat("_FogHeight", 0.72f);
        if (mat.HasProperty("_FogSmoothness")) mat.SetFloat("_FogSmoothness", 0.04f);
        if (mat.HasProperty("_RotationSpeed")) mat.SetFloat("_RotationSpeed", 0.65f);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    // -------------------------------------------------------------------------
    // PROSEDÜREL FLAT-SHADED MESH ÜRETİCİLERİ
    // -------------------------------------------------------------------------

    static Mesh CreateOrUpdateMeshAsset(string path, Mesh generated)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(generated, path);
            return generated;
        }

        EditorUtility.CopySerialized(generated, existing);
        Object.DestroyImmediate(generated);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    static Mesh BuildMountainRingMesh(
        string meshName,
        int seed,
        float innerRadius,
        float outerRadius,
        int radialSteps,
        int angularSegments,
        float minPeakHeight,
        float maxPeakHeight,
        float snowThreshold,
        float cliffSlopeThreshold,
        float baseElevation,
        float highlightAngleDeg,
        out Vector3[,] grid)
    {
        int rings = radialSteps + 1;
        grid = new Vector3[rings, angularSegments];
        float[,] normHeight = new float[rings, angularSegments];

        float offX = (seed * 17.31f) % 100f;
        float offZ = (seed * 31.73f) % 100f;

        for (int r = 0; r < rings; r++)
        {
            float rt = (float)r / radialSteps;
            float baseR = Mathf.Lerp(innerRadius, outerRadius, rt);

            // İç ve dış kenarda yere/vadiye inen, ortada yükselen profil
            float ridgeProfile = Mathf.Sin(rt * Mathf.PI);
            ridgeProfile = Mathf.Pow(ridgeProfile, 0.82f);

            for (int a = 0; a < angularSegments; a++)
            {
                float angle = ((float)a / angularSegments) * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                float n1 = Mathf.PerlinNoise(cos * 1.8f + offX, sin * 1.8f + offZ);
                float n2 = Mathf.PerlinNoise(cos * 4.2f + offX * 2f, sin * 4.2f + offZ * 2f) * 0.45f;
                float n3 = Mathf.PerlinNoise(cos * 8.5f + rt * 2.1f + offX, sin * 8.5f + offZ) * 0.2f;
                float noiseVal = Mathf.Clamp01((n1 + n2 + n3) / 1.45f);

                // Dağ silsilesine birkaç dramatik zirve ekle
                float ridgeWave = 0.72f + 0.28f * Mathf.Sin(angle * 3f + seed * 0.5f) * Mathf.Cos(angle * 2f - 0.7f);

                // Bayrak yerleştirilen yamacın daima görkemli ve geniş sırtlı olması için yumuşak sırt desteği
                if (!float.IsNaN(highlightAngleDeg))
                {
                    float dAngle = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, highlightAngleDeg));
                    float ridgeBoost = Mathf.Exp(-(dAngle * dAngle) / (2f * 20f * 20f));
                    noiseVal = Mathf.Lerp(noiseVal, Mathf.Max(noiseVal, 0.86f), ridgeBoost);
                    ridgeWave = Mathf.Lerp(ridgeWave, 1.0f, ridgeBoost);
                }

                float radialJitter = (r > 0 && r < rings - 1) ? (n2 - 0.22f) * 14f : 0f;
                float rad = baseR + radialJitter;

                float peakH = Mathf.Lerp(minPeakHeight, maxPeakHeight, noiseVal * ridgeWave);
                float h = baseElevation + peakH * ridgeProfile;
                if (r == 0 || r == rings - 1)
                    h = baseElevation - 2f;

                grid[r, a] = new Vector3(cos * rad, h, sin * rad);
                normHeight[r, a] = Mathf.Clamp01((h - baseElevation) / Mathf.Max(1f, maxPeakHeight));
            }
        }

        var verts = new List<Vector3>(angularSegments * radialSteps * 6);
        var normals = new List<Vector3>(angularSegments * radialSteps * 6);
        var uvs = new List<Vector2>(angularSegments * radialSteps * 6);
        var sub0 = new List<int>(); // Foothill
        var sub1 = new List<int>(); // CliffRock
        var sub2 = new List<int>(); // SnowCap

        void AddTri(Vector3 p0, Vector3 p1, Vector3 p2, float hAvg)
        {
            Vector3 n = Vector3.Cross(p1 - p0, p2 - p0).normalized;
            if (n.sqrMagnitude < 0.0001f) n = Vector3.up;

            int baseIdx = verts.Count;
            verts.Add(p0); verts.Add(p1); verts.Add(p2);
            normals.Add(n); normals.Add(n); normals.Add(n);
            uvs.Add(Vector2.zero); uvs.Add(Vector2.right); uvs.Add(Vector2.up);

            List<int> target;
            if (hAvg >= snowThreshold && n.y >= 0.42f)
                target = sub2;
            else if (n.y < cliffSlopeThreshold || hAvg >= snowThreshold * 0.72f)
                target = sub1;
            else
                target = sub0;

            target.Add(baseIdx);
            target.Add(baseIdx + 1);
            target.Add(baseIdx + 2);
        }

        for (int r = 0; r < radialSteps; r++)
        {
            for (int a = 0; a < angularSegments; a++)
            {
                int aNext = (a + 1) % angularSegments;
                Vector3 p00 = grid[r, a];
                Vector3 p10 = grid[r + 1, a];
                Vector3 p01 = grid[r, aNext];
                Vector3 p11 = grid[r + 1, aNext];

                float hA = (normHeight[r, a] + normHeight[r + 1, a] + normHeight[r + 1, aNext]) / 3f;
                float hB = (normHeight[r, a] + normHeight[r + 1, aNext] + normHeight[r, aNext]) / 3f;

                // Dıştan/üstten görünür winding
                AddTri(p00, p01, p11, hB);
                AddTri(p00, p11, p10, hA);
            }
        }

        // Eğer bir submesh boşsa güvenli 0-alanlı sahte üçgen koyma; boş bırakmak yerine submesh sayısını koru
        var mesh = new Mesh { name = meshName };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 3;
        mesh.SetTriangles(sub0, 0);
        mesh.SetTriangles(sub1, 1);
        mesh.SetTriangles(sub2, 2);
        mesh.RecalculateBounds();
        return mesh;
    }

    static void SampleGridSurface(
        Vector3[,] grid,
        float rt,
        float angleRad,
        out Vector3 position,
        out Vector3 inwardNormal)
    {
        int rings = grid.GetLength(0);
        int radialSteps = rings - 1;
        int angularSegments = grid.GetLength(1);

        float clampedRt = Mathf.Clamp01(rt);
        float rFloat = clampedRt * radialSteps;
        int r = Mathf.Min(radialSteps - 1, Mathf.FloorToInt(rFloat));
        float fr = Mathf.Clamp01(rFloat - r);

        float normAngle = Mathf.Repeat(angleRad, Mathf.PI * 2f);
        float aFloat = (normAngle / (Mathf.PI * 2f)) * angularSegments;
        int aFloor = Mathf.FloorToInt(aFloat);
        int a = ((aFloor % angularSegments) + angularSegments) % angularSegments;
        float fa = Mathf.Clamp01(aFloat - aFloor);
        int aNext = (a + 1) % angularSegments;

        Vector3 p00 = grid[r, a];
        Vector3 p10 = grid[r + 1, a];
        Vector3 p01 = grid[r, aNext];
        Vector3 p11 = grid[r + 1, aNext];

        Vector3 n;
        if (fa >= fr)
        {
            position = p00 * (1f - fa) + p01 * (fa - fr) + p11 * fr;
            n = Vector3.Cross(p01 - p00, p11 - p00);
        }
        else
        {
            position = p00 * (1f - fr) + p11 * fa + p10 * (fr - fa);
            n = Vector3.Cross(p11 - p00, p10 - p00);
        }

        if (n.sqrMagnitude < 0.0001f)
            n = new Vector3(-position.x, 1f, -position.z);
        n.Normalize();

        Vector3 toCenter = new Vector3(-position.x, 0f, -position.z);
        if (Vector3.Dot(n, toCenter) < 0f)
            n = -n;

        inwardNormal = n;
    }

    static Mesh BuildMountainFlagPatchMesh(
        string meshName,
        Vector3[,] grid,
        float centerAngleDeg,
        float targetWidthMeters,
        float rtMin,
        float rtMax,
        int segU,
        int segV,
        float normalOffset,
        bool flatShaded)
    {
        float midRt = (rtMin + rtMax) * 0.5f;
        float centerAngleRad = centerAngleDeg * Mathf.Deg2Rad;
        SampleGridSurface(grid, midRt, centerAngleRad, out Vector3 midSample, out _);
        float midRadius = new Vector2(midSample.x, midSample.z).magnitude;
        float arcRad = Mathf.Clamp(targetWidthMeters / Mathf.Max(20f, midRadius), 0.18f, 0.72f);

        int cols = segU + 1;
        int rows = segV + 1;
        Vector3[,] pts = new Vector3[rows, cols];
        Vector3[,] norms = new Vector3[rows, cols];
        Vector2[,] uvGrid = new Vector2[rows, cols];

        for (int iv = 0; iv < rows; iv++)
        {
            float v = (float)iv / segV;
            float rt = Mathf.Lerp(rtMin, rtMax, v);
            for (int iu = 0; iu < cols; iu++)
            {
                float u = (float)iu / segU;
                // Merkezden bakıldığında sol->sağ (u=0..1) için açı azalır
                float angle = centerAngleRad + (0.5f - u) * arcRad;
                SampleGridSurface(grid, rt, angle, out Vector3 surfPos, out Vector3 inwardNormal);
                pts[iv, iu] = surfPos + inwardNormal * normalOffset;
                norms[iv, iu] = inwardNormal;
                uvGrid[iv, iu] = new Vector2(u, v);
            }
        }

        var mesh = new Mesh { name = meshName };

        if (!flatShaded)
        {
            int vertCount = cols * rows;
            var verts = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var tris = new int[segU * segV * 6];

            for (int iv = 0; iv < rows; iv++)
            {
                for (int iu = 0; iu < cols; iu++)
                {
                    int idx = iv * cols + iu;
                    verts[idx] = pts[iv, iu];
                    normals[idx] = norms[iv, iu];
                    uvs[idx] = uvGrid[iv, iu];
                }
            }

            int t = 0;
            for (int iv = 0; iv < segV; iv++)
            {
                for (int iu = 0; iu < segU; iu++)
                {
                    int i00 = iv * cols + iu;
                    int i10 = i00 + 1;
                    int i01 = i00 + cols;
                    int i11 = i01 + 1;

                    Vector3 expectedN = norms[iv, iu];
                    if (Vector3.Dot(Vector3.Cross(verts[i01] - verts[i00], verts[i11] - verts[i00]), expectedN) >= 0f)
                    {
                        tris[t++] = i00; tris[t++] = i01; tris[t++] = i11;
                        tris[t++] = i00; tris[t++] = i11; tris[t++] = i10;
                    }
                    else
                    {
                        tris[t++] = i00; tris[t++] = i11; tris[t++] = i01;
                        tris[t++] = i00; tris[t++] = i10; tris[t++] = i11;
                    }
                }
            }

            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
        else
        {
            var verts = new List<Vector3>(segU * segV * 6);
            var normals = new List<Vector3>(segU * segV * 6);
            var uvs = new List<Vector2>(segU * segV * 6);
            var tris = new List<int>(segU * segV * 6);

            void PushTri(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Vector3 expectedN)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), expectedN) < 0f)
                {
                    (b, c) = (c, b);
                    (ub, uc) = (uc, ub);
                }

                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                if (n.sqrMagnitude < 0.0001f) n = expectedN;
                int idx = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                normals.Add(n); normals.Add(n); normals.Add(n);
                uvs.Add(ua); uvs.Add(ub); uvs.Add(uc);
                tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
            }

            for (int iv = 0; iv < segV; iv++)
            {
                for (int iu = 0; iu < segU; iu++)
                {
                    Vector3 expN = norms[iv, iu];
                    PushTri(pts[iv, iu], pts[iv + 1, iu], pts[iv + 1, iu + 1],
                        uvGrid[iv, iu], uvGrid[iv + 1, iu], uvGrid[iv + 1, iu + 1], expN);
                    PushTri(pts[iv, iu], pts[iv + 1, iu + 1], pts[iv, iu + 1],
                        uvGrid[iv, iu], uvGrid[iv + 1, iu + 1], uvGrid[iv, iu + 1], expN);
                }
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    static Mesh BuildRockPillarRootMesh()
    {
        var builder = new FlatMeshBuilder("RockPillarRoot");
        // yBottom < yTop sırasıyla eklenmeli ki üçgen normalleri dışa baksın; üst yarıçap ~1.0 (Y = 0), aşağı daralır (Y = -1)
        builder.AddTaperedPrism(Vector3.zero, 6, 0.16f, 0.48f, 0.25f, -1.00f, -0.75f, 0.05f, 37);
        builder.AddTaperedPrism(Vector3.zero, 7, 0.48f, 0.78f, 0.65f, -0.75f, -0.35f, 0.09f, 23);
        builder.AddTaperedPrism(Vector3.zero, 7, 0.78f, 1.02f, 0.35f, -0.35f, 0.00f, 0.08f, 11);
        return builder.ToMesh();
    }

    static Mesh BuildCloudMesh()
    {
        var builder = new FlatMeshBuilder("LowPolyCloud");
        builder.AddIcoBox(new Vector3(0f, 0.2f, 0f), new Vector3(3.4f, 1.4f, 2.2f));
        builder.AddIcoBox(new Vector3(-1.9f, -0.1f, 0.2f), new Vector3(2.2f, 1.0f, 1.8f));
        builder.AddIcoBox(new Vector3(1.9f, 0.0f, -0.15f), new Vector3(2.4f, 1.1f, 1.9f));
        builder.AddIcoBox(new Vector3(0.4f, 0.65f, 0.1f), new Vector3(2.1f, 1.05f, 1.6f));
        return builder.ToMesh();
    }

    static Mesh BuildEagleMesh()
    {
        var b = new FlatMeshBuilder("MountainEagle");
        // Gövde
        Vector3 nose = new Vector3(0f, 0.04f, 0.75f);
        Vector3 tailL = new Vector3(-0.22f, 0f, -0.75f);
        Vector3 tailR = new Vector3(0.22f, 0f, -0.75f);
        Vector3 back = new Vector3(0f, 0.12f, -0.15f);
        Vector3 belly = new Vector3(0f, -0.10f, 0.0f);

        b.AddTri(nose, tailR, back);
        b.AddTri(nose, back, tailL);
        b.AddTri(nose, belly, tailR);
        b.AddTri(nose, tailL, belly);

        // Sol ve sağ açık kanatlar (hafif V-açılı)
        Vector3 wLRootF = new Vector3(-0.15f, 0.05f, 0.25f);
        Vector3 wLRootB = new Vector3(-0.15f, 0.03f, -0.25f);
        Vector3 wLMid = new Vector3(-0.95f, 0.22f, 0.12f);
        Vector3 wLTip = new Vector3(-1.75f, 0.16f, -0.22f);
        b.AddTri(wLRootF, wLMid, wLRootB);
        b.AddTri(wLMid, wLTip, wLRootB);
        b.AddTri(wLRootF, wLRootB, wLMid);
        b.AddTri(wLMid, wLRootB, wLTip);

        Vector3 wRRootF = new Vector3(0.15f, 0.05f, 0.25f);
        Vector3 wRRootB = new Vector3(0.15f, 0.03f, -0.25f);
        Vector3 wRMid = new Vector3(0.95f, 0.22f, 0.12f);
        Vector3 wRTip = new Vector3(1.75f, 0.16f, -0.22f);
        b.AddTri(wRRootF, wRRootB, wRMid);
        b.AddTri(wRMid, wRRootB, wRTip);
        b.AddTri(wRRootF, wRMid, wRRootB);
        b.AddTri(wRMid, wRTip, wRRootB);

        return b.ToMesh();
    }

    static Mesh BuildStoneCastleMesh()
    {
        var b = new FlatMeshBuilder("StoneCastleSilhouette");
        // Ana sur tabanı + avlu duvarı
        b.AddBox(new Vector3(0f, 2.2f, 0f), new Vector3(14f, 4.4f, 10f));
        // Köşe burçları
        b.AddPrism(new Vector3(-6.4f, 0f, -4.4f), 8, 2.1f, 2.0f, 8.2f);
        b.AddPrism(new Vector3(6.4f, 0f, -4.4f), 8, 2.1f, 2.0f, 8.2f);
        b.AddPrism(new Vector3(-6.4f, 0f, 4.4f), 8, 2.1f, 2.0f, 7.6f);
        b.AddPrism(new Vector3(6.4f, 0f, 4.4f), 8, 2.1f, 2.0f, 7.6f);
        // Merkez yüksek gözetleme kulesi (Erzincan/Amed/Erbil iç kale hissi)
        b.AddBox(new Vector3(0f, 5.5f, 0f), new Vector3(5.6f, 6.2f, 5.2f));
        b.AddBox(new Vector3(0f, 9.0f, 0f), new Vector3(6.2f, 1.0f, 5.8f));
        // Mazgal dişleri
        for (int i = -2; i <= 2; i += 2)
        {
            b.AddBox(new Vector3(i * 1.1f, 9.85f, 2.4f), new Vector3(0.75f, 0.9f, 0.75f));
            b.AddBox(new Vector3(i * 1.1f, 9.85f, -2.4f), new Vector3(0.75f, 0.9f, 0.75f));
        }
        return b.ToMesh();
    }

    static Mesh BuildHighlandVillageMesh()
    {
        var b = new FlatMeshBuilder("HighlandVillageCluster");
        // Teraslı yayla taş evleri (düz damlı yöresel siluet)
        b.AddBox(new Vector3(0f, 1.2f, 0f), new Vector3(4.2f, 2.4f, 3.4f));
        b.AddBox(new Vector3(0f, 2.55f, 0f), new Vector3(4.5f, 0.3f, 3.7f));

        b.AddBox(new Vector3(-4.8f, 1.0f, 1.6f), new Vector3(3.4f, 2.0f, 3.0f));
        b.AddBox(new Vector3(-4.8f, 2.15f, 1.6f), new Vector3(3.7f, 0.3f, 3.3f));

        b.AddBox(new Vector3(4.6f, 1.4f, -1.2f), new Vector3(3.6f, 2.8f, 3.2f));
        b.AddBox(new Vector3(4.6f, 2.95f, -1.2f), new Vector3(3.9f, 0.3f, 3.5f));

        b.AddBox(new Vector3(1.2f, 0.8f, -4.4f), new Vector3(5.2f, 1.6f, 2.6f));
        // Taş teras istinat duvarı
        b.AddBox(new Vector3(0f, -0.35f, 0f), new Vector3(14.5f, 1.1f, 11.5f));
        return b.ToMesh();
    }

    static Mesh BuildNewrozBrazierMesh()
    {
        var b = new FlatMeshBuilder("NewrozBrazier");
        // Oyma taş taban, ayak ve geniş Newroz ateş çanağı
        b.AddPrism(new Vector3(0f, 0f, 0f), 8, 0.72f, 0.56f, 0.28f);
        b.AddPrism(new Vector3(0f, 0.28f, 0f), 8, 0.36f, 0.28f, 0.54f);
        b.AddPrism(new Vector3(0f, 0.82f, 0f), 8, 0.32f, 0.88f, 0.42f);
        b.AddPrism(new Vector3(0f, 1.24f, 0f), 8, 0.90f, 0.82f, 0.14f);
        return b.ToMesh();
    }

    static Mesh BuildStoneTorchPedestalMesh()
    {
        var b = new FlatMeshBuilder("StoneTorchPedestal");
        b.AddPrism(new Vector3(0f, 0f, 0f), 6, 0.40f, 0.30f, 0.22f);
        b.AddPrism(new Vector3(0f, 0.22f, 0f), 6, 0.19f, 0.15f, 1.38f);
        b.AddPrism(new Vector3(0f, 1.60f, 0f), 6, 0.18f, 0.38f, 0.28f);
        return b.ToMesh();
    }

    static Mesh BuildFlameCoreMesh()
    {
        var b = new FlatMeshBuilder("FlameCore");
        // Stilize 3 dilli alev formu
        b.AddCone(new Vector3(0f, 0f, 0f), 6, 0.28f, 0.72f);
        b.AddCone(new Vector3(0.11f, 0f, 0.06f), 5, 0.16f, 0.48f);
        b.AddCone(new Vector3(-0.10f, 0f, -0.05f), 5, 0.15f, 0.44f);
        return b.ToMesh();
    }

    sealed class FlatMeshBuilder
    {
        readonly string name;
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> tris = new List<int>();

        public FlatMeshBuilder(string meshName) { name = meshName; }

        public void AddTri(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            if (n.sqrMagnitude < 0.0001f) n = Vector3.up;
            int idx = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            normals.Add(n); normals.Add(n); normals.Add(n);
            uvs.Add(Vector2.zero); uvs.Add(Vector2.right); uvs.Add(Vector2.up);
            tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            AddTri(a, b, c);
            AddTri(a, c, d);
        }

        public void AddBox(Vector3 center, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3 p0 = center + new Vector3(-h.x, -h.y, -h.z);
            Vector3 p1 = center + new Vector3(h.x, -h.y, -h.z);
            Vector3 p2 = center + new Vector3(h.x, h.y, -h.z);
            Vector3 p3 = center + new Vector3(-h.x, h.y, -h.z);
            Vector3 p4 = center + new Vector3(-h.x, -h.y, h.z);
            Vector3 p5 = center + new Vector3(h.x, -h.y, h.z);
            Vector3 p6 = center + new Vector3(h.x, h.y, h.z);
            Vector3 p7 = center + new Vector3(-h.x, h.y, h.z);

            AddQuad(p0, p3, p2, p1); // -Z
            AddQuad(p5, p6, p7, p4); // +Z
            AddQuad(p4, p7, p3, p0); // -X
            AddQuad(p1, p2, p6, p5); // +X
            AddQuad(p3, p7, p6, p2); // +Y
            AddQuad(p4, p0, p1, p5); // -Y
        }

        public void AddIcoBox(Vector3 center, Vector3 size)
        {
            // Köşeleri pahlı low-poly küme görünümü için 2 katmanlı sekizgen
            AddTaperedPrism(center, 8, size.x * 0.34f, size.x * 0.50f, size.x * 0.50f, -size.y * 0.5f, -size.y * 0.15f, 0f, 1, size.z / Mathf.Max(0.01f, size.x));
            AddTaperedPrism(center, 8, size.x * 0.50f, size.x * 0.50f, size.x * 0.32f, -size.y * 0.15f, size.y * 0.22f, 0f, 1, size.z / Mathf.Max(0.01f, size.x));
            AddTaperedPrism(center, 8, size.x * 0.50f, size.x * 0.28f, size.x * 0.32f, size.y * 0.22f, size.y * 0.5f, 0f, 1, size.z / Mathf.Max(0.01f, size.x));
        }

        public void AddPrism(Vector3 baseCenter, int sides, float bottomRadius, float topRadius, float height)
        {
            AddTaperedPrism(baseCenter, sides, bottomRadius, topRadius, 0f, 0f, height, 0f, 1, 1f);
        }

        public void AddCone(Vector3 baseCenter, int sides, float radius, float height)
        {
            Vector3 tip = baseCenter + new Vector3(0f, height, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = ((float)i / sides) * Mathf.PI * 2f;
                float a1 = ((float)(i + 1) / sides) * Mathf.PI * 2f;
                Vector3 p0 = baseCenter + new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                Vector3 p1 = baseCenter + new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                AddTri(p0, tip, p1);
                AddTri(baseCenter, p0, p1);
            }
        }

        public void AddTaperedPrism(
            Vector3 origin,
            int sides,
            float bottomRadius,
            float topRadius,
            float unused,
            float yBottom,
            float yTop,
            float jitter,
            int seed,
            float zScale = 1f)
        {
            _ = unused;
            var bot = new Vector3[sides];
            var top = new Vector3[sides];

            for (int i = 0; i < sides; i++)
            {
                float angle = ((float)i / sides) * Mathf.PI * 2f;
                float jB = jitter > 0f ? (Mathf.PerlinNoise(i * 1.7f + seed, seed * 0.3f) - 0.5f) * jitter : 0f;
                float jT = jitter > 0f ? (Mathf.PerlinNoise(i * 2.3f + seed, seed * 0.9f) - 0.5f) * jitter : 0f;
                float rb = Mathf.Max(0.05f, bottomRadius + jB);
                float rt = Mathf.Max(0.05f, topRadius + jT);
                bot[i] = origin + new Vector3(Mathf.Cos(angle) * rb, yBottom, Mathf.Sin(angle) * rb * zScale);
                top[i] = origin + new Vector3(Mathf.Cos(angle) * rt, yTop, Mathf.Sin(angle) * rt * zScale);
            }

            Vector3 botC = origin + new Vector3(0f, yBottom, 0f);
            Vector3 topC = origin + new Vector3(0f, yTop, 0f);

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                AddQuad(bot[i], top[i], top[next], bot[next]);
                AddTri(topC, top[next], top[i]);
                AddTri(botC, bot[i], bot[next]);
            }
        }

        public Mesh ToMesh()
        {
            var m = new Mesh { name = name };
            m.SetVertices(verts);
            m.SetNormals(normals);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
