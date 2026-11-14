using UnityEngine;

/// <summary>
/// Dönen grupların (çark veya silindir) ortak pivot nesnesinin üzerinde durur.
/// Oyuncunun hangi dönen yüzeyin üstünde olduğunu bulmak için kullanılır.
/// </summary>
public class RotatorPivotMarker : MonoBehaviour
{
    public ContinuousRotator wheel;
    public RotatingCylinderObstacle cylinder;
}
