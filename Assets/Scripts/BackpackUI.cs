using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// ProgramovÏ vytvo¯Ì UI batohu a napojÌ ho na InventoryManager
/// (nastavÌ mu inventoryUI a inventoryText). Ovl·d·nÌ (kl·vesa E, öipky, Enter)
/// z˘st·v· v InventoryManageru.
///
/// PouûitÌ: p¯idej tento skript na libovoln˝ GameObject ve scÈnÏ (nap¯. na ten s InventoryManager).
/// </summary>
[DefaultExecutionOrder(-50)]
public class BackpackUI : MonoBehaviour
{
    [Header("ObecnÈ")]
    [SerializeField] private string title = "BATOH";
    [SerializeField] private Vector2 panelSize = new Vector2(700, 480);
    [SerializeField] private int fontSize = 26;

    [Header("Barvy")]
    [SerializeField] private Color panelColor = new Color32(0x1B, 0x1D, 0x2A, 235);
    [SerializeField] private Color headerColor = new Color32(0x2A, 0x2D, 0x44, 255);
    [SerializeField] private Color accentColor = new Color32(0xF2, 0xB8, 0x4B, 255);
    [SerializeField] private Color textColor = new Color32(0xE6, 0xE8, 0xF0, 255);
    [SerializeField] private Color trackColor = new Color32(0xFF, 0xFF, 0xFF, 25);
    [SerializeField] private Color handleColor = new Color32(0xF2, 0xB8, 0x4B, 200);

    private GameObject panel;
    private TextMeshProUGUI contentText;
    private RectTransform contentRt;
    private RectTransform viewportRt;
    private ScrollRect scroll;
    private Sprite roundedSprite;
    private string lastText;

    // ------------------------------------------------------------------
    // éivotnÌ cyklus
    // ------------------------------------------------------------------

    private void Awake()
    {
        roundedSprite = CreateRoundedSprite(64, 16);
        EnsureEventSystem();
        Build();
        panel.SetActive(false);
        LinkToInventory();
    }

    private void LateUpdate()
    {
        if (panel == null || !panel.activeInHierarchy) return;

        // Kdyû manager zmÏnÌ text (nap¯. posun v˝bÏru), srolujeme na vybranou poloûku.
        string current = contentText.text;
        if (current == lastText) return;
        lastText = current;
        ScrollToSelected();
    }

    // ------------------------------------------------------------------
    // NapojenÌ na InventoryManager
    // ------------------------------------------------------------------

    private void LinkToInventory()
    {
        var inv = FindFirstObjectByType<InventoryManager>();
        if (inv == null)
        {
            Debug.LogWarning("BackpackUI: ve scÈnÏ nenÌ InventoryManager, UI nenÌ s ËÌm propojit.");
            return;
        }

        // Star˝ panel (pokud nÏjak˝ byl) vypneme, aù nez˘stane viset ve scÈnÏ.
        if (inv.inventoryUI != null && inv.inventoryUI != panel
            && !panel.transform.IsChildOf(inv.inventoryUI.transform))
        {
            inv.inventoryUI.SetActive(false);
        }

        inv.inventoryUI = panel;
        inv.inventoryText = contentText;
    }

    // ------------------------------------------------------------------
    // Auto-scroll na vybranou (ûlutou) poloûku
    // ------------------------------------------------------------------

    private void ScrollToSelected()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel.GetComponent<RectTransform>());
        contentText.ForceMeshUpdate();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);

        var info = contentText.textInfo;
        int line = -1;
        for (int i = 0; i < info.characterCount; i++)
        {
            var c = info.characterInfo[i];
            if (!c.isVisible) continue;
            // InventoryManager znaËÌ vybranou poloûku barvou yellow
            if (c.color.r == 255 && c.color.g == 255 && c.color.b == 0)
            {
                line = c.lineNumber;
                break;
            }
        }
        if (line < 0) return;

        var li = info.lineInfo[line];
        float top = -li.ascender;     // vzd·lenost ¯·dku od hornÌho okraje contentu
        float bottom = -li.descender;

        float viewH = viewportRt.rect.height;
        float maxOffset = Mathf.Max(0f, contentRt.rect.height - viewH);
        float offset = contentRt.anchoredPosition.y;
        const float pad = 8f;

        if (bottom + pad > offset + viewH) offset = bottom + pad - viewH;
        if (top - pad < offset) offset = top - pad;
        offset = Mathf.Clamp(offset, 0f, maxOffset);

        scroll.velocity = Vector2.zero;
        contentRt.anchoredPosition = new Vector2(contentRt.anchoredPosition.x, offset);
    }

    // ------------------------------------------------------------------
    // Stavba UI
    // ------------------------------------------------------------------

    private void Build()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            canvas = CreateCanvas();

        // --- Panel ---
        panel = NewUI("BackpackPanel", canvas.transform);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = panelSize;
        panelRt.anchoredPosition = Vector2.zero;

        var panelImg = panel.AddComponent<Image>();
        panelImg.sprite = roundedSprite;
        panelImg.type = Image.Type.Sliced;
        panelImg.color = panelColor;

        var vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(20, 20, 20, 20);
        vlg.spacing = 10;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // --- HlaviËka ---
        var header = NewUI("Header", panel.transform);
        var headerImg = header.AddComponent<Image>();
        headerImg.sprite = roundedSprite;
        headerImg.type = Image.Type.Sliced;
        headerImg.color = headerColor;
        header.AddComponent<LayoutElement>().preferredHeight = 60;

        var titleGo = NewUI("Title", header.transform);
        Stretch(titleGo.GetComponent<RectTransform>());
        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = title;
        titleTmp.fontSize = 36;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.characterSpacing = 6;
        titleTmp.color = accentColor;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.raycastTarget = false;

        // --- OddÏlovacÌ linka ---
        var divider = NewUI("Divider", panel.transform);
        divider.AddComponent<Image>().color = accentColor;
        divider.AddComponent<LayoutElement>().preferredHeight = 3;

        // --- Scroll View ---
        var scrollGo = NewUI("Scroll View", panel.transform);
        var scrollLe = scrollGo.AddComponent<LayoutElement>();
        scrollLe.flexibleHeight = 1;
        scrollLe.minHeight = 100;
        scrollGo.AddComponent<Image>().color = new Color(0, 0, 0, 0.001f);
        scroll = scrollGo.AddComponent<ScrollRect>();

        var viewport = NewUI("Viewport", scrollGo.transform);
        viewportRt = viewport.GetComponent<RectTransform>();
        Stretch(viewportRt);
        viewport.AddComponent<RectMask2D>();

        // Content = p¯Ìmo text, roste podle dÈlky
        var contentGo = NewUI("Content", viewport.transform);
        contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.offsetMin = new Vector2(10, 0);
        contentRt.offsetMax = new Vector2(-10, 0);

        contentText = contentGo.AddComponent<TextMeshProUGUI>();
        contentText.fontSize = fontSize;
        contentText.color = textColor;
        contentText.alignment = TextAlignmentOptions.TopLeft;
        contentText.lineSpacing = 12;
        contentText.richText = true;
        contentText.text = "";

        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // --- Scrollbar ---
        var scrollbarGo = NewUI("Scrollbar", scrollGo.transform);
        var sbRt = scrollbarGo.GetComponent<RectTransform>();
        sbRt.anchorMin = new Vector2(1, 0);
        sbRt.anchorMax = new Vector2(1, 1);
        sbRt.pivot = new Vector2(1, 1);
        sbRt.sizeDelta = new Vector2(8, 0);
        sbRt.anchoredPosition = Vector2.zero;

        var trackImg = scrollbarGo.AddComponent<Image>();
        trackImg.sprite = roundedSprite;
        trackImg.type = Image.Type.Sliced;
        trackImg.color = trackColor;

        var slidingArea = NewUI("Sliding Area", scrollbarGo.transform);
        Stretch(slidingArea.GetComponent<RectTransform>());

        var handle = NewUI("Handle", slidingArea.transform);
        var handleImg = handle.AddComponent<Image>();
        handleImg.sprite = roundedSprite;
        handleImg.type = Image.Type.Sliced;
        handleImg.color = handleColor;

        var scrollbar = scrollbarGo.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle.GetComponent<RectTransform>();
        scrollbar.targetGraphic = handleImg;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        // --- NapojenÌ ScrollRectu ---
        scroll.viewport = viewportRt;
        scroll.content = contentRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scroll.verticalScrollbarSpacing = -4;
    }

    private Canvas CreateCanvas()
    {
        var go = new GameObject("UI Canvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    // ------------------------------------------------------------------
    // PomocnÈ funkce
    // ------------------------------------------------------------------

    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>Vygeneruje zaoblen˝ 9-slice sprite (funguje i v buildu).</summary>
    private static Sprite CreateRoundedSprite(int size, int radius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        return Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
    }
}