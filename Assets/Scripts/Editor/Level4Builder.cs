using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// KayKit Platformer Pack varlıklarını kullanarak Level 4 (Gökyüzü Adaları) sahnesini
/// temiz, URP uyumlu ve KayKitLevelKit üzerinden tekrar üretilebilir şekilde oluşturur.
/// </summary>
public static class Level4Builder
{
    [MenuItem("Tools/Parkur/Level 4 Sahnesini Olustur")]
    public static void BuildLevel4Scene()
    {
        var ctx = KayKitLevelKit.CreateSceneContext(
            "Assets/Scenes/Level4.unity",
            "LEVEL 4",
            new Vector3(0f, 1.05f, -1.5f),
            Quaternion.identity,
            -14f,
            new Vector3(25f, -18f, 65f),
            new Vector3(220f, 8f, 240f));

        BuildSection1_HarborAndMovingPlatforms(ctx.LevelRoot, ctx.BeaconMat);
        BuildSection2_EastwardSweepers(ctx.LevelRoot, ctx.BeaconMat);
        BuildSection3_SkywardTrampolineClimb(ctx.LevelRoot, ctx.BeaconMat);
        BuildSection4_WestwardGauntletAndSwitch(ctx.LevelRoot, ctx.BeaconMat);
        BuildSection5_SummitAndVictory(ctx.LevelRoot);

        KayKitLevelKit.SaveSceneAndRegisterBuild(ctx);
        Debug.Log("[Level4Builder] Level 4 sahnesi KayKitLevelKit ile başarıyla oluşturuldu!");
    }

    static void BuildSection1_HarborAndMovingPlatforms(Transform parent, Material beaconMat)
    {
        var sec = new GameObject("Section1_HarborAndMovingPlatforms").transform;
        sec.SetParent(parent, false);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x2_blue", "StartIsland", new Vector3(0f, -1f, 0f), Vector3.one, sec);
        KayKitLevelKit.PlaceArch("blue/arch_wide_blue", "WelcomeArch", new Vector3(0f, 1f, 2.4f), Quaternion.identity, Vector3.one, sec);

        KayKitLevelKit.PlacePlatform("blue/platform_4x4x1_blue", "HarborStep_1", new Vector3(0f, 0.2f, 7.0f), Vector3.one, sec);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_1", new Vector3(0f, 2.0f, 7.0f), sec);

        KayKitLevelKit.PlaceMovingPlatform("yellow/platform_4x4x1_yellow", "MovingPlatform_1A",
            new Vector3(-2.8f, 0.5f, 13.0f), new Vector3(5.6f, 0f, 0f), 2.8f, 0.5f, sec);
        KayKitLevelKit.PlacePlatform("blue/platform_4x4x1_blue", "HarborMid_1B", new Vector3(0f, 0.8f, 19.2f), Vector3.one, sec);

        KayKitLevelKit.PlaceMovingPlatform("yellow/platform_4x4x1_yellow", "MovingPlatform_1B",
            new Vector3(0f, 1.0f, 24.6f), new Vector3(0f, 1.3f, 6.0f), 3.0f, 0.5f, sec);

        KayKitLevelKit.PlacePlatform("blue/platform_4x2x1_blue", "SpikeEntry_1", new Vector3(0f, 2.3f, 35.5f), Vector3.one, sec);
        KayKitLevelKit.PlaceTimedSpikeTrap("TimedSpikes_1", new Vector3(0f, 2.3f, 38.5f), 0f, sec);
        KayKitLevelKit.PlacePlatform("blue/platform_4x2x1_blue", "SpikeExit_1", new Vector3(0f, 2.3f, 41.5f), Vector3.one, sec);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_2", new Vector3(0f, 4.1f, 41.5f), sec);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "Checkpoint1_Island", new Vector3(0f, 2.4f, 48.0f), Vector3.one, sec);
        KayKitLevelKit.PlaceCheckpoint(1, new Vector3(0f, 3.4f, 48.0f), Quaternion.Euler(0f, 90f, 0f), beaconMat, sec, new Vector3(-2.0f, 0f, 0f));
        KayKitLevelKit.PlaceProp("yellow/signage_arrows_right_yellow", "TurnRightSign_1", new Vector3(0f, 3.4f, 50.6f), Quaternion.Euler(0f, 180f, 0f), Vector3.one * 0.7f, sec, addCollider: true);
    }

    static void BuildSection2_EastwardSweepers(Transform parent, Material beaconMat)
    {
        var sec = new GameObject("Section2_EastwardSweepers").transform;
        sec.SetParent(parent, false);

        KayKitLevelKit.PlacePlatform("blue/platform_4x4x1_blue", "EastStep_2A", new Vector3(7.0f, 2.6f, 48.0f), Vector3.one, sec);
        KayKitLevelKit.PlacePlatform("blue/platform_4x4x1_blue", "EastStep_2B", new Vector3(13.6f, 2.8f, 48.0f), Vector3.one, sec);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "SweeperArena_2A", new Vector3(22.0f, 3.0f, 48.0f), new Vector3(1.4f, 1f, 1.4f), sec);
        KayKitLevelKit.PlaceHighSweeper("red/swiper_double_long_red", "HighSweeper_2A", new Vector3(22.0f, 4.0f, 48.0f),
            new Vector3(0.95f, 1.35f, 1.35f), 50f, false, sec);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_3", new Vector3(22.0f, 4.8f, 44.6f), sec);

        KayKitLevelKit.PlacePlatform("yellow/platform_2x2x1_yellow", "SideStep_1", new Vector3(22.0f, 3.1f, 54.8f), Vector3.one, sec);
        KayKitLevelKit.PlacePlatform("neutral/floor_wood_2x6", "SideBridge_1", new Vector3(22.0f, 3.7f, 60.5f), Vector3.one, sec, Quaternion.Euler(0f, 90f, 0f));
        KayKitLevelKit.PlacePlatform("green/platform_2x2x1_green", "DiamondPedestal_1", new Vector3(22.0f, 3.4f, 65.8f), new Vector3(1.4f, 1f, 1.4f), sec);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Diamond, "Diamond_1", new Vector3(22.0f, 5.2f, 65.8f), sec);

        KayKitLevelKit.PlacePlatform("neutral/floor_wood_2x6", "WoodBridge_2", new Vector3(31.5f, 3.6f, 48.0f), Vector3.one, sec);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "SweeperArena_2B", new Vector3(40.6f, 3.2f, 48.0f), new Vector3(1.3f, 1f, 1.3f), sec);
        KayKitLevelKit.PlaceHighSweeper("red/swiper_quad_red", "HighSweeper_2B", new Vector3(40.6f, 4.2f, 48.0f),
            new Vector3(1.35f, 1.45f, 1.35f), -42f, true, sec);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_4", new Vector3(40.6f, 5.0f, 51.0f), sec);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "Checkpoint2_Island", new Vector3(50.0f, 3.4f, 48.0f), Vector3.one, sec);
        KayKitLevelKit.PlaceCheckpoint(2, new Vector3(50.0f, 4.4f, 48.0f), Quaternion.identity, beaconMat, sec, new Vector3(0f, 0f, -2.0f));
        KayKitLevelKit.PlaceProp("yellow/signage_arrows_left_yellow", "TurnLeftSign_2", new Vector3(52.5f, 4.4f, 48.0f), Quaternion.Euler(0f, -90f, 0f), Vector3.one * 0.7f, sec, addCollider: true);
    }

    static void BuildSection3_SkywardTrampolineClimb(Transform parent, Material beaconMat)
    {
        var sec = new GameObject("Section3_SkywardTrampolineClimb").transform;
        sec.SetParent(parent, false);

        KayKitLevelKit.PlacePlatform("blue/platform_4x4x1_blue", "ClimbEntry_3A", new Vector3(50.0f, 3.6f, 55.2f), Vector3.one, sec);

        KayKitLevelKit.PlacePlatform("yellow/platform_4x4x1_yellow", "TrampolineBase_1", new Vector3(50.0f, 3.6f, 61.5f), Vector3.one, sec);
        KayKitLevelKit.PlaceSpringPad("SpringPad_1", new Vector3(50.0f, 3.85f, 61.5f), 9.2f, sec);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "SkyTerrace_3A", new Vector3(50.0f, 9.6f, 68.0f), Vector3.one, sec);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_5", new Vector3(50.0f, 11.4f, 68.0f), sec);

        KayKitLevelKit.PlacePlatform("neutral/floor_wood_2x6", "SkyBridge_3", new Vector3(50.0f, 10.2f, 76.4f), Vector3.one, sec, Quaternion.Euler(0f, 90f, 0f));
        KayKitLevelKit.PlaceTimedSpikeTrap("TimedSpikes_2", new Vector3(50.0f, 9.8f, 83.6f), 0.8f, sec);

        KayKitLevelKit.PlacePlatform("yellow/platform_4x4x1_yellow", "TrampolineBase_2", new Vector3(50.0f, 9.9f, 90.8f), Vector3.one, sec);
        KayKitLevelKit.PlaceSpringPad("SpringPad_2", new Vector3(50.0f, 10.15f, 90.8f), 13.0f, sec);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "Checkpoint3_Island", new Vector3(50.0f, 16.0f, 98.5f), Vector3.one, sec);
        KayKitLevelKit.PlaceCheckpoint(3, new Vector3(50.0f, 17.0f, 98.5f), Quaternion.Euler(0f, -90f, 0f), beaconMat, sec, new Vector3(2.0f, 0f, 0f));
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_6", new Vector3(50.0f, 17.8f, 96.2f), sec);
        KayKitLevelKit.PlaceProp("yellow/signage_arrows_left_yellow", "TurnLeftSign_3", new Vector3(50.0f, 17.0f, 101.2f), Quaternion.Euler(0f, 180f, 0f), Vector3.one * 0.7f, sec, addCollider: true);
    }

    static void BuildSection4_WestwardGauntletAndSwitch(Transform parent, Material beaconMat)
    {
        var sec = new GameObject("Section4_WestwardGauntletAndSwitch").transform;
        sec.SetParent(parent, false);

        KayKitLevelKit.PlacePlatform("blue/platform_4x4x1_blue", "HammerEntry", new Vector3(43.0f, 16.2f, 98.5f), Vector3.one, sec);
        KayKitLevelKit.PlacePlatform("neutral/floor_wood_2x6", "HammerBridge", new Vector3(36.0f, 16.7f, 98.5f), Vector3.one, sec);
        KayKitLevelKit.PlaceSwingingHammer("SwingingHammer_4", new Vector3(36.0f, 17.2f, 98.5f), sec);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "SawArena_4", new Vector3(27.0f, 16.4f, 98.5f), new Vector3(1.25f, 1f, 1.25f), sec);
        KayKitLevelKit.PlaceSlidingSawTrap("SlidingSaw_4", new Vector3(27.0f, 17.55f, 95.8f), new Vector3(0f, 0f, 5.4f), sec);
        KayKitLevelKit.PlacePlatform("green/platform_2x2x1_green", "DiamondPedestal_2", new Vector3(27.0f, 16.5f, 92.2f), Vector3.one, sec);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Diamond, "Diamond_2", new Vector3(27.0f, 18.3f, 92.2f), sec);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "SwitchPlaza_4", new Vector3(18.0f, 16.5f, 98.5f), Vector3.one, sec);

        KayKitLevelKit.PlacePlatform("green/platform_4x4x1_green", "ButtonAlcove_4", new Vector3(18.0f, 16.6f, 105.5f), Vector3.one, sec);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_7", new Vector3(18.0f, 18.4f, 103.8f), sec);

        var bridge1 = KayKitLevelKit.PlaceActivatedBridge("green/platform_4x4x1_green", "ActivatedBridge_1",
            new Vector3(11.5f, 10.5f, 98.5f), new Vector3(0f, 6.0f, 0f), 6.5f, sec);
        var bridge2 = KayKitLevelKit.PlaceActivatedBridge("green/platform_4x4x1_green", "ActivatedBridge_2",
            new Vector3(6.0f, 10.5f, 98.5f), new Vector3(0f, 6.0f, 0f), 6.5f, sec);
        var gate = KayKitLevelKit.PlaceActivatedBridge("red/barrier_4x1x2_red", "ActivatedGate_Barrier",
            new Vector3(15.2f, 17.5f, 98.5f), new Vector3(0f, -3.2f, 0f), 5.0f, sec, Quaternion.Euler(0f, 90f, 0f), new Vector3(1.5f, 1f, 1f));

        KayKitLevelKit.PlacePressureButton("BridgeButton_4", new Vector3(18.0f, 17.6f, 106.0f),
            new List<ActivatedBridge> { bridge1, bridge2, gate }, sec);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x1_blue", "Checkpoint4_Island", new Vector3(-1.0f, 16.6f, 98.5f), Vector3.one, sec);
        KayKitLevelKit.PlaceCheckpoint(4, new Vector3(-1.0f, 17.6f, 98.5f), Quaternion.identity, beaconMat, sec);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Star, "Star_8", new Vector3(-1.0f, 18.4f, 96.2f), sec);
    }

    static void BuildSection5_SummitAndVictory(Transform parent)
    {
        var sec = new GameObject("Section5_SummitAndVictory").transform;
        sec.SetParent(parent, false);

        KayKitLevelKit.PlaceMovingPlatform("yellow/platform_4x4x1_yellow", "MovingPlatform_5A",
            new Vector3(-1.0f, 17.0f, 105.5f), new Vector3(0f, 1.3f, 5.0f), 2.8f, 0.5f, sec);

        KayKitLevelKit.PlaceSlope("blue/platform_slope_4x6x4_blue", "FinalSlope_5",
            new Vector3(-1.0f, 18.3f, 117.5f), Quaternion.Euler(0f, 180f, 0f), Vector3.one, sec);

        KayKitLevelKit.PlacePlatform("green/platform_2x2x1_green", "DiamondPedestal_3", new Vector3(-6.0f, 21.3f, 120.5f), new Vector3(1.3f, 1f, 1.3f), sec);
        KayKitLevelKit.PlaceCollectible(CollectibleItem.CollectibleType.Diamond, "Diamond_3", new Vector3(-6.0f, 23.1f, 120.5f), sec);

        KayKitLevelKit.PlacePlatform("blue/platform_6x6x2_blue", "VictorySummitArena", new Vector3(-1.0f, 20.0f, 127.5f), new Vector3(1.6f, 1f, 1.6f), sec);
        KayKitLevelKit.PlaceVictoryZone(new Vector3(-1.0f, 22.0f, 127.5f), Quaternion.identity, "Level1", sec);
    }
}
