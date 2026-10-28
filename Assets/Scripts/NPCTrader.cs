using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct ItemChanceConfig
{
    public string rewardName;
    public int minCookies;
    public int maxCookies;
    [Range(0.1f, 100f)] public float spawnChanceWeight; // Procentuální / relativní šance (váha) na získání
}

public class NPCTrader : MonoBehaviour
{
    [Header("Nastavení obchodu")]
    public float interactDistance = 3.5f;
    public int numberOfOffers = 3; // Kolik věcí NPC nabízí najednou

    [Header("Seznam všech předmětů a šancí (Automaticky předvyplněno)")]
    public List<ItemChanceConfig> allPossibleItems = new List<ItemChanceConfig>();

    private List<TradeOffer> generatedOffers = new List<TradeOffer>();
    private Transform playerTransform;

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;

        // Pokud je seznam prázdný, automaticky ho naplníme všemi vašimi věcmi a šancemi
        if (allPossibleItems.Count == 0)
        {
            InitializeItemsWithChances();
        }

        GenerateOffers();
    }

    private void InitializeItemsWithChances()
    {
        // --- 1. SURVIVAL TOOLS (Vysoká šance - běžné věci) ---
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "RoughKnife", minCookies = 2, maxCookies = 5, spawnChanceWeight = 10f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "RoughPickaxe", minCookies = 3, maxCookies = 6, spawnChanceWeight = 10f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "RoughAxe", minCookies = 3, maxCookies = 6, spawnChanceWeight = 10f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "RoughSpear", minCookies = 4, maxCookies = 7, spawnChanceWeight = 9f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "RoughShovel", minCookies = 3, maxCookies = 6, spawnChanceWeight = 10f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "RoughHoe", minCookies = 2, maxCookies = 5, spawnChanceWeight = 10f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "RoughHammer", minCookies = 3, maxCookies = 6, spawnChanceWeight = 10f });

        // --- 2. MODERN TOOLS & WEAPONS (Střední šance) ---
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "MetalKnife", minCookies = 8, maxCookies = 15, spawnChanceWeight = 7f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "MetalPickaxe", minCookies = 10, maxCookies = 18, spawnChanceWeight = 6f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "MetalAxe", minCookies = 10, maxCookies = 18, spawnChanceWeight = 6f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "MetalSpear", minCookies = 12, maxCookies = 20, spawnChanceWeight = 5f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "MetalShovel", minCookies = 9, maxCookies = 16, spawnChanceWeight = 6f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "MetalHoe", minCookies = 8, maxCookies = 14, spawnChanceWeight = 7f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "MetalHammer", minCookies = 10, maxCookies = 18, spawnChanceWeight = 6f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "ClaveHammer", minCookies = 12, maxCookies = 22, spawnChanceWeight = 5f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "WoodenBat", minCookies = 8, maxCookies = 15, spawnChanceWeight = 7f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "MetalBat", minCookies = 14, maxCookies = 25, spawnChanceWeight = 5f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "GolfBat", minCookies = 20, maxCookies = 35, spawnChanceWeight = 4f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "GolfClub", minCookies = 20, maxCookies = 35, spawnChanceWeight = 4f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "Katana", minCookies = 30, maxCookies = 50, spawnChanceWeight = 2f }); // Vzácná
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "BarnFork", minCookies = 10, maxCookies = 18, spawnChanceWeight = 6f });

        // --- 3. MAGICAL TOOLS (Nízká šance - velmi vzácné) ---
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "Scythe", minCookies = 40, maxCookies = 70, spawnChanceWeight = 1.5f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "Staff", minCookies = 45, maxCookies = 80, spawnChanceWeight = 1.2f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "Wand", minCookies = 35, maxCookies = 60, spawnChanceWeight = 2.0f });

        // --- 4. CONTAINERS & BREAKABLE OBJECTS ---
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "Chest", minCookies = 15, maxCookies = 25, spawnChanceWeight = 5f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "Bag", minCookies = 5, maxCookies = 10, spawnChanceWeight = 8f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "Barrel", minCookies = 6, maxCookies = 12, spawnChanceWeight = 7f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "Bowl", minCookies = 2, maxCookies = 4, spawnChanceWeight = 10f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "Plate", minCookies = 2, maxCookies = 4, spawnChanceWeight = 10f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "Vase", minCookies = 5, maxCookies = 10, spawnChanceWeight = 8f });

        // --- 5. LAMPS & INSTRUMENTS ---
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "CeilingLamp", minCookies = 10, maxCookies = 18, spawnChanceWeight = 5f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "WallLamp", minCookies = 8, maxCookies = 15, spawnChanceWeight = 6f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "Steampunk", minCookies = 12, maxCookies = 22, spawnChanceWeight = 4f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "Flute", minCookies = 15, maxCookies = 28, spawnChanceWeight = 4f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "Guitar", minCookies = 25, maxCookies = 45, spawnChanceWeight = 2.5f });
        allPossibleItems.Add(new ItemChanceConfig { rewardName = "PanFlute", minCookies = 18, maxCookies = 32, spawnChanceWeight = 3f });
    }

    private void GenerateOffers()
    {
        generatedOffers.Clear();
        if (allPossibleItems.Count == 0) return;

        List<ItemChanceConfig> availablePool = new List<ItemChanceConfig>(allPossibleItems);
        int count = Mathf.Min(numberOfOffers, availablePool.Count);

        for (int i = 0; i < count; i++)
        {
            ItemChanceConfig chosenConfig = GetRandomItemByChance(availablePool);
            availablePool.Remove(chosenConfig); // Aby NPC nenabízelo dvakrát to samé

            TradeOffer offer = new TradeOffer
            {
                rewardName = chosenConfig.rewardName,
                requiredCookies = Random.Range(chosenConfig.minCookies, chosenConfig.maxCookies + 1)
            };
            generatedOffers.Add(offer);
        }
    }

    // Algoritmus pro výběr na základě procentuální šance (váhy)
    private ItemChanceConfig GetRandomItemByChance(List<ItemChanceConfig> pool)
    {
        float totalWeight = 0f;
        foreach (var item in pool)
        {
            totalWeight += item.spawnChanceWeight;
        }

        float randomValue = Random.Range(0f, totalWeight);
        float currentSum = 0f;

        foreach (var item in pool)
        {
            currentSum += item.spawnChanceWeight;
            if (randomValue <= currentSum)
            {
                return item;
            }
        }

        return pool[0]; // Pojistka
    }

    private void OnMouseDown()
    {
        if (playerTransform == null) return;

        float distance = Vector3.Distance(playerTransform.position, transform.position);
        if (distance <= interactDistance)
        {
            if (TraderUI.Instance != null)
                TraderUI.Instance.Open(this, generatedOffers);
            else if (ShopManager.Instance != null)
                ShopManager.Instance.OpenShop(this, generatedOffers);
        }
    }

    public void OnTradeComplete()
    {
        Destroy(gameObject, 0.2f);
    }
}