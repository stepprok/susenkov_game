using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct ItemPriceConfig
{
    public string rewardName;
    public int minCookies;
    public int maxCookies;
}

public class NPCTrader : MonoBehaviour
{
    [Header("Nastavení obchodu")]
    public float interactDistance = 3.5f;
    public List<ItemPriceConfig> possibleItems = new List<ItemPriceConfig>();
    public int numberOfOffers = 3; // Kolik věcí NPC nabízí najednou

    private List<TradeOffer> generatedOffers = new List<TradeOffer>();
    private Transform playerTransform;

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;

        GenerateOffers();
    }

    private void GenerateOffers()
    {
        generatedOffers.Clear();
        if (possibleItems.Count == 0) return;

        // Náhodně vybere 1 až N nabídek pro toto NPC
        int count = Mathf.Min(numberOfOffers, possibleItems.Count);
        List<ItemPriceConfig> shuffled = new List<ItemPriceConfig>(possibleItems);
        
        for (int i = 0; i < count; i++)
        {
            int randomIndex = Random.Range(0, shuffled.Count);
            ItemPriceConfig config = shuffled[randomIndex];
            shuffled.RemoveAt(randomIndex);

            TradeOffer offer = new TradeOffer
            {
                rewardName = config.rewardName,
                requiredCookies = Random.Range(config.minCookies, config.maxCookies + 1)
            };
            generatedOffers.Add(offer);
        }
    }

    private void OnMouseDown()
    {
        if (playerTransform == null) return;

        float distance = Vector3.Distance(playerTransform.position, transform.position);
        if (distance <= interactDistance)
        {
            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.OpenShop(this, generatedOffers);
            }
        }
        else
        {
            Debug.Log("Jsi příliš daleko od NPC!");
        }
    }

    public void OnTradeComplete()
    {
        // Odstraní NPC po úspěšném obchodu
        Destroy(gameObject, 0.2f);
    }
}