using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance;

    [Header("UI Elementy")]
    public GameObject upgradeUI;
    public TextMeshProUGUI upgradeText;

    [Header("Nastavení Vylepšení Batohu")]
    public float baseInventoryCost = 2.0f;       // Výchozí cena prvního vylepšení ($2)
    public int slotsPerUpgrade = 20;             // Kolik slotů se přidá za nákup
    public float costMultiplier = 2.0f;          // Násobitel ceny pro další úroveň ($2 -> $4 -> $8...)

    private int inventoryUpgradeLevel = 0;
    private bool isUpgradeMenuOpen = false;
    private int selectedIndex = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (upgradeUI != null) upgradeUI.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        // Otevření / zavření menu vylepšení klávesou U nebo ESC
        if (Keyboard.current.uKey.wasPressedThisFrame)
        {
            ToggleUpgradeMenu();
        }
        else if (isUpgradeMenuOpen && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseUpgradeMenu();
        }

        if (!isUpgradeMenuOpen) return;

        // Navigace v menu (příprava pro více vylepšení do budoucna)
        if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            selectedIndex = 0; // Zatím 1 položka
            UpdateUI();
        }

        // Potvrzení nákupu stisknutím Enter
        if (Keyboard.current.enterKey.wasPressedThisFrame)
        {
            BuyInventoryUpgrade();
        }
    }

    public void ToggleUpgradeMenu()
    {
        isUpgradeMenuOpen = !isUpgradeMenuOpen;

        if (upgradeUI != null) upgradeUI.SetActive(isUpgradeMenuOpen);

        Cursor.lockState = isUpgradeMenuOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isUpgradeMenuOpen;

        if (isUpgradeMenuOpen)
        {
            UpdateUI();
        }
    }

    public void CloseUpgradeMenu()
    {
        isUpgradeMenuOpen = false;
        if (upgradeUI != null) upgradeUI.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public float GetCurrentUpgradeCost()
    {
        // Výpočet ceny podle úrovně ($2, $4, $8, $16...)
        return baseInventoryCost * Mathf.Pow(costMultiplier, inventoryUpgradeLevel);
    }

    private void BuyInventoryUpgrade()
    {
        if (InventoryManager.Instance == null) return;

        float currentCost = GetCurrentUpgradeCost();

        // Zkontroluje a odečte peníze
        if (InventoryManager.Instance.SpendMoney(currentCost))
        {
            // Zvýší kapacitu batohu o 20
            InventoryManager.Instance.capacity += slotsPerUpgrade;
            inventoryUpgradeLevel++;

            Debug.Log($"Vylepšeno! Nová kapacita: {InventoryManager.Instance.capacity}, Další úroveň stojí: {GetCurrentUpgradeCost():F2} $");

            InventoryManager.Instance.UpdateUI();
            UpdateUI();
        }
        else
        {
            Debug.Log($"Nemáš dostatek peněz! Potřebuješ {currentCost:F2} $.");
        }
    }

    private void UpdateUI()
    {
        if (upgradeText == null || InventoryManager.Instance == null) return;

        float currentCost = GetCurrentUpgradeCost();
        float playerMoney = InventoryManager.Instance.money;
        int currentCapacity = InventoryManager.Instance.capacity;

        bool canAfford = playerMoney >= currentCost;
        string priceColorHex = canAfford ? "#00FF00" : "#FF5555"; // Zelená pokud máš dost $, červená pokud ne

        upgradeText.text = $"<b>--- VYLEPŠENÍ (UPGRADES) ---</b>\n" +
                           $"Moje peníze: <b>{playerMoney:F2} $</b>\n\n";

        // Zvýrazněná položka pro vylepšení batohu
        if (selectedIndex == 0)
        {
            upgradeText.text += $"<color=yellow>> <b>Rozšíření batohu (+{slotsPerUpgrade} slotů)</b> </color>\n" +
                                $"  Aktuální kapacita: {currentCapacity} slotů\n" +
                                $"  Cena: <color={priceColorHex}>{currentCost:F2} $</color> <\n";
        }

        upgradeText.text += "\n<size=80%><i>[ENTER] Koupit vylepšení  |  [U / ESC] Zavřít</i></size>";
    }
}