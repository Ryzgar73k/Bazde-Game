using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

namespace ParkourGame.Editor
{
    public class LevelBoxColliderPostprocessor : AssetPostprocessor
    {
        // Sürüm değişince FBX otomatik yeniden import edilir (eski hiyerarşi değişiklikleri temizlenir)
        public override uint GetVersion() { return 7; }

        // Assets/Models altındaki FBX'lere sadece collider ekler. Hiyerarşiyi DEĞİŞTİRMEZ,
        // bileşen (rotator vb.) eklemez: bunlar sahnede ApplyWheelAndTrampolineInScene ile eklenir.
        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith("Assets/Models/", System.StringComparison.OrdinalIgnoreCase))
                return;

            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf == null || mf.sharedMesh == null) continue;
                GameObject go = mf.gameObject;
                if (go.GetComponent<Collider>() != null) continue;

                LevelBoxColliderTool.ApplyColliderForMesh(go, mf.sharedMesh, false, out _);
            }
        }

        public static bool IsRotatingCylinderName(string objName)
        {
            if (string.IsNullOrEmpty(objName)) return false;
            return objName.IndexOf("Sisme_Silindir", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                   objName.IndexOf("Destek", System.StringComparison.OrdinalIgnoreCase) < 0;
        }

        public static bool IsConvexRotatingColliderName(string objName)
        {
            if (string.IsNullOrEmpty(objName)) return false;
            return IsRotatingCylinderName(objName) ||
                   objName.StartsWith("OnSag_Cark_Kol", System.StringComparison.OrdinalIgnoreCase) ||
                   objName.IndexOf("Trambolin", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsExactBoxMesh(Mesh mesh)
        {
            if (mesh == null) return false;
            Vector3[] verts = mesh.vertices;
            if (verts == null || verts.Length < 4) return false;

            Bounds b = mesh.bounds;
            Vector3 min = b.min;
            Vector3 max = b.max;
            Vector3 size = b.size;

            // Düz 4 köşeli zemin düzlemi (Plane / Turkuaz_Su_Havuzu gibi) için BoxCollider uygundur
            int zeroAxes = (size.x < 0.0005f ? 1 : 0) + (size.y < 0.0005f ? 1 : 0) + (size.z < 0.0005f ? 1 : 0);
            if (verts.Length == 4 && zeroAxes == 1)
                return true;

            if (zeroAxes > 0)
                return false;

            // Blender'dan 100x scale ile gelen mesh'lerde mutlak metre yerine eksen boyutuna orantılı tolerans kullan
            float epsX = Mathf.Max(1e-5f, size.x * 0.005f);
            float epsY = Mathf.Max(1e-5f, size.y * 0.005f);
            float epsZ = Mathf.Max(1e-5f, size.z * 0.005f);
            int cornerMask = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 p = verts[i];
                bool nearMinX = Mathf.Abs(p.x - min.x) <= epsX;
                bool nearMaxX = Mathf.Abs(p.x - max.x) <= epsX;
                bool nearMinY = Mathf.Abs(p.y - min.y) <= epsY;
                bool nearMaxY = Mathf.Abs(p.y - max.y) <= epsY;
                bool nearMinZ = Mathf.Abs(p.z - min.z) <= epsZ;
                bool nearMaxZ = Mathf.Abs(p.z - max.z) <= epsZ;

                // İç girinti, pah, sekizgen, yamuk veya çapraz direk köşeleri bounds sınırına oturmaz -> MeshCollider
                if (!nearMinX && !nearMaxX && !nearMinY && !nearMaxY && !nearMinZ && !nearMaxZ)
                    return false;

                if ((nearMinX || nearMaxX) && (nearMinY || nearMaxY) && (nearMinZ || nearMaxZ))
                {
                    int idx = (nearMaxX ? 1 : 0) | (nearMaxY ? 2 : 0) | (nearMaxZ ? 4 : 0);
                    cornerMask |= (1 << idx);
                }
            }

            return cornerMask == 0xFF;
        }

        public static bool ShouldUseMeshCollider(string objName, Mesh mesh = null)
        {
            if (string.IsNullOrEmpty(objName)) return mesh != null && !IsExactBoxMesh(mesh);
            if (IsConvexRotatingColliderName(objName)) return true;

            string[] meshKeywords =
            {
                "Kemer", "Rampa", "Silindir", "Samandira", "Salincak", "Sekizgen",
                "Dis_Halka", "Dis_Cember", "UViraj", "Bariyer", "Minderi",
                "Basamak", "Trambolin", "Kule", "Kanat", "Piramit", "Direk", "Ag_"
            };
            foreach (string kw in meshKeywords)
            {
                if (objName.IndexOf(kw, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return mesh != null && !IsExactBoxMesh(mesh);
        }
    }

    [InitializeOnLoad]
    public static class LevelAutoInitializer
    {
        static LevelAutoInitializer()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += RunAutoSetup;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Enter Play Mode Options (Reload Domain disabled) açıkken domain yenilenmediği için
            // Play Mode'dan çıkıldığında bekleyen controller/sahne kurulumunu tamamla.
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += RunAutoSetup;
            }
        }

        static void RunAutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            AnimationSetupTool.EnsureUpToDateController();

            const string prefabMigrationKey = "Bazde.ParkurColliderFixVersion";
            const int targetMigrationVersion = 2;
            if (EditorPrefs.GetInt(prefabMigrationKey, 0) < targetMigrationVersion)
            {
                EditorPrefs.SetInt(prefabMigrationKey, targetMigrationVersion);
                LevelBoxColliderTool.EnsureParkurPrefabsHaveExactColliders(true);
            }

            LevelBoxColliderTool.ApplyWheelAndTrampolineInScene(false);
        }
    }

    public static class LevelBoxColliderTool
    {
        private const string LevelModelPath = "Assets/Models/level-one.fbx";

        [MenuItem("Tools/Parkur/3. Bolum Nesnelerine Box Collider Ekle")]
        public static void SetupLevelAndAddBoxColliders()
        {
            ModelImporter importer = AssetImporter.GetAtPath(LevelModelPath) as ModelImporter;
            if (importer != null)
            {
                bool changed = false;
                if (importer.importCameras) { importer.importCameras = false; changed = true; }
                if (importer.importLights) { importer.importLights = false; changed = true; }
                if (changed) importer.SaveAndReimport();
                else AssetDatabase.ImportAsset(LevelModelPath, ImportAssetOptions.ForceUpdate);
            }

            GameObject levelRoot = GameObject.Find("level-one");
            if (levelRoot == null)
            {
                GameObject levelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LevelModelPath);
                if (levelPrefab != null)
                {
                    levelRoot = (GameObject)PrefabUtility.InstantiatePrefab(levelPrefab);
                    levelRoot.name = "level-one";
                    levelRoot.transform.position = Vector3.zero;
                    levelRoot.transform.rotation = Quaternion.identity;
                    Undo.RegisterCreatedObjectUndo(levelRoot, "Level-One Sahneye Eklendi");
                }
            }

            int addedCount = 0;
            int updatedCount = 0;

            EnsureParkurPrefabsHaveExactColliders(true);

            foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>())
            {
                if (mf == null || mf.sharedMesh == null) continue;
                GameObject go = mf.gameObject;
                if (go.GetComponentInParent<PlayerController>() != null) continue;

                ApplyColliderForMesh(go, mf.sharedMesh, true, out bool wasAdded);
                if (wasAdded) addedCount++;
                else updatedCount++;
            }

            ApplyWheelAndTrampolineInScene(true);

            foreach (Camera cam in Object.FindObjectsByType<Camera>())
            {
                if (cam != null && cam.gameObject.name.Contains("Ana_Kamera"))
                    cam.gameObject.SetActive(false);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Parkur] İşlem tamamlandı! {addedCount} nesneye yeni BoxCollider eklendi, {updatedCount} nesnenin BoxCollider boyutu güncellendi.");
        }

        [MenuItem("Tools/Parkur/Secili Nesnelere Box Collider Ekle")]
        public static void AddBoxCollidersToSelection()
        {
            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                Debug.LogWarning("[Parkur] Lütfen Hierarchy ekranından en az bir nesne seçin.");
                return;
            }

            int count = 0;
            foreach (GameObject root in selectedObjects)
            {
                foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf == null || mf.sharedMesh == null) continue;
                    BoxCollider box = mf.gameObject.GetComponent<BoxCollider>();
                    if (box == null) box = Undo.AddComponent<BoxCollider>(mf.gameObject);
                    box.center = mf.sharedMesh.bounds.center;
                    box.size = mf.sharedMesh.bounds.size;
                    count++;
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Parkur] Seçili nesnelerdeki {count} parçaya BoxCollider eklendi.");
        }

        [MenuItem("Tools/Parkur/4. Cark ve Trambolini Otomatik Bagla")]
        public static void SetupWheelAndTrampoline()
        {
            AssetDatabase.ImportAsset(LevelModelPath, ImportAssetOptions.ForceUpdate);
            ApplyWheelAndTrampolineInScene(true);
        }

        public static void EnsureParkurPrefabsHaveExactColliders(bool logSummary)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Parkur3D" });
            if (guids == null || guids.Length == 0) return;

            int updatedPrefabs = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path)) continue;

                    GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
                    if (prefabRoot == null) continue;

                    bool modified = false;
                    try
                    {
                        foreach (MeshFilter mf in prefabRoot.GetComponentsInChildren<MeshFilter>(true))
                        {
                            if (mf == null || mf.sharedMesh == null) continue;
                            if (ApplyColliderForMesh(mf.gameObject, mf.sharedMesh, false, out _))
                                modified = true;
                        }

                        if (modified)
                        {
                            PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                            updatedPrefabs++;
                        }
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(prefabRoot);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (updatedPrefabs > 0)
            {
                AssetDatabase.SaveAssets();
                if (logSummary)
                    Debug.Log($"[Parkur] {updatedPrefabs} adet Parkur3D prefabının collider'ı geometriye birebir oturtuldu (MeshCollider/BoxCollider).");
            }
        }

        public static bool ApplyColliderForMesh(GameObject go, Mesh mesh, bool recordUndo, out bool addedNew)
        {
            addedNew = false;
            if (go == null || mesh == null) return false;

            bool wantsMesh = LevelBoxColliderPostprocessor.ShouldUseMeshCollider(go.name, mesh);
            bool wantsConvex = LevelBoxColliderPostprocessor.IsConvexRotatingColliderName(go.name);
            Collider existingCol = go.GetComponent<Collider>();
            bool changed = false;

            if (wantsMesh)
            {
                bool wasTrigger = existingCol != null && existingCol.isTrigger;
                if (existingCol is BoxCollider oldBox)
                {
                    if (recordUndo) Undo.DestroyObjectImmediate(oldBox);
                    else Object.DestroyImmediate(oldBox);
                    existingCol = null;
                    changed = true;
                }

                MeshCollider mc = existingCol as MeshCollider;
                if (mc == null)
                {
                    mc = recordUndo ? Undo.AddComponent<MeshCollider>(go) : go.AddComponent<MeshCollider>();
                    addedNew = true;
                    changed = true;
                }
                if (mc != null)
                {
                    if (wantsConvex && !mc.convex)
                    {
                        mc.convex = true;
                        changed = true;
                    }
                    if (wasTrigger && !mc.isTrigger)
                    {
                        mc.isTrigger = true;
                        changed = true;
                    }
                }
                return changed;
            }

            BoxCollider box = existingCol as BoxCollider;
            if (existingCol == null)
            {
                box = recordUndo ? Undo.AddComponent<BoxCollider>(go) : go.AddComponent<BoxCollider>();
                addedNew = true;
                changed = true;
            }
            if (box != null)
            {
                Vector3 targetCenter = mesh.bounds.center;
                Vector3 targetSize = new Vector3(
                    Mathf.Max(0.002f, mesh.bounds.size.x),
                    Mathf.Max(0.002f, mesh.bounds.size.y),
                    Mathf.Max(0.002f, mesh.bounds.size.z));
                if ((box.center - targetCenter).sqrMagnitude > 1e-6f || (box.size - targetSize).sqrMagnitude > 1e-6f)
                {
                    box.center = targetCenter;
                    box.size = targetSize;
                    changed = true;
                }
            }
            return changed;
        }

        static void EnsureConvexMeshCollider(GameObject go, ref bool dirty)
        {
            BoxCollider oldBox = go.GetComponent<BoxCollider>();
            if (oldBox != null) { Object.DestroyImmediate(oldBox); dirty = true; }
            MeshCollider mc = go.GetComponent<MeshCollider>();
            if (mc == null)
            {
                mc = Undo.AddComponent<MeshCollider>(go);
                dirty = true;
            }
            if (mc != null && !mc.convex)
            {
                mc.convex = true;
                EditorUtility.SetDirty(mc);
                dirty = true;
            }
        }

        static void RemoveExtra<T>(GameObject go, ref bool dirty) where T : Component
        {
            var comps = go.GetComponents<T>();
            for (int i = comps.Length - 1; i >= 1; i--)
            {
                try { Object.DestroyImmediate(comps[i]); dirty = true; }
                catch (System.Exception) { /* prefab'a ait bileşen, yeniden import ile temizlenecek */ }
            }
        }

        public static void ApplyWheelAndTrampolineInScene(bool logMessages)
        {
            Transform wheelOuter = null;
            List<Transform> wheelArms = new List<Transform>();
            List<Transform> cylinders = new List<Transform>();
            List<Transform> others = new List<Transform>();
            Transform trampoline = null;

            foreach (Transform t in Object.FindObjectsByType<Transform>())
            {
                if (t == null) continue;
                string n = t.name;

                if (n.StartsWith("OnSag_Cark_Dis_Cember")) wheelOuter = t;
                else if (n.StartsWith("OnSag_Cark_Kol")) wheelArms.Add(t);
                else if (LevelBoxColliderPostprocessor.IsRotatingCylinderName(n)) cylinders.Add(t);
                else if (n.StartsWith("Merkez_Trambolin_Yuzeyi")) trampoline = t;
                else others.Add(t);
            }

            bool hasLegacyLevel = GameObject.Find("level-one") != null;
            bool dirty = false;

            if (hasLegacyLevel)
            {
                // Yanlışlıkla başka nesnelere (örn. 'doner' parent'ı, kollar) takılmış döndürücüleri temizle
                foreach (Transform t in wheelArms.ToArray())
                {
                    var r = t.GetComponent<ContinuousRotator>();
                    if (r != null) { Object.DestroyImmediate(r); dirty = true; }
                }
                foreach (Transform t in others)
                {
                    var r = t.GetComponent<ContinuousRotator>();
                    if (r != null && t.GetComponentInParent<PlayerController>() == null) { Object.DestroyImmediate(r); dirty = true; }
                    var c = t.GetComponent<RotatingCylinderObstacle>();
                    if (c != null) { Object.DestroyImmediate(c); dirty = true; }
                    if (t.name == "doner" && t.GetComponent<MeshCollider>() != null && t.GetComponent<MeshFilter>() == null)
                    {
                        Object.DestroyImmediate(t.GetComponent<MeshCollider>()); dirty = true;
                    }
                }
            }

            // 1. Dış çember ve 4 kolu
            if (wheelOuter != null)
            {
                RemoveExtra<ContinuousRotator>(wheelOuter.gameObject, ref dirty);
                bool wheelChanged = false;
                var rotator = wheelOuter.GetComponent<ContinuousRotator>();
                if (rotator == null)
                {
                    rotator = Undo.AddComponent<ContinuousRotator>(wheelOuter.gameObject);
                    wheelChanged = true;
                }
                if (rotator.configVersion < 3)
                {
                    rotator.rotationAxis = ContinuousRotator.Axis.Y_YatayDonus; // Blender Z = Unity Y
                    rotator.configVersion = 3;
                    wheelChanged = true;
                }
                if (rotator.followedArms == null || rotator.followedArms.Count != wheelArms.Count)
                {
                    rotator.SetFollowedParts(wheelArms);
                    wheelChanged = true;
                }
                else
                {
                    for (int i = 0; i < wheelArms.Count; i++)
                    {
                        if (rotator.followedArms[i] != wheelArms[i])
                        {
                            rotator.SetFollowedParts(wheelArms);
                            wheelChanged = true;
                            break;
                        }
                    }
                }
                if (wheelChanged)
                {
                    EditorUtility.SetDirty(rotator);
                    dirty = true;
                }
            }

            // 2. Döner silindir parçaları
            foreach (Transform cyl in cylinders)
            {
                if (cyl == null) continue;

                EnsureConvexMeshCollider(cyl.gameObject, ref dirty);

                ContinuousRotator wrongRot = cyl.GetComponent<ContinuousRotator>();
                if (wrongRot != null) { Object.DestroyImmediate(wrongRot); dirty = true; }

                RemoveExtra<RotatingCylinderObstacle>(cyl.gameObject, ref dirty);
                bool cylChanged = false;
                var cylRot = cyl.GetComponent<RotatingCylinderObstacle>();
                if (cylRot == null)
                {
                    cylRot = Undo.AddComponent<RotatingCylinderObstacle>(cyl.gameObject);
                    cylRot.rotationSpeed = 35f;
                    cylChanged = true;
                }
                if (cylRot.configVersion < 3)
                {
                    cylRot.rotationAxis = RotatingCylinderObstacle.Axis.Y_YatayDonus; // Blender Z = Unity Y
                    cylRot.configVersion = 3;
                    cylChanged = true;
                }
                if (cylChanged)
                {
                    EditorUtility.SetDirty(cylRot);
                    dirty = true;
                }
            }

            // 3. Trambolin
            if (trampoline != null)
            {
                EnsureConvexMeshCollider(trampoline.gameObject, ref dirty);

                RemoveExtra<TrampolinePad>(trampoline.gameObject, ref dirty);
                var pad = trampoline.GetComponent<TrampolinePad>();
                if (pad == null)
                {
                    pad = Undo.AddComponent<TrampolinePad>(trampoline.gameObject);
                    dirty = true;
                }
                if (pad != null && pad.configVersion < TrampolinePad.LatestConfigVersion)
                {
                    pad.ApplyConfigMigration();
                    EditorUtility.SetDirty(pad);
                    dirty = true;
                }
            }

            // 4. Oyuncu hareket/zıplama ayarları ve Foot IK kurulumu
            foreach (PlayerController pc in Object.FindObjectsByType<PlayerController>())
            {
                if (pc == null || EditorUtility.IsPersistent(pc.gameObject)) continue;
                if (pc.configVersion < PlayerController.LatestConfigVersion)
                {
                    pc.ApplyConfigMigration();
                    EditorUtility.SetDirty(pc);
                    CharacterController migratedCc = pc.GetComponent<CharacterController>();
                    if (migratedCc != null)
                        EditorUtility.SetDirty(migratedCc);
                    dirty = true;
                }
                Animator anim = pc.animator != null ? pc.animator : pc.GetComponentInChildren<Animator>();
                if (anim != null && !EditorUtility.IsPersistent(anim.gameObject))
                {
                    if (anim.GetComponent<FootIK>() == null)
                    {
                        Undo.AddComponent<FootIK>(anim.gameObject);
                        dirty = true;
                    }
                    CharacterController cc = pc.GetComponent<CharacterController>();
                    if (cc != null && anim.transform.parent == pc.transform)
                    {
                        float bottomLocalY = cc.center.y - (cc.height * 0.5f) - cc.skinWidth;
                        Vector3 lp = anim.transform.localPosition;
                        if (Mathf.Abs(lp.y - bottomLocalY) > 0.01f)
                        {
                            lp.y = bottomLocalY;
                            anim.transform.localPosition = lp;
                            EditorUtility.SetDirty(anim.transform);
                            dirty = true;
                        }
                    }
                }
            }

            foreach (ThirdPersonCamera camCtrl in Object.FindObjectsByType<ThirdPersonCamera>())
            {
                if (camCtrl == null || EditorUtility.IsPersistent(camCtrl.gameObject)) continue;
                if (camCtrl.configVersion < ThirdPersonCamera.LatestConfigVersion)
                {
                    camCtrl.ApplyConfigMigration();
                    EditorUtility.SetDirty(camCtrl);
                    dirty = true;
                }
            }

            if (dirty)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            if (logMessages)
                Debug.Log($"[Parkur] Çark ve {cylinders.Count} adet silindir parçası hazırlandı.");
        }
    }
}
