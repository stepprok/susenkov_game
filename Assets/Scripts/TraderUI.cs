using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

[System.Serializable]
public struct TradeOffer
{
    public string rewardName;    // Název odmìny (napø. "Katana", "Golf Club")
    public int requiredCookies;  // Požadovaný poèet sušenek
}

/// <summary>
/// Zpìtná kompatibilita: starý kód volající ShopManager.Instance.OpenShop(...) / CloseShop()
/// funguje dál, jen se všechno pøesmìruje do TraderUI. Žádný objekt ve scénì není potøeba.
/// </summary>
public class ShopManager
{
    private static ShopManager instance;
    public static ShopManager Instance => instance ?? (instance = new ShopManager());

    public void OpenShop(NPCTrader npc, List<TradeOffer> offers) => TraderUI.Ensure().Open(npc, offers);
    public void CloseShop() { if (TraderUI.Instance != null) TraderUI.Instance.Close(); }
}

/// <summary>
/// Animované UI obchodníka (slouèený TraderUI + ShopManager). Staví se samo (žádné ruèní skládání v Unity).
/// Otevírá ho NPCTrader voláním TraderUI.Instance.Open(...) nebo ShopManager.Instance.OpenShop(...).
/// Výmìnu øeší pøes InventoryManager (odebere sušenky, pøidá odmìnu).
/// </summary>
[DefaultExecutionOrder(-50)]
public class TraderUI : MonoBehaviour
{
    public static TraderUI Instance;
    /// <summary>True, když je obchod otevøený (hodí se k zablokování pohybu hráèe).</summary>
    public static bool IsOpen { get; private set; }

    [Header("Výmìna")]
    [SerializeField] private string greeting = "Dobrý den, co mi dáte?";
    [SerializeField] private float typeSpeed = 45f;

    [Header("Barvy")]
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.74f);
    [SerializeField] private Color cardColor = new Color32(0x1B, 0x1D, 0x2A, 248);
    [SerializeField] private Color columnColor = new Color32(0x24, 0x27, 0x3B, 255);
    [SerializeField] private Color itemColor = new Color32(0x2E, 0x32, 0x50, 255);
    [SerializeField] private Color accentColor = new Color32(0xF2, 0xB8, 0x4B, 255);
    [SerializeField] private Color textColor = new Color32(0xE6, 0xE8, 0xF0, 255);
    [SerializeField] private Color mutedColor = new Color32(0x8D, 0x92, 0xAA, 255);
    [SerializeField] private Color goodColor = new Color32(0x5B, 0xE3, 0x7D, 255);
    [SerializeField] private Color badColor = new Color32(0xFF, 0x5C, 0x5C, 255);
    [SerializeField] private Color disabledBuy = new Color32(0x4A, 0x4D, 0x66, 255);

    // --- UI reference ---
    private GameObject overlay;
    private CanvasGroup overlayGroup, cardGroup;
    private RectTransform cardRt, avatarRt;
    private Image glowRing, buyImage;
    private RectTransform buyRt, offersContainer;
    private TextMeshProUGUI speechText, messageText, walletText;
    private Sprite white, rounded, ringSprite, circleSprite, avatarSprite, cookieSprite;

    // --- stav ---
    private readonly List<TraderOfferCard> cards = new List<TraderOfferCard>();
    private NPCTrader npc;
    private int selected;
    private bool closing, busy;
    private float shownCookies;
    private int lastShownCookies = -1;
    private float messageTimer;
    private Color messageBase;
    private Coroutine typeRoutine;
    private CursorLockMode prevLock;
    private bool prevVisible;

    private static readonly Color Clear = new Color(0, 0, 0, 0);

    // ------------------------------------------------------------------
    // Životní cyklus
    // ------------------------------------------------------------------

    /// <summary>Vrátí existující TraderUI, nebo ho vytvoøí (aby fungovalo i bez objektu ve scénì).</summary>
    public static TraderUI Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("TraderUI");
        return go.AddComponent<TraderUI>(); // Awake nastaví Instance
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        white = CreateWhiteSprite();
        rounded = CreateRoundedSprite(64, 16);
        ringSprite = CreateRingSprite(256, 0.93f, 0.99f);
        circleSprite = MakeIcon(96, (x, y) => InCircle(x, y, 0, 0, 0.97f) ? Color.white : Clear);
        avatarSprite = MakeIcon(192, AvatarShader);
        cookieSprite = MakeIcon(96, CookieShader);

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
    // Veøejné API
    // ------------------------------------------------------------------

    public void Open(NPCTrader trader, List<TradeOffer> offers)
    {
        if (IsOpen || closing) return;

        StopAllCoroutines();
        IsOpen = true;
        busy = false;
        npc = trader;

        prevLock = Cursor.lockState;
        prevVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        BuildOffers(offers);

        var inv = InventoryManager.Instance;
        int owned = inv != null ? inv.CountCookies() : 0;
        selected = FirstAffordable(owned);

        shownCookies = 0f;
        lastShownCookies = -1;
        messageTimer = 0f;
        messageText.color = new Color(1, 1, 1, 0);
        speechText.text = "";

        // poèáteèní (skrytý) stav
        overlay.SetActive(true);
        overlayGroup.alpha = 0f;
        cardGroup.alpha = 0f;
        cardRt.localScale = Vector3.one * 0.86f;
        avatarRt.localScale = Vector3.zero;

        // --- vstupní animace ---
        StartCoroutine(Tween(0.3f, 0f, t => overlayGroup.alpha = EaseOutCubic(t)));
        StartCoroutine(Tween(0.55f, 0f, t =>
        {
            cardGroup.alpha = Mathf.Clamp01(EaseOutCubic(t) * 1.4f);
            cardRt.localScale = Vector3.one * Mathf.LerpUnclamped(0.86f, 1f, EaseOutBack(t));
            cardRt.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(-50f, 0f, EaseOutCubic(t)));
        }));
        StartCoroutine(Tween(0.6f, 0.15f, t =>
            avatarRt.localScale = Vector3.one * Mathf.LerpUnclamped(0f, 1f, EaseOutBack(t))));

        for (int i = 0; i < cards.Count; i++)
            StartCoroutine(CardIn(cards[i], 0.30f + 0.09f * i));

        Say(greeting, 0.45f);
    }

    public void Close()
    {
        if (!IsOpen || closing) return;
        StopAllCoroutines();
        StartCoroutine(CloseSequence());
    }

    // --- názvy ze starého ShopManageru (stejné chování) ---

    public void OpenShop(NPCTrader trader, List<TradeOffer> offers) => Open(trader, offers);
    public void CloseShop() => Close();

    /// <summary>Vybere nabídku podle indexu a zkusí výmìnu (jako pùvodní ShopManager.ConfirmTrade).</summary>
    public void ConfirmTrade(int index)
    {
        if (index < 0 || index >= cards.Count) return;
        selected = index;
        TryTrade();
    }

    // ------------------------------------------------------------------
    // Animaèní sekvence
    // ------------------------------------------------------------------

    private IEnumerator CardIn(TraderOfferCard c, float delay)
    {
        c.group.alpha = 0f;
        c.entranceX = 70f;
        yield return StartCoroutine(Tween(0.5f, delay, t =>
        {
            c.group.alpha = EaseOutCubic(t);
            c.entranceX = Mathf.LerpUnclamped(70f, 0f, EaseOutBack(t));
        }));
        c.group.alpha = 1f;
        c.entranceX = 0f;
    }

    private IEnumerator CloseSequence()
    {
        closing = true;
        busy = true;

        float a0 = overlayGroup.alpha, c0 = cardGroup.alpha, s0 = cardRt.localScale.x;
        yield return StartCoroutine(Tween(0.28f, 0f, t =>
        {
            float e = EaseInCubic(t);
            overlayGroup.alpha = Mathf.Lerp(a0, 0f, e);
            cardGroup.alpha = Mathf.Lerp(c0, 0f, e);
            cardRt.localScale = Vector3.one * Mathf.Lerp(s0, 0.92f, e);
        }));

        overlay.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        npc = null;
        closing = false;
        IsOpen = false;
    }

    private IEnumerator SuccessSequence(TraderOfferCard picked, string prettyName, NPCTrader trader)
    {
        busy = true;
        ShowMessage($"Získal jsi: {prettyName}", goodColor);
        Say("Bájeènì, dìkuji!", 0f);

        // vybraná karta "vyskoèí"
        yield return StartCoroutine(Tween(0.14f, 0f, t => picked.pop = Mathf.Lerp(1f, 1.1f, EaseOutCubic(t))));

        // ostatní zešednou a odsunou se
        foreach (var other in cards)
        {
            if (other == picked) continue;
            var o = other;
            StartCoroutine(Tween(0.4f, 0f, t =>
            {
                o.group.alpha = Mathf.Lerp(1f, 0.15f, EaseOutCubic(t));
                o.entranceX = Mathf.Lerp(0f, -40f, EaseOutCubic(t));
            }));
        }

        // vybraná se zvìtší a rozplyne
        yield return StartCoroutine(Tween(0.4f, 0f, t =>
        {
            picked.pop = Mathf.LerpUnclamped(1.1f, 1.35f, EaseOutCubic(t));
            picked.group.alpha = 1f - EaseInCubic(t);
        }));

        yield return new WaitForSecondsRealtime(0.8f);

        if (trader != null) trader.OnTradeComplete();
        Close();
    }

    private IEnumerator Shake(TraderOfferCard c)
    {
        yield return StartCoroutine(Tween(0.4f, 0f, t => c.shakeX = Mathf.Sin(t * Mathf.PI * 7f) * 16f * (1f - t)));
        c.shakeX = 0f;
    }

    private IEnumerator Punch(RectTransform rt, float from)
    {
        yield return StartCoroutine(Tween(0.35f, 0f, t =>
            rt.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, EaseOutBack(t))));
        rt.localScale = Vector3.one;
    }

    private IEnumerator Typewriter(string msg, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        speechText.text = msg;
        speechText.maxVisibleCharacters = 0;
        float shown = 0f;
        while (shown < msg.Length)
        {
            shown += typeSpeed * Time.unscaledDeltaTime;
            speechText.maxVisibleCharacters = Mathf.FloorToInt(shown);
            yield return null;
        }
        speechText.maxVisibleCharacters = 99999;
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
    // Logika výmìny
    // ------------------------------------------------------------------

    private void TryTrade()
    {
        if (busy || closing || cards.Count == 0) return;

        var card = cards[selected];
        var inv = InventoryManager.Instance;
        if (inv == null) return;

        int owned = inv.CountCookies();
        if (owned < card.cost)
        {
            StartCoroutine(Shake(card));
            ShowMessage("Nemáš dost sušenek", badColor);
            Say("To mi nestaèí...", 0f);
            Debug.Log($"Nemáš dostatek sušenek! Potøebuješ {card.cost}, ale máš jen {owned}.");
            return;
        }

        if (!inv.RemoveCookies(card.cost))
        {
            StartCoroutine(Shake(card));
            ShowMessage("Výmìna se nezdaøila", badColor);
            return;
        }

        if (!inv.AddItem(card.rewardName))
        {
            for (int i = 0; i < card.cost; i++) inv.AddItem(inv.cookieNames.Count > 0 ? inv.cookieNames[0] : "Cookies"); // vrátit sušenky
            StartCoroutine(Shake(card));
            ShowMessage("Tvùj batoh je plný", badColor);
            return;
        }

        Debug.Log($"Obchod úspìšný! Koupil jsi {card.rewardName} za {card.cost} sušenek.");
        StartCoroutine(SuccessSequence(card, Pretty(card.rewardName), npc));
    }

    private int FirstAffordable(int owned)
    {
        for (int i = 0; i < cards.Count; i++)
            if (owned >= cards[i].cost) return i;
        return 0;
    }

    private void Say(string text, float delay)
    {
        if (typeRoutine != null) StopCoroutine(typeRoutine);
        typeRoutine = StartCoroutine(Typewriter(text, delay));
    }

    private void ShowMessage(string text, Color color)
    {
        messageText.text = text;
        messageBase = color;
        messageTimer = 2.2f;
    }

    // ------------------------------------------------------------------
    // Každý snímek
    // ------------------------------------------------------------------

    private void Update()
    {
        if (overlay == null || !overlay.activeSelf) return;

        float dt = Time.unscaledDeltaTime;
        float k = 1f - Mathf.Exp(-14f * dt);

        var inv = InventoryManager.Instance;
        int owned = inv != null ? inv.CountCookies() : 0;

        if (IsOpen && !closing && !busy) HandleInput();

        // karty
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            c.sel = Mathf.Lerp(c.sel, i == selected ? 1f : 0f, k);
            c.hov = Mathf.Lerp(c.hov, c.hovered ? 1f : 0f, k);

            var bc = accentColor; bc.a = c.sel;
            c.border.color = bc;

            c.visual.localScale = Vector3.one * ((1f + 0.03f * c.sel + 0.012f * c.hov) * c.pop);
            c.visual.anchoredPosition = new Vector2(c.entranceX + c.shakeX + 8f * c.sel, c.entranceY);

            bool affordable = owned >= c.cost;
            c.barShown = Mathf.Lerp(c.barShown, Mathf.Clamp01(owned / (float)c.cost), 1f - Mathf.Exp(-6f * dt));
            c.barFill.fillAmount = c.barShown;
            c.barFill.color = affordable ? goodColor : badColor;
            c.costText.color = affordable ? goodColor : badColor;
        }

        // poèítadlo sušenek (plynule se dopoèítá)
        shownCookies = Mathf.Lerp(shownCookies, owned, 1f - Mathf.Exp(-5f * dt));
        int shownInt = Mathf.RoundToInt(shownCookies);
        if (shownInt != lastShownCookies)
        {
            lastShownCookies = shownInt;
            walletText.text = shownInt.ToString();
        }

        // avatar: jemné vznášení a pulzující záøe
        avatarRt.anchoredPosition = new Vector2(0f, Mathf.Sin(Time.unscaledTime * 1.6f) * 4f);
        var gc = accentColor; gc.a = 0.45f + 0.35f * Mathf.Sin(Time.unscaledTime * 2f);
        glowRing.color = gc;

        // hláška
        if (messageTimer > 0f)
        {
            messageTimer -= dt;
            var mc = messageBase; mc.a = Mathf.Clamp01(messageTimer);
            messageText.color = mc;
        }

        // tlaèítko Vymìnit
        bool canBuy = cards.Count > 0 && owned >= cards[selected].cost;
        buyImage.color = Color.Lerp(buyImage.color, canBuy ? accentColor : disabledBuy, k);
    }

    private void HandleInput()
    {
        bool up, down, enter, esc;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;
        up = kb.upArrowKey.wasPressedThisFrame;
        down = kb.downArrowKey.wasPressedThisFrame;
        enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
        esc = kb.escapeKey.wasPressedThisFrame;
#else
        up = Input.GetKeyDown(KeyCode.UpArrow);
        down = Input.GetKeyDown(KeyCode.DownArrow);
        enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        esc = Input.GetKeyDown(KeyCode.Escape);
#endif
        if (esc) { Close(); return; }
        if (cards.Count == 0) return;

        if (down) selected = (selected + 1) % cards.Count;
        else if (up) selected = (selected - 1 + cards.Count) % cards.Count;

        if (enter)
        {
            StartCoroutine(Punch(buyRt, 0.9f));
            TryTrade();
        }
    }

    // ------------------------------------------------------------------
    // Stavba UI
    // ------------------------------------------------------------------

    private void Build()
    {
        var canvasGo = new GameObject("TraderCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 160;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // zatemnìní (klik mimo okno = zavøít)
        overlay = NewUI("TraderOverlay", canvasGo.transform);
        Stretch(overlay.GetComponent<RectTransform>());
        var dim = overlay.AddComponent<Image>();
        dim.color = dimColor;
        overlayGroup = overlay.AddComponent<CanvasGroup>();
        var dimBtn = overlay.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.targetGraphic = dim;
        var dn = dimBtn.navigation; dn.mode = Navigation.Mode.None; dimBtn.navigation = dn;
        dimBtn.onClick.AddListener(Close);

        // hlavní karta
        var card = NewUI("Card", overlay.transform);
        cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = cardRt.anchorMax = cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(1240, 760);
        SetRounded(card.AddComponent<Image>(), cardColor);
        cardGroup = card.AddComponent<CanvasGroup>();

        var h = card.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(32, 32, 32, 28);
        h.spacing = 28;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;

        BuildNpcColumn(card.transform);
        BuildOffersColumn(card.transform);
    }

    private void BuildNpcColumn(Transform parent)
    {
        var col = NewUI("NpcColumn", parent);
        SetRounded(col.AddComponent<Image>(), columnColor);
        var le = col.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = 370;
        le.flexibleWidth = 0;
        VLG(col, 16, new RectOffset(24, 24, 28, 24), TextAnchor.UpperCenter, true);

        // avatar se záøícím kroužkem
        var box = NewUI("AvatarBox", col.transform);
        var ble = box.AddComponent<LayoutElement>();
        ble.preferredHeight = ble.minHeight = 230;

        var glow = NewUI("Glow", box.transform);
        var grt = glow.GetComponent<RectTransform>();
        grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(0.5f, 0.5f);
        grt.sizeDelta = new Vector2(230, 230);
        glowRing = glow.AddComponent<Image>();
        glowRing.sprite = ringSprite;
        glowRing.color = accentColor;
        glowRing.raycastTarget = false;

        var av = NewUI("Avatar", box.transform);
        avatarRt = av.GetComponent<RectTransform>();
        avatarRt.anchorMin = avatarRt.anchorMax = avatarRt.pivot = new Vector2(0.5f, 0.5f);
        avatarRt.sizeDelta = new Vector2(206, 206);
        var avImg = av.AddComponent<Image>();
        avImg.sprite = avatarSprite;
        avImg.raycastTarget = false;

        var title = Text(col.transform, "OBCHODNÍK", 32, accentColor, FontStyles.Bold, TextAlignmentOptions.Center);
        title.characterSpacing = 8;

        // bublina s textem
        var bubble = NewUI("Bubble", col.transform);
        SetRounded(bubble.AddComponent<Image>(), itemColor);
        var bl = bubble.AddComponent<LayoutElement>();
        bl.minHeight = 130;
        VLG(bubble, 0, new RectOffset(22, 22, 18, 18), TextAnchor.MiddleCenter, true);
        speechText = Text(bubble.transform, "", 30, textColor, FontStyles.Italic, TextAlignmentOptions.Center);

        var spacer = NewUI("Spacer", col.transform);
        spacer.AddComponent<LayoutElement>().flexibleHeight = 1;

        // penìženka se sušenkami
        var wallet = NewUI("Wallet", col.transform);
        SetRounded(wallet.AddComponent<Image>(), new Color32(0x2A, 0x2D, 0x44, 255));
        wallet.AddComponent<LayoutElement>().minHeight = 72;
        var wh = wallet.AddComponent<HorizontalLayoutGroup>();
        wh.padding = new RectOffset(18, 22, 10, 10);
        wh.spacing = 14;
        wh.childAlignment = TextAnchor.MiddleLeft;
        wh.childControlWidth = true;
        wh.childControlHeight = true;
        wh.childForceExpandWidth = false;
        wh.childForceExpandHeight = false;

        Icon(wallet.transform, cookieSprite, 46);
        var wl = Text(wallet.transform, "Tvoje sušenky", 26, mutedColor, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        wl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        walletText = Text(wallet.transform, "0", 40, textColor, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
    }

    private void BuildOffersColumn(Transform parent)
    {
        var col = NewUI("OffersColumn", parent);
        col.AddComponent<LayoutElement>().flexibleWidth = 1;
        VLG(col, 16, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft, true);

        var title = Text(col.transform, "NABÍDKA", 44, accentColor, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        title.characterSpacing = 8;

        Text(col.transform, "Vyber, co chceš získat výmìnou za sušenky", 26, mutedColor,
            FontStyles.Normal, TextAlignmentOptions.MidlineLeft);

        var line = NewUI("Line", col.transform);
        line.AddComponent<Image>().color = accentColor;
        line.AddComponent<LayoutElement>().preferredHeight = 3;

        // seznam karet
        var list = NewUI("Offers", col.transform);
        offersContainer = list.GetComponent<RectTransform>();
        list.AddComponent<LayoutElement>().flexibleHeight = 1;
        VLG(list, 12, new RectOffset(0, 0, 4, 0), TextAnchor.UpperLeft, true);

        // spodní øádek: hláška + tlaèítko
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

        var buy = NewUI("BuyButton", bottom.transform);
        buyRt = buy.GetComponent<RectTransform>();
        buyImage = buy.AddComponent<Image>();
        SetRounded(buyImage, accentColor);
        var bl = buy.AddComponent<LayoutElement>();
        bl.preferredWidth = bl.minWidth = 240;
        bl.preferredHeight = bl.minHeight = 66;

        var btn = buy.AddComponent<Button>();
        btn.targetGraphic = buyImage;
        var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
        var cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        cb.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        cb.selectedColor = Color.white;
        btn.colors = cb;
        btn.onClick.AddListener(() =>
        {
            StartCoroutine(Punch(buyRt, 0.9f));
            TryTrade();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        });

        var lbl = Text(buy.transform, "VYMÌNIT", 30, new Color32(0x1B, 0x1D, 0x2A, 255),
            FontStyles.Bold, TextAlignmentOptions.Center);
        lbl.characterSpacing = 4;
        Stretch(lbl.rectTransform);

        string a = ColorUtility.ToHtmlStringRGB(accentColor);
        var hint = Text(col.transform,
            $"<color=#{a}>[Šipky]</color> výbìr     <color=#{a}>[Enter]</color> vymìnit     <color=#{a}>[Esc]</color> zavøít",
            23, mutedColor, FontStyles.Normal, TextAlignmentOptions.Center);
        hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
    }

    private void BuildOffers(List<TradeOffer> offers)
    {
        foreach (Transform child in offersContainer) Destroy(child.gameObject);
        cards.Clear();

        if (offers == null || offers.Count == 0)
        {
            Text(offersContainer, "Dnes nemám nic k nabídnutí.", 30, mutedColor,
                FontStyles.Italic, TextAlignmentOptions.MidlineLeft);
            return;
        }

        for (int i = 0; i < offers.Count; i++)
            cards.Add(CreateOfferCard(i, offers[i].rewardName, offers[i].requiredCookies));
    }

    private TraderOfferCard CreateOfferCard(int index, string rewardName, int cost)
    {
        cost = Mathf.Max(1, cost);
        GetRarity(cost, out string rarityLabel, out Color rarityColor);
        string pretty = Pretty(rewardName);

        var root = NewUI("Offer", offersContainer);
        var rle = root.AddComponent<LayoutElement>();
        rle.preferredHeight = rle.minHeight = 112;

        var visual = NewUI("Visual", root.transform);
        var vrt = visual.GetComponent<RectTransform>();
        Stretch(vrt);
        var group = visual.AddComponent<CanvasGroup>();

        // zlatý rámeèek (viditelný jen u vybrané karty)
        var border = NewUI("Border", visual.transform);
        Stretch(border.GetComponent<RectTransform>());
        var borderImg = border.AddComponent<Image>();
        SetRounded(borderImg, new Color(accentColor.r, accentColor.g, accentColor.b, 0f));
        borderImg.raycastTarget = false;

        var face = NewUI("Face", visual.transform);
        var frt = face.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(3, 3);
        frt.offsetMax = new Vector2(-3, -3);
        SetRounded(face.AddComponent<Image>(), itemColor);

        var hl = face.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(20, 22, 12, 12);
        hl.spacing = 18;
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = true;
        hl.childControlHeight = true;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = false;

        // odznak s písmenem
        var badge = NewUI("Badge", face.transform);
        var bimg = badge.AddComponent<Image>();
        bimg.sprite = circleSprite;
        bimg.color = rarityColor;
        bimg.raycastTarget = false;
        var ble = badge.AddComponent<LayoutElement>();
        ble.preferredWidth = ble.minWidth = 68;
        ble.preferredHeight = ble.minHeight = 68;
        var letter = Text(badge.transform, pretty.Substring(0, 1), 36, new Color32(0x1B, 0x1D, 0x2A, 255),
            FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(letter.rectTransform);

        // názvy
        var texts = NewUI("Texts", face.transform);
        texts.AddComponent<LayoutElement>().flexibleWidth = 1;
        VLG(texts, 4, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft, true);
        Text(texts.transform, pretty, 31, textColor, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        Text(texts.transform, rarityLabel, 22, rarityColor, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);

        // cena + ukazatel, kolik sušenek mám
        var right = NewUI("Cost", face.transform);
        var rl = right.AddComponent<LayoutElement>();
        rl.preferredWidth = rl.minWidth = 170;
        VLG(right, 8, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleRight, false);

        var costRow = NewUI("CostRow", right.transform);
        var crh = costRow.AddComponent<HorizontalLayoutGroup>();
        crh.spacing = 8;
        crh.childAlignment = TextAnchor.MiddleRight;
        crh.childControlWidth = true;
        crh.childControlHeight = true;
        crh.childForceExpandWidth = false;
        crh.childForceExpandHeight = false;
        Icon(costRow.transform, cookieSprite, 36);
        var costText = Text(costRow.transform, "×" + cost, 36, textColor, FontStyles.Bold, TextAlignmentOptions.MidlineRight);

        var track = NewUI("BarTrack", right.transform);
        var timg = track.AddComponent<Image>();
        timg.sprite = white;
        timg.color = new Color(1f, 1f, 1f, 0.12f);
        timg.raycastTarget = false;
        var tle = track.AddComponent<LayoutElement>();
        tle.preferredWidth = tle.minWidth = 170;
        tle.preferredHeight = tle.minHeight = 8;

        var fill = NewUI("BarFill", track.transform);
        Stretch(fill.GetComponent<RectTransform>());
        var fimg = fill.AddComponent<Image>();
        fimg.sprite = white;
        fimg.type = Image.Type.Filled;
        fimg.fillMethod = Image.FillMethod.Horizontal;
        fimg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fimg.fillAmount = 0f;
        fimg.color = goodColor;
        fimg.raycastTarget = false;

        // komponenta pro myš a stav animací
        var view = root.AddComponent<TraderOfferCard>();
        view.index = index;
        view.rewardName = rewardName;
        view.cost = cost;
        view.visual = vrt;
        view.group = group;
        view.border = borderImg;
        view.barFill = fimg;
        view.costText = costText;
        view.onHover = i => { if (!busy && !closing) selected = i; };
        view.onClick = i => { if (!busy && !closing) selected = i; };

        return view;
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

    /// <summary>Vzácnost podle ceny ve sušenkách.</summary>
    private static void GetRarity(int cost, out string label, out Color color)
    {
        if (cost <= 6) { label = "Bìžné"; color = new Color32(0x9A, 0xA0, 0xB8, 255); }
        else if (cost <= 20) { label = "Neobvyklé"; color = new Color32(0x5B, 0xE3, 0x7D, 255); }
        else if (cost <= 45) { label = "Vzácné"; color = new Color32(0xB0, 0x7C, 0xFF, 255); }
        else { label = "Legendární"; color = new Color32(0xF2, 0xB8, 0x4B, 255); }
    }

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

    // ------------------------------------------------------------------
    // Kreslené sprity (žádné externí obrázky)
    // ------------------------------------------------------------------

    private static bool InCircle(float x, float y, float cx, float cy, float r)
        => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;

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

    private static Color AvatarShader(float x, float y)
    {
        if (!InCircle(x, y, 0, 0, 0.98f)) return Clear;

        var figure = new Color(0.82f, 0.85f, 0.95f, 1f);
        var bg = new Color(0.17f, 0.19f, 0.31f, 1f);

        // hlava
        if (InCircle(x, y, 0f, 0.22f, 0.33f)) return figure;
        // ramena (elipsa oøíznutá kruhem avataru)
        float ex = x / 0.70f, ey = (y + 0.80f) / 0.66f;
        if (ex * ex + ey * ey <= 1f) return figure;
        return bg;
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

    private static Sprite CreateWhiteSprite()
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var px = new Color32[16];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
    }

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

/// <summary>Jedna karta nabídky: drží stav animací a reaguje na myš.</summary>
public class TraderOfferCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public int index;
    public string rewardName;
    public int cost;
    public bool hovered;
    public System.Action<int> onHover;
    public System.Action<int> onClick;

    public RectTransform visual;
    public CanvasGroup group;
    public Image border;
    public Image barFill;
    public TextMeshProUGUI costText;

    // animaèní hodnoty (mìní je TraderUI)
    public float sel, hov, entranceX, entranceY, shakeX, barShown;
    public float pop = 1f;

    public void OnPointerEnter(PointerEventData e) { hovered = true; onHover?.Invoke(index); }
    public void OnPointerExit(PointerEventData e) { hovered = false; }
    public void OnPointerClick(PointerEventData e) { onClick?.Invoke(index); }
}