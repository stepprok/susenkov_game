using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

[System.Serializable]
public struct ItemSellPrice
{
    public string itemName;  // Název předmětu
    public float sellPrice;  // Výkupní cena v $
}

public class StallManager : MonoBehaviour
{
    public static StallManager Instance;

    [Header("UI Elementy")]
    public GameObject stallUI;
    public TextMeshProUGUI stallText;

    [Header("Ceník výkupu")]
    public List<ItemSellPrice> priceList = new List<ItemSellPrice>()
    {
        new ItemSellPrice { itemName = "Cookies", sellPrice = 0.001f },
        new ItemSellPrice { itemName = "Golf Club", sellPrice = 12.00f },
        new ItemSellPrice { itemName = "Katana", sellPrice = 25.00f },
        new ItemSellPrice { itemName = "Rough Knife", sellPrice = 2.50f },
        new ItemSellPrice { itemName = "Metal Axe", sellPrice = 15.00f },
        new ItemSellPrice { itemName = "Magic Wand", sellPrice = 30.00f }
    };

    public float defaultUnknownItemPrice = 1.00f; // Cena pro neuvedené předměty

    private bool isStallOpen = false;
    private int selectedIndex = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (stallUI != null) stallUI.SetActive(false);
    }

    private void Update()
    {
        if (!isStallOpen) return;

        // Zavření stánku klávesou ESC nebo E
        if (Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame))
        {
            CloseStall();
            return;
        }

        if (InventoryManager.Instance == null || InventoryManager.Instance.items.Count == 0) return;

        List<string> playerItems = InventoryManager.Instance.items;

        // Pohyb šipkami v okně prodeje
        if (Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            selectedIndex = (selectedIndex + 1) % playerItems.Count;
            UpdateStallUI();
        }
        else if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            selectedIndex = (selectedIndex - 1 + playerItems.Count) % playerItems.Count;
            UpdateStallUI();
        }

        // Prodej předmětu klávesou Enter
        if (Keyboard.current.enterKey.wasPressedThisFrame)
        {
            SellSelectedItem();
        }
    }

    public void OpenStall()
    {
        isStallOpen = true;
        selectedIndex = 0;

        if (stallUI != null) stallUI.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        UpdateStallUI();
    }

    public void CloseStall()
    {
        isStallOpen = false;

        if (stallUI != null) stallUI.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private float GetPriceForItem(string itemName)
    {
        ItemSellPrice match = priceList.Find(p => p.itemName == itemName);
        return match.itemName != null ? match.sellPrice : defaultUnknownItemPrice;
    }

    private void SellSelectedItem()
    {
        if (InventoryManager.Instance == null || InventoryManager.Instance.items.Count == 0) return;

        string itemToSell = InventoryManager.Instance.items[selectedIndex];
        float price = GetPriceForItem(itemToSell);

        // Odebere 1 kus z inventáře a přičte peníze
        bool removed = InventoryManager.Instance.RemoveItems(itemToSell, 1);
        if (removed)
        {
            InventoryManager.Instance.AddMoney(price);
            Debug.Log($"Prodaný předmět {itemToSell} za {price:F3} $");

            if (selectedIndex >= InventoryManager.Instance.items.Count && InventoryManager.Instance.items.Count > 0)
            {
                selectedIndex = InventoryManager.Instance.items.Count - 1;
            }

            UpdateStallUI();
        }
    }

    private void UpdateStallUI()
    {
        if (stallText == null) return;

        if (InventoryManager.Instance == null || InventoryManager.Instance.items.Count == 0)
        {
            stallText.text = "<b>--- VÝKUPNÍ STÁNEK ---</b>\n\nNemáš v inventáři nic k prodeji.";
            return;
        }

        List<string> items = InventoryManager.Instance.items;
        float currentMoney = InventoryManager.Instance.money;

        stallText.text = $"<b>--- VÝKUPNÍ STÁNEK ---</b>\nPeníze: <b>{currentMoney:F3} $</b>\n\n<b>Vyber předmět k prodeji:</b>\n";

        for (int i = 0; i < items.Count; i++)
        {
            string itemName = items[i];
            float price = GetPriceForItem(itemName);

            if (i == selectedIndex)
            {
                stallText.text += $"<color=yellow>> <b>{itemName}</b> | Výkup: <color=#00FF00>+{price:F3} $</color> <</color>\n";
            }
            else
            {
                stallText.text += $"- {itemName} | Výkup: +{price:F3} $\n";
            }
        }

        stallText.text += "\n<size=80%><i>[ENTER] Prodat předmět  |  [ESC / E] Zavřít</i></size>";
    }
}