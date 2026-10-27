using UnityEngine;
using TMPro;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    [Header("UI Elementy")]
    public TextMeshProUGUI levelText; // Text v horní èásti obrazovky (napø. "Level: 3")

    [Header("Nastavení levelu")]
    public int currentLevel = 1;
    public int upgradesPerLevel = 1; // Kolik zakoupených upgradù posune hráèe o 1 level

    private int totalUpgradesBought = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        UpdateLevelUI();
    }

    // Volá se pokaždé, když hráè úspìšnì koupí jakýkoliv upgrade
    public void AddUpgradeProgress()
    {
        totalUpgradesBought++;

        // Výpoèet levelu podle celkového poètu koupených upgradù
        currentLevel = 1 + (totalUpgradesBought / upgradesPerLevel);

        UpdateLevelUI();

        // OPRAVENO: Odstranìno neplatné jméno parametru "level:"
        Debug.Log($"Level zvýšen! Celkem upgradù: {totalUpgradesBought}, Aktuální Level: {currentLevel}");
    }

    private void UpdateLevelUI()
    {
        if (levelText != null)
        {
            levelText.text = $"<b>Level: {currentLevel}</b>\n<size=75%>Upgrady: {totalUpgradesBought}</size>";
        }
    }
}