using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Döner şişme silindir parkuru (Sag_Dev_Sisme_Silindir_Parca_*). Çark kodundan bağımsızdır.
/// Aynı gruptaki parçalar (aynı parent + aynı ".001" soneki) oyun başında ölçeği 1 olan ortak bir
/// pivotun altına alınır ve grup tek parça gibi döner. Her grup kendi merkezinde döner.
/// </summary>
public class RotatingCylinderObstacle : MonoBehaviour
{
    public enum Axis
    {
        Otomatik_SilindirUzunlugu, // eski sürümlerle uyumluluk için duruyor, Y gibi davranır
        Y_YatayDonus,              // Dünya Y (yukarı) ekseni = Blender'daki Z ekseni
        Z_DikeyTekerlek,
        X_TaklaEksen,
        OzelEksen
    }

    [Header("Silindir Dönüş Ayarları")]
    [Tooltip("Y_YatayDonus = Blender'daki Z ekseni (yukarı eksen). Dünya eksenine göre döner.")]
    public Axis rotationAxis = Axis.Y_YatayDonus;

    [Tooltip("Saniyedeki dönüş hızı (Derece). Ters yöne dönmesi için eksi (-) değer verin.")]
    public float rotationSpeed = 35f;

    [HideInInspector] public Space rotationSpace = Space.World;

    [Tooltip("Sadece 'OzelEksen' seçiliyse kullanılır (dünya ekseni)")]
    public Vector3 customAxis = Vector3.up;

    [Header("Oyuncu Kaydırma Etkisi")]
    [Range(0f, 2f)]
    public float playerPushMultiplier = 1f;

    [HideInInspector] public int configVersion = 0;

    static readonly HashSet<Transform> handled = new HashSet<Transform>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { handled.Clear(); }

    Transform pivot;
    RotatingCylinderObstacle leader;

    RotatingCylinderObstacle Leader { get { return leader != null ? leader : this; } }

    public Vector3 CurrentPivotWorld
    {
        get { return Leader.pivot != null ? Leader.pivot.position : transform.position; }
    }

    public Vector3 CurrentWorldAxis
    {
        get
        {
            var l = Leader;
            switch (l.rotationAxis)
            {
                case Axis.Z_DikeyTekerlek: return Vector3.forward;
                case Axis.X_TaklaEksen: return Vector3.right;
                case Axis.OzelEksen: return l.customAxis.sqrMagnitude > 0.0001f ? l.customAxis.normalized : Vector3.up;
                default: return Vector3.up;
            }
        }
    }

    static string GroupKey(Transform t)
    {
        int dot = t.name.IndexOf('.');
        string suffix = dot >= 0 ? t.name.Substring(dot) : "";
        return (t.parent != null ? t.parent.GetHashCode() : 0) + "|" + suffix;
    }

    void Awake()
    {
        if (handled.Contains(transform)) return; // başka bir parça grubu zaten kurdu

        string key = GroupKey(transform);
        var members = new List<RotatingCylinderObstacle>();
        foreach (var o in FindObjectsByType<RotatingCylinderObstacle>(FindObjectsSortMode.None))
        {
            if (!handled.Contains(o.transform) && GroupKey(o.transform) == key) members.Add(o);
        }

        bool has = false;
        Bounds b = new Bounds(transform.position, Vector3.zero);
        foreach (var m in members)
        {
            foreach (Renderer r in m.GetComponentsInChildren<Renderer>())
            {
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
        }

        int dot = key.IndexOf('.');
        pivot = new GameObject("Pivot_Silindir_" + (dot >= 0 ? key.Substring(dot) : "ana")).transform;
        pivot.position = has ? b.center : transform.position;
        pivot.rotation = Quaternion.identity;
        pivot.localScale = Vector3.one;
        pivot.gameObject.AddComponent<RotatorPivotMarker>().cylinder = this;

        foreach (var m in members)
        {
            handled.Add(m.transform);
            m.transform.SetParent(pivot, true);
            if (m != this) m.leader = this;
        }
        leader = this;
    }

    void Update()
    {
        if (leader != this || pivot == null) return; // sadece grup lideri pivotu döndürür
        pivot.Rotate(CurrentWorldAxis, rotationSpeed * Time.deltaTime, Space.World);
    }

    public Vector3 CalculatePlayerDisplacement(Vector3 playerFootPosition, float dt)
    {
        var l = Leader;
        if (l.pivot == null || l.playerPushMultiplier <= 0.001f) return Vector3.zero;
        Vector3 r = playerFootPosition - l.pivot.position;
        Vector3 rotated = Quaternion.AngleAxis(l.rotationSpeed * dt * l.playerPushMultiplier, CurrentWorldAxis) * r;
        Vector3 d = rotated - r;
        d.y = 0f;
        return d;
    }
}

