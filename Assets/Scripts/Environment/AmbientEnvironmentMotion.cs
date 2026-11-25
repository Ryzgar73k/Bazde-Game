using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sahne başına tek örnek olarak çalışan hafif doğa animatörü:
/// gökyüzü bulutlarının kayması, dağ kartallarının süzülmesi ve alt göl/nehir/sis yüzeyinin hareketi.
/// </summary>
public class AmbientEnvironmentMotion : MonoBehaviour
{
    [Serializable]
    public struct EagleOrbit
    {
        public Transform transform;
        public Vector3 center;
        public float radius;
        public float angularSpeed;
        public float bobAmplitude;
        public float phase;
    }

    [Header("Süzülen Bulutlar")]
    public List<Transform> clouds = new List<Transform>();
    public Vector3 cloudDriftVelocity = new Vector3(1.8f, 0f, 0.55f);
    public Vector3 wrapCenter = Vector3.zero;
    public Vector2 wrapHalfExtents = new Vector2(260f, 260f);

    [Header("Dağ Kartalları")]
    public List<EagleOrbit> eagles = new List<EagleOrbit>();

    [Header("Alt Yüzey (Göl / Nehir / Sis / Bulut Denizi)")]
    public Renderer animatedSurfaceRenderer;
    public Vector2 uvScrollSpeed = new Vector2(0.012f, 0.025f);
    public float verticalBobAmplitude = 0.08f;
    public float verticalBobSpeed = 0.9f;

    private Material surfaceMaterialInstance;
    private Vector3 surfaceBasePosition;
    private bool hasSurfaceBasePos;
    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
    private Vector4 baseMapSt = new Vector4(1f, 1f, 0f, 0f);

    void Start()
    {
        if (animatedSurfaceRenderer != null)
        {
            surfaceBasePosition = animatedSurfaceRenderer.transform.position;
            hasSurfaceBasePos = true;

            // Paylaşımlı .mat varlığını sahnede kalıcı değiştirmemek için runtime kopya al
            surfaceMaterialInstance = animatedSurfaceRenderer.material;
            if (surfaceMaterialInstance != null && surfaceMaterialInstance.HasProperty(BaseMapStId))
            {
                baseMapSt = surfaceMaterialInstance.GetVector(BaseMapStId);
            }
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        UpdateClouds(dt);
        UpdateEagles(Time.time);
        UpdateSurface(Time.time);
    }

    private void UpdateClouds(float dt)
    {
        if (clouds == null || clouds.Count == 0) return;

        float minX = wrapCenter.x - wrapHalfExtents.x;
        float maxX = wrapCenter.x + wrapHalfExtents.x;
        float minZ = wrapCenter.z - wrapHalfExtents.y;
        float maxZ = wrapCenter.z + wrapHalfExtents.y;
        float spanX = Mathf.Max(10f, maxX - minX);
        float spanZ = Mathf.Max(10f, maxZ - minZ);

        for (int i = 0; i < clouds.Count; i++)
        {
            Transform c = clouds[i];
            if (c == null) continue;

            Vector3 p = c.position + cloudDriftVelocity * dt;
            if (p.x > maxX) p.x -= spanX;
            else if (p.x < minX) p.x += spanX;
            if (p.z > maxZ) p.z -= spanZ;
            else if (p.z < minZ) p.z += spanZ;
            c.position = p;
        }
    }

    private void UpdateEagles(float time)
    {
        if (eagles == null || eagles.Count == 0) return;

        for (int i = 0; i < eagles.Count; i++)
        {
            EagleOrbit e = eagles[i];
            if (e.transform == null) continue;

            float angle = e.phase + time * e.angularSpeed;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            Vector3 pos = e.center + new Vector3(
                cos * e.radius,
                Mathf.Sin(time * 1.15f + e.phase) * e.bobAmplitude,
                sin * e.radius);

            Vector3 tangent = new Vector3(-sin, 0f, cos) * Mathf.Sign(e.angularSpeed);
            e.transform.position = pos;
            if (tangent.sqrMagnitude > 0.0001f)
            {
                Quaternion look = Quaternion.LookRotation(tangent.normalized, Vector3.up);
                float bankAngle = -Mathf.Sign(e.angularSpeed) * 14f;
                e.transform.rotation = look * Quaternion.Euler(0f, 0f, bankAngle);
            }
        }
    }

    private void UpdateSurface(float time)
    {
        if (animatedSurfaceRenderer == null) return;

        if (hasSurfaceBasePos && verticalBobAmplitude > 0.0001f)
        {
            float y = surfaceBasePosition.y + Mathf.Sin(time * verticalBobSpeed) * verticalBobAmplitude;
            animatedSurfaceRenderer.transform.position = new Vector3(surfaceBasePosition.x, y, surfaceBasePosition.z);
        }

        if (surfaceMaterialInstance != null && uvScrollSpeed.sqrMagnitude > 0.000001f)
        {
            Vector2 offset = new Vector2(
                Mathf.Repeat(time * uvScrollSpeed.x, 1f),
                Mathf.Repeat(time * uvScrollSpeed.y, 1f));

            if (surfaceMaterialInstance.HasProperty("_BaseMap"))
                surfaceMaterialInstance.SetTextureOffset("_BaseMap", offset);
            else if (surfaceMaterialInstance.HasProperty(BaseMapStId))
                surfaceMaterialInstance.SetVector(BaseMapStId, new Vector4(baseMapSt.x, baseMapSt.y, offset.x, offset.y));
        }
    }
}
