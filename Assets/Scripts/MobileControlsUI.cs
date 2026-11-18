using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Çalışma anında mobil kontrolleri oluşturur: sol sanal joystick, sağda Zıpla ve Koş butonları,
/// ekranın geri kalan sağ yarısında kamera döndürme. Değerleri PlayerInputState'e yazar.
/// Sahneye boş bir GameObject'e eklemek yeterli.
/// </summary>
public class MobileControlsUI : MonoBehaviour
{
    public bool showOnDesktop = true; // Editörde test için açık bırak

    void Start()
    {
        if (!showOnDesktop && !Application.isMobilePlatform) return;

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        var canvasGO = new GameObject("MobileControlsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.transform.SetParent(transform, false);

        // Kamera döndürme alanı (arkada, şeffaf)
        var lookArea = CreateRect("LookArea", canvasGO.transform, new Vector2(0.4f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        AddImage(lookArea.gameObject, new Color(0, 0, 0, 0));
        lookArea.gameObject.AddComponent<LookPad>();

        // Joystick
        var joyBg = CreateRect("Joystick", canvasGO.transform, Vector2.zero, Vector2.zero, new Vector2(300, 300), new Vector2(250, 250));
        joyBg.anchorMin = joyBg.anchorMax = Vector2.zero;
        joyBg.pivot = new Vector2(0.5f, 0.5f);
        AddImage(joyBg.gameObject, new Color(1, 1, 1, 0.25f), true);
        var handle = CreateRect("Handle", joyBg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(120, 120), Vector2.zero);
        AddImage(handle.gameObject, new Color(1, 1, 1, 0.6f), true).raycastTarget = false;
        var joy = joyBg.gameObject.AddComponent<VirtualJoystick>();
        joy.handle = handle;
        joy.radius = 150f;

        // Zıpla butonu
        var jump = CreateButton("JumpButton", canvasGO.transform, "ZIPLA", new Vector2(-200, 250), new Vector2(200, 200));
        var jumpHb = jump.gameObject.AddComponent<HoldButton>();
        jumpHb.onDown = () => { PlayerInputState.TouchJumpPressed = true; PlayerInputState.TouchJumpHeld = true; };
        jumpHb.onUp = () => PlayerInputState.TouchJumpHeld = false;

        // Koş butonu (basılı tut)
        var run = CreateButton("RunButton", canvasGO.transform, "KOŞ", new Vector2(-450, 150), new Vector2(160, 160));
        var hb = run.gameObject.AddComponent<HoldButton>();
        hb.onDown = () => PlayerInputState.TouchRun = true;
        hb.onUp = () => PlayerInputState.TouchRun = false;
    }

    // ---------- yardımcılar ----------
    static RectTransform CreateRect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        if (aMin == aMax || size != Vector2.zero) { rt.sizeDelta = size; rt.anchoredPosition = pos; }
        else { rt.offsetMin = rt.offsetMax = Vector2.zero; }
        return rt;
    }

    static Image AddImage(GameObject go, Color c, bool circle = false)
    {
        var img = go.AddComponent<Image>();
        img.color = c;
        if (circle) img.sprite = CircleSprite();
        return img;
    }

    static RectTransform CreateButton(string name, Transform parent, string label, Vector2 pos, Vector2 size)
    {
        // Sağ alt köşeye sabitlenir (pos negatif x = sağdan içeri)
        var rt = CreateRect(name, parent, new Vector2(1, 0), new Vector2(1, 0), size, pos);
        rt.pivot = new Vector2(0.5f, 0.5f);
        AddImage(rt.gameObject, new Color(1, 1, 1, 0.35f), true);

        var textRT = CreateRect("Label", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var txt = textRT.gameObject.AddComponent<Text>();
        txt.text = label;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 40;
        txt.color = Color.white;
        txt.raycastTarget = false;
        return rt;
    }

    static Sprite circleSprite;
    static Sprite CircleSprite()
    {
        if (circleSprite != null) return circleSprite;
        const int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float r = s / 2f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d)));
            }
        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        return circleSprite;
    }
}

/// <summary>Sanal joystick: PlayerInputState.TouchMove değerini yazar.</summary>
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public RectTransform handle;
    public float radius = 150f;
    RectTransform rt;

    void Awake() { rt = (RectTransform)transform; }

    public void OnPointerDown(PointerEventData e) { OnDrag(e); }

    public void OnDrag(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out Vector2 local);
        Vector2 v = Vector2.ClampMagnitude(local, radius);
        handle.anchoredPosition = v;
        PlayerInputState.TouchMove = v / radius;
    }

    public void OnPointerUp(PointerEventData e)
    {
        handle.anchoredPosition = Vector2.zero;
        PlayerInputState.TouchMove = Vector2.zero;
    }
}

/// <summary>Kamera döndürmek için sürükleme alanı.</summary>
public class LookPad : MonoBehaviour, IDragHandler
{
    public void OnDrag(PointerEventData e) { PlayerInputState.TouchLookDelta += e.delta; }
}

/// <summary>Basma/bırakma olaylarını bildiren buton.</summary>
public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public System.Action onDown, onUp;
    public void OnPointerDown(PointerEventData e) { onDown?.Invoke(); }
    public void OnPointerUp(PointerEventData e) { onUp?.Invoke(); }
}
