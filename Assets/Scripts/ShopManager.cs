using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

[System.Serializable]
public struct TradeOffer
{
    public string rewardName;    // Název odměny (např. "Katana", "Golf Club")
    public int requiredCookies;  // Požadovaný počet sušenek
}

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance;

    [Header("UI Elementy")]
    public GameObject shopUI;
    public TextMeshProUGUI shopText;

    private List<TradeOffer> activeOffers = new List<TradeOffer>();
    private NPCTrader currentNPC;
    private bool isShopOpen = false;
    private int selectedIndex = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (shopUI != null) shopUI.SetActive(false);
    }

    private void Update()
    {
        if (!isShopOpen) return;

        // Zavření obchodu klávesou Escape nebo E
        if (Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame))
        {
            CloseShop();
            return;
        }

        // Pohyb šipkami v nabídce obchodu
        if (activeOffers.Count > 0)
        {
            if (Keyboard.current.downArrowKey.wasPressedThisFrame)
            {
                selectedIndex = (selectedIndex + 1) % activeOffers.Count;
                UpdateShopUI();
            }
            else if (Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                selectedIndex = (selectedIndex - 1 + activeOffers.Count) % activeOffers.Count;
                UpdateShopUI();
            }

            // Potvrzení nákupu stisknutím Enter
            if (Keyboard.current.enterKey.wasPressedThisFrame)
            {
                ConfirmTrade(selectedIndex);
            }
        }
    }

    public void OpenShop(NPCTrader npc, List<TradeOffer> offers)
    {
        currentNPC = npc;
        activeOffers = offers;
        selectedIndex = 0;
        isShopOpen = true;

        if (shopUI != null) shopUI.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        UpdateShopUI();
    }

    public void CloseShop()
    {
        isShopOpen = false;
        currentNPC = null;

        if (shopUI != null) shopUI.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void ConfirmTrade(int index)
    {
        if (index < 0 || index >= activeOffers.Count) return;

        TradeOffer offer = activeOffers[index];
        int playerCookies = InventoryManager.Instance != null ? InventoryManager.Instance.GetItemCount("Cookies") : 0;

        if (playerCookies >= offer.requiredCookies)
        {
            // Odebere sušenky a přidá zakoupenou věc
            InventoryManager.Instance.RemoveItems("Cookies", offer.requiredCookies);
            InventoryManager.Instance.AddItem(offer.rewardName);

            Debug.Log($"Obchod úspěšný! Koupil jsi {offer.rewardName} za {offer.requiredCookies} sušenek.");

            // Informuje NPC o dokončení obchodu
            if (currentNPC != null)
            {
                currentNPC.OnTradeComplete();
            }

            CloseShop();
        }
        else
        {
            Debug.Log($"Nemáš dostatek sušenek! Potřebuješ {offer.requiredCookies}, ale máš jen {playerCookies}.");
        }
    }

    private void UpdateShopUI()
    {
        if (shopText == null) return;

        if (activeOffers.Count == 0)
        {
            shopText.text = "Obchodník nemá žádné zboží.";
            return;
        }

        int playerCookies = InventoryManager.Instance != null ? InventoryManager.Instance.GetItemCount("Cookies") : 0;
        
        shopText.text = $"<b>--- OBCHOD ---</b>\nMoje sušenky: <b>{playerCookies}</b>\n\n";

        for (int i = 0; i < activeOffers.Count; i++)
        {
            TradeOffer offer = activeOffers[i];
            bool canAfford = playerCookies >= offer.requiredCookies;
            string colorHex = canAfford ? "#00FF00" : "#FF5555"; // Zelená pokud máš dost sušenek, červená pokud ne

            if (i == selectedIndex)
            {
                shopText.text += $"<color=yellow>> <b>{offer.rewardName}</b> </color> | Cena: <color={colorHex}>{offer.requiredCookies} sušenek</color> <\n";
            }
            else
            {
                shopText.text += $"- {offer.rewardName} | Cena: <color={colorHex}>{offer.requiredCookies} sušenek</color>\n";
            }
        }

        shopText.text += "\n<size=80%><i>[ENTER] Koupit  |  [ESC / E] Zavřít</i></size>";
    }
}