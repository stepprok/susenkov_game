using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Panely Canvasù")]
    public GameObject mainMenuCanvas;    // Canvas / Panel Hlavního Menu
    public GameObject settingsCanvas;    // Canvas / Panel Nastavení

    private void Start()
    {
        // Pøi naètení scény se ujistíme, že je Hlavní menu vidìt a Nastavení je skryté
        if (mainMenuCanvas != null) mainMenuCanvas.SetActive(true);
        if (settingsCanvas != null) settingsCanvas.SetActive(false);
    }

    // Funkce pro tlaèítko "HRÁT"
    public void PlayGame()
    {
        // Naète následující scénu podle poøadí v Build Settings (nebo zadejte název scény napø. SceneManager.LoadScene("SampleScene"))
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    // Funkce pro tlaèítko "NASTAVENÍ"
    public void OpenSettings()
    {
        if (mainMenuCanvas != null) mainMenuCanvas.SetActive(false);
        if (settingsCanvas != null) settingsCanvas.SetActive(true);
    }

    // Funkce pro tlaèítko "ZPÌT" (v menu nastavení)
    public void CloseSettings()
    {
        if (settingsCanvas != null) settingsCanvas.SetActive(false);
        if (mainMenuCanvas != null) mainMenuCanvas.SetActive(true);
    }

    // Funkce pro tlaèítko "KONEC"
    public void QuitGame()
    {
        Debug.Log("Hra se ukonèuje...");

        // Ukonèí sestavenou hru (.exe)
        Application.Quit();

        // Ukonèí Play Mode, pokud testujete pøímo v Unity Editoru
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}