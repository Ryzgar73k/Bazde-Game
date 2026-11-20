using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Hafif parkur HUD'u: süre, aktif checkpoint, toplanan yıldız/elmas ve kısa bildirim banner'ı.
/// </summary>
public class LevelHUD : MonoBehaviour
{
    public static LevelHUD Instance { get; private set; }

    public string levelLabel = "LEVEL 4";

    public int CollectedStars { get; private set; }
    public int TotalStars { get; private set; }
    public int CollectedDiamonds { get; private set; }
    public int TotalDiamonds { get; private set; }

    private int totalCheckpoints;
    private int activeCheckpointIndex;
    private float elapsedTime;

    private GameObject canvasGO;
    private Text timerText;
    private Text checkpointText;
    private Text collectibleText;
    private Text toastText;
    private CanvasGroup toastGroup;
    private Coroutine toastRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        var items = FindObjectsByType<CollectibleItem>();
        foreach (var item in items)
        {
            if (item.itemType == CollectibleItem.CollectibleType.Diamond) TotalDiamonds++;
            else TotalStars++;
        }

        totalCheckpoints = FindObjectsByType<Checkpoint>().Length;

        if (RespawnManager.Instance != null)
            RespawnManager.Instance.OnCheckpointChanged += HandleCheckpointChanged;

        if (string.IsNullOrEmpty(levelLabel))
            levelLabel = SceneManager.GetActiveScene().name.ToUpperInvariant();

        BuildUI();
        RefreshText();
    }

    void OnDestroy()
    {
        if (RespawnManager.Instance != null)
            RespawnManager.Instance.OnCheckpointChanged -= HandleCheckpointChanged;
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (VictoryUI.IsAnyVictoryVisible)
        {
            if (canvasGO != null && canvasGO.activeSelf) canvasGO.SetActive(false);
            return;
        }

        elapsedTime += Time.deltaTime;
        if (timerText != null)
        {
            int min = Mathf.FloorToInt(elapsedTime / 60f);
            float sec = elapsedTime % 60f;
            timerText.text = string.Format("{0:00}:{1:00.0}", min, sec);
        }
    }

    public void NotifyCollected(CollectibleItem item)
    {
        if (item == null) return;
        if (item.itemType == CollectibleItem.CollectibleType.Diamond)
        {
            CollectedDiamonds++;
            ShowToast($"ELMAS BULUNDU! ({CollectedDiamonds}/{TotalDiamonds})");
        }
        else
        {
            CollectedStars++;
        }
        RefreshText();
    }

    private void HandleCheckpointChanged(Checkpoint cp)
    {
        if (cp == null) return;
        activeCheckpointIndex = Mathf.Max(activeCheckpointIndex, cp.checkpointIndex);
        RefreshText();
        ShowToast($"CHECKPOINT {cp.checkpointIndex} ALINDI!");
    }

    public void ShowToast(string message, float duration = 2.0f)
    {
        if (toastText == null || toastGroup == null) return;
        toastText.text = message;
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastRoutine(duration));
    }

    private IEnumerator ToastRoutine(float duration)
    {
        toastGroup.alpha = 1f;
        yield return new WaitForSeconds(Mathf.Max(0.4f, duration - 0.35f));
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            toastGroup.alpha = 1f - Mathf.Clamp01(t / 0.35f);
            yield return null;
        }
        toastGroup.alpha = 0f;
    }

    private void RefreshText()
    {
        if (checkpointText != null)
            checkpointText.text = $"CP {activeCheckpointIndex}/{totalCheckpoints}";

        if (collectibleText != null)
            collectibleText.text = $"★ {CollectedStars}/{TotalStars}   ◆ {CollectedDiamonds}/{TotalDiamonds}";
    }

    private void BuildUI()
    {
        canvasGO = new GameObject("LevelHUDCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Sol üst kompakt bilgi paneli (MobileControls ile çakışmaz)
        var panel = CreateRect("HudPanel", canvasGO.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(460, 104), new Vector2(250, -72));
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.11f, 0.18f, 0.72f);
        bg.raycastTarget = false;

        var titleRT = CreateRect("LevelLabel", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(210, 36), new Vector2(122, -26));
        CreateText(titleRT.gameObject, levelLabel, 24, new Color(0.95f, 0.85f, 0.25f), TextAnchor.MiddleLeft);

        var timeRT = CreateRect("Timer", panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(190, 36), new Vector2(-112, -26));
        timerText = CreateText(timeRT.gameObject, "00:00.0", 26, Color.white, TextAnchor.MiddleRight);

        var cpRT = CreateRect("Checkpoint", panel, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(160, 36), new Vector2(98, 28));
        checkpointText = CreateText(cpRT.gameObject, "CP 0/0", 22, new Color(0.35f, 0.95f, 0.55f), TextAnchor.MiddleLeft);

        var colRT = CreateRect("Collectibles", panel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(260, 36), new Vector2(-148, 28));
        collectibleText = CreateText(colRT.gameObject, "★ 0/0   ◆ 0/0", 22, new Color(0.65f, 0.88f, 1f), TextAnchor.MiddleRight);

        // Üst orta bildirim banner'ı
        var toastRT = CreateRect("Toast", canvasGO.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(560, 56), new Vector2(0, -64));
        var toastBg = toastRT.gameObject.AddComponent<Image>();
        toastBg.color = new Color(0.1f, 0.16f, 0.25f, 0.85f);
        toastBg.raycastTarget = false;
        toastGroup = toastRT.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f;

        var toastTxtRT = CreateRect("ToastLabel", toastRT, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        toastText = CreateText(toastTxtRT.gameObject, "", 26, new Color(0.35f, 1f, 0.55f), TextAnchor.MiddleCenter);
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        if (aMin == aMax || size != Vector2.zero)
        {
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }
        else
        {
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        return rt;
    }

    private static Text CreateText(GameObject go, string content, int fontSize, Color col, TextAnchor align)
    {
        var txt = go.AddComponent<Text>();
        txt.text = content;
        txt.fontSize = fontSize;
        txt.color = col;
        txt.alignment = align;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Font.CreateDynamicFontFromOSFont("Arial", fontSize);
        txt.raycastTarget = false;
        return txt;
    }
}
