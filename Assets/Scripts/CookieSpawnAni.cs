using System.Collections;
using UnityEngine;

public class CookieSpawnAnimation : MonoBehaviour
{
    [Header("Nastavení Animace")]
    public float duration = 0.35f;            // Jak dlouho animace trvá (v sekundách)
    public float overshootMultiplier = 1.3f; // O kolik % se sušenka přezvětší před smrštěním na normál

    private Vector3 targetScale;

    private void Awake()
    {
        // Uloží si původní velikost z prefapu a nastaví ji na 0
        targetScale = transform.localScale;
        transform.localScale = Vector3.zero;
    }

    private void Start()
    {
        StartCoroutine(AnimatePopIn());
    }

    private IEnumerator AnimatePopIn()
    {
        float timer = 0f;

        // Během animace se sušenka roztahuje
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = timer / duration;

            // Výpočet pružného zvětšení (od 0 přes 130 % zpět na 100 %)
            float currentScaleFactor;
            if (progress < 0.7f)
            {
                // Rychlé vyjetí z 0 na 130 %
                currentScaleFactor = Mathf.Lerp(0f, overshootMultiplier, progress / 0.7f);
            }
            else
            {
                // Zpětné propružení ze 130 % na 100 %
                currentScaleFactor = Mathf.Lerp(overshootMultiplier, 1f, (progress - 0.7f) / 0.3f);
            }

            transform.localScale = targetScale * currentScaleFactor;
            yield return null;
        }

        // Zajištění přesné finální velikosti
        transform.localScale = targetScale;
    }
}