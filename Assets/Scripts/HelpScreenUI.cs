using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Vytvo¯Ì v levÈm hornÌm rohu n·pis [H] N·povÏda a po stisku H zobrazÌ
/// p¯ehlednou obrazovku s ovl·d·nÌm a cÌli hry. Vöechny ikony se kreslÌ kÛdem,
/// takûe nepot¯ebujeö û·dnÈ obr·zky ani assety.
///
/// PouûitÌ: p¯idej skript na libovoln˝ GameObject ve scÈnÏ a spusù hru.
/// </summary>
public class HelpScreenUI : MonoBehaviour
{
    [Header("Ovl·d·nÌ")]
#if ENABLE_INPUT_SYSTEM
    [SerializeField] private Key toggleKey = Key.H;
#else
    [SerializeField] private KeyCode toggleKey = KeyCode.H;
#endif
    [SerializeField] private bool showOnStart = false;
    [SerializeField] private bool pauseGameWhileOpen = false;

    [Header("VlastnÌ obr·zky (nepovinnÈ, pr·zdnÈ = nakreslen· ikona)")]
    [SerializeField] private Sprite cookieImage;   // 1. PosbÌrej suöenky
    [SerializeField] private Sprite swapImage;     // 2. SmÏÚ suöenky s lidmi
    [SerializeField] private Sprite coinImage;     // 3. Prodej smÏnÏnÈ vÏci
    [SerializeField] private Sprite upgradeImage;  // 4. Vylepöi postavu
    [SerializeField] private Sprite mouseImage;    // Interakce kliknutÌm

    [Header("Barvy")]
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.72f);
    [SerializeField] private Color cardColor = new Color32(0x1B, 0x1D, 0x2A, 245);
    [SerializeField] private Color columnColor = new Color32(0x24, 0x27, 0x3B, 255);
    [SerializeField] private Color accentColor = new Color32(0xF2, 0xB8, 0x4B, 255);
    [SerializeField] private Color textColor = new Color32(0xE6, 0xE8, 0xF0, 255);
    [SerializeField] private Color mutedColor = new Color32(0x8D, 0x92, 0xAA, 255);
    [SerializeField] private Color keyFace = new Color32(0x3A, 0x3E, 0x5C, 255);
    [SerializeField] private Color keyShadow = new Color32(0x12, 0x13, 0x1D, 255);

    private GameObject overlay;
    private Sprite rounded;
    private Sprite iconCookie, iconSwap, iconCoin, iconUpgrade, iconMouse, iconArrow;
    private bool isOpen;
    private float previousTimeScale = 1f;

    private static readonly Color Clear = new Color(0, 0, 0, 0);

    // ------------------------------------------------------------------
    // éivotnÌ cyklus
    // ------------------------------------------------------------------

    private void Awake()
    {
        rounded = CreateRoundedSprite(64, 16);
        CreateIcons();
        Build();
        SetOpen(showOnStart);
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb[toggleKey].wasPressedThisFrame) Toggle();
        else if (isOpen && kb.escapeKey.wasPressedThisFrame) SetOpen(false);
#else
        if (Input.GetKeyDown(toggleKey)) Toggle();
        else if (isOpen && Input.GetKeyDown(KeyCode.Escape)) SetOpen(false);
#endif
    }

    private void OnDestroy()
    {
        if (isOpen && pauseGameWhileOpen) Time.timeScale = previousTimeScale;
    }

    public void Toggle() => SetOpen(!isOpen);

    public void SetOpen(bool open)
    {
        if (pauseGameWhileOpen)
        {
            if (open && !isOpen) { previousTimeScale = Time.timeScale; Time.timeScale = 0f; }
            else if (!open && isOpen) { Time.timeScale = previousTimeScale; }
        }

        isOpen = open;
        if (overlay != null) overlay.SetActive(open);
    }

    // ------------------------------------------------------------------
    // Stavba UI
    // ------------------------------------------------------------------

    private void Build()
    {
        // VlastnÌ Canvas, vûdy nad ostatnÌm UI
        var canvasGo = new GameObject("HelpCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        BuildCornerHint(canvasGo.transform);
        BuildHelpScreen(canvasGo.transform);
    }

    // --- Mal˝ n·pis v levÈm hornÌm rohu ---
    private void BuildCornerHint(Transform canvas)
    {
        var pill = NewUI("HelpHint", canvas);
        var rt = pill.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(30, -30);

        var img = pill.AddComponent<Image>();
        img.sprite = rounded;
        img.type = Image.Type.Sliced;
        img.color = new Color32(0x1B, 0x1D, 0x2A, 210);

        var hlg = pill.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(14, 22, 10, 14);
        hlg.spacing = 14;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var fitter = pill.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Keycap(pill.transform, "H", null, 46, 46, 28);
    }

    // --- Cel· obrazovka s n·povÏdou ---
    private void BuildHelpScreen(Transform canvas)
    {
        overlay = NewUI("HelpOverlay", canvas);
        Stretch(overlay.GetComponent<RectTransform>());
        overlay.AddComponent<Image>().color = dimColor;

        var card = NewUI("Card", overlay.transform);
        var cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = cardRt.anchorMax = cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(1240, 780);
        var cardImg = card.AddComponent<Image>();
        cardImg.sprite = rounded;
        cardImg.type = Image.Type.Sliced;
        cardImg.color = cardColor;

        var vlg = card.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(32, 32, 28, 24);
        vlg.spacing = 16;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // HlaviËka
        var header = NewUI("Header", card.transform);
        header.AddComponent<LayoutElement>().preferredHeight = 64;
        var hh = header.AddComponent<HorizontalLayoutGroup>();
        hh.childControlWidth = true;
        hh.childControlHeight = true;
        hh.childForceExpandWidth = false;
        hh.childForceExpandHeight = true;
        hh.spacing = 20;

        var title = Text(header.transform, "N¡POVÃDA", 46, accentColor, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        title.characterSpacing = 8;
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        var closeRow = NewUI("CloseHint", header.transform);
        var ch = closeRow.AddComponent<HorizontalLayoutGroup>();
        ch.spacing = 10;
        ch.childAlignment = TextAnchor.MiddleRight;
        ch.childControlWidth = true;
        ch.childControlHeight = true;
        ch.childForceExpandWidth = false;
        ch.childForceExpandHeight = false;
        Text(closeRow.transform, "zav¯Ìt", 26, mutedColor, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
        Keycap(closeRow.transform, "H", null, 46, 46, 28);

        // Zlat· linka
        var divider = NewUI("Divider", card.transform);
        divider.AddComponent<Image>().color = accentColor;
        divider.AddComponent<LayoutElement>().preferredHeight = 3;

        // Dva sloupce
        var columns = NewUI("Columns", card.transform);
        columns.AddComponent<LayoutElement>().flexibleHeight = 1;
        var colLayout = columns.AddComponent<HorizontalLayoutGroup>();
        colLayout.spacing = 28;
        colLayout.childControlWidth = true;
        colLayout.childControlHeight = true;
        colLayout.childForceExpandWidth = true;
        colLayout.childForceExpandHeight = true;

        BuildControlsColumn(columns.transform);
        BuildGoalsColumn(columns.transform);

        // PatiËka
        var footer = Text(card.transform, "Zav¯eö kl·vesou H nebo Esc", 24, mutedColor,
            FontStyles.Italic, TextAlignmentOptions.Center);
        footer.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
    }

    private void BuildControlsColumn(Transform parent)
    {
        var col = NewColumn(parent, "OVL¡D¡NÕ");

        var r = ControlRow(col, "Ch˘ze");
        Keycap(r, "W", null, 64, 64, 30);
        Keycap(r, "A", null, 64, 64, 30);
        Keycap(r, "S", null, 64, 64, 30);
        Keycap(r, "D", null, 64, 64, 30);

        r = ControlRow(col, "BÏh");
        Keycap(r, "Shift", null, 138, 64, 26);

        r = ControlRow(col, "KliknutÌm na vÏc s nÌ interagujeö");
        Icon(r, iconMouse, 68);

        r = ControlRow(col, "Posun v batohu a obchodÏ");
        Keycap(r, "", iconArrow, 64, 64, 30, false);
        Keycap(r, "", iconArrow, 64, 64, 30, true);

        r = ControlRow(col, "Otev¯Ìt batoh");
        Keycap(r, "E", null, 64, 64, 30);
    }

    private void BuildGoalsColumn(Transform parent)
    {
        var col = NewColumn(parent, "CÕL HRY");

        GoalRow(col, iconCookie, 1, "PosbÌrej suöenky");
        GoalRow(col, iconSwap, 2, "SmÏÚ suöenky s lidmi");
        GoalRow(col, iconCoin, 3, "Prodej smÏnÏnÈ vÏci");
        GoalRow(col, iconUpgrade, 4, "Vylepöi postavu");
    }

    // ------------------------------------------------------------------
    // StavebnÌ kameny UI
    // ------------------------------------------------------------------

    private Transform NewColumn(Transform parent, string heading)
    {
        var col = NewUI("Column", parent);
        var img = col.AddComponent<Image>();
        img.sprite = rounded;
        img.type = Image.Type.Sliced;
        img.color = columnColor;
        col.AddComponent<LayoutElement>().flexibleWidth = 1;

        var vlg = col.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(28, 28, 24, 24);
        vlg.spacing = 20;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var h = Text(col.transform, heading, 30, accentColor, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        h.characterSpacing = 6;

        var line = NewUI("Line", col.transform);
        var lineImg = line.AddComponent<Image>();
        lineImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.45f);
        line.AddComponent<LayoutElement>().preferredHeight = 2;

        return col.transform;
    }

    /// <summary>Vytvo¯Ì ¯·dek ovl·d·nÌ a vr·tÌ kontejner, do kterÈho se vkl·dajÌ kl·vesy.</summary>
    private Transform ControlRow(Transform column, string description)
    {
        var row = NewUI("ControlRow", column);
        row.AddComponent<LayoutElement>().minHeight = 80;
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 24;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var keys = NewUI("Keys", row.transform);
        var kle = keys.AddComponent<LayoutElement>();
        kle.minWidth = 290;
        kle.preferredWidth = 290;
        var khl = keys.AddComponent<HorizontalLayoutGroup>();
        khl.spacing = 8;
        khl.childAlignment = TextAnchor.MiddleLeft;
        khl.childControlWidth = true;
        khl.childControlHeight = true;
        khl.childForceExpandWidth = false;
        khl.childForceExpandHeight = false;

        var t = Text(row.transform, description, 28, textColor, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        return keys.transform;
    }

    private void GoalRow(Transform column, Sprite icon, int number, string text)
    {
        var row = NewUI("GoalRow", column);
        row.AddComponent<LayoutElement>().minHeight = 110;
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 24;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        Icon(row.transform, icon, 92);

        string accent = ColorUtility.ToHtmlStringRGB(accentColor);
        var t = Text(row.transform, $"<color=#{accent}><b>{number}.</b></color>  {text}", 32,
            textColor, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
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

    private void Keycap(Transform parent, string label, Sprite icon, float w, float h, int fontSize, bool flip = false)
    {
        var root = NewUI("Key", parent);
        var le = root.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = w;
        le.preferredHeight = le.minHeight = h;

        // stÌn pod kl·vesou (p˘sobÌ jako v˝öka)
        var shadow = NewUI("Shadow", root.transform);
        var srt = shadow.GetComponent<RectTransform>();
        Stretch(srt);
        srt.offsetMin = new Vector2(0, -5);
        srt.offsetMax = new Vector2(0, -5);
        var simg = shadow.AddComponent<Image>();
        simg.sprite = rounded;
        simg.type = Image.Type.Sliced;
        simg.color = keyShadow;
        simg.raycastTarget = false;

        var face = NewUI("Face", root.transform);
        Stretch(face.GetComponent<RectTransform>());
        var fimg = face.AddComponent<Image>();
        fimg.sprite = rounded;
        fimg.type = Image.Type.Sliced;
        fimg.color = keyFace;
        fimg.raycastTarget = false;

        if (icon != null)
        {
            var ic = NewUI("Icon", face.transform);
            var irt = ic.GetComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
            irt.sizeDelta = new Vector2(h * 0.55f, h * 0.55f);
            if (flip) irt.localRotation = Quaternion.Euler(0, 0, 180);
            var iimg = ic.AddComponent<Image>();
            iimg.sprite = icon;
            iimg.preserveAspect = true;
            iimg.raycastTarget = false;
        }
        else
        {
            var t = Text(face.transform, label, fontSize, textColor, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);
        }
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

    // ------------------------------------------------------------------
    // Ikony kreslenÈ kÛdem (û·dnÈ externÌ obr·zky)
    // ------------------------------------------------------------------

    private void CreateIcons()
    {
        iconCookie = cookieImage != null ? cookieImage : MakeIcon(128, CookieShader);
        iconSwap = swapImage != null ? swapImage : MakeIcon(128, SwapShader);
        iconCoin = coinImage != null ? coinImage : MakeIcon(128, CoinShader);
        iconUpgrade = upgradeImage != null ? upgradeImage : MakeIcon(128, UpgradeShader);
        iconMouse = mouseImage != null ? mouseImage : MakeIcon(128, MouseShader);
        iconArrow = MakeIcon(128, ArrowShader);
    }

    // Sou¯adnice x,y jsou v rozsahu -1..1, y roste nahoru. VracÌ barvu (alfa 0 = pr˘hlednÈ).

    private static readonly Vector2[] Chips =
    {
        new Vector2(-0.30f,  0.35f), new Vector2( 0.25f,  0.40f), new Vector2( 0.42f, -0.10f),
        new Vector2(-0.45f, -0.15f), new Vector2( 0.00f, -0.50f), new Vector2( 0.30f, -0.45f),
    };

    private static Color CookieShader(float x, float y)
    {
        if (!InCircle(x, y, 0, 0, 0.88f)) return Clear;
        foreach (var c in Chips)
            if (InCircle(x, y, c.x, c.y, 0.11f)) return new Color(0.33f, 0.19f, 0.10f, 1f);
        if (!InCircle(x, y, 0, 0, 0.78f)) return new Color(0.72f, 0.45f, 0.20f, 1f);
        return new Color(0.93f, 0.68f, 0.36f, 1f);
    }

    private static Color SwapShader(float x, float y)
    {
        // hornÌ öipka doprava
        if (InBox(x, y, -0.225f, 0.40f, 0.475f, 0.10f) ||
            InTri(x, y, 0.25f, 0.70f, 0.25f, 0.10f, 0.82f, 0.40f))
            return new Color(0.45f, 0.78f, 1f, 1f);
        // spodnÌ öipka doleva
        if (InBox(x, y, 0.225f, -0.40f, 0.475f, 0.10f) ||
            InTri(x, y, -0.25f, -0.10f, -0.25f, -0.70f, -0.82f, -0.40f))
            return new Color(0.95f, 0.72f, 0.29f, 1f);
        return Clear;
    }

    private static Color CoinShader(float x, float y)
    {
        float d = Mathf.Sqrt(x * x + y * y);
        if (d > 0.88f) return Clear;
        if (d > 0.78f) return new Color(0.85f, 0.58f, 0.10f, 1f);
        if (d > 0.62f && d < 0.70f) return new Color(0.80f, 0.54f, 0.08f, 1f);
        if (InBox(x, y, 0, 0, 0.06f, 0.42f)) return new Color(0.80f, 0.54f, 0.08f, 1f);
        return new Color(0.98f, 0.77f, 0.25f, 1f);
    }

    private static Color UpgradeShader(float x, float y)
    {
        var green = new Color(0.40f, 0.86f, 0.50f, 1f);
        if (InTri(x, y, 0f, 0.90f, -0.62f, 0.20f, 0.62f, 0.20f)) return green;
        if (InBox(x, y, 0f, -0.20f, 0.22f, 0.40f)) return green;
        if (InBox(x, y, 0f, -0.82f, 0.62f, 0.08f)) return green;
        return Clear;
    }

    private static Color MouseShader(float x, float y)
    {
        var outline = new Color(0.85f, 0.87f, 0.94f, 1f);
        var body = new Color(0.16f, 0.17f, 0.25f, 1f);
        var gold = new Color(0.95f, 0.72f, 0.29f, 1f);

        float d = SegDist(x, y, 0f, -0.40f, 0f, 0.40f);
        if (d > 0.55f) return Clear;
        if (d > 0.46f) return outline;
        if (Mathf.Abs(y - 0.15f) < 0.035f) return outline;
        if (y > 0.15f)
        {
            if (Mathf.Abs(x) < 0.04f) return outline;
            if (x < 0f) return gold;
        }
        return body;
    }

    private static Color ArrowShader(float x, float y)
    {
        var white = new Color(0.92f, 0.94f, 0.98f, 1f);
        if (InTri(x, y, 0f, 0.85f, -0.62f, 0.05f, 0.62f, 0.05f)) return white;
        if (InBox(x, y, 0f, -0.35f, 0.22f, 0.45f)) return white;
        return Clear;
    }

    // --- tvary ---

    private static bool InCircle(float x, float y, float cx, float cy, float r)
        => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;

    private static bool InBox(float x, float y, float cx, float cy, float hw, float hh)
        => Mathf.Abs(x - cx) <= hw && Mathf.Abs(y - cy) <= hh;

    private static float TriSign(float px, float py, float ax, float ay, float bx, float by)
        => (px - bx) * (ay - by) - (ax - bx) * (py - by);

    private static bool InTri(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
    {
        float d1 = TriSign(x, y, ax, ay, bx, by);
        float d2 = TriSign(x, y, bx, by, cx, cy);
        float d3 = TriSign(x, y, cx, cy, ax, ay);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0;
        bool pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    private static float SegDist(float px, float py, float ax, float ay, float bx, float by)
    {
        float abx = bx - ax, aby = by - ay;
        float t = Mathf.Clamp01(((px - ax) * abx + (py - ay) * aby) / (abx * abx + aby * aby));
        float dx = px - (ax + abx * t), dy = py - (ay + aby * t);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Vyrenderuje ikonu z funkce (3◊3 supersampling pro hladkÈ okraje).</summary>
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