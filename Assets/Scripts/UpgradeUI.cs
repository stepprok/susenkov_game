using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Programově vytvoří menu vylepšení: kruhový ukazatel levelu, peníze a kartu vylepšení,
/// a napojí ho na UpgradeManager. Ovládání (U, Esc, Enter) zůstává v UpgradeManageru.
///
/// Použití: přidej skript na libovolný GameObject ve scéně.
/// </summary>
[DefaultExecutionOrder(-50)]
public class UpgradeUI : MonoBehaviour
{
    [Header("Level")]
    [Tooltip("Kolik vylepšení je potřeba k naplnění kolečka (= nový level).")]
    [SerializeField] private int upgradesPerLevel = 5;
    [Tooltip("Rychlost animace kolečka (celé kolečko za 1/rychlost sekundy).")]
    [SerializeField] private float ringSpeed = 0.8f;

    [Header("Barvy")]
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.72f);
    [SerializeField] private Color cardColor = new Color32(0x1B, 0x1D, 0x2A, 245);
    [SerializeField] private Color columnColor = new Color32(0x24, 0x27, 0x3B, 255);
    [SerializeField] private Color itemColor = new Color32(0x2E, 0x32, 0x50, 255);
    [SerializeField] private Color accentColor = new Color32(0xF2, 0xB8, 0x4B, 255);
    [SerializeField] private Color textColor = new Color32(0xE6, 0xE8, 0xF0, 255);
    [SerializeField] private Color mutedColor = new Color32(0x8D, 0x92, 0xAA, 255);
    [SerializeField] private Color goodColor = new Color32(0x5B, 0xE3, 0x7D, 255);
    [SerializeField] private Color badColor = new Color32(0xFF, 0x5C, 0x5C, 255);
    [SerializeField] private Color trackColor = new Color32(0xFF, 0xFF, 0xFF, 28);
    [SerializeField] private Color disabledBuy = new Color32(0x4A, 0x4D, 0x66, 255);

    // UI reference
    private GameObject overlay;
    private Image ringFill;
    private Image buyImage;
    private TextMeshProUGUI levelNumber, progressText, bannerText, moneyText;
    private TextMeshProUGUI subtitleText, priceText, messageText;
    private Sprite rounded, ringSprite, coinIcon, backpackIcon;
    private UpgradeManager um;

    // stav animace
    private bool wasOpen;
    private int shownLevel = 1;
    private float shownFill;
    private float holdTimer;
    private float pulse;
    private float bannerTimer;
    private float messageTimer;
    private Color messageBase;

    // cache pro překreslení textů
    private bool needsRefresh = true;
    private float lastMoney = -1f, lastCost = -1f;
    private int lastCapacity = -1, lastTotal = -1, lastShownLevel = -1;

    private static readonly Color Clear = new Color(0, 0, 0, 0);

    // ------------------------------------------------------------------
    // Životní cyklus
    // ------------------------------------------------------------------

    private void Awake()
    {
        rounded = CreateRoundedSprite(64, 16);
        ringSprite = CreateRingSprite(256, 0.80f, 0.98f);
        coinIcon = MakeIcon(96, CoinShader);
        backpackIcon = MakeIcon(128, BackpackShader);

        EnsureEventSystem();
        Build();
        Link();
    }

    private void OnDestroy()
    {
        if (um != null)
        {
            um.OnUpgradePurchased -= HandlePurchased;
            um.OnPurchaseFailed -= HandleFailed;
        }
    }

    private void Link()
    {
        um = FindFirstObjectByType<UpgradeManager>();
        if (um == null)
        {
            Debug.LogWarning("UpgradeUI: ve scéně není UpgradeManager.");
            return;
        }

        // Starý panel vypneme (pokud nepatří manageru ani tomuto skriptu)
        var old = um.upgradeUI;
        if (old != null && old != overlay && old != gameObject
            && old.GetComponent<UpgradeManager>() == null
            && !um.transform.IsChildOf(old.transform))
        {
            old.SetActive(false);
        }

        um.upgradeUI = overlay;
        um.upgradeText = null; // texty teď kreslí UpgradeUI

        um.OnUpgradePurchased += HandlePurchased;
        um.OnPurchaseFailed += HandleFailed;
    }

    private void HandlePurchased() => ShowMessage("Vylepšeno!", goodColor);
    private void HandleFailed() => ShowMessage("Nemáš dost peněz", badColor);

    private void ShowMessage(string text, Color color)
    {
        messageText.text = text;
        messageBase = color;
        messageTimer = 2f;
    }

    // ------------------------------------------------------------------
    // Každý snímek
    // ------------------------------------------------------------------

    private void Update()
    {
        if (overlay == null || um == null) return;
        if (!overlay.activeInHierarchy) { wasOpen = false; return; }

        int total = um.InventoryUpgradeLevel;
        int n = Mathf.Max(1, upgradesPerLevel);
        int targetLevel = 1 + total / n;
        float targetFill = (total % n) / (float)n;

        if (!wasOpen)
        {
            // po otevření rovnou zobrazíme aktuální stav bez animace
            wasOpen = true;
            shownLevel = targetLevel;
            shownFill = targetFill;
            holdTimer = 0f; pulse = 0f; bannerTimer = 0f; messageTimer = 0f;
            needsRefresh = true;
        }

        float dt = Time.unscaledDeltaTime;
        AnimateRing(dt, targetLevel, targetFill);

        ringFill.fillAmount = shownFill;

        if (shownLevel != lastShownLevel)
        {
            lastShownLevel = shownLevel;
            levelNumber.text = shownLevel.ToString();
        }

        // efekty
        pulse = Mathf.MoveTowards(pulse, 0f, dt * 1.5f);
        levelNumber.rectTransform.localScale = Vector3.one * (1f + 0.35f * pulse);

        if (bannerTimer > 0f)
        {
            bannerTimer -= dt;
            var c = accentColor; c.a = Mathf.Clamp01(bannerTimer);
            bannerText.color = c;
        }
        if (messageTimer > 0f)
        {
            messageTimer -= dt;
            var c = messageBase; c.a = Mathf.Clamp01(messageTimer);
            messageText.color = c;
        }

        RefreshTexts(total, n);
    }

    private void AnimateRing(float dt, int targetLevel, float targetFill)
    {
        // krátké "podržení" plného kolečka, než naskočí nový level
        if (holdTimer > 0f)
        {
            holdTimer -= dt;
            if (holdTimer <= 0f)
            {
                shownLevel++;
                shownFill = 0f;
                pulse = 1f;
                bannerTimer = 2.2f;
            }
            return;
        }

        if (shownLevel < targetLevel)
        {
            shownFill = Mathf.MoveTowards(shownFill, 1f, ringSpeed * dt);
            if (shownFill >= 1f) holdTimer = 0.5f;
        }
        else if (shownLevel == targetLevel)
        {
            shownFill = Mathf.MoveTowards(shownFill, targetFill, ringSpeed * dt);
        }
        else
        {
            shownLevel = targetLevel;
            shownFill = targetFill;
        }
    }

    private void RefreshTexts(int total, int n)
    {
        var inv = InventoryManager.Instance;
        float money = inv != null ? inv.money : 0f;
        int capacity = inv != null ? inv.capacity : 0;
        float cost = um.GetCurrentUpgradeCost();

        if (!needsRefresh && Mathf.Approximately(money, lastMoney) && Mathf.Approximately(cost, lastCost)
            && capacity == lastCapacity && total == lastTotal)
            return;

        needsRefresh = false;
        lastMoney = money; lastCost = cost; lastCapacity = capacity; lastTotal = total;

        bool canAfford = money >= cost;

        moneyText.text = $"{money:F2} $";
        subtitleText.text = $"Kapacita {capacity}  →  {capacity + um.slotsPerUpgrade} slotů";
        priceText.text = $"{cost:F2} $";
        priceText.color = canAfford ? goodColor : badColor;
        buyImage.color = canAfford ? accentColor : disabledBuy;
        progressText.text = $"{total % n} / {n} vylepšení do dalšího levelu";
    }

    // ------------------------------------------------------------------
    // Stavba UI
    // ------------------------------------------------------------------

    private void Build()
    {
        var canvasGo = new GameObject("UpgradeCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        overlay = NewUI("UpgradeOverlay", canvasGo.transform);
        Stretch(overlay.GetComponent<RectTransform>());
        overlay.AddComponent<Image>().color = dimColor;

        var card = NewUI("Card", overlay.transform);
        var cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = cardRt.anchorMax = cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(1140, 660);
        SetRounded(card.AddComponent<Image>(), cardColor);
        VLG(card, 18, new RectOffset(32, 32, 28, 22), TextAnchor.UpperLeft, true);

        BuildHeader(card.transform);

        var divider = NewUI("Divider", card.transform);
        divider.AddComponent<Image>().color = accentColor;
        divider.AddComponent<LayoutElement>().preferredHeight = 3;

        var body = NewUI("Body", card.transform);
        body.AddComponent<LayoutElement>().flexibleHeight = 1;
        var bodyH = body.AddComponent<HorizontalLayoutGroup>();
        bodyH.spacing = 28;
        bodyH.childControlWidth = true;
        bodyH.childControlHeight = true;
        bodyH.childForceExpandWidth = false;
        bodyH.childForceExpandHeight = true;

        BuildLevelColumn(body.transform);
        BuildUpgradeColumn(body.transform);

        var footer = Text(card.transform,
            $"<color=#{Hex(accentColor)}>[Enter]</color> koupit     <color=#{Hex(accentColor)}>[U]</color> / <color=#{Hex(accentColor)}>[Esc]</color> zavřít",
            24, mutedColor, FontStyles.Normal, TextAlignmentOptions.Center);
        footer.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
    }

    private void BuildHeader(Transform parent)
    {
        var header = NewUI("Header", parent);
        header.AddComponent<LayoutElement>().preferredHeight = 64;
        var h = header.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 20;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;

        var title = Text(header.transform, "VYLEPŠENÍ", 46, accentColor, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        title.characterSpacing = 8;
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        // kapsle s penězi
        var pill = NewUI("MoneyPill", header.transform);
        SetRounded(pill.AddComponent<Image>(), new Color32(0x2A, 0x2D, 0x44, 255));
        var ph = pill.AddComponent<HorizontalLayoutGroup>();
        ph.padding = new RectOffset(18, 24, 8, 8);
        ph.spacing = 12;
        ph.childAlignment = TextAnchor.MiddleCenter;
        ph.childControlWidth = true;
        ph.childControlHeight = true;
        ph.childForceExpandWidth = false;
        ph.childForceExpandHeight = false;

        Icon(pill.transform, coinIcon, 40);
        moneyText = Text(pill.transform, "0.00 $", 34, textColor, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
    }

    private void BuildLevelColumn(Transform parent)
    {
        var col = NewUI("LevelColumn", parent);
        SetRounded(col.AddComponent<Image>(), columnColor);
        var le = col.AddComponent<LayoutElement>();
        le.preferredWidth = 380;
        le.minWidth = 380;
        le.flexibleWidth = 0;
        VLG(col, 14, new RectOffset(24, 24, 24, 24), TextAnchor.MiddleCenter, false);

        // kolečko
        var ring = NewUI("Ring", col.transform);
        var rle = ring.AddComponent<LayoutElement>();
        rle.preferredWidth = rle.minWidth = 270;
        rle.preferredHeight = rle.minHeight = 270;

        var track = NewUI("Track", ring.transform);
        Stretch(track.GetComponent<RectTransform>());
        var timg = track.AddComponent<Image>();
        timg.sprite = ringSprite;
        timg.color = trackColor;
        timg.raycastTarget = false;

        var fill = NewUI("Fill", ring.transform);
        Stretch(fill.GetComponent<RectTransform>());
        ringFill = fill.AddComponent<Image>();
        ringFill.sprite = ringSprite;
        ringFill.color = accentColor;
        ringFill.type = Image.Type.Filled;
        ringFill.fillMethod = Image.FillMethod.Radial360;
        ringFill.fillOrigin = (int)Image.Origin360.Top;
        ringFill.fillClockwise = true;
        ringFill.fillAmount = 0f;
        ringFill.raycastTarget = false;

        var label = Text(ring.transform, "LEVEL", 28, mutedColor, FontStyles.Bold, TextAlignmentOptions.Center);
        label.characterSpacing = 8;
        PlaceCentered(label.rectTransform, 200, 40, 62);

        levelNumber = Text(ring.transform, "1", 110, accentColor, FontStyles.Bold, TextAlignmentOptions.Center);
        PlaceCentered(levelNumber.rectTransform, 200, 130, -10);

        progressText = Text(col.transform, "0 / 5 vylepšení do dalšího levelu", 24, mutedColor,
            FontStyles.Normal, TextAlignmentOptions.Center);

        bannerText = Text(col.transform, "NOVÝ LEVEL!", 36, accentColor, FontStyles.Bold, TextAlignmentOptions.Center);
        bannerText.characterSpacing = 4;
        bannerText.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;
        var bc = accentColor; bc.a = 0f;
        bannerText.color = bc;
    }

    private void BuildUpgradeColumn(Transform parent)
    {
        var col = NewUI("UpgradeColumn", parent);
        SetRounded(col.AddComponent<Image>(), columnColor);
        col.AddComponent<LayoutElement>().flexibleWidth = 1;
        VLG(col, 20, new RectOffset(28, 28, 24, 24), TextAnchor.UpperLeft, true);

        var heading = Text(col.transform, "CO VYLEPŠIT", 30, accentColor, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        heading.characterSpacing = 6;

        var line = NewUI("Line", col.transform);
        line.AddComponent<Image>().color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.45f);
        line.AddComponent<LayoutElement>().preferredHeight = 2;

        // --- karta vylepšení (zlatý rámeček) ---
        var border = NewUI("ItemBorder", col.transform);
        SetRounded(border.AddComponent<Image>(), accentColor);
        var ble = border.AddComponent<LayoutElement>();
        ble.minHeight = ble.preferredHeight = 140;

        var inner = NewUI("Item", border.transform);
        var irt = inner.GetComponent<RectTransform>();
        irt.anchorMin = Vector2.zero;
        irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(3, 3);
        irt.offsetMax = new Vector2(-3, -3);
        SetRounded(inner.AddComponent<Image>(), itemColor);

        var ih = inner.AddComponent<HorizontalLayoutGroup>();
        ih.padding = new RectOffset(22, 22, 14, 14);
        ih.spacing = 20;
        ih.childAlignment = TextAnchor.MiddleLeft;
        ih.childControlWidth = true;
        ih.childControlHeight = true;
        ih.childForceExpandWidth = false;
        ih.childForceExpandHeight = false;

        Icon(inner.transform, backpackIcon, 84);

        var texts = NewUI("Texts", inner.transform);
        texts.AddComponent<LayoutElement>().flexibleWidth = 1;
        VLG(texts, 6, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft, true);
        Text(texts.transform, "Rozšíření batohu", 32, textColor, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        subtitleText = Text(texts.transform, "", 26, mutedColor, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);

        var price = NewUI("Price", inner.transform);
        var pr = price.AddComponent<HorizontalLayoutGroup>();
        pr.spacing = 8;
        pr.childAlignment = TextAnchor.MiddleRight;
        pr.childControlWidth = true;
        pr.childControlHeight = true;
        pr.childForceExpandWidth = false;
        pr.childForceExpandHeight = false;
        Icon(price.transform, coinIcon, 38);
        priceText = Text(price.transform, "", 34, goodColor, FontStyles.Bold, TextAlignmentOptions.MidlineRight);

        // místo pro další vylepšení do budoucna
        var spacer = NewUI("Spacer", col.transform);
        spacer.AddComponent<LayoutElement>().flexibleHeight = 1;

        // --- spodní řádek: hláška + tlačítko Koupit ---
        var bottom = NewUI("BuyRow", col.transform);
        bottom.AddComponent<LayoutElement>().minHeight = 70;
        var bh = bottom.AddComponent<HorizontalLayoutGroup>();
        bh.spacing = 16;
        bh.childAlignment = TextAnchor.MiddleLeft;
        bh.childControlWidth = true;
        bh.childControlHeight = true;
        bh.childForceExpandWidth = false;
        bh.childForceExpandHeight = false;

        messageText = Text(bottom.transform, "", 28, goodColor, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        messageText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        messageBase = goodColor;
        messageText.color = new Color(goodColor.r, goodColor.g, goodColor.b, 0f);

        var buy = NewUI("BuyButton", bottom.transform);
        buyImage = buy.AddComponent<Image>();
        SetRounded(buyImage, accentColor);
        var bl = buy.AddComponent<LayoutElement>();
        bl.preferredWidth = bl.minWidth = 230;
        bl.preferredHeight = bl.minHeight = 66;

        var btn = buy.AddComponent<Button>();
        btn.targetGraphic = buyImage;
        var nav = btn.navigation;
        nav.mode = Navigation.Mode.None;
        btn.navigation = nav;
        var cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        cb.selectedColor = Color.white;
        btn.colors = cb;
        btn.onClick.AddListener(() =>
        {
            if (um != null) um.BuyInventoryUpgrade();
            // aby Enter nespouštěl tlačítko podruhé
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        });

        var buyLabel = Text(buy.transform, "KOUPIT", 30, new Color32(0x1B, 0x1D, 0x2A, 255),
            FontStyles.Bold, TextAlignmentOptions.Center);
        buyLabel.characterSpacing = 4;
        Stretch(buyLabel.rectTransform);
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
    // Pomocné UI funkce
    // ------------------------------------------------------------------

    private static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

    private void SetRounded(Image img, Color color)
    {
        img.sprite = rounded;
        img.type = Image.Type.Sliced;
        img.color = color;
    }

    private static void VLG(GameObject go, float spacing, RectOffset padding, TextAnchor align, bool expandWidth)
    {
        var v = go.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing;
        v.padding = padding;
        v.childAlignment = align;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = expandWidth;
        v.childForceExpandHeight = false;
    }

    private void Icon(Transform parent, Sprite sprite, float size)
    {
        var go = NewUI("Icon", parent);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = size;
        le.preferredHeight = le.minHeight = size;
    }

    private TextMeshProUGUI Text(Transform parent, string txt, int size, Color color,
        FontStyles style, TextAlignmentOptions align)
    {
        var go = NewUI("Text", parent);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = txt;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = style;
        t.alignment = align;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

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

    private static void PlaceCentered(RectTransform rt, float w, float h, float y)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(0, y);
    }

    // ------------------------------------------------------------------
    // Ikony a sprity kreslené kódem
    // ------------------------------------------------------------------

    private static Color CoinShader(float x, float y)
    {
        float d = Mathf.Sqrt(x * x + y * y);
        if (d > 0.88f) return Clear;
        if (d > 0.78f) return new Color(0.85f, 0.58f, 0.10f, 1f);
        if (d > 0.62f && d < 0.70f) return new Color(0.80f, 0.54f, 0.08f, 1f);
        if (Mathf.Abs(x) <= 0.06f && Mathf.Abs(y) <= 0.42f) return new Color(0.80f, 0.54f, 0.08f, 1f);
        return new Color(0.98f, 0.77f, 0.25f, 1f);
    }

    private static Color BackpackShader(float x, float y)
    {
        var gold = new Color(0.95f, 0.72f, 0.29f, 1f);
        var dark = new Color(0.72f, 0.48f, 0.10f, 1f);

        float hd = Mathf.Sqrt(x * x + (y - 0.72f) * (y - 0.72f));
        if (hd < 0.26f && hd > 0.15f) return dark;                              // ucho
        if (Mathf.Abs(x) <= 0.38f && Mathf.Abs(y + 0.45f) <= 0.22f) return dark; // kapsa
        bool top = x * x + (y - 0.15f) * (y - 0.15f) <= 0.55f * 0.55f;
        bool bottom = Mathf.Abs(x) <= 0.55f && Mathf.Abs(y + 0.35f) <= 0.55f;
        if (top || bottom) return gold;
        return Clear;
    }

    private static Sprite MakeIcon(int size, System.Func<float, float, Color> shader)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        const int ss = 3;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                for (int sy = 0; sy < ss; sy++)
                {
                    for (int sx = 0; sx < ss; sx++)
                    {
                        float u = ((x + (sx + 0.5f) / ss) / size) * 2f - 1f;
                        float v = ((y + (sy + 0.5f) / ss) / size) * 2f - 1f;
                        Color c = shader(u, v);
                        r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;
                    }
                }
                px[y * size + x] = a > 0f
                    ? (Color32)new Color(r / a, g / a, b / a, a / (ss * ss))
                    : new Color32(0, 0, 0, 0);
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>Bílý prstenec (mezikruží) pro kruhový ukazatel.</summary>
    private static Sprite CreateRingSprite(int size, float inner, float outer)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float px = 2f / size;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = ((x + 0.5f) / size) * 2f - 1f;
                float v = ((y + 0.5f) / size) * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Clamp01((outer - r) / px + 0.5f) * Mathf.Clamp01((r - inner) / px + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

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

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }
}