using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[System.Serializable]
public struct StallItemPrice
{
    public string itemName;
    [Range(1, 12)] public int level;
    [Tooltip("0 = cena podle levelu")] public float customPrice;
}

/// <summary>
/// METALŠROT - výkup šrotu. UI se postaví samo (žádné ruční skládání v Unity).
/// Otevírá se přes StallManager.Instance.OpenStall() (Stall.cs funguje beze změny).
/// </summary>
public class StallManager : MonoBehaviour
{
    public static StallManager Instance;
    public static bool IsOpen { get; private set; }

    [Header("Starý UI (pokud je přiřazený, schová se)")]
    public GameObject stallUI;

    [Header("Ceny: třída předmětu (L1-L8 v ceníku)")]
    [Tooltip("Základní násobek podle třídy předmětu. Běžné věci = 1, vzácné víc.")]
    public float[] itemTierFactors = { 1f, 1.3f, 1.8f, 2.5f, 3.5f, 5f, 7f, 10f };
    public float tierGrowthAfterLast = 1.4f;

    [Header("Ceny: růst podle levelu hráče (LevelManager)")]
    public bool scaleWithPlayerLevel = true;
    [Tooltip("Level 1 = 1$, level 2 = 1.5$, level 3 = 3$ ... pro nejběžnější věc")]
    public float[] playerLevelMultipliers = { 1f, 1.5f, 3f, 5f, 8f, 12f, 20f, 35f };
    public float growthAfterLast = 1.7f;

    [Tooltip("Ruční bonus navíc (1 = žádný)")]
    public float priceMultiplier = 1f;

    [Header("Ceník (automaticky se naplní, když je prázdný)")]
    public List<StallItemPrice> priceList = new List<StallItemPrice>();

    [Header("Barvy")]
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.78f);
    [SerializeField] private Color bodyColor = new Color32(0x2A, 0x2E, 0x33, 255);
    [SerializeField] private Color panelColor = new Color32(0x35, 0x3A, 0x40, 255);
    [SerializeField] private Color rowColor = new Color32(0x41, 0x47, 0x4F, 255);
    [SerializeField] private Color accentColor = new Color32(0xF5, 0xB3, 0x01, 255);
    [SerializeField] private Color rustColor = new Color32(0xD9, 0x6A, 0x2B, 255);
    [SerializeField] private Color textColor = new Color32(0xE8, 0xEA, 0xED, 255);
    [SerializeField] private Color mutedColor = new Color32(0x9A, 0xA3, 0xAD, 255);
    [SerializeField] private Color goodColor = new Color32(0x6B, 0xE0, 0x8A, 255);
    [SerializeField] private Color badColor = new Color32(0xFF, 0x5A, 0x4D, 255);
    [SerializeField] private Color darkColor = new Color32(0x16, 0x18, 0x1B, 255);
    [SerializeField] private Color disabledColor = new Color32(0x5A, 0x5F, 0x66, 255);

    private static readonly Color32[] LevelColors =
    {
        new Color32(0x9A,0xA3,0xAD,255), new Color32(0x6B,0xE0,0x8A,255), new Color32(0x4F,0xB3,0xFF,255),
        new Color32(0xB0,0x7C,0xFF,255), new Color32(0xFF,0x9A,0x3C,255), new Color32(0xFF,0x5A,0x4D,255),
        new Color32(0xF5,0xB3,0x01,255), new Color32(0x5C,0xF2,0xE6,255)
    };

    private const int VisibleRows = 6;
    private const float HookSize = 96f;
    private static readonly Color Clear = new Color(0, 0, 0, 0);

    private class Entry { public string name; public int count; public int level; public float price; }

    // --- UI reference ---
    private GameObject overlay;
    private CanvasGroup overlayGroup, windowGroup;
    private RectTransform canvasRt, windowRt, cursorRoot, hookRt, cableRt, sellRt, sellAllRt;
    private Image hookImg, detailIcon, sellImg, sellAllImg;
    private Image[] pips = new Image[8];
    private TextMeshProUGUI walletText, countText, emptyText, detailName, detailLetter, detailLevel,
        detailPrice, detailOwned, messageText, sellLabel, sellAllLabel;
    private StallHoverFlag sellFlag, sellAllFlag;
    private Sprite white, rounded, circle, stripe, hookOpen, hookClosed, craneSprite;
    private readonly List<StallRow> rows = new List<StallRow>();

    // --- stav ---
    private readonly List<Entry> entries = new List<Entry>();
    private int selected, scroll;
    private bool closing, keyboardMode;
    private float openTime, messageTimer, shownMoney, hookAngle, hookVel, hookScale = 1f;
    private int lastMoneyShown = int.MinValue;
    private Color messageBase;
    private Vector2 lastMouse, cursorPos, prevCursorPos;

    private bool InputReady => Time.unscaledTime - openTime > 0.35f;

    // ------------------------------------------------------------------
    // Životní cyklus
    // ------------------------------------------------------------------

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (stallUI != null) stallUI.SetActive(false);

        if (priceList.Count == 0 || priceList[0].level == 0)
        {
            priceList.Clear();
            InitializeDefaultPrices();
        }

        white = CreateWhiteSprite();
        rounded = CreateRoundedSprite(32, 7);
        circle = MakeSprite(96, 96, (u, v) => InCircle(u * 2 - 1, v * 2 - 1, 0.97f) ? Color.white : Clear, false);
        stripe = MakeSprite(64, 64, StripeShader, true);
        hookOpen = MakeSprite(96, 96, (u, v) => HookShader(u, v, false), false);
        hookClosed = MakeSprite(96, 96, (u, v) => HookShader(u, v, true), false);
        craneSprite = MakeSprite(240, 120, CraneShader, false);

        EnsureEventSystem();
        Build();
        overlay.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        IsOpen = false;
    }

    // ------------------------------------------------------------------
    // Ceník
    // ------------------------------------------------------------------

    public void RegisterItem(string itemName, int level, float customPrice = 0f)
    {
        priceList.Add(new StallItemPrice { itemName = itemName, level = Mathf.Max(1, level), customPrice = customPrice });
    }

    private void InitializeDefaultPrices()
    {
        // Měna (speciální cena, neškáluje se levelem)
        RegisterItem("Cookies", 1, 0.001f);

        // ===== COMMON (třída 1-2) =====
        // L1
        foreach (var n in new[] { "Bowl", "Plate", "ShallowPlate", "SimpleCup", "SimpleMug", "RoughHoe", "RoughKnife" }) RegisterItem(n, 1);
        // L2
        foreach (var n in new[] { "Bag", "Vase", "Barrel", "RoughPickaxe", "RoughAxe", "RoughShovel", "RoughHammer", "RoughSpear" }) RegisterItem(n, 2);

        // ===== UNCOMMON (třída 3-4) =====
        // L3
        foreach (var n in new[] { "MetalKnife", "MetalHoe", "WoodenBat", "WallLamp", "CeilingLamp", "BarnFork", "MetalPickaxe", "MetalAxe", "MetalShovel", "MetalHammer" }) RegisterItem(n, 3);
        // L4
        foreach (var n in new[] { "MetalSpear", "ClaveHammer", "MetalBat", "Steampunk", "Chest", "Flute" }) RegisterItem(n, 4);

        // ===== RARE (třída 5-6) =====
        // L5
        foreach (var n in new[] { "GolfBat", "GolfClub", "PanFlute" }) RegisterItem(n, 5);
        // L6
        foreach (var n in new[] { "Guitar", "Katana" }) RegisterItem(n, 6);

        // ===== LEGENDARY (třída 7-8) =====
        // L7
        foreach (var n in new[] { "Wand", "Scythe" }) RegisterItem(n, 7);
        // L8
        RegisterItem("Staff", 8);
    }

    public static string RarityName(int tier)
    {
        if (tier <= 2) return "Běžné";
        if (tier <= 4) return "Neobvyklé";
        if (tier <= 6) return "Vzácné";
        return "Legendární";
    }

    public int PlayerLevel => LevelManager.Instance != null ? LevelManager.Instance.currentLevel : 1;

    /// <summary>Násobek ceny podle levelu hráče (1, 1.5, 3, 5, 8...).</summary>
    public float LevelMultiplier()
    {
        if (!scaleWithPlayerLevel || playerLevelMultipliers == null || playerLevelMultipliers.Length == 0) return 1f;
        int n = playerLevelMultipliers.Length;
        int lv = Mathf.Max(1, PlayerLevel);
        return lv <= n ? playerLevelMultipliers[lv - 1]
                       : playerLevelMultipliers[n - 1] * Mathf.Pow(growthAfterLast, lv - n);
    }

    /// <summary>Cena předmětu dané třídy při aktuálním levelu hráče.</summary>
    public float PriceForLevel(int tier)
    {
        tier = Mathf.Max(1, tier);
        float factor = 1f;
        if (itemTierFactors != null && itemTierFactors.Length > 0)
        {
            int n = itemTierFactors.Length;
            factor = tier <= n ? itemTierFactors[tier - 1]
                               : itemTierFactors[n - 1] * Mathf.Pow(tierGrowthAfterLast, tier - n);
        }
        return factor * LevelMultiplier() * priceMultiplier;
    }

    private static string Norm(string s) =>
        string.IsNullOrEmpty(s) ? "" : s.Replace(" ", "").Replace("_", "").ToLowerInvariant();

    public float GetItemPrice(string itemName) => GetItemPrice(itemName, out _);

    public float GetItemPrice(string itemName, out int level)
    {
        string n = Norm(itemName);
        int best = -1, bestLen = 0;
        for (int i = 0; i < priceList.Count; i++)
        {
            string k = Norm(priceList[i].itemName);
            if (k.Length > bestLen && n.Contains(k)) { best = i; bestLen = k.Length; }
        }

        if (best < 0) { level = 1; return PriceForLevel(1); }

        var def = priceList[best];
        level = Mathf.Max(1, def.level);
        return def.customPrice > 0f ? def.customPrice * priceMultiplier : PriceForLevel(level);
    }

    private static string Money(float v) => v < 0.1f ? v.ToString("F3") : v.ToString("F2");

    // ------------------------------------------------------------------
    // Veřejné API
    // ------------------------------------------------------------------

    public void OpenStall()
    {
        if (IsOpen || closing) return;

        StopAllCoroutines();
        IsOpen = true;
        openTime = Time.unscaledTime;
        keyboardMode = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false; // místo něj je jeřábový hák

        foreach (Transform ch in overlay.transform)
            if (ch.name == "Chip") Destroy(ch.gameObject);

        selected = 0; scroll = 0;
        entries.Clear();
        RefreshAll(null);

        var inv = InventoryManager.Instance;
        shownMoney = inv != null ? inv.money : 0f;
        lastMoneyShown = int.MinValue;
        messageTimer = 0f;
        messageText.color = Clear;

        overlay.SetActive(true);
        overlayGroup.alpha = 0f;
        windowGroup.alpha = 0f;
        windowRt.localScale = Vector3.one * 0.88f;
        hookAngle = hookVel = 0f;
        SnapCursor();

        StartCoroutine(Tween(0.3f, 0f, t => overlayGroup.alpha = EaseOutCubic(t)));
        StartCoroutine(Tween(0.5f, 0f, t =>
        {
            windowGroup.alpha = Mathf.Clamp01(EaseOutCubic(t) * 1.4f);
            windowRt.localScale = Vector3.one * Mathf.LerpUnclamped(0.88f, 1f, EaseOutBack(t));
            windowRt.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(-60f, 0f, EaseOutCubic(t)));
        }));
        for (int i = 0; i < rows.Count; i++) StartCoroutine(RowIn(rows[i], 0.22f + 0.07f * i));
    }

    public void CloseStall()
    {
        if (!IsOpen || closing) return;
        StopAllCoroutines();
        StartCoroutine(CloseSequence());
    }

    // ------------------------------------------------------------------
    // Prodej
    // ------------------------------------------------------------------

    private void Sell(bool all)
    {
        if (closing) return;
        var inv = InventoryManager.Instance;
        if (inv == null) return;

        if (entries.Count == 0)
        {
            ShowMessage("Nemáš co prodat", badColor);
            StartCoroutine(Punch(sellRt, 0.9f));
            return;
        }

        var e = entries[selected];
        int amount = all ? e.count : 1;

        if (!inv.RemoveItems(e.name, amount))
        {
            ShowMessage("Prodej se nezdařil", badColor);
            return;
        }

        float total = e.price * amount;
        inv.AddMoney(total);
        Debug.Log($"Prodáno: {amount}x {e.name} za {total} $!");

        ShowMessage($"+{Money(total)} $", goodColor);
        SpawnChip(LevelColors[Mathf.Clamp(e.level - 1, 0, LevelColors.Length - 1)]);
        StartCoroutine(Punch(all ? sellAllRt : sellRt, 0.9f));

        int slot = selected - scroll;
        if (slot >= 0 && slot < rows.Count) StartCoroutine(PunchRow(rows[slot]));

        RefreshAll(e.name);
    }

    // ------------------------------------------------------------------
    // Data -> UI
    // ------------------------------------------------------------------

    private void BuildEntries()
    {
        entries.Clear();
        var inv = InventoryManager.Instance;
        if (inv == null) return;

        var map = new Dictionary<string, Entry>();
        foreach (var n in inv.items)
        {
            if (string.IsNullOrEmpty(n)) continue;
            if (map.TryGetValue(n, out var ex)) { ex.count++; continue; }

            int lv;
            float p = GetItemPrice(n, out lv);
            var ne = new Entry { name = n, count = 1, level = lv, price = p };
            map[n] = ne;
            entries.Add(ne);
        }

        entries.Sort((a, b) =>
        {
            int c = b.price.CompareTo(a.price);
            return c != 0 ? c : string.CompareOrdinal(a.name, b.name);
        });
    }

    private void RefreshAll(string keepName)
    {
        BuildEntries();
        if (keepName != null)
        {
            int f = entries.FindIndex(x => x.name == keepName);
            if (f >= 0) selected = f;
        }
        ClampSelection();
        RefreshList();
        RefreshDetail();
    }

    private void ClampSelection()
    {
        if (entries.Count == 0) { selected = 0; scroll = 0; return; }
        selected = Mathf.Clamp(selected, 0, entries.Count - 1);
        if (selected < scroll) scroll = selected;
        else if (selected >= scroll + VisibleRows) scroll = selected - VisibleRows + 1;
        scroll = Mathf.Clamp(scroll, 0, Mathf.Max(0, entries.Count - VisibleRows));
    }

    private void SetSelected(int index)
    {
        if (entries.Count == 0) return;
        selected = index;
        ClampSelection();
        RefreshList();
        RefreshDetail();
    }

    private void RefreshList()
    {
        bool empty = entries.Count == 0;
        emptyText.gameObject.SetActive(empty);
        countText.text = empty ? "Batoh je prázdný"
            : $"Položky: {selected + 1} / {entries.Count}      (kolečko myši = rolování)";

        for (int i = 0; i < rows.Count; i++)
        {
            int idx = scroll + i;
            var r = rows[i];
            if (idx >= entries.Count) { r.gameObject.SetActive(false); continue; }

            r.gameObject.SetActive(true);
            var e = entries[idx];
            Color lc = LevelColors[Mathf.Clamp(e.level - 1, 0, LevelColors.Length - 1)];
            r.badge.color = lc;
            r.levelNum.text = e.level.ToString();
            r.nameText.text = Pretty(e.name);
            r.subText.text = "Úroveň " + e.level;
            r.subText.color = lc;
            r.countText.text = "×" + e.count;
            r.priceText.text = Money(e.price) + " $";
        }
    }

    private void RefreshDetail()
    {
        bool has = entries.Count > 0;
        if (!has)
        {
            detailName.text = "-";
            detailLetter.text = "?";
            detailLevel.text = "";
            detailPrice.text = "0 $";
            detailOwned.text = "";
            detailIcon.color = disabledColor;
            for (int i = 0; i < pips.Length; i++) pips[i].color = new Color(1, 1, 1, 0.12f);
            sellLabel.text = "PRODAT";
            sellAllLabel.text = "PRODAT VŠE";
            return;
        }

        var e = entries[selected];
        Color lc = LevelColors[Mathf.Clamp(e.level - 1, 0, LevelColors.Length - 1)];
        string pretty = Pretty(e.name);

        detailName.text = pretty;
        detailLetter.text = pretty.Substring(0, 1);
        detailIcon.color = lc;
        detailLevel.text = "ÚROVEŇ " + e.level;
        detailLevel.color = lc;
        detailPrice.text = Money(e.price) + " $";
        detailOwned.text = "V batohu: ×" + e.count;

        for (int i = 0; i < pips.Length; i++)
            pips[i].color = i < e.level ? lc : new Color(1, 1, 1, 0.12f);

        sellLabel.text = "PRODAT ×1";
        sellAllLabel.text = $"PRODAT VŠE ×{e.count}";
    }

    private void ShowMessage(string text, Color color)
    {
        messageText.text = text;
        messageBase = color;
        messageTimer = 1.8f;
    }

    // ------------------------------------------------------------------
    // Každý snímek
    // ------------------------------------------------------------------

    private void Update()
    {
        if (overlay == null || !overlay.activeSelf) return;

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        float k = 1f - Mathf.Exp(-14f * dt);

        if (IsOpen && !closing) HandleInput();

        // řádky
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (!r.gameObject.activeSelf) continue;
            int idx = scroll + i;
            r.sel = Mathf.Lerp(r.sel, idx == selected ? 1f : 0f, k);
            var bc = accentColor; bc.a = r.sel;
            r.border.color = bc;
            r.visual.localScale = Vector3.one * ((1f + 0.02f * r.sel) * r.pop);
            r.visual.anchoredPosition = new Vector2(r.entranceX + r.shakeX + 10f * r.sel, 0f);
        }

        // peníze
        var inv = InventoryManager.Instance;
        float money = inv != null ? inv.money : 0f;
        shownMoney = Mathf.Lerp(shownMoney, money, 1f - Mathf.Exp(-6f * dt));
        if (Mathf.Abs(shownMoney - money) < 0.0005f) shownMoney = money;
        int mi = Mathf.RoundToInt(shownMoney * 1000f);
        if (mi != lastMoneyShown)
        {
            lastMoneyShown = mi;
            walletText.text = Money(shownMoney) + " $";
        }

        // hláška
        if (messageTimer > 0f)
        {
            messageTimer -= dt;
            var mc = messageBase; mc.a = Mathf.Clamp01(messageTimer);
            messageText.color = mc;
        }

        // tlačítka
        bool can = entries.Count > 0;
        sellImg.color = Color.Lerp(sellImg.color, can ? accentColor : disabledColor, k);
        sellAllImg.color = Color.Lerp(sellAllImg.color, can ? rustColor : disabledColor, k);

        UpdateCursor(dt);
    }

    private void HandleInput()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.escapeKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) { CloseStall(); return; }

            if (entries.Count > 0)
            {
                if (kb.downArrowKey.wasPressedThisFrame) { keyboardMode = true; SetSelected((selected + 1) % entries.Count); }
                else if (kb.upArrowKey.wasPressedThisFrame) { keyboardMode = true; SetSelected((selected - 1 + entries.Count) % entries.Count); }
            }

            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                keyboardMode = true;
                Sell(kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
            }
        }

        var mouse = Mouse.current;
        if (mouse != null && entries.Count > 0)
        {
            float wheel = mouse.scroll.ReadValue().y;
            if (wheel > 0.1f) SetSelected(Mathf.Max(0, selected - 1));
            else if (wheel < -0.1f) SetSelected(Mathf.Min(entries.Count - 1, selected + 1));
        }
    }

    // ------------------------------------------------------------------
    // Jeřábový kurzor
    // ------------------------------------------------------------------

    private Vector2 CursorTarget(out bool atRow)
    {
        atRow = false;
        var mouse = Mouse.current;
        Vector2 sp = mouse != null ? mouse.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        if (keyboardMode)
        {
            int slot = selected - scroll;
            if (slot >= 0 && slot < rows.Count && rows[slot].gameObject.activeSelf)
            {
                Vector3 wp = rows[slot].visual.TransformPoint(new Vector3(60f, 0f, 0f));
                sp = RectTransformUtility.WorldToScreenPoint(null, wp);
                atRow = true;
            }
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, null, out Vector2 lp);
        return lp;
    }

    private void SnapCursor()
    {
        cursorPos = prevCursorPos = CursorTarget(out _);
        var mouse = Mouse.current;
        lastMouse = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
        cursorRoot.anchoredPosition = cursorPos;
    }

    private void UpdateCursor(float dt)
    {
        var mouse = Mouse.current;
        Vector2 mp = mouse != null ? mouse.position.ReadValue() : lastMouse;
        if ((mp - lastMouse).sqrMagnitude > 4f) keyboardMode = false;
        lastMouse = mp;

        Vector2 target = CursorTarget(out bool atRow);
        cursorPos = keyboardMode ? Vector2.Lerp(cursorPos, target, 1f - Mathf.Exp(-16f * dt)) : target;
        cursorRoot.anchoredPosition = cursorPos;

        // houpání háku (tlumená pružina)
        Vector2 vel = (cursorPos - prevCursorPos) / Mathf.Max(dt, 0.0001f);
        prevCursorPos = cursorPos;
        float targetAngle = Mathf.Clamp(-vel.x * 0.012f, -28f, 28f) + Mathf.Sin(Time.unscaledTime * 1.7f) * 2f;
        hookVel += ((targetAngle - hookAngle) * 120f - hookVel * 9f) * dt;
        hookAngle += hookVel * dt;
        hookRt.localRotation = Quaternion.Euler(0f, 0f, hookAngle);

        // stav háku: sevřený při najetí / kliknutí
        bool anyHover = (sellFlag != null && sellFlag.hovered) || (sellAllFlag != null && sellAllFlag.hovered);
        foreach (var r in rows) if (r.hovered && r.gameObject.activeSelf) anyHover = true;
        bool grab = anyHover || (mouse != null && mouse.leftButton.isPressed)
                    || (keyboardMode && atRow && (cursorPos - target).sqrMagnitude < 400f);

        hookImg.sprite = grab ? hookClosed : hookOpen;
        hookScale = Mathf.Lerp(hookScale, grab ? 1.12f : 1f, 1f - Mathf.Exp(-16f * dt));
        hookRt.localScale = Vector3.one * hookScale;

        // lano od horní hrany obrazovky k háku
        float topY = canvasRt.rect.height * 0.5f;
        float pivotY = HookPivotY();
        cableRt.sizeDelta = new Vector2(6f, Mathf.Max(10f, topY - cursorPos.y - pivotY + 30f));
    }

    private static float HookPivotY() => 0.715f * HookSize;
    private static float HookTipOffsetX() => -0.4f * HookSize * 0.5f;

    private void SpawnChip(Color c)
    {
        var go = NewUI("Chip", overlay.transform);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(58, 58);
        var img = go.AddComponent<Image>();
        SetRounded(img, c);
        img.raycastTarget = false;
        var grp = go.AddComponent<CanvasGroup>();
        var lbl = Text(go.transform, "$", 34, darkColor, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(lbl.rectTransform);

        Vector2 start = cursorPos + new Vector2(HookTipOffsetX() + 20f, 34f);
        float drift = Random.Range(-18f, 18f);
        StartCoroutine(ChipRoutine(go, rt, grp, start, drift));
        cursorRoot.SetAsLastSibling();
    }

    private IEnumerator ChipRoutine(GameObject go, RectTransform rt, CanvasGroup grp, Vector2 start, float drift)
    {
        yield return StartCoroutine(Tween(0.8f, 0f, t =>
        {
            if (rt == null) return;
            float e = EaseInCubic(t);
            rt.anchoredPosition = start + new Vector2(drift * t, 380f * e);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 9f) * 10f);
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, t);
            grp.alpha = 1f - Mathf.Clamp01((t - 0.55f) / 0.45f);
        }));
        if (go != null) Destroy(go);
    }

    // ------------------------------------------------------------------
    // Animace
    // ------------------------------------------------------------------

    private IEnumerator RowIn(StallRow r, float delay)
    {
        r.group.alpha = 0f;
        r.entranceX = 80f;
        yield return StartCoroutine(Tween(0.45f, delay, t =>
        {
            r.group.alpha = EaseOutCubic(t);
            r.entranceX = Mathf.LerpUnclamped(80f, 0f, EaseOutBack(t));
        }));
        r.group.alpha = 1f;
        r.entranceX = 0f;
    }

    private IEnumerator PunchRow(StallRow r)
    {
        yield return StartCoroutine(Tween(0.35f, 0f, t => r.pop = Mathf.LerpUnclamped(0.93f, 1f, EaseOutBack(t))));
        r.pop = 1f;
    }

    private IEnumerator Punch(RectTransform rt, float from)
    {
        yield return StartCoroutine(Tween(0.35f, 0f, t =>
            rt.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, EaseOutBack(t))));
        rt.localScale = Vector3.one;
    }

    private IEnumerator CloseSequence()
    {
        closing = true;
        float a0 = overlayGroup.alpha, c0 = windowGroup.alpha, s0 = windowRt.localScale.x;
        yield return StartCoroutine(Tween(0.25f, 0f, t =>
        {
            float e = EaseInCubic(t);
            overlayGroup.alpha = Mathf.Lerp(a0, 0f, e);
            windowGroup.alpha = Mathf.Lerp(c0, 0f, e);
            windowRt.localScale = Vector3.one * Mathf.Lerp(s0, 0.92f, e);
        }));

        overlay.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        closing = false;
        IsOpen = false;
    }

    private IEnumerator Tween(float duration, float delay, System.Action<float> apply)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            apply(Mathf.Clamp01(t / duration));
            yield return null;
        }
        apply(1f);
    }

    private static float EaseOutCubic(float t) { t = 1f - t; return 1f - t * t * t; }
    private static float EaseInCubic(float t) => t * t * t;
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    // ------------------------------------------------------------------
    // Stavba UI
    // ------------------------------------------------------------------

    private void Build()
    {
        var canvasGo = new GameObject("StallCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 170;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasRt = canvasGo.GetComponent<RectTransform>();

        // zatemnění (klik mimo okno = zavřít)
        overlay = NewUI("StallOverlay", canvasGo.transform);
        Stretch(overlay.GetComponent<RectTransform>());
        var dim = overlay.AddComponent<Image>();
        dim.color = dimColor;
        overlayGroup = overlay.AddComponent<CanvasGroup>();
        var dimBtn = overlay.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.targetGraphic = dim;
        var dn = dimBtn.navigation; dn.mode = Navigation.Mode.None; dimBtn.navigation = dn;
        dimBtn.onClick.AddListener(() => { if (InputReady) CloseStall(); });

        // okno
        var win = NewUI("Window", overlay.transform);
        windowRt = win.GetComponent<RectTransform>();
        windowRt.anchorMin = windowRt.anchorMax = windowRt.pivot = new Vector2(0.5f, 0.5f);
        windowRt.sizeDelta = new Vector2(1180, 770);
        windowGroup = win.AddComponent<CanvasGroup>();

        var frame = NewUI("Frame", win.transform);
        var frt = frame.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(-5, -5); frt.offsetMax = new Vector2(5, 5);
        SetRounded(frame.AddComponent<Image>(), accentColor);

        var body = NewUI("Body", win.transform);
        Stretch(body.GetComponent<RectTransform>());
        SetRounded(body.AddComponent<Image>(), bodyColor);
        VLG(body, 0, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft, true);

        BuildStripe(body.transform, 18);
        BuildHeader(body.transform);

        var line = NewUI("Line", body.transform);
        line.AddComponent<Image>().color = rustColor;
        line.AddComponent<LayoutElement>().preferredHeight = 3;

        var main = NewUI("Main", body.transform);
        main.AddComponent<LayoutElement>().flexibleHeight = 1;
        HLG(main, 26, new RectOffset(32, 32, 20, 14), TextAnchor.UpperLeft, true);
        BuildListColumn(main.transform);
        BuildDetailColumn(main.transform);

        string a = ColorUtility.ToHtmlStringRGB(accentColor);
        var hint = Text(body.transform,
            $"<color=#{a}>[↑ ↓ / kolečko]</color> výběr    <color=#{a}>[Enter / klik]</color> prodat 1    <color=#{a}>[Shift+Enter]</color> prodat vše    <color=#{a}>[Esc / E]</color> zavřít",
            22, mutedColor, FontStyles.Normal, TextAlignmentOptions.Center);
        hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;

        BuildStripe(body.transform, 12);

        BuildCursor(overlay.transform);
    }

    private void BuildStripe(Transform parent, float height)
    {
        var go = NewUI("Stripe", parent);
        var img = go.AddComponent<Image>();
        img.sprite = stripe;
        img.type = Image.Type.Tiled;
        img.raycastTarget = false;
        go.AddComponent<LayoutElement>().preferredHeight = height;
    }

    private void BuildHeader(Transform parent)
    {
        var header = NewUI("Header", parent);
        header.AddComponent<LayoutElement>().preferredHeight = 128;
        HLG(header, 24, new RectOffset(40, 40, 14, 10), TextAnchor.MiddleLeft);

        var tcol = NewUI("Title", header.transform);
        tcol.AddComponent<LayoutElement>().flexibleWidth = 1;
        VLG(tcol, 2, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft, true);
        var title = Text(tcol.transform, "METALŠROT", 68, accentColor, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        title.characterSpacing = 10;
        Text(tcol.transform, "VÝKUP KOVU, ŠROTU A NÁŘADÍ  |  nejlepší ceny ve městě", 24, mutedColor,
            FontStyles.Normal, TextAlignmentOptions.MidlineLeft);

        Icon(header.transform, craneSprite, 220, 110);

        var wal = NewUI("Wallet", header.transform);
        SetRounded(wal.AddComponent<Image>(), panelColor);
        var wle = wal.AddComponent<LayoutElement>();
        wle.preferredWidth = wle.minWidth = 270;
        wle.preferredHeight = wle.minHeight = 92;
        VLG(wal, 0, new RectOffset(18, 18, 10, 10), TextAnchor.MiddleCenter, true);
        var wl = Text(wal.transform, "TVOJE PENÍZE", 20, mutedColor, FontStyles.Bold, TextAlignmentOptions.Center);
        wl.characterSpacing = 4;
        walletText = Text(wal.transform, "0 $", 44, goodColor, FontStyles.Bold, TextAlignmentOptions.Center);
    }

    private void BuildListColumn(Transform parent)
    {
        var left = NewUI("ListCol", parent);
        var lle = left.AddComponent<LayoutElement>();
        lle.preferredWidth = lle.minWidth = 650;
        lle.flexibleWidth = 0;
        VLG(left, 8, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft, true);

        countText = Text(left.transform, "", 22, mutedColor, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        countText.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;

        var list = NewUI("Rows", left.transform);
        list.AddComponent<LayoutElement>().flexibleHeight = 1;
        VLG(list, 8, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft, true);

        for (int i = 0; i < VisibleRows; i++) rows.Add(CreateRow(i, list.transform));

        emptyText = Text(list.transform, "Nemáš nic na prodej.\nPřines mi nějaký šrot!", 30, mutedColor,
            FontStyles.Italic, TextAlignmentOptions.Center);
        emptyText.gameObject.AddComponent<LayoutElement>().preferredHeight = 200;
    }

    private StallRow CreateRow(int slot, Transform parent)
    {
        var root = NewUI("Row", parent);
        var le = root.AddComponent<LayoutElement>();
        le.preferredHeight = le.minHeight = 68;

        var visual = NewUI("Visual", root.transform);
        var vrt = visual.GetComponent<RectTransform>();
        Stretch(vrt);
        var group = visual.AddComponent<CanvasGroup>();

        var border = NewUI("Border", visual.transform);
        Stretch(border.GetComponent<RectTransform>());
        var borderImg = border.AddComponent<Image>();
        SetRounded(borderImg, new Color(accentColor.r, accentColor.g, accentColor.b, 0f));
        borderImg.raycastTarget = false;

        var face = NewUI("Face", visual.transform);
        var frt = face.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(3, 3); frt.offsetMax = new Vector2(-3, -3);
        SetRounded(face.AddComponent<Image>(), rowColor);
        HLG(face, 14, new RectOffset(16, 18, 8, 8), TextAnchor.MiddleLeft);

        // odznak s levelem
        var badge = NewUI("Badge", face.transform);
        var bimg = badge.AddComponent<Image>();
        bimg.sprite = circle;
        bimg.raycastTarget = false;
        var ble = badge.AddComponent<LayoutElement>();
        ble.preferredWidth = ble.minWidth = 46;
        ble.preferredHeight = ble.minHeight = 46;
        var num = Text(badge.transform, "1", 26, darkColor, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(num.rectTransform);

        // názvy
        var texts = NewUI("Texts", face.transform);
        texts.AddComponent<LayoutElement>().flexibleWidth = 1;
        VLG(texts, 0, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft, true);
        var nameT = Text(texts.transform, "", 28, textColor, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        nameT.overflowMode = TextOverflowModes.Ellipsis;
        var subT = Text(texts.transform, "", 20, mutedColor, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);

        var cnt = Text(face.transform, "", 28, mutedColor, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
        var cle = cnt.gameObject.AddComponent<LayoutElement>();
        cle.preferredWidth = cle.minWidth = 70;

        var price = Text(face.transform, "", 32, goodColor, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
        var ple = price.gameObject.AddComponent<LayoutElement>();
        ple.preferredWidth = ple.minWidth = 140;

        var view = root.AddComponent<StallRow>();
        view.slot = slot;
        view.visual = vrt;
        view.group = group;
        view.border = borderImg;
        view.badge = bimg;
        view.levelNum = num;
        view.nameText = nameT;
        view.subText = subT;
        view.countText = cnt;
        view.priceText = price;
        view.onHover = s =>
        {
            if (closing) return;
            int idx = scroll + s;
            if (idx < entries.Count && idx != selected) SetSelected(idx);
        };
        view.onClick = s =>
        {
            if (closing || !InputReady) return;
            int idx = scroll + s;
            if (idx >= entries.Count) return;
            if (idx != selected) SetSelected(idx);
            Sell(false);
        };
        return view;
    }

    private void BuildDetailColumn(Transform parent)
    {
        var right = NewUI("Detail", parent);
        right.AddComponent<LayoutElement>().flexibleWidth = 1;
        SetRounded(right.AddComponent<Image>(), panelColor);
        VLG(right, 12, new RectOffset(28, 28, 22, 22), TextAnchor.UpperCenter, true);

        var lab = Text(right.transform, "VYBRANÁ POLOŽKA", 22, mutedColor, FontStyles.Bold, TextAlignmentOptions.Center);
        lab.characterSpacing = 6;

        // ikona + název + level
        var top = NewUI("Top", right.transform);
        top.AddComponent<LayoutElement>().preferredHeight = 110;
        HLG(top, 18, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

        var iconBox = NewUI("Icon", top.transform);
        var ile = iconBox.AddComponent<LayoutElement>();
        ile.preferredWidth = ile.minWidth = 100;
        ile.preferredHeight = ile.minHeight = 100;
        detailIcon = iconBox.AddComponent<Image>();
        SetRounded(detailIcon, mutedColor);
        detailIcon.raycastTarget = false;
        detailLetter = Text(iconBox.transform, "?", 64, darkColor, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(detailLetter.rectTransform);

        var info = NewUI("Info", top.transform);
        info.AddComponent<LayoutElement>().flexibleWidth = 1;
        VLG(info, 4, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft, true);
        detailName = Text(info.transform, "", 32, textColor, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        detailLevel = Text(info.transform, "", 22, mutedColor, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        detailLevel.characterSpacing = 4;

        var pipRow = NewUI("Pips", info.transform);
        pipRow.AddComponent<LayoutElement>().preferredHeight = 14;
        HLG(pipRow, 5, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);
        for (int i = 0; i < pips.Length; i++)
        {
            var p = NewUI("Pip", pipRow.transform);
            var pi = p.AddComponent<Image>();
            pi.sprite = white;
            pi.raycastTarget = false;
            var ple = p.AddComponent<LayoutElement>();
            ple.preferredWidth = ple.minWidth = 26;
            ple.preferredHeight = ple.minHeight = 10;
            pips[i] = pi;
        }

        var div = NewUI("Div", right.transform);
        div.AddComponent<Image>().color = new Color(1, 1, 1, 0.1f);
        div.AddComponent<LayoutElement>().preferredHeight = 2;

        var pl = Text(right.transform, "VÝKUPNÍ CENA", 20, mutedColor, FontStyles.Bold, TextAlignmentOptions.Center);
        pl.characterSpacing = 6;
        detailPrice = Text(right.transform, "", 62, goodColor, FontStyles.Bold, TextAlignmentOptions.Center);
        detailOwned = Text(right.transform, "", 24, mutedColor, FontStyles.Normal, TextAlignmentOptions.Center);

        var spacer = NewUI("Spacer", right.transform);
        spacer.AddComponent<LayoutElement>().flexibleHeight = 1;

        messageText = Text(right.transform, "", 28, goodColor, FontStyles.Bold, TextAlignmentOptions.Center);
        messageText.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;

        var b1 = CreateButton(right.transform, "PRODAT ×1", accentColor, out sellImg, out sellRt, out sellLabel, () => Sell(false));
        sellFlag = b1.gameObject.AddComponent<StallHoverFlag>();
        var b2 = CreateButton(right.transform, "PRODAT VŠE", rustColor, out sellAllImg, out sellAllRt, out sellAllLabel, () => Sell(true));
        sellAllFlag = b2.gameObject.AddComponent<StallHoverFlag>();
    }

    private Button CreateButton(Transform parent, string label, Color color, out Image img,
        out RectTransform rt, out TextMeshProUGUI lbl, System.Action onClick)
    {
        var go = NewUI("Button", parent);
        rt = go.GetComponent<RectTransform>();
        img = go.AddComponent<Image>();
        SetRounded(img, color);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = le.minHeight = 58;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
        var cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        cb.selectedColor = Color.white;
        btn.colors = cb;
        btn.onClick.AddListener(() =>
        {
            if (InputReady && !closing) onClick();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        });

        lbl = Text(go.transform, label, 28, darkColor, FontStyles.Bold, TextAlignmentOptions.Center);
        lbl.characterSpacing = 3;
        Stretch(lbl.rectTransform);
        return btn;
    }

    private void BuildCursor(Transform parent)
    {
        var cr = NewUI("CraneCursor", parent);
        cursorRoot = cr.GetComponent<RectTransform>();
        cursorRoot.anchorMin = cursorRoot.anchorMax = cursorRoot.pivot = new Vector2(0.5f, 0.5f);
        cursorRoot.sizeDelta = Vector2.zero;

        float px = HookTipOffsetX();
        float py = HookPivotY();

        var cable = NewUI("Cable", cr.transform);
        cableRt = cable.GetComponent<RectTransform>();
        cableRt.anchorMin = cableRt.anchorMax = new Vector2(0.5f, 0.5f);
        cableRt.pivot = new Vector2(0.5f, 0f);
        cableRt.anchoredPosition = new Vector2(px, py);
        var ci = cable.AddComponent<Image>();
        ci.sprite = white;
        ci.color = new Color32(0x1E, 0x20, 0x24, 255);
        ci.raycastTarget = false;

        var hook = NewUI("Hook", cr.transform);
        hookRt = hook.GetComponent<RectTransform>();
        hookRt.anchorMin = hookRt.anchorMax = new Vector2(0.5f, 0.5f);
        hookRt.pivot = new Vector2(0.45f, 0.715f);
        hookRt.sizeDelta = new Vector2(HookSize, HookSize);
        hookRt.anchoredPosition = new Vector2(px, py);
        hookImg = hook.AddComponent<Image>();
        hookImg.sprite = hookOpen;
        hookImg.raycastTarget = false;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    // ------------------------------------------------------------------
    // Pomocné funkce
    // ------------------------------------------------------------------

    /// <summary>"RoughKnife" -> "Rough Knife"</summary>
    private static string Pretty(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "?";
        var sb = new StringBuilder();
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(raw[i - 1])) sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }

    private void SetRounded(Image img, Color color)
    {
        img.sprite = rounded;
        img.type = Image.Type.Sliced;
        img.color = color;
    }

    private static VerticalLayoutGroup VLG(GameObject go, float spacing, RectOffset padding, TextAnchor align, bool expandWidth)
    {
        var v = go.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing;
        v.padding = padding;
        v.childAlignment = align;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = expandWidth;
        v.childForceExpandHeight = false;
        return v;
    }

    private static HorizontalLayoutGroup HLG(GameObject go, float spacing, RectOffset padding, TextAnchor align, bool expandHeight = false)
    {
        var h = go.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.padding = padding;
        h.childAlignment = align;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = expandHeight;
        return h;
    }

    private void Icon(Transform parent, Sprite sprite, float w, float h)
    {
        var go = NewUI("Icon", parent);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = w;
        le.preferredHeight = le.minHeight = h;
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
    // Kreslené sprity (žádné externí obrázky)
    // ------------------------------------------------------------------

    private static bool InCircle(float x, float y, float r) => x * x + y * y <= r * r;

    private static Color StripeShader(float u, float v)
    {
        return Mathf.Repeat((u + v) * 2f, 1f) < 0.5f
            ? new Color(0.96f, 0.70f, 0.0f, 1f)
            : new Color(0.09f, 0.10f, 0.11f, 1f);
    }

    private static int HookPart(float x, float y, bool closed)
    {
        float de = Mathf.Sqrt((x + 0.1f) * (x + 0.1f) + (y - 0.78f) * (y - 0.78f));
        if (de < 0.20f && de > 0.09f) return 1;                                   // oko
        if (x > -0.2f && x < 0f && y > -0.1f && y < 0.70f) return 1;              // dřík
        float dc = Mathf.Sqrt((x - 0.3f) * (x - 0.3f) + (y + 0.1f) * (y + 0.1f));
        if (dc < 0.5f && dc > 0.3f && y <= -0.1f) return 1;                       // oblouk
        if (x > 0.6f && x < 0.8f && y > -0.1f && y < 0.14f) return 1;             // špička
        if (closed && x >= 0f && x < 0.62f && y > 0.0f && y < 0.09f) return 2;    // pojistka
        return 0;
    }

    private static Color HookShader(float u, float v, bool closed)
    {
        float x = u * 2f - 1f;
        float y = v * 2f - 1f + 0.35f;
        int p = HookPart(x, y, closed);
        if (p == 1) return new Color(0.80f, 0.83f, 0.88f, 1f);
        if (p == 2) return new Color(0.96f, 0.70f, 0.0f, 1f);

        const float o = 0.055f;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                if (HookPart(x + dx * o, y + dy * o, closed) != 0) return new Color(0.09f, 0.10f, 0.12f, 1f);
            }
        return Clear;
    }

    private static bool NearSegment(float px, float py, float ax, float ay, float bx, float by, float w)
    {
        float dx = bx - ax, dy = by - ay;
        float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy));
        float cx = ax + t * dx, cy = ay + t * dy;
        return (px - cx) * (px - cx) + (py - cy) * (py - cy) < w * w;
    }

    private static Color CraneShader(float u, float v)
    {
        var steel = new Color(0.62f, 0.67f, 0.74f, 1f);
        var yel = new Color(0.96f, 0.70f, 0.0f, 1f);
        var dark = new Color(0.35f, 0.38f, 0.43f, 1f);

        float ux = u * 2f; // korekce poměru stran 2:1

        if (v < 0.06f && u > 0.18f && u < 0.46f) return dark;                     // podstavec

        if (u > 0.27f && u < 0.37f && v >= 0.06f && v < 0.82f)                    // stožár (mříž)
        {
            float a = Mathf.Repeat(v * 10f, 1f);
            float x = (u - 0.27f) / 0.10f;
            bool diag = Mathf.Abs(a - x) < 0.14f || Mathf.Abs(a - (1f - x)) < 0.14f;
            bool edge = x < 0.14f || x > 0.86f;
            return (diag || edge) ? yel : Clear;
        }

        if (v >= 0.82f && v < 0.90f && u > 0.04f && u < 0.97f) return yel;        // rameno
        if (v >= 0.70f && v < 0.82f && u > 0.05f && u < 0.15f) return dark;       // protizávaží
        if (v >= 0.68f && v < 0.78f && u > 0.37f && u < 0.46f) return steel;      // kabina
        if (u > 0.29f && u < 0.35f && v >= 0.90f && v < 0.99f) return yel;        // věž

        if (NearSegment(ux, v, 0.64f, 0.99f, 0.08f, 0.90f, 0.012f)) return steel; // lana
        if (NearSegment(ux, v, 0.64f, 0.99f, 1.94f, 0.90f, 0.012f)) return steel;

        if (u > 0.70f && u < 0.75f && v >= 0.78f && v < 0.82f) return dark;       // vozík
        if (u > 0.715f && u < 0.735f && v >= 0.50f && v < 0.78f) return dark;     // lano háku
        if (u > 0.70f && u < 0.75f && v >= 0.44f && v < 0.50f) return steel;      // hák

        return Clear;
    }

    private static Sprite MakeSprite(int w, int h, System.Func<float, float, Color> shader, bool repeat)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp
        };

        const int ss = 3;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                for (int sy = 0; sy < ss; sy++)
                {
                    for (int sx = 0; sx < ss; sx++)
                    {
                        float u = (x + (sx + 0.5f) / ss) / w;
                        float v = (y + (sy + 0.5f) / ss) / h;
                        Color c = shader(u, v);
                        r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;
                    }
                }
                px[y * w + x] = a > 0f
                    ? (Color32)new Color(r / a, g / a, b / a, a / (ss * ss))
                    : new Color32(0, 0, 0, 0);
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    private static Sprite CreateWhiteSprite()
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var px = new Color32[16];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
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

/// <summary>Jeden řádek seznamu: drží stav animací a reaguje na myš.</summary>
public class StallRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public int slot;
    public bool hovered;
    public System.Action<int> onHover;
    public System.Action<int> onClick;

    public RectTransform visual;
    public CanvasGroup group;
    public Image border, badge;
    public TextMeshProUGUI levelNum, nameText, subText, countText, priceText;

    // animační hodnoty (mění je StallManager)
    public float sel, entranceX, shakeX;
    public float pop = 1f;

    public void OnPointerEnter(PointerEventData e) { hovered = true; onHover?.Invoke(slot); }
    public void OnPointerExit(PointerEventData e) { hovered = false; }
    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left) onClick?.Invoke(slot);
    }

    private void OnDisable() { hovered = false; }
}

/// <summary>Malý pomocník: ví, jestli je nad tlačítkem myš (hák se pak zavře).</summary>
public class StallHoverFlag : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public bool hovered;
    public void OnPointerEnter(PointerEventData e) { hovered = true; }
    public void OnPointerExit(PointerEventData e) { hovered = false; }
    private void OnDisable() { hovered = false; }
}