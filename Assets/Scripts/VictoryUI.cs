using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Seviye tamamlandığında dinamik olarak oluşturulan UGUI Zafer Ekranı (Victory Screen).
/// Geçen süre, toplanabilir özeti (varsa LevelHUD), en iyi süre kaydı ve navigasyon butonları sunar.
/// </summary>
public class VictoryUI : MonoBehaviour
{
    public static VictoryUI Instance { get; private set; }
    public static bool IsAnyVictoryVisible => Instance != null && Instance.isVisible;

    [Header("Tema Renkleri")]
    public Color overlayColor = new Color(0.04f, 0.05f, 0.08f, 0.82f);
    public Color panelColor = new Color(0.12f, 0.14f, 0.22f, 0.98f);
    public Color goldColor = new Color(1f, 0.84f, 0.15f, 1f);
    public Color timerColor = new Color(0.2f, 0.95f, 0.5f, 1f);
    public Color nextButtonColor = new Color(0.15f, 0.75f, 0.35f, 1f);
    public Color restartButtonColor = new Color(0.18f, 0.52f, 0.92f, 1f);
    public Color menuButtonColor = new Color(0.4f, 0.45f, 0.55f, 1f);

    public AudioClip buttonClickSound;

    private GameObject canvasGO;
    private CanvasGroup canvasGroup;
    private RectTransform cardTransform;
    private Text subtitleText;
    private Text timerText;
    private Text recordText;
    private Text nextButtonText;

    private string cachedNextScene = "";
    private string cachedMenuScene = "Level1";
    private bool isVisible = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ShowVictory(float completionTime, string nextScene = "", string menuScene = "Level1")
    {
        if (isVisible) return;
        isVisible = true;

        cachedNextScene = nextScene;
        cachedMenuScene = string.IsNullOrEmpty(menuScene) ? "Level1" : menuScene;

        var mobileControls = Object.FindAnyObjectByType<MobileControlsUI>();
        if (mobileControls != null)
            mobileControls.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (canvasGO == null)
            BuildUI();

        string activeSceneName = SceneManager.GetActiveScene().name;
        if (activeSceneName.Equals("Level1", System.StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(cachedNextScene)) cachedNextScene = "Level2";
            if (nextButtonText != null) nextButtonText.text = "SONRAKİ SEVİYE (SEVİYE 2)";
        }
        else if (activeSceneName.Equals("Level2", System.StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(cachedNextScene)) cachedNextScene = "Level3";
            if (nextButtonText != null) nextButtonText.text = "SONRAKİ SEVİYE (SEVİYE 3)";
        }
        else if (activeSceneName.Equals("Level3", System.StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(cachedNextScene)) cachedNextScene = "Level4";
            if (nextButtonText != null) nextButtonText.text = "SONRAKİ SEVİYE (SEVİYE 4)";
        }
        else if (activeSceneName.Equals("Level4", System.StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(cachedNextScene)) cachedNextScene = cachedMenuScene;
            if (nextButtonText != null) nextButtonText.text = "BAŞA DÖN (SEVİYE 1)";
        }
        else
        {
            if (nextButtonText != null) nextButtonText.text = "SONRAKİ SEVİYE";
        }

        if (subtitleText != null)
        {
            var hud = LevelHUD.Instance;
            if (hud != null && (hud.TotalStars > 0 || hud.TotalDiamonds > 0))
            {
                subtitleText.text = $"SEVİYE TAMAMLANDI  •  ★ {hud.CollectedStars}/{hud.TotalStars}   ◆ {hud.CollectedDiamonds}/{hud.TotalDiamonds}";
            }
            else
            {
                subtitleText.text = "SEVİYE BAŞARIYLA TAMAMLANDI";
            }
        }

        canvasGO.SetActive(true);

        int minutes = Mathf.FloorToInt(completionTime / 60f);
        float seconds = completionTime % 60f;
        string formattedTime = string.Format("{0:00}:{1:00.00}", minutes, seconds);

        if (timerText != null)
            timerText.text = formattedTime;

        string prefKey = "BestTime_" + activeSceneName;
        float bestTime = PlayerPrefs.GetFloat(prefKey, float.MaxValue);
        bool isNewRecord = completionTime < bestTime;

        if (isNewRecord)
        {
            bestTime = completionTime;
            PlayerPrefs.SetFloat(prefKey, bestTime);
            PlayerPrefs.Save();
        }

        if (recordText != null)
        {
            if (isNewRecord)
            {
                recordText.text = "★ YENİ REKOR! ★";
                recordText.color = goldColor;
            }
            else
            {
                int bMin = Mathf.FloorToInt(bestTime / 60f);
                float bSec = bestTime % 60f;
                recordText.text = string.Format("En İyi Süre: {0:00}:{1:00.00}", bMin, bSec);
                recordText.color = new Color(0.75f, 0.8f, 0.88f, 1f);
            }
        }

        StartCoroutine(AnimateEntrance());
    }

    private void BuildUI()
    {
        var es = Object.FindAnyObjectByType<EventSystem>();
        if (es == null)
        {
            var esGO = new GameObject("EventSystem", typeof(EventSystem));
            es = esGO.GetComponent<EventSystem>();
        }

        var inputModule = es.GetComponent<InputSystemUIInputModule>();
        if (inputModule == null)
            inputModule = es.gameObject.AddComponent<InputSystemUIInputModule>();
        inputModule.AssignDefaultActions();

        canvasGO = new GameObject("VictoryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = canvasGO.AddComponent<CanvasGroup>();

        var bg = CreateRect("DarkOverlay", canvasGO.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.color = overlayColor;

        cardTransform = CreateRect("VictoryCard", canvasGO.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(760, 680), Vector2.zero);
        var cardImg = cardTransform.gameObject.AddComponent<Image>();
        cardImg.color = panelColor;
        cardImg.sprite = CreateRoundedRectSprite(48);
        cardImg.type = Image.Type.Sliced;

        var starsRT = CreateRect("Stars", cardTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(600, 50), new Vector2(0, -50));
        CreateText(starsRT.gameObject, "★  ★  ★", 40, goldColor, TextAnchor.MiddleCenter);

        var titleRT = CreateRect("Title", cardTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(650, 65), new Vector2(0, -110));
        CreateText(titleRT.gameObject, "TEBRİKLER!", 52, goldColor, TextAnchor.MiddleCenter);

        var subTitleRT = CreateRect("SubTitle", cardTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(680, 40), new Vector2(0, -165));
        subtitleText = CreateText(subTitleRT.gameObject, "SEVİYE BAŞARIYLA TAMAMLANDI", 22, new Color(0.85f, 0.9f, 0.98f), TextAnchor.MiddleCenter);

        var timeBoxRT = CreateRect("TimeBox", cardTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(560, 120), new Vector2(0, -255));
        var timeBoxImg = timeBoxRT.gameObject.AddComponent<Image>();
        timeBoxImg.color = new Color(0.08f, 0.1f, 0.15f, 0.95f);
        timeBoxImg.sprite = CreateRoundedRectSprite(28);
        timeBoxImg.type = Image.Type.Sliced;

        var timeLabelRT = CreateRect("TimeLabel", timeBoxRT, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(500, 30), new Vector2(0, -18));
        CreateText(timeLabelRT.gameObject, "TAMAMLANMA SÜRESİ", 18, new Color(0.6f, 0.65f, 0.75f), TextAnchor.MiddleCenter);

        var timeValRT = CreateRect("TimeValue", timeBoxRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(500, 50), new Vector2(0, -6));
        timerText = CreateText(timeValRT.gameObject, "00:00.00", 44, timerColor, TextAnchor.MiddleCenter);

        var recordRT = CreateRect("RecordLabel", timeBoxRT, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(500, 25), new Vector2(0, 15));
        recordText = CreateText(recordRT.gameObject, "En İyi: --:--.--", 18, goldColor, TextAnchor.MiddleCenter);

        float btnStartY = -395f;
        float btnSpacing = 80f;

        var nextBtnRT = CreateActionButton("NextButton", cardTransform, "SONRAKİ SEVİYE", new Vector2(0, btnStartY), nextButtonColor, OnNextLevelClicked);
        nextButtonText = nextBtnRT.GetComponentInChildren<Text>();

        CreateActionButton("RestartButton", cardTransform, "TEKRAR OYNA", new Vector2(0, btnStartY - btnSpacing), restartButtonColor, OnRestartClicked);
        CreateActionButton("MenuButton", cardTransform, "SEVİYE 1'E DÖN", new Vector2(0, btnStartY - btnSpacing * 2f), menuButtonColor, OnMainMenuClicked);
    }

    private RectTransform CreateActionButton(string name, Transform parent, string label, Vector2 pos, Color btnColor, UnityEngine.Events.UnityAction action)
    {
        var rt = CreateRect(name, parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(480, 68), pos);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = btnColor;
        img.sprite = CreateRoundedRectSprite(32);
        img.type = Image.Type.Sliced;

        var btn = rt.gameObject.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.selectedColor = Color.white;
        btn.colors = colors;

        btn.onClick.AddListener(() =>
        {
            if (buttonClickSound != null)
                AudioSource.PlayClipAtPoint(buttonClickSound, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            action?.Invoke();
        });

        var textRT = CreateRect("Text", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        CreateText(textRT.gameObject, label, 26, Color.white, TextAnchor.MiddleCenter);
        return rt;
    }

    private void OnRestartClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnNextLevelClicked()
    {
        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(cachedNextScene))
        {
            SceneManager.LoadScene(cachedNextScene);
            return;
        }

        string currentName = SceneManager.GetActiveScene().name;
        if (currentName.Equals("Level1", System.StringComparison.OrdinalIgnoreCase))
            SceneManager.LoadScene("Level2");
        else if (currentName.Equals("Level2", System.StringComparison.OrdinalIgnoreCase))
            SceneManager.LoadScene("Level3");
        else if (currentName.Equals("Level3", System.StringComparison.OrdinalIgnoreCase))
            SceneManager.LoadScene("Level4");
        else
            SceneManager.LoadScene(cachedMenuScene);
    }

    private void OnMainMenuClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(string.IsNullOrEmpty(cachedMenuScene) ? "Level1" : cachedMenuScene);
    }

    private IEnumerator AnimateEntrance()
    {
        canvasGroup.alpha = 0f;
        cardTransform.localScale = Vector3.one * 0.75f;

        float duration = 0.35f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = 1f - Mathf.Pow(1f - t, 3f);

            canvasGroup.alpha = ease;
            cardTransform.localScale = Vector3.LerpUnclamped(Vector3.one * 0.75f, Vector3.one, ease);

            yield return null;
        }

        canvasGroup.alpha = 1f;
        cardTransform.localScale = Vector3.one;
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

    private static Sprite roundedRectSprite;
    private static Sprite CreateRoundedRectSprite(int cornerRadius)
    {
        if (roundedRectSprite != null) return roundedRectSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float r = cornerRadius;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Abs(x - size * 0.5f) - (size * 0.5f - r));
                float dy = Mathf.Max(0f, Mathf.Abs(y - size * 0.5f) - (size * 0.5f - r));
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(r - dist + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        roundedRectSprite = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(r, r, r, r)
        );
        return roundedRectSprite;
    }
}
