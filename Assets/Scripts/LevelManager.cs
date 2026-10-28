using UnityEngine;
using TMPro;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    [Header("UI Elementy")]
    public TextMeshProUGUI levelText; // Optional old text. The new HUD doesn't need it - you can leave it empty.

    [Header("Nastavení levelu")]
    public int currentLevel = 1;
    public int upgradesPerLevel = 1; // Kolik zakoupených upgradù posune hráèe o 1 level

    [Header("HUD - název a dokonèení úrovnì")]
    public string levelName = "Tutoriál";
    public int upgradesToComplete = 10; // Kolik upgradù = 100 % dokonèení

    private int totalUpgradesBought = 0;

    /// <summary>0..1 completion used by the HUD progress bar.</summary>
    public float CompletionPercent => Mathf.Clamp01(totalUpgradesBought / (float)Mathf.Max(1, upgradesToComplete));

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