using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Builds the whole HUD at runtime (no manual UI setup needed):
///  - top-left:    [CTRL] + [H] Help
///  - below it:    level name + completion % + progress bar
///  - bottom-left: cookie icon + number of cookies
///  - bottom-right: stamina icon + % + stamina bar
/// Put this on an empty GameObject and drag the three sprites into the slots.
/// </summary>
public class HUDManager : MonoBehaviour
{
    [Header("Icons (drag your sprites here)")]
    public Sprite cookieIcon;
    public Sprite staminaIcon;

    [Header("Data")]
    public string helpModifierLabel = "CTRL";
    public string helpLabel = "Help";

    [Header("Look")]
    public TMP_FontAsset font; // optional, leave empty for the TMP default
    public int sortingOrder = 0;
    public float margin = 60f;
    public Color textColor = Color.white;
    public Color barBackground = new Color(0f, 0f, 0f, 0.55f);
    public Color levelBarColor = new Color(0.45f, 0.85f, 0.4f);
    public Color staminaColor = new Color(0.97f, 0.69f, 0.2f);
    public Color staminaLowColor = new Color(0.9f, 0.25f, 0.2f);
    [Range(0f, 1f)] public float lowStaminaThreshold = 0.25f;

    private TextMeshProUGUI levelNameText, levelPercentText, cookieText, staminaText;
    private RectTransform levelFill, staminaFill;
    private Image staminaFillImage;

    private int lastCookies = -1, lastLevelPct = -1, lastStaminaPct = -1;
    private string lastLevelName;

    private void Awake()
    {
        BuildHUD();
    }

    private void Update()
    {
        UpdateCookies();
        UpdateLevel();
        UpdateStamina();
    }

    // ---------------------------------------------------------------- Updating

    private void UpdateCookies()
    {
        int count = InventoryManager.Instance != null ? InventoryManager.Instance.CountCookies() : 0;
        if (count != lastCookies)
        {
            lastCookies = count;
            cookieText.text = count.ToString();
        }
    }

    private void UpdateLevel()
    {
        LevelManager lm = LevelManager.Instance;
        if (lm == null) return;

        if (lm.levelName != lastLevelName)
        {
            lastLevelName = lm.levelName;
            levelNameText.text = lm.levelName;
        }

        int pct = Mathf.RoundToInt(lm.CompletionPercent * 100f);
        if (pct != lastLevelPct)
        {
            lastLevelPct = pct;
            levelPercentText.text = pct + "%";
            SetBar(levelFill, lm.CompletionPercent);
        }
    }

    private void UpdateStamina()
    {
        StaminaSystem s = StaminaSystem.Instance;
        if (s == null) return;

        float n = s.Normalized;
        SetBar(staminaFill, n);
        staminaFillImage.color = (s.IsExhausted || n <= lowStaminaThreshold) ? staminaLowColor : staminaColor;

        int pct = Mathf.RoundToInt(n * 100f);
        if (pct != lastStaminaPct)
        {
            lastStaminaPct = pct;
            staminaText.text = pct + "%";
        }
    }

    private static void SetBar(RectTransform fill, float value01)
    {
        fill.anchorMax = new Vector2(Mathf.Clamp01(value01), 1f);
    }

    // ---------------------------------------------------------------- Building

    private void BuildHUD()
    {
        GameObject canvasGo = new GameObject("HUD Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = (RectTransform)canvasGo.transform;

        BuildHelpHint(root);
        BuildLevelProgress(root);
        BuildCookies(root);
        BuildStamina(root);
    }

    private void BuildHelpHint(RectTransform root)
    {
      
    }

    private void BuildLevelProgress(RectTransform root)
    {
        RectTransform level = NewRect("Level Progress", root);
        Place(level, new Vector2(0, 1), new Vector2(0, 1), new Vector2(margin, -margin - 90f), new Vector2(420, 80));

        levelNameText = NewText("Name", level, "", 34, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        Stretch(levelNameText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -44), new Vector2(-110, 0));

        levelPercentText = NewText("Percent", level, "0%", 28, TextAlignmentOptions.MidlineRight);
        Place(levelPercentText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(110, 44));

        levelFill = NewBar("Bar", level, levelBarColor, out _);
        Stretch((RectTransform)levelFill.parent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -66), new Vector2(0, -48));
        SetBar(levelFill, 0f);
    }

    private void BuildCookies(RectTransform root)
    {
        RectTransform cookies = NewRect("Cookies", root);
        Place(cookies, new Vector2(0, 0), new Vector2(0, 0), new Vector2(margin, margin), new Vector2(260, 90));

        Image icon = NewImage("Icon", cookies, cookieIcon, Color.white);
        Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(84, 84));

        cookieText = NewText("Count", cookies, "0", 54, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        Stretch(cookieText.rectTransform, Vector2.zero, Vector2.one, new Vector2(104, 0), Vector2.zero);
    }

    private void BuildStamina(RectTransform root)
    {
        RectTransform stamina = NewRect("Stamina", root);
        Place(stamina, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-margin, margin), new Vector2(380, 100));

        Image icon = NewImage("Icon", stamina, staminaIcon, Color.white);
        Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(50, 90));

        staminaText = NewText("Percent", stamina, "100%", 40, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        Stretch(staminaText.rectTransform, Vector2.zero, Vector2.one, new Vector2(70, 50), Vector2.zero);

        staminaFill = NewBar("Bar", stamina, staminaColor, out staminaFillImage);
        Stretch((RectTransform)staminaFill.parent, new Vector2(0, 0), new Vector2(1, 0), new Vector2(70, 8), new Vector2(0, 30));
        SetBar(staminaFill, 1f);
    }

    // ---------------------------------------------------------------- Helpers

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = oMin;
        rt.offsetMax = oMax;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rt = NewRect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    private TextMeshProUGUI NewText(string name, Transform parent, string text, float size, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        RectTransform rt = NewRect(name, parent);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = textColor;
        t.alignment = align;
        t.fontStyle = style;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>Creates a background + fill. Returns the fill RectTransform (its parent is the background).</summary>
    private RectTransform NewBar(string name, Transform parent, Color fillColor, out Image fillImage)
    {
        Image bg = NewImage(name, parent, null, barBackground);
        bg.preserveAspect = false;

        RectTransform fillRt = NewRect("Fill", bg.transform);
        fillImage = fillRt.gameObject.AddComponent<Image>();
        fillImage.color = fillColor;
        fillImage.raycastTarget = false;
        Stretch(fillRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return fillRt;
    }
}