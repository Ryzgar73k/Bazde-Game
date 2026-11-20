using UnityEngine;

/// <summary>
/// KayKit yıldız ve elmas toplanabilir öğesi.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CollectibleItem : MonoBehaviour
{
    public enum CollectibleType { Star, Diamond }

    public CollectibleType itemType = CollectibleType.Star;
    public float spinSpeed = 110f;
    public float bobAmplitude = 0.18f;
    public float bobFrequency = 2.4f;

    public bool IsCollected { get; private set; }

    private Vector3 basePosition;
    private float bobPhase;

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
        basePosition = transform.position;
        bobPhase = (basePosition.x + basePosition.z) * 0.35f;
    }

    void Update()
    {
        if (IsCollected) return;
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        float y = basePosition.y + Mathf.Sin(Time.time * bobFrequency + bobPhase) * bobAmplitude;
        transform.position = new Vector3(basePosition.x, y, basePosition.z);
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsCollected) return;

        var player = other.GetComponentInParent<PlayerController>();
        if (player == null && !other.CompareTag("Player")) return;

        IsCollected = true;
        if (LevelHUD.Instance != null)
            LevelHUD.Instance.NotifyCollected(this);

        gameObject.SetActive(false);
    }
}
