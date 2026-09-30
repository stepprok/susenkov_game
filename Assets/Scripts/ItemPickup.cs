using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public string itemName = "Cookies";
    public float pickupDistance = 3.0f; // Maximální vzdálenost pro sebrání
    private Transform playerTransform;

    private void Start()
    {
        // Najde hráče podle Tagu "Player"
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    private void OnMouseDown()
    {
        if (playerTransform == null) return;

        // Kontrola vzdálenosti mezi hráčem a sušenkou
        float distance = Vector3.Distance(playerTransform.position, transform.position);

        if (distance <= pickupDistance)
        {
            if (InventoryManager.Instance != null)
            {
                bool added = InventoryManager.Instance.AddItem(itemName);
                if (added)
                {
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