using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsManager : MonoBehaviour
{
    [Header("Panely")]
    public GameObject settingsCanvas;
    public GameObject mainMenuCanvas;

    [Header("UI Elementy")]
    public TMP_Dropdown qualityDropdown;
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;

    [Header("Reference na Spawner")]
    public NPCSpawner npcSpawner; // Pøetáhnìte sem váš NPCSpawner v Inspectoru

    private List<Resolution> filteredResolutions = new List<Resolution>();

    private void Start()
    {
        SetupResolutions();
        SetupQuality();

        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = Screen.fullScreen;
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }
    }

    private void SetupResolutions()
    {
        if (resolutionDropdown == null) return;

        Resolution[] resolutions = Screen.resolutions;
        filteredResolutions.Clear();
        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
        int currentResolutionIndex = 0;

        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = resolutions[i].width + " x " + resolutions[i].height;

            if (!options.Contains(option))
            {
                options.Add(option);
                filteredResolutions.Add(resolutions[i]);

                if (resolutions[i].width == Screen.width && resolutions[i].height == Screen.height)
                {
                    currentResolutionIndex = filteredResolutions.Count - 1;
                }
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();

        resolutionDropdown.onValueChanged.AddListener(SetResolution);
    }

    private void SetupQuality()
    {
        if (qualityDropdown == null) return;

        qualityDropdown.ClearOptions();
        List<string> options = new List<string>(QualitySettings.names);

        qualityDropdown.AddOptions(options);

        int currentQualityLevel = QualitySettings.GetQualityLevel();
        qualityDropdown.value = currentQualityLevel;
        qualityDropdown.RefreshShownValue();

        qualityDropdown.onValueChanged.AddListener(SetQuality);

        // Aplikujeme nastavení NPC podle výchozí kvality pøi spuštìní
        UpdateNPCSpawnLimitByQuality(currentQualityLevel);
    }

    // Nastavení Kvality
    public void SetQuality(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex);

        if (npcSpawner != null)
        {
            npcSpawner.UpdateLimitByQualityLevel(qualityIndex);
        }
    }

    // Úprava poètu NPC na základì úrovnì kvality
    private void UpdateNPCSpawnLimitByQuality(int qualityIndex)
    {
        if (npcSpawner == null) return;

        // V Unity bývají úrovnì kvality seøazeny od 0 (nejnižší / Very Low / Low)
        // podle poøadí v Project Settings > Quality.
        switch (qualityIndex)
        {
            case 0: // Very Low / Low
                npcSpawner.SetMaxNPCs(15);
                break;
            case 1: // Medium
                npcSpawner.SetMaxNPCs(30);
                break;
            case 2: // High
                npcSpawner.SetMaxNPCs(50); // Pùvodní hodnota
                break;
            default: // Ultra / Very High (pokud máte více úrovní)
                npcSpawner.SetMaxNPCs(75);
                break;
        }
    }

    public void SetResolution(int resolutionIndex)
    {
        if (resolutionIndex < 0 || resolutionIndex >= filteredResolutions.Count) return;

        Resolution resolution = filteredResolutions[resolutionIndex];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }

    public void BackToMainMenu()
    {
        if (settingsCanvas != null) settingsCanvas.SetActive(false);
        if (mainMenuCanvas != null) mainMenuCanvas.SetActive(true);
    }
}