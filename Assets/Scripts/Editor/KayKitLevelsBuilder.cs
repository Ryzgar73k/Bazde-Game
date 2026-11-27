using UnityEditor;
using UnityEngine;

/// <summary>
/// Level 1, 2 ve 3 sahnelerini KayKit - Platformer Pack varlıkları ve ortak mobil fizik
/// standartlarıyla sıfırdan oluşturan Editör inşa aracı.
/// </summary>
public static class KayKitLevelsBuilder
{
    [MenuItem("Tools/Parkur/Tum KayKit Levellerini Olustur (1-4)")]
    public static void BuildAllStandardLevels()
    {
        BuildLevel1Scene();
        BuildLevel2Scene();
        BuildLevel3Scene();
        Level4Builder.BuildLevel4Scene();
        KayKitLevelKit.RegisterAllStandardBuildScenes();
        Debug.Log("[KayKitLevelsBuilder] Tüm 4 KayKit parkur sahnesi (Level1–Level4) oluşturuldu ve Build Settings güncellendi!");
    }

    [MenuItem("Tools/Parkur/Level 1 Sahnesini Olustur (KayKit)")]
    public static void BuildLevel1Scene()
    {
        var ctx = KayKitLevelKit.CreateSceneContext(
            "Assets/Scenes/Level1.unity",
            "LEVEL 1",
            new Vector3(0f, 1.05f, -1.5f),
            Quaternion.identity,
            -12f,
            new Vector3(-10f, -15f, 32f),
            new Vector3(120f, 8f, 140f));

        // BÖLÜM 1: Başlangıç & 3 Engelli Köprü (+Z)
        var s1 = new GameObject("Section1_StartAndHurdles").transform;
        s1.SetParent(ctx.LevelRoot, false);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x2_blue", "StartIsland", new Vector3(0f, -1f, 0f), Vector3.one, s1);
        KayKitLevelKit.PlaceArch("blue/arch_wide_blue", "WelcomeArch", new Vector3(0f, 1f, 2.4f), Quaternion.identity, Vector3.one, s1);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "HurdleBridge", new Vector3(0f, 0.1f, 11.4f), new Vector3(0.75f, 1f, 2.2f), s1);
        KayKitLevelKit.PlaceHurdleBar("yellow/barrier_4x1x1_yellow", "HurdleBar_1", new Vector3(0f, 1.1f, 8.0f), Quaternion.identity, new Vector3(1.05f, 0.55f, 0.65f), s1);
        KayKitLevelKit.PlaceHurdleBar("yellow/barrier_4x1x1_yellow", "HurdleBar_2", new Vector3(0f, 1.1f, 11.4f), Quaternion.identity, new Vector3(1.05f, 0.55f, 0.65f), s1);
        KayKitLevelKit.PlaceHurdleBar("yellow/barrier_4x1x1_yellow", "HurdleBar_3", new Vector3(0f, 1.1f, 14.8f), Quaternion.identity, new Vector3(1.05f, 0.55f, 0.65f), s1);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_1", new Vector3(0f, 2.2f, 11.4f), s1);

        KayKitLevelKit.PlacePlatform("blue/platform_4x4x1_blue", "StepPad_1A", new Vector3(0f, 0.3f, 21.8f), Vector3.one, s1);
        KayKitLevelKit.PlacePlatform("yellow/platform_4x4x1_yellow", "StepPad_1B", new Vector3(0f, 0.5f, 27.4f), Vector3.one, s1);

        // BÖLÜM 2: Dönen Çark Platformu & Ahşap Köprü -> Checkpoint 1
        var s2 = new GameObject("Section2_SpinningWheel").transform;
        s2.SetParent(ctx.LevelRoot, false);

        KayKitLevelKit.PlaceSpinningWheelDeck("SpinWheel_1", new Vector3(0f, 1.0f, 35.8f), 1.15f, 32f, s2);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_2", new Vector3(0f, 2.8f, 35.8f), s2);

        KayKitLevelKit.PlacePlatform("neutral/floor_wood_2x6", "WoodBridge_1", new Vector3(0f, 1.8f, 44.8f), Vector3.one, s2, Quaternion.Euler(0f, 90f, 0f));
        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "Checkpoint1_Island", new Vector3(0f, 1.4f, 52.6f), Vector3.one, s2);
        KayKitLevelKit.PlaceCheckpoint(1, new Vector3(0f, 2.4f, 52.6f), Quaternion.identity, ctx.BeaconMat, s2, new Vector3(2.28f, 0f, 2.28f));
        KayKitLevelKit.PlaceProp("yellow/signage_arrows_left_yellow", "UTurnSign_1", new Vector3(2.35f, 2.4f, 51.85f), Quaternion.Euler(0f, 260f, 0f), Vector3.one * 0.56f, s2, addCollider: true);

        // BÖLÜM 3: Kavisli U-Dönüş & Zorunlu Trambolin Tırmanışı
        var s3 = new GameObject("Section3_UTurnAndTrampoline").transform;
        s3.SetParent(ctx.LevelRoot, false);

        KayKitLevelKit.PlacePlatform("yellow/platform_4x4x1_yellow", "UTurn_1", new Vector3(0f, 1.5f, 59.4f), Vector3.one, s3);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_3", new Vector3(0f, 3.3f, 59.4f), s3);

        KayKitLevelKit.PlacePlatform("blue/platform_4x4x1_blue", "UTurn_2", new Vector3(-3.8f, 1.6f, 63.8f), Vector3.one, s3, Quaternion.Euler(0f, -35f, 0f));
        KayKitLevelKit.PlacePlatform("blue/platform_4x4x1_blue", "UTurn_3", new Vector3(-9.4f, 1.7f, 65.2f), Vector3.one, s3, Quaternion.Euler(0f, -90f, 0f));
        KayKitLevelKit.PlacePlatform("blue/platform_4x4x1_blue", "UTurn_4", new Vector3(-14.8f, 1.8f, 63.6f), Vector3.one, s3, Quaternion.Euler(0f, -145f, 0f));

        // U-Dönüş Tepe Risk/Ödül Elması #1
        KayKitLevelKit.PlacePlatform("green/platform_2x2x1_green", "DiamondPedestal_1", new Vector3(-9.4f, 1.8f, 70.6f), new Vector3(1.3f, 1f, 1.3f), s3);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Diamond, "Diamond_1", new Vector3(-9.4f, 3.6f, 70.6f), s3);

        KayKitLevelKit.PlacePlatform("yellow/platform_4x4x1_yellow", "UTurn_5", new Vector3(-16.8f, 1.9f, 58.6f), Vector3.one, s3);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_4", new Vector3(-16.8f, 3.7f, 58.6f), s3);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "Checkpoint2_Island", new Vector3(-16.8f, 2.0f, 51.8f), Vector3.one, s3);
        KayKitLevelKit.PlaceCheckpoint(2, new Vector3(-16.8f, 3.0f, 51.8f), Quaternion.Euler(0f, 180f, 0f), ctx.BeaconMat, s3);

        // İşlevsel Trambolin (Üst kata +3.85m tırmanış; yürüyüş ve koşu hızında güvenle ulaşır)
        KayKitLevelKit.PlacePlatform("yellow/platform_4x4x1_yellow", "TrampolineBase_1", new Vector3(-16.8f, 2.1f, 45.2f), Vector3.one, s3);
        KayKitLevelKit.PlaceSpringPad("SpringPad_1", new Vector3(-16.8f, 2.35f, 45.2f), 7.4f, s3);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "UpperDeck_1", new Vector3(-16.8f, 6.2f, 38.6f), Vector3.one, s3);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_5", new Vector3(-16.8f, 8.0f, 38.6f), s3);

        // BÖLÜM 4: Döner Boru Silindir Geçişi & Zirve Rampası -> Finish (-Z)
        var s4 = new GameObject("Section4_RollersAndFinish").transform;
        s4.SetParent(ctx.LevelRoot, false);

        KayKitLevelKit.PlaceRollingCylinder("RollingCylinder_1", new Vector3(-16.8f, 6.25f, 30.4f), 4, 1.15f, 130f, s4, alongZ: true, pushMultiplier: 0.56f);
        KayKitLevelKit.PlacePlatform("yellow/platform_4x4x1_yellow", "RollerMid_1", new Vector3(-16.8f, 6.5f, 23.2f), Vector3.one, s4);
        KayKitLevelKit.PlaceRollingCylinder("RollingCylinder_2", new Vector3(-16.8f, 6.55f, 16.0f), 4, 1.15f, -145f, s4, alongZ: true, pushMultiplier: 0.56f);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "Checkpoint3_Island", new Vector3(-16.8f, 6.8f, 7.6f), Vector3.one, s4);
        KayKitLevelKit.PlaceCheckpoint(3, new Vector3(-16.8f, 7.8f, 7.6f), Quaternion.Euler(0f, 180f, 0f), ctx.BeaconMat, s4);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_6", new Vector3(-16.8f, 8.6f, 9.4f), s4);

        // KayKit Rampa (1m ön dudak payı dahil; Z=4.4 Y=7.8 girişinden -Z'ye Y=10.8 zirvesine yükselir)
        KayKitLevelKit.PlaceSlope("blue/platform_slope_4x6x4_blue", "SummitSlope_1", new Vector3(-16.8f, 6.8f, 1.4f), Quaternion.identity, Vector3.one, s4);

        // Opsiyonel Elmas #2 (Zirve yanı küçük ada)
        KayKitLevelKit.PlacePlatform("green/platform_2x2x1_green", "DiamondPedestal_2", new Vector3(-23.0f, 10.0f, -6.2f), new Vector3(1.3f, 1f, 1.3f), s4);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Diamond, "Diamond_2", new Vector3(-23.0f, 11.8f, -6.2f), s4);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x2_blue", "VictorySummitArena", new Vector3(-16.8f, 8.8f, -6.2f), new Vector3(1.5f, 1f, 1.5f), s4);
        KayKitLevelKit.PlaceVictoryZone(new Vector3(-16.8f, 10.8f, -6.2f), Quaternion.Euler(0f, 180f, 0f), "Level2", s4);

        KayKitLevelKit.SaveSceneAndRegisterBuild(ctx);
        Debug.Log("[KayKitLevelsBuilder] Level 1 başarıyla oluşturuldu!");
    }

    [MenuItem("Tools/Parkur/Level 2 Sahnesini Olustur (KayKit)")]
    public static void BuildLevel2Scene()
    {
        var ctx = KayKitLevelKit.CreateSceneContext(
            "Assets/Scenes/Level2.unity",
            "LEVEL 2",
            new Vector3(0f, 1.05f, -1.5f),
            Quaternion.identity,
            -12f,
            new Vector3(32f, -15f, 40f),
            new Vector3(130f, 8f, 130f));

        // BÖLÜM 1 (+Z): Kademeli Atlama Taşları, Dar Ahşap Köprü & Dönen Çark -> Checkpoint 1
        var s1 = new GameObject("Section1_SteppingStonesAndWheel").transform;
        s1.SetParent(ctx.LevelRoot, false);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x2_blue", "StartIsland", new Vector3(0f, -1f, 0f), Vector3.one, s1);
        KayKitLevelKit.PlaceArch("blue/arch_wide_blue", "WelcomeArch", new Vector3(0f, 1f, 2.4f), Quaternion.identity, Vector3.one, s1);

        KayKitLevelKit.PlacePlatform("yellow/platform_4x4x1_yellow", "StepPad_2A", new Vector3(-1.4f, 0.2f, 6.6f), new Vector3(0.75f, 1f, 0.75f), s1);
        KayKitLevelKit.PlacePlatform("blue/platform_4x4x1_blue", "StepPad_2B", new Vector3(1.5f, 0.4f, 10.8f), new Vector3(0.75f, 1f, 0.75f), s1);
        KayKitLevelKit.PlacePlatform("neutral/floor_wood_2x6", "WoodBridge_1", new Vector3(0f, 1.0f, 15.8f), Vector3.one, s1, Quaternion.Euler(0f, 90f, 0f));
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_1", new Vector3(0f, 2.7f, 15.8f), s1);

        KayKitLevelKit.PlaceSpinningWheelDeck("SpinWheel_2", new Vector3(0f, 0.8f, 24.4f), 1.15f, 38f, s1);
        KayKitLevelKit.PlacePlatform("yellow/platform_4x4x1_yellow", "StepPad_1", new Vector3(0f, 1.1f, 31.4f), Vector3.one, s1);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_2", new Vector3(0f, 2.9f, 31.4f), s1);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "Checkpoint1_Island", new Vector3(0f, 1.3f, 37.6f), Vector3.one, s1);
        KayKitLevelKit.PlaceCheckpoint(1, new Vector3(0f, 2.3f, 37.6f), Quaternion.Euler(0f, 90f, 0f), ctx.BeaconMat, s1, new Vector3(-2.0f, 0f, 0f));
        KayKitLevelKit.PlaceProp("yellow/signage_arrows_right_yellow", "TurnRightSign_2", new Vector3(0f, 2.3f, 40.2f), Quaternion.Euler(0f, 180f, 0f), Vector3.one * 0.7f, s1, addCollider: true);

        // BÖLÜM 2 (+X): Hareketli Platform Zinciri & Opsiyonel Elmas -> Checkpoint 2
        var s2 = new GameObject("Section2_MovingPlatforms").transform;
        s2.SetParent(ctx.LevelRoot, false);

        KayKitLevelKit.PlaceMovingPlatform("yellow/platform_4x4x1_yellow", "MovingPlatform_2A",
            new Vector3(7.0f, 1.5f, 35.0f), new Vector3(0f, 0f, 5.2f), 2.7f, 0.45f, s2);
        KayKitLevelKit.PlacePlatform("blue/platform_4x4x1_blue", "EastMid_2", new Vector3(13.8f, 1.7f, 37.6f), Vector3.one, s2);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_3", new Vector3(13.8f, 3.5f, 37.6f), s2);

        KayKitLevelKit.PlacePlatform("green/platform_2x2x1_green", "SidePad_2", new Vector3(13.8f, 1.8f, 42.6f), Vector3.one, s2);
        KayKitLevelKit.PlacePlatform("green/platform_2x2x1_green", "DiamondPedestal_1", new Vector3(13.8f, 2.0f, 47.0f), new Vector3(1.3f, 1f, 1.3f), s2);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Diamond, "Diamond_1", new Vector3(13.8f, 3.8f, 47.0f), s2);

        KayKitLevelKit.PlaceMovingPlatform("yellow/platform_4x4x1_yellow", "MovingPlatform_2B",
            new Vector3(19.6f, 1.9f, 37.6f), new Vector3(5.0f, 1.4f, 0f), 2.8f, 0.5f, s2);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "Checkpoint2_Island", new Vector3(31.4f, 3.4f, 37.6f), Vector3.one, s2);
        KayKitLevelKit.PlaceCheckpoint(2, new Vector3(31.4f, 4.4f, 37.6f), Quaternion.Euler(0f, 90f, 0f), ctx.BeaconMat, s2);

        // BÖLÜM 3 (+X): Yüksek Zıt Süpürücüler & Zamanlı Diken Tuzağı -> Checkpoint 3
        var s3 = new GameObject("Section3_SweepersAndSpikes").transform;
        s3.SetParent(ctx.LevelRoot, false);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "SweeperArena_2A", new Vector3(40.4f, 3.6f, 37.6f), new Vector3(1.35f, 1f, 1.35f), s3);
        KayKitLevelKit.PlaceHighSweeper("red/swiper_double_long_red", "HighSweeper_2A", new Vector3(40.4f, 4.6f, 37.6f), new Vector3(0.95f, 1.35f, 1.35f), 48f, false, s3);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_4", new Vector3(40.4f, 5.4f, 34.4f), s3);

        KayKitLevelKit.PlaceTimedSpikeTrap("TimedSpikes_2A", new Vector3(48.4f, 3.7f, 37.6f), 0f, s3);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "SweeperArena_2B", new Vector3(56.4f, 3.9f, 37.6f), new Vector3(1.3f, 1f, 1.3f), s3);
        KayKitLevelKit.PlaceHighSweeper("red/swiper_quad_red", "HighSweeper_2B", new Vector3(56.4f, 4.9f, 37.6f), new Vector3(1.35f, 1.45f, 1.35f), -42f, true, s3);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_5", new Vector3(56.4f, 5.7f, 40.6f), s3);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "Checkpoint3_Island", new Vector3(65.4f, 4.1f, 37.6f), Vector3.one, s3);
        KayKitLevelKit.PlaceCheckpoint(3, new Vector3(65.4f, 5.1f, 37.6f), Quaternion.identity, ctx.BeaconMat, s3, new Vector3(0f, 0f, -2.0f));
        KayKitLevelKit.PlaceProp("yellow/signage_arrows_left_yellow", "TurnLeftSign_2", new Vector3(68.0f, 5.1f, 37.6f), Quaternion.Euler(0f, -90f, 0f), Vector3.one * 0.7f, s3, addCollider: true);

        // BÖLÜM 4 (+Z): Zorunlu Trambolin Tırmanışı & Dar Ahşap Köprü -> Finish
        var s4 = new GameObject("Section4_ClimbAndVictory").transform;
        s4.SetParent(ctx.LevelRoot, false);

        KayKitLevelKit.PlacePlatform("yellow/platform_4x4x1_yellow", "TrampolineBase_2", new Vector3(65.4f, 4.2f, 44.2f), Vector3.one, s4);
        KayKitLevelKit.PlaceSpringPad("SpringPad_2", new Vector3(65.4f, 4.45f, 44.2f), 9.0f, s4);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "SkyTerrace_2", new Vector3(65.4f, 9.8f, 50.8f), Vector3.one, s4);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_6", new Vector3(65.4f, 11.6f, 50.8f), s4);

        KayKitLevelKit.PlacePlatform("green/platform_2x2x1_green", "DiamondPedestal_2", new Vector3(70.8f, 10.0f, 50.8f), new Vector3(1.3f, 1f, 1.3f), s4);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Diamond, "Diamond_2", new Vector3(70.8f, 11.8f, 50.8f), s4);

        KayKitLevelKit.PlacePlatform("neutral/floor_wood_2x6", "WoodBridge_2", new Vector3(65.4f, 10.6f, 58.6f), Vector3.one, s4, Quaternion.Euler(0f, 90f, 0f));
        KayKitLevelKit.PlacePlatform("blue/platform_4x4x1_blue", "SummitStep_2", new Vector3(65.4f, 10.3f, 65.4f), Vector3.one, s4);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x2_blue", "VictorySummitArena", new Vector3(65.4f, 9.5f, 73.6f), new Vector3(1.5f, 1f, 1.5f), s4);
        KayKitLevelKit.PlaceVictoryZone(new Vector3(65.4f, 11.5f, 73.6f), Quaternion.identity, "Level3", s4);

        KayKitLevelKit.SaveSceneAndRegisterBuild(ctx);
        Debug.Log("[KayKitLevelsBuilder] Level 2 başarıyla oluşturuldu!");
    }

    [MenuItem("Tools/Parkur/Level 3 Sahnesini Olustur (KayKit)")]
    public static void BuildLevel3Scene()
    {
        var ctx = KayKitLevelKit.CreateSceneContext(
            "Assets/Scenes/Level3.unity",
            "LEVEL 3",
            new Vector3(0f, 1.05f, -2.0f),
            Quaternion.identity,
            -14f,
            new Vector3(0f, -18f, 107f),
            new Vector3(120f, 8f, 270f));

        // BÖLÜM 1 (+Z): Hareketli Platformlar -> Checkpoint 1
        var s1 = new GameObject("Section1_MovingPlatforms").transform;
        s1.SetParent(ctx.LevelRoot, false);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x2_blue", "StartIsland", new Vector3(0f, -1f, 0f), Vector3.one, s1);
        KayKitLevelKit.PlaceArch("blue/arch_wide_blue", "WelcomeArch", new Vector3(0f, 1f, 2.4f), Quaternion.identity, Vector3.one, s1);

        KayKitLevelKit.PlaceMovingPlatform("yellow/platform_4x4x1_yellow", "MovingPlatform_1A",
            new Vector3(-2.8f, 0.3f, 6.8f), new Vector3(5.6f, 0f, 0f), 2.7f, 0.45f, s1);
        KayKitLevelKit.PlacePlatform("blue/platform_4x4x1_blue", "MidPier_1", new Vector3(0f, 0.7f, 13.0f), Vector3.one, s1);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_1", new Vector3(0f, 2.5f, 13.0f), s1);

        KayKitLevelKit.PlaceMovingPlatform("yellow/platform_4x4x1_yellow", "MovingPlatform_1B",
            new Vector3(0f, 1.0f, 18.8f), new Vector3(0f, 1.4f, 5.5f), 2.9f, 0.5f, s1);
        KayKitLevelKit.PlacePlatform("blue/platform_4x4x1_blue", "MidPier_2", new Vector3(0f, 2.5f, 29.5f), Vector3.one, s1);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_2", new Vector3(0f, 4.3f, 29.5f), s1);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "Checkpoint1_Island", new Vector3(0f, 2.8f, 36.6f), Vector3.one, s1);
        KayKitLevelKit.PlaceCheckpoint(1, new Vector3(0f, 3.8f, 36.6f), Quaternion.identity, ctx.BeaconMat, s1);

        // BÖLÜM 2 (+Z): Zıt Yönlü Dönen Çarklar & Silindirler -> Checkpoint 2
        var s2 = new GameObject("Section2_CounterRotatingObstacles").transform;
        s2.SetParent(ctx.LevelRoot, false);

        KayKitLevelKit.PlaceSpinningWheelDeck("SpinWheel_3A", new Vector3(-1.8f, 3.1f, 44.6f), 1.05f, 36f, s2);
        KayKitLevelKit.PlacePlatform("yellow/platform_4x4x1_yellow", "StepStone_BetweenWheels", new Vector3(0f, 3.4f, 52.6f), Vector3.one, s2);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_3", new Vector3(0f, 5.2f, 52.6f), s2);
        KayKitLevelKit.PlaceSpinningWheelDeck("SpinWheel_3B", new Vector3(1.8f, 3.7f, 60.6f), 1.05f, -36f, s2);

        KayKitLevelKit.PlaceRollingCylinder("Roller_3A", new Vector3(0f, 4.18f, 70.2f), 4, 1.12f, 145f, s2, alongZ: true, pushMultiplier: 0.56f);
        KayKitLevelKit.PlacePlatform("yellow/platform_4x4x1_yellow", "StepStone_2B", new Vector3(0f, 4.4f, 77.4f), Vector3.one, s2);
        KayKitLevelKit.PlacePlatform("green/platform_2x2x1_green", "DiamondPedestal_1", new Vector3(5.4f, 4.5f, 77.4f), new Vector3(1.3f, 1f, 1.3f), s2);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Diamond, "Diamond_1", new Vector3(5.4f, 6.3f, 77.4f), s2);

        KayKitLevelKit.PlaceRollingCylinder("Roller_3B", new Vector3(0f, 4.95f, 85.0f), 4, 1.12f, -160f, s2, alongZ: true, pushMultiplier: 0.56f);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "Checkpoint2_Island", new Vector3(0f, 5.2f, 93.2f), Vector3.one, s2);
        KayKitLevelKit.PlaceCheckpoint(2, new Vector3(0f, 6.2f, 93.2f), Quaternion.identity, ctx.BeaconMat, s2);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_4", new Vector3(0f, 7.0f, 91.2f), s2);

        // BÖLÜM 3 (+Z): Çift Gökyüzü Trambolini Tırmanışı -> Checkpoint 3
        var s3 = new GameObject("Section3_SkyTrampolines").transform;
        s3.SetParent(ctx.LevelRoot, false);

        KayKitLevelKit.PlacePlatform("yellow/platform_4x4x1_yellow", "TrampolineLaunch_3A", new Vector3(0f, 5.4f, 100.0f), Vector3.one, s3);
        KayKitLevelKit.PlaceSpringPad("SpringPad_3A", new Vector3(0f, 5.65f, 100.0f), 9.2f, s3);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "SuspendedTerrace_1", new Vector3(0f, 11.2f, 106.6f), Vector3.one, s3);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_5", new Vector3(0f, 13.0f, 106.6f), s3);

        KayKitLevelKit.PlacePlatform("yellow/platform_4x4x1_yellow", "SkyStone_1", new Vector3(1.6f, 11.6f, 113.4f), Vector3.one, s3);
        KayKitLevelKit.PlacePlatform("yellow/platform_4x4x1_yellow", "SkyStone_2", new Vector3(-1.4f, 12.0f, 119.6f), Vector3.one, s3);
        KayKitLevelKit.PlacePlatform("green/platform_2x2x1_green", "DiamondPedestal_2", new Vector3(-6.4f, 12.1f, 119.6f), new Vector3(1.3f, 1f, 1.3f), s3);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Diamond, "Diamond_2", new Vector3(-6.4f, 13.9f, 119.6f), s3);

        KayKitLevelKit.PlacePlatform("yellow/platform_4x4x1_yellow", "TrampolineLaunch_3B", new Vector3(0f, 12.2f, 126.0f), Vector3.one, s3);
        KayKitLevelKit.PlaceSpringPad("SpringPad_3B", new Vector3(0f, 12.45f, 126.0f), 9.8f, s3);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "Checkpoint3_Island", new Vector3(0f, 18.0f, 132.8f), Vector3.one, s3);
        KayKitLevelKit.PlaceCheckpoint(3, new Vector3(0f, 19.0f, 132.8f), Quaternion.identity, ctx.BeaconMat, s3);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_6", new Vector3(0f, 19.8f, 130.8f), s3);

        // BÖLÜM 4 (+Z): Dar Ahşap Kirişler, Zikzak & Kayar Testere -> Checkpoint 4
        var s4 = new GameObject("Section4_NarrowBeamsAndHazards").transform;
        s4.SetParent(ctx.LevelRoot, false);

        KayKitLevelKit.PlacePlatform("neutral/floor_wood_2x6", "TightropeBeam_1", new Vector3(0f, 18.8f, 140.8f), Vector3.one, s4, Quaternion.Euler(0f, 90f, 0f));
        KayKitLevelKit.PlaceTimedSpikeTrap("TimedSpikes_3A", new Vector3(0f, 18.3f, 147.6f), 0.3f, s4);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_7", new Vector3(0f, 20.4f, 147.6f), s4);

        KayKitLevelKit.PlacePlatform("neutral/floor_wood_2x6", "ZigzagBeam_1", new Vector3(1.6f, 19.1f, 153.8f), Vector3.one, s4, Quaternion.Euler(0f, -70f, 0f));
        KayKitLevelKit.PlacePlatform("yellow/platform_4x4x1_yellow", "CornerPlatform_1", new Vector3(3.0f, 18.8f, 160.2f), Vector3.one, s4);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "SawArena_3", new Vector3(0f, 19.0f, 167.6f), new Vector3(1.25f, 1f, 1.25f), s4);
        KayKitLevelKit.PlaceSlidingSawTrap("SlidingSaw_3", new Vector3(-2.6f, 20.15f, 167.6f), new Vector3(5.2f, 0f, 0f), s4, 3.0f);

        KayKitLevelKit.PlacePlatform("neutral/floor_wood_2x6", "NarrowBridge_2", new Vector3(0f, 19.7f, 175.2f), Vector3.one, s4, Quaternion.Euler(0f, 90f, 0f));
        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "Checkpoint4_Island", new Vector3(0f, 19.4f, 182.4f), Vector3.one, s4);
        KayKitLevelKit.PlaceCheckpoint(4, new Vector3(0f, 20.4f, 182.4f), Quaternion.identity, ctx.BeaconMat, s4);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_8", new Vector3(0f, 21.2f, 180.4f), s4);

        // BÖLÜM 5 (+Z): Yükselen Basamaklar & Zafer Zirvesi
        var s5 = new GameObject("Section5_SummitAndVictory").transform;
        s5.SetParent(ctx.LevelRoot, false);

        for (int i = 0; i < 4; i++)
        {
            KayKitLevelKit.PlacePlatform("yellow/platform_4x2x1_yellow", $"SummitStep_{i + 1}",
                new Vector3(0f, 20.1f + i * 0.7f, 188.7f + i * 5.3f), new Vector3(1.3f, 1f, 1.3f), s5);
        }

        KayKitLevelKit.PlacePlatform("green/platform_2x2x1_green", "DiamondPedestal_3", new Vector3(4.8f, 21.8f, 199.3f), new Vector3(1.3f, 1f, 1.3f), s5);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Diamond, "Diamond_3", new Vector3(4.8f, 23.6f, 199.3f), s5);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x2_blue", "VictorySummitArena", new Vector3(0f, 21.6f, 213.0f), new Vector3(1.6f, 1f, 1.6f), s5);
        KayKitLevelKit.PlaceVictoryZone(new Vector3(0f, 23.6f, 213.0f), Quaternion.identity, "Level4", s5);

        KayKitLevelKit.SaveSceneAndRegisterBuild(ctx);
        Debug.Log("[KayKitLevelsBuilder] Level 3 başarıyla oluşturuldu!");
    }
}
