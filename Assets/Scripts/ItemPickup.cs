using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public string itemName = "Cookies";
    public float pickupDistance = 3.0f; // Maximální vzdálenost pro sebrání

    // Hráč se hledá jen JEDNOU pro všechny sušenky (dřív to dělala každá zvlášť)
    private static Transform playerTransform;

    private void Start()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }
    }

    private void OnMouseDown()
    {
        if (playerTransform == null) return;

        // sqrMagnitude = bez odmocniny, rychlejší
        float sqrDist = (playerTransform.position - transform.position).sqrMagnitude;

        if (sqrDist <= pickupDistance * pickupDistance)
        {
            if (InventoryManager.Instance != null)
            {
                bool added = InventoryManager.Instance.AddItem(itemName);
                if (added)
                {
                    // Spustí déšť sušenek kolem hráče po sebrání první sušenky
                    if (CookieSpawner.Instance != null)
                    {
                        CookieSpawner.Instance.TriggerCookieRain(playerTransform.position);
                    }

                    Destroy(gameObject); // Odstraní sušenku ze scény
                }
            }
        }
        else
        {
            Debug.Log("Jsi příliš daleko od sušenky!");
        }
    }
}