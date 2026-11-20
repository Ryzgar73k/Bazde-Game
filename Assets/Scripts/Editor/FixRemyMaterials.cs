using UnityEditor;
using UnityEngine;

/// <summary>
/// Remy karakterinin dokularını ve materyallerini FBX içinden dışarı çıkarıp URP Lit materyali yapar.
/// </summary>
public static class FixRemyMaterials
{
    [MenuItem("Tools/Parkur/Karakter Renklerini ve Materyallerini Düzelt")]
    public static void ExtractAndFixMaterials()
    {
        string fbxPath = "Assets/char.fbx";
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError("char.fbx bulunamadı!");
            return;
        }

        string matFolder = "Assets/Materials";
        string texFolder = "Assets/Textures";
        if (!AssetDatabase.IsValidFolder(matFolder)) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(texFolder)) AssetDatabase.CreateFolder("Assets", "Textures");

        // Dokuları dışarı çıkar
        importer.ExtractTextures(texFolder);

        // Materyal ayarlarını güncelle
        importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        importer.materialSearch = ModelImporterMaterialSearch.Everywhere;
        importer.SaveAndReimport();

        // Çıkarılan materyalleri URP/Lit shader'ına geçir
        string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
        var urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLitShader == null) urpLitShader = Shader.Find("Standard");

        int count = 0;
        foreach (var guid in matGuids)
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (mat != null && urpLitShader != null)
            {
                mat.shader = urpLitShader;
                EditorUtility.SetDirty(mat);
                count++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Karakter dokuları çıkarıldı ve {count} adet materyal renklendirildi!");
    }
}
