using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Sadece Dış Çember (OnSag_Cark_Dis_Cember) ve 4 kolu için döndürücü.
/// Oyun başında çember + kollar, ölçeği 1 olan ortak bir pivot nesnesinin altına alınır ve yalnızca
/// o pivot döndürülür. Böylece eşit olmayan ölçekli (non-uniform) nesnelerde şekil bozulması olmaz
/// ve çember ile kollar tek parça gibi döner.
/// </summary>
public class ContinuousRotator : MonoBehaviour
{
    public enum Axis
    {
        Y_YatayDonus,       // Dünya Y (yukarı) ekseni = Blender'daki Z ekseni (düz yatay dönüş)
        Z_DikeyTekerlek,
        X_TaklaEksen,
        OzelEksen
    }

    [Header("Çark Dönüş Ayarları")]
    [Tooltip("Y_YatayDonus = Blender'daki Z ekseni (yukarı eksen). Dünya eksenine göre döner.")]
    public Axis rotationAxis = Axis.Y_YatayDonus;

    [Tooltip("Saniyedeki dönüş hızı (Derece). Ters yöne dönmesi için eksi (-) değer verin.")]
    public float rotationSpeed = 90f;

    [HideInInspector] public Space rotationSpace = Space.World;

    [Tooltip("Sadece 'OzelEksen' seçiliyse kullanılır (dünya ekseni)")]
    public Vector3 customAxis = Vector3.up;

    [Header("Çemberle Birlikte Dönecek Parçalar (4 Kol)")]
    public List<Transform> followedArms = new List<Transform>();

    [HideInInspector] public int configVersion = 0;

    Transform pivot;

    public Vector3 CurrentPivotWorld
    {
        get { return pivot != null ? pivot.position : transform.position; }
    }

    public Vector3 CurrentWorldAxis
    {
        get
        {
            switch (rotationAxis)
            {
                case Axis.Y_YatayDonus: return Vector3.up;
                case Axis.Z_DikeyTekerlek: return Vector3.forward;
                case Axis.X_TaklaEksen: return Vector3.right;
                default: return customAxis.sqrMagnitude > 0.0001f ? customAxis.normalized : Vector3.up;
            }
        }
    }

    public void SetFollowedParts(List<Transform> arms)
    {
        followedArms = new List<Transform>(arms);
    }

    void Awake()
    {
        // Sadece dış çemberde çalış; yanlışlıkla başka nesneye takılmışsa kapat
        if (!name.StartsWith("OnSag_Cark_Dis_Cember")) { enabled = false; return; }

        // Aynı nesnede çift bileşen varsa yalnızca ilki çalışsın
        var all = GetComponents<ContinuousRotator>();
        if (all.Length > 1 && all[0] != this) { enabled = false; return; }

        // Kollar listede yoksa sahneden bul
        followedArms.RemoveAll(a => a == null);
        if (followedArms.Count == 0)
        {
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t != transform && t.name.StartsWith("OnSag_Cark_Kol")) followedArms.Add(t);
            }
        }

        // Çember + kolların ortak merkezi
        bool has = false;
        Bounds b = new Bounds(transform.position, Vector3.zero);
        var parts = new List<Transform> { transform };
        parts.AddRange(followedArms);
        foreach (Transform p in parts)
        {
            foreach (Renderer r in p.GetComponentsInChildren<Renderer>())
            {
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
        }

        pivot = new GameObject("Pivot_Cark").transform;
        pivot.position = has ? b.center : transform.position;
        pivot.rotation = Quaternion.identity;
        pivot.localScale = Vector3.one;
        pivot.gameObject.AddComponent<RotatorPivotMarker>().wheel = this;

        foreach (Transform p in parts) p.SetParent(pivot, true);
    }

    void Update()
    {
        if (pivot == null) return;
        pivot.Rotate(CurrentWorldAxis, rotationSpeed * Time.deltaTime, Space.World);
    }

    /// <summary>Bu karede üstteki oyuncuya uygulanacak (yatay) kayma vektörü.</summary>
    public Vector3 CalculatePlayerDisplacement(Vector3 playerFootPosition, float dt)
    {
        if (pivot == null) return Vector3.zero;
        Vector3 r = playerFootPosition - pivot.position;
        Vector3 rotated = Quaternion.AngleAxis(rotationSpeed * dt, CurrentWorldAxis) * r;
        Vector3 d = rotated - r;
        d.y = 0f;
        return d;
    }
}
