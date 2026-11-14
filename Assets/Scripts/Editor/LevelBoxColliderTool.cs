using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

namespace ParkourGame.Editor
{
    public class LevelBoxColliderPostprocessor : AssetPostprocessor
    {
        // Sürüm değişince FBX otomatik yeniden import edilir (eski hiyerarşi değişiklikleri temizlenir)
        public override uint GetVersion() { return 5; }

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

                if (IsRotatingCylinderName(go.name))
                {
                    go.AddComponent<MeshCollider>();
                }
                else
                {
                    BoxCollider box = go.AddComponent<BoxCollider>();
                    box.center = mf.sharedMesh.bounds.center;
                    box.size = mf.sharedMesh.bounds.size;
                }
            }
        }

        public static bool IsRotatingCylinderName(string objName)
        {
            if (string.IsNullOrEmpty(objName)) return false;
            return objName.IndexOf("Sisme_Silindir", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                   objName.IndexOf("Destek", System.StringComparison.OrdinalIgnoreCase) < 0;
        }
    }

    [InitializeOnLoad]
    public static class LevelAutoInitializer
    {
        static LevelAutoInitializer()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                LevelBoxColliderTool.ApplyWheelAndTrampolineInScene(false);
            };
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

            foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if (mf == null || mf.sharedMesh == null) continue;
                GameObject go = mf.gameObject;
                if (go.GetComponentInParent<PlayerController>() != null) continue;

                Collider existingCol = go.GetComponent<Collider>();
                if (existingCol == null)
                {
                    BoxCollider box = Undo.AddComponent<BoxCollider>(go);
                    box.center = mf.sharedMesh.bounds.center;
                    box.size = mf.sharedMesh.bounds.size;
                    addedCount++;
                }
                else if (existingCol is BoxCollider box)
                {
                    box.center = mf.sharedMesh.bounds.center;
                    box.size = mf.sharedMesh.bounds.size;
                    updatedCount++;
                }
            }

            ApplyWheelAndTrampolineInScene(true);

            foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
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

            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t == null) continue;
                string n = t.name;

                if (n.StartsWith("OnSag_Cark_Dis_Cember")) wheelOuter = t;
                else if (n.StartsWith("OnSag_Cark_Kol")) wheelArms.Add(t);
                else if (LevelBoxColliderPostprocessor.IsRotatingCylinderName(n)) cylinders.Add(t);
                else if (n.StartsWith("Merkez_Trambolin_Yuzeyi")) trampoline = t;
                else others.Add(t);
            }

            bool dirty = false;

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

            // 1. Dış çember ve 4 kolu
            if (wheelOuter != null)
            {
                RemoveExtra<ContinuousRotator>(wheelOuter.gameObject, ref dirty);
                var rotator = wheelOuter.GetComponent<ContinuousRotator>();
                if (rotator == null)
                {
                    rotator = Undo.AddComponent<ContinuousRotator>(wheelOuter.gameObject);
                    dirty = true;
                }
                if (rotator.configVersion < 3)
                {
                    rotator.rotationAxis = ContinuousRotator.Axis.Y_YatayDonus; // Blender Z = Unity Y
                    rotator.rotationSpace = Space.World;
                    rotator.configVersion = 3;
                    dirty = true;
                }
                rotator.SetFollowedParts(wheelArms);
                EditorUtility.SetDirty(rotator);
            }

            // 2. Döner silindir parçaları
            foreach (Transform cyl in cylinders)
            {
                if (cyl == null) continue;

                BoxCollider oldBox = cyl.GetComponent<BoxCollider>();
                if (oldBox != null) { Object.DestroyImmediate(oldBox); dirty = true; }
                if (cyl.GetComponent<MeshCollider>() == null)
                {
                    Undo.AddComponent<MeshCollider>(cyl.gameObject);
                    dirty = true;
                }

                ContinuousRotator wrongRot = cyl.GetComponent<ContinuousRotator>();
                if (wrongRot != null) { Object.DestroyImmediate(wrongRot); dirty = true; }

                RemoveExtra<RotatingCylinderObstacle>(cyl.gameObject, ref dirty);
                var cylRot = cyl.GetComponent<RotatingCylinderObstacle>();
                if (cylRot == null)
                {
                    cylRot = Undo.AddComponent<RotatingCylinderObstacle>(cyl.gameObject);
                    cylRot.rotationSpeed = 35f;
                    dirty = true;
                }
                if (cylRot.configVersion < 3)
                {
                    cylRot.rotationAxis = RotatingCylinderObstacle.Axis.Y_YatayDonus; // Blender Z = Unity Y
                    cylRot.rotationSpace = Space.World;
                    cylRot.configVersion = 3;
                    dirty = true;
                }
                EditorUtility.SetDirty(cylRot);
            }

            // 3. Trambolin
            if (trampoline != null)
            {
                RemoveExtra<TrampolinePad>(trampoline.gameObject, ref dirty);
                if (trampoline.GetComponent<TrampolinePad>() == null)
                {
                    Undo.AddComponent<TrampolinePad>(trampoline.gameObject);
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
