using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

[System.Serializable]
public struct StallItemPrice
{
    public string itemName;
    public float sellPrice;
}

public class StallManager : MonoBehaviour
{
    public static StallManager Instance;

    [Header("UI Elementy")]
    public GameObject stallUI;
    public TextMeshProUGUI stallText;

    [Header("Ceník ve stánku (Automaticky se naplní, když je prázdný)")]
    public List<StallItemPrice> priceList = new List<StallItemPrice>();

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

        // Pokud je ceník prázdný, automaticky do něj nahraje všechny předměty z fotek
        if (priceList.Count == 0)
        {
            InitializeDefaultPrices();
        }
    }

    private void InitializeDefaultPrices()
    {
        // Měna
        priceList.Add(new StallItemPrice { itemName = "Cookies", sellPrice = 0.001f });

        // Survival Tools
        priceList.Add(new StallItemPrice { itemName = "RoughAxe", sellPrice = 2.0f });
        priceList.Add(new StallItemPrice { itemName = "RoughHammer", sellPrice = 2.0f });
        priceList.Add(new StallItemPrice { itemName = "RoughHoe", sellPrice = 1.5f });
        priceList.Add(new StallItemPrice { itemName = "RoughKnife", sellPrice = 2.0f });
        priceList.Add(new StallItemPrice { itemName = "RoughPickaxe", sellPrice = 2.5f });
        priceList.Add(new StallItemPrice { itemName = "RoughShovel", sellPrice = 2.0f });
        priceList.Add(new StallItemPrice { itemName = "RoughSpear", sellPrice = 2.5f });

        // Modern Tools & Weapons (Nože, Katany, Pálky, Klubu)
        priceList.Add(new StallItemPrice { itemName = "MetalKnife", sellPrice = 4.0f });
        priceList.Add(new StallItemPrice { itemName = "MetalPickaxe", sellPrice = 5.0f });
        priceList.Add(new StallItemPrice { itemName = "MetalAxe", sellPrice = 5.0f });
        priceList.Add(new StallItemPrice { itemName = "MetalSpear", sellPrice = 5.5f });
        priceList.Add(new StallItemPrice { itemName = "MetalShovel", sellPrice = 4.5f });
        priceList.Add(new StallItemPrice { itemName = "MetalHoe", sellPrice = 4.0f });
        priceList.Add(new StallItemPrice { itemName = "MetalHammer", sellPrice = 5.0f });
        priceList.Add(new StallItemPrice { itemName = "ClaveHammer", sellPrice = 6.0f });
        priceList.Add(new StallItemPrice { itemName = "Wooden", sellPrice = 4.0f }); // Wooden Baseball Bats
        priceList.Add(new StallItemPrice { itemName = "MetalBat", sellPrice = 6.0f });
        priceList.Add(new StallItemPrice { itemName = "GolfBat", sellPrice = 12.0f });
        priceList.Add(new StallItemPrice { itemName = "GolfClub", sellPrice = 12.0f });
        priceList.Add(new StallItemPrice { itemName = "Katana", sellPrice = 15.0f });
        priceList.Add(new StallItemPrice { itemName = "BarnFork", sellPrice = 5.0f });

        // Magical Tools
        priceList.Add(new StallItemPrice { itemName = "Scythe", sellPrice = 20.0f });
        priceList.Add(new StallItemPrice { itemName = "Staff", sellPrice = 22.0f });
        priceList.Add(new StallItemPrice { itemName = "Wand", sellPrice = 18.0f });

        // Containers & Breakable Objects
        priceList.Add(new StallItemPrice { itemName = "Chest", sellPrice = 8.0f });
        priceList.Add(new StallItemPrice { itemName = "Bag", sellPrice = 3.0f });
        priceList.Add(new StallItemPrice { itemName = "Barrel", sellPrice = 4.0f });
        priceList.Add(new StallItemPrice { itemName = "Bowl", sellPrice = 1.0f });
        priceList.Add(new StallItemPrice { itemName = "Plate", sellPrice = 1.0f });
        priceList.Add(new StallItemPrice { itemName = "ShallowPlate", sellPrice = 1.0f });
        priceList.Add(new StallItemPrice { itemName = "SimpleCup", sellPrice = 1.5f });
        priceList.Add(new StallItemPrice { itemName = "SimpleMug", sellPrice = 1.5f });
        priceList.Add(new StallItemPrice { itemName = "Vase", sellPrice = 3.0f });

        // Lamps & Instruments
        priceList.Add(new StallItemPrice { itemName = "CeilingLamp", sellPrice = 5.0f });
        priceList.Add(new StallItemPrice { itemName = "WallLamp", sellPrice = 4.0f });
        priceList.Add(new StallItemPrice { itemName = "Steampunk", sellPrice = 6.0f });
        priceList.Add(new StallItemPrice { itemName = "Flute", sellPrice = 7.0f });
        priceList.Add(new StallItemPrice { itemName = "Guitar", sellPrice = 10.0f });
        priceList.Add(new StallItemPrice { itemName = "PanFlute", sellPrice = 8.0f });
    }

    private void Update()
    {
        if (!isStallOpen) return;

        if (Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame))
        {
            CloseStall();
            return;
        }

        if (InventoryManager.Instance == null) return;
        List<string> playerItems = InventoryManager.Instance.items;

        if (playerItems.Count > 0)
        {
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

            if (Keyboard.current.enterKey.wasPressedThisFrame)
            {
                SellSelectedItem(selectedIndex);
            }
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

    private float GetItemPrice(string itemName)
    {
        foreach (var p in priceList)
        {
            // Pokud název v inventáři obsahuje klíčové slovo (např. GolfBatBlue obsahuje GolfBat)
            if (itemName.Contains(p.itemName))
            {
                return p.sellPrice;
            }
        }
        return 1.0f; // Výchozí cena, pokud by předmět chyběl
    }

    public void SellSelectedItem(int index)
    {
        if (InventoryManager.Instance == null) return;
        List<string> playerItems = InventoryManager.Instance.items;

        if (index < 0 || index >= playerItems.Count) return;

        string itemName = playerItems[index];
        float price = GetItemPrice(itemName);

        bool success = InventoryManager.Instance.RemoveItems(itemName, 1);
        if (success)
        {
            InventoryManager.Instance.AddMoney(price);
            Debug.Log($"Prodáno: {itemName} za {price} $!");

            if (selectedIndex >= playerItems.Count && playerItems.Count > 0)
            {
                selectedIndex = playerItems.Count - 1;
            }
            UpdateStallUI();
        }
    }

    private void UpdateStallUI()
    {
        if (stallText == null) return;

        if (InventoryManager.Instance == null) return;
        List<string> playerItems = InventoryManager.Instance.items;

        float playerMoney = InventoryManager.Instance.money;
        stallText.text = $"<b>--- STÁNEK (VÝKUP) ---</b>\nTvoje peníze: <b>{playerMoney:F3} $</b>\n\n";

        if (playerItems.Count == 0)
        {
            stallText.text += "Nemáš v inventáři žádné věci na prodej.";
            return;
        }

        stallText.text += "<b>Vyber věc k prodeji:</b>\n";

        for (int i = 0; i < playerItems.Count; i++)
        {
            string itemName = playerItems[i];
            float price = GetItemPrice(itemName);

            if (i == selectedIndex)
            {
                stallText.text += $"<color=yellow>> <b>{itemName}</b> </color> | Výkup: <color=#00FF00>{price:F3} $</color>\n";
            }
            else
            {
                stallText.text += $"- {itemName} | Výkup: <color=#00FF00>{price:F3} $</color>\n";
            }
        }

        stallText.text += "\n<size=80%><i>[ENTER] Prodat  |  [ESC / E] Zavřít</i></size>";
    }
}