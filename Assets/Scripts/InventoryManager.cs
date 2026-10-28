using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("UI a Nastavení")]
    public GameObject inventoryUI;
    public TextMeshProUGUI inventoryText;
    public int capacity = 20;

    [Header("Finance")]
    public float money = 0.0f; // Aktuální stav peněz hráče

    [Header("Sušenky")]
    [Tooltip("Všechny názvy, které se počítají jako sušenka (nezáleží na velikosti písmen). HUD i obchodník používají tento seznam.")]
    public List<string> cookieNames = new List<string> { "Cookies", "Cookie", "Sušenka", "Sušenky" };

    [Header("Seznam položek")]
    public List<string> items = new List<string>();

    private bool isUIOpen = false;
    private int selectedIndex = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (inventoryUI != null) inventoryUI.SetActive(false);
        UpdateUI();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame)
        {
            ToggleInventory();
        }

        if (isUIOpen && items.Count > 0)
        {
            if (Keyboard.current.downArrowKey.wasPressedThisFrame)
            {
                selectedIndex = (selectedIndex + 1) % items.Count;
                UpdateUI();
            }
            else if (Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                selectedIndex = (selectedIndex - 1 + items.Count) % items.Count;
                UpdateUI();
            }

            if (Keyboard.current.enterKey.wasPressedThisFrame)
            {
                string selectedItem = items[selectedIndex];
                if (PlayerHandManager.Instance != null)
                {
                    PlayerHandManager.Instance.EquipItem(selectedItem);
                }
            }
        }
    }

    public void ToggleInventory()
    {
        isUIOpen = !isUIOpen;

        if (inventoryUI != null) inventoryUI.SetActive(isUIOpen);

        Cursor.lockState = isUIOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isUIOpen;

        if (isUIOpen)
        {
            if (selectedIndex >= items.Count) selectedIndex = 0;
            UpdateUI();
        }
    }

    public void AddMoney(float amount)
    {
        money += amount;
        UpdateUI();
    }

    public bool AddItem(string item)
    {
        if (items.Count >= capacity) return false;

        items.Add(item);
        UpdateUI();
        return true;
    }

    public int GetItemCount(string itemName)
    {
        int count = 0;
        foreach (string item in items)
        {
            if (item == itemName) count++;
        }
        return count;
    }

    // ------------------------------------------------------------------
    // Sušenky (jedno místo, které rozhoduje, co je sušenka)
    // ------------------------------------------------------------------

    public bool IsCookie(string item)
    {
        if (string.IsNullOrEmpty(item)) return false;
        string trimmed = item.Trim();
        foreach (string name in cookieNames)
        {
            if (!string.IsNullOrEmpty(name) &&
                string.Equals(name.Trim(), trimmed, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public int CountCookies()
    {
        int count = 0;
        foreach (string item in items)
            if (IsCookie(item)) count++;
        return count;
    }

    public bool RemoveCookies(int amount)
    {
        if (CountCookies() < amount) return false;

        int removed = 0;
        for (int i = items.Count - 1; i >= 0 && removed < amount; i--)
        {
            if (IsCookie(items[i]))
            {
                items.RemoveAt(i);
                removed++;
            }
        }

        if (selectedIndex >= items.Count && items.Count > 0)
            selectedIndex = items.Count - 1;

        UpdateUI();
        return true;
    }

    public bool RemoveItems(string itemName, int amount)
    {
        if (GetItemCount(itemName) < amount) return false;

        int removed = 0;
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] == itemName)
            {
                items.RemoveAt(i);
                removed++;
                if (removed >= amount) break;
            }
        }

        if (selectedIndex >= items.Count && items.Count > 0)
        {
            selectedIndex = items.Count - 1;
        }

        UpdateUI();
        return true;
    }

    public void UpdateUI()
    {
        if (inventoryText == null) return;

        if (items.Count == 0)
        {
            inventoryText.text = $"<b>Peníze: {money:F3} $</b>\n\nInventář je prázdný.";
            return;
        }

        inventoryText.text = $"<b>Peníze: {money:F3} $</b>\n<b>Předměty ({items.Count}/{capacity}):</b>\n";
        for (int i = 0; i < items.Count; i++)
        {
            if (i == selectedIndex && isUIOpen)
            {
                inventoryText.text += $"<color=yellow>> <b>{items[i]}</b> <</color>\n";
            }
            else
            {
                inventoryText.text += "- " + items[i] + "\n";
            }
        }
    }

    public bool SpendMoney(float amount)
    {
        if (money >= amount)
        {
            money -= amount;
            UpdateUI();
            return true;
        }
        return false;
    }
}