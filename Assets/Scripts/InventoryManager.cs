using System.Collections.Generic;
using UnityEngine;
using TMPro; // Použito pro TextMeshPro (standard v Unity)

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("UI a Nastavení")]
    public GameObject inventoryUI;          // Panel inventáře
    public TextMeshProUGUI inventoryText;  // Textové pole pro výpis předmětů
    public int capacity = 20;

    [Header("Seznam položek")]
    public List<string> items = new List<string>();

    private bool isUIOpen = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Skryje inventář při startu hry
        if (inventoryUI != null)
        {
            inventoryUI.SetActive(false);
        }

        UpdateUI();
    }

    private void Update()
    {
        // Otevření / zavření inventáře klávesou E
        if (Input.GetKeyDown(KeyCode.E))
        {
            ToggleInventory();
        }
    }

    public void ToggleInventory()
    {
        isUIOpen = !isUIOpen;

        if (inventoryUI != null)
        {
            inventoryUI.SetActive(isUIOpen);
        }

        // Správa kurzoru myši
        Cursor.lockState = isUIOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isUIOpen;

        if (isUIOpen)
        {
            UpdateUI();
        }
    }

    public bool AddItem(string item)
    {
        if (items.Count >= capacity)
        {
            Debug.Log("Inventář je plný!");
            return false;
        }

        items.Add(item);
        UpdateUI(); // Aktualizuje výpis v UI
        return true;
    }

    // Metoda pro vykreslení seznamu předmětů do UI
    public void UpdateUI()
    {
        if (inventoryText == null) return;

        if (items.Count == 0)
        {
            inventoryText.text = "Inventář je prázdný.";
            return;
        }

        inventoryText.text = "<b>Předměty (" + items.Count + "/" + capacity + "):</b>\n";
        foreach (string item in items)
        {
            inventoryText.text += "- " + item + "\n";
        }
    }
}