using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Parkur > Oyuncuyu Kur : aktif sahnede Remy (char.fbx) oyuncu, kamera, zemin, materyal ve animasyonları kurar.
/// </summary>
public static class PlayerSetupMenu
{
    [MenuItem("Tools/Parkur/Oyuncuyu Kur")]
    static void Setup()
    {
        // 1. Önce Remy'nin materyal ve renklerini çıkarıp URP'ye uyarla
        FixRemyMaterials.ExtractAndFixMaterials();

        // 2. Zemin
        if (GameObject.Find("Ground") == null && GameObject.Find("Plane") == null)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = new Vector3(40, 1, 40);
            Undo.RegisterCreatedObjectUndo(ground, "Ground");
        }

        // 3. Varsa eski Player nesnesini bul veya yeni oluştur
        var existingPlayer = GameObject.Find("Player");
        GameObject player;

        if (existingPlayer != null)
        {
            player = existingPlayer;
            var mf = player.GetComponent<MeshFilter>();
            if (mf != null) Object.DestroyImmediate(mf);
            var mr = player.GetComponent<MeshRenderer>();
            if (mr != null) Object.DestroyImmediate(mr);
        }
        else
        {
            player = new GameObject("Player");
            player.transform.position = new Vector3(0, 0.5f, 0);
            Undo.RegisterCreatedObjectUndo(player, "Player");
        }

        // CharacterController ayarla
        var cc = player.GetComponent<CharacterController>();
        if (cc == null) cc = player.AddComponent<CharacterController>();
        cc.height = 1.9f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0, 0.95f, 0);
        cc.stepOffset = 0.55f;
        cc.slopeLimit = 55f;

        // PlayerController ayarla
        var pc = player.GetComponent<PlayerController>();
        if (pc == null) pc = player.AddComponent<PlayerController>();

        // 4. Remy (char.fbx) modelini ekle
        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/char.fbx");
        if (modelAsset != null)
        {
            for (int i = player.transform.childCount - 1; i >= 0; i--)
            {
                var child = player.transform.GetChild(i);
                if (child.name != "MobileControlsCanvas")
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, player.transform);
            instance.name = "CharacterModel";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            // Remy modeli Mixamo'da ~3.8m boyunda geldiği için 1.9m kapsüle tam oturacak şekilde ölçekle
            Bounds bounds = new Bounds(instance.transform.position, Vector3.zero);
            var renderers = instance.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }
            if (bounds.size.y > 2.4f)
            {
                float scaleFactor = 1.85f / bounds.size.y;
                instance.transform.localScale = Vector3.one * scaleFactor;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Add New Model");

            var anim = instance.GetComponent<Animator>();
            if (anim == null) anim = instance.AddComponent<Animator>();
            anim.applyRootMotion = false;
            pc.animator = anim;
        }

        // 5. Kamera
        var cam = Camera.main;
        if (cam == null)
        {
            var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGO.tag = "MainCamera";
            cam = camGO.GetComponent<Camera>();
        }
        var follow = cam.GetComponent<ThirdPersonCamera>();
        if (follow == null) follow = cam.gameObject.AddComponent<ThirdPersonCamera>();
        follow.target = player.transform;
        follow.distance = 3.2f;
        follow.height = 1.6f;
        follow.followSmoothing = 30f;
        follow.lockInstant = true;
        pc.cameraTransform = cam.transform;

        // 6. Mobil kontroller
        if (Object.FindFirstObjectByType<MobileControlsUI>() == null)
        {
            var ui = new GameObject("MobileControls", typeof(MobileControlsUI));
            Undo.RegisterCreatedObjectUndo(ui, "MobileControls");
        }

        Selection.activeGameObject = player;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        // 7. Animasyonları bağla
        AnimationSetupTool.SetupAnimations();
    }
}
