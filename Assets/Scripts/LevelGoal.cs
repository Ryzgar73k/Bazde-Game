using System.Collections;
using UnityEngine;

/// <summary>
/// Seviye bitiş çizgisi, bayrağı veya kapısına eklenen bileşen.
/// Oyuncu hedefe ulaştığında süreyi durdurur, oyuncuyu durdurur ve VictoryUI ekranını açar.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LevelGoal : MonoBehaviour
{
    public string playerTag = "Player";
    public bool stopPlayerOnFinish = true;
    public float uiDelay = 0.5f;

    [Header("Görsel ve Ses Efektleri")]
    public AudioClip victorySound;
    [Range(0f, 1f)]
    public float victorySoundVolume = 0.9f;
    public GameObject victoryVFX;
    public Transform vfxSpawnPoint;

    [Header("Sonraki Seviye Bilgileri")]
    public string nextSceneName = "";
    public string mainMenuSceneName = "Level1";

    public bool IsFinished { get; private set; }
    public float LevelElapsedTime { get; private set; }

    private bool timerRunning = true;
    private Collider goalCollider;

    void Awake()
    {
        goalCollider = GetComponent<Collider>();
        if (goalCollider != null)
            goalCollider.isTrigger = true;
    }

    void Start()
    {
        LevelElapsedTime = 0f;
        timerRunning = true;
        IsFinished = false;
    }

    void Update()
    {
        if (timerRunning)
            LevelElapsedTime += Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsFinished) return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null && other.CompareTag(playerTag))
            player = Object.FindAnyObjectByType<PlayerController>();

        if (player != null)
            CompleteLevel(player);
    }

    public void CompleteLevel(PlayerController player)
    {
        if (IsFinished) return;
        IsFinished = true;
        timerRunning = false;

        if (stopPlayerOnFinish && player != null)
        {
            player.canControl = false;
            player.ResetVelocity();
        }

        if (victorySound != null)
            AudioSource.PlayClipAtPoint(victorySound, transform.position, victorySoundVolume);

        if (victoryVFX != null)
        {
            Vector3 spawnPos = vfxSpawnPoint != null ? vfxSpawnPoint.position : transform.position;
            Quaternion spawnRot = vfxSpawnPoint != null ? vfxSpawnPoint.rotation : Quaternion.identity;
            Instantiate(victoryVFX, spawnPos, spawnRot);
        }

        StartCoroutine(ShowVictoryUIRoutine());
    }

    private IEnumerator ShowVictoryUIRoutine()
    {
        if (uiDelay > 0f)
            yield return new WaitForSeconds(uiDelay);

        VictoryUI ui = VictoryUI.Instance;
        if (ui == null)
        {
            var uiGO = new GameObject("VictoryUI");
            ui = uiGO.AddComponent<VictoryUI>();
        }

        ui.ShowVictory(LevelElapsedTime, nextSceneName, mainMenuSceneName);
    }

    void OnDrawGizmos()
    {
        if (goalCollider == null) goalCollider = GetComponent<Collider>();
        if (goalCollider == null) return;

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.4f);
        Gizmos.matrix = transform.localToWorldMatrix;

        if (goalCollider is BoxCollider box)
        {
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (goalCollider is SphereCollider sphere)
        {
            Gizmos.DrawSphere(sphere.center, sphere.radius);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }
    }
}
