using UnityEngine;

public class CookieSpawnAnimation : MonoBehaviour
{
    [Header("Nastavení Animace")]
    [Tooltip("Čím kratší, tím méně snímků se mění scale objektu s fyzikou (to je drahé). Doporučeno 0.3.")]
    public float duration = 0.3f;
    public float overshootMultiplier = 1.3f;

    private Vector3 targetScale;
    private float timer;

    private void Awake()
    {
        targetScale = transform.localScale;
        transform.localScale = Vector3.zero;
    }

    // Místo coroutiny (alokace odpadu na každé sušence) jednoduchý Update, který se po konci vypne.
    private void Update()
    {
        timer += Time.deltaTime;
        float progress = timer / duration;

        if (progress >= 1f)
        {
            transform.localScale = targetScale;
            enabled = false; // už se nevolá
            return;
        }

        float f = progress < 0.7f
            ? Mathf.Lerp(0f, overshootMultiplier, progress / 0.7f)
            : Mathf.Lerp(overshootMultiplier, 1f, (progress - 0.7f) / 0.3f);

        transform.localScale = targetScale * f;
    }
}