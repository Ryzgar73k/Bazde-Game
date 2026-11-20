using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Oyuncu bastığında hedeflenen köprü veya kapıyı aktive eden yer düğmesi (KayKit button_base_green).
/// </summary>
[RequireComponent(typeof(Collider))]
public class PressureButton : MonoBehaviour
{
    [Header("Görsel ve Hedefler")]
    public Transform buttonCap;
    public Light indicatorLight;
    public float pressDepth = 0.09f;
    public string toastMessage = "KÖPRÜ AKTİF EDİLDİ!";
    public List<ActivatedBridge> targets = new List<ActivatedBridge>();

    public bool IsPressed { get; private set; }

    private Vector3 capRestPos;

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        if (buttonCap == null && transform.childCount > 0)
            buttonCap = transform.GetChild(0);

        if (buttonCap != null)
            capRestPos = buttonCap.localPosition;
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsPressed) return;

        var player = other.GetComponentInParent<PlayerController>();
        if (player == null && !other.CompareTag("Player")) return;

        Activate();
    }

    public void Activate()
    {
        if (IsPressed) return;
        IsPressed = true;

        if (buttonCap != null)
            buttonCap.localPosition = capRestPos - new Vector3(0f, pressDepth, 0f);

        if (indicatorLight != null)
        {
            indicatorLight.color = new Color(0.2f, 1f, 0.4f);
            indicatorLight.intensity = 3.2f;
        }

        foreach (var target in targets)
        {
            if (target != null) target.Activate();
        }

        if (LevelHUD.Instance != null && !string.IsNullOrEmpty(toastMessage))
            LevelHUD.Instance.ShowToast(toastMessage);
    }
}
