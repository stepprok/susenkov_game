using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public struct ItemHandModel
{
    public string itemName;            // Název předmětu (např. "Golf Club", "Cookies", "Katana")
    public GameObject modelPrefab;      // Prefab 3D modelu
    public Vector3 spawnPositionOffset; // Posun pozice v ruce (X, Y, Z)
    public Vector3 spawnRotationOffset; // Otočení v ruce ve stupních (X, Y, Z)
}

public class PlayerHandManager : MonoBehaviour
{
    public static PlayerHandManager Instance;

    [Header("Pozice Ruky")]
    public Transform handHolder;

    [Header("3D Modely v Ruce")]
    public List<ItemHandModel> handModels = new List<ItemHandModel>();

    private GameObject currentHeldObject;
    private string currentHeldItemName = "";
    private bool isAnimating = false; // Blokuje spuštění více animací najednou

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        if (currentHeldObject == null || isAnimating) return;

        // Detekce stisku Pravého tlačítka myši nebo klávesy F
        bool rightClick = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        bool fKey = Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;

        if (rightClick || fKey)
        {
            if (currentHeldItemName == "Cookies")
            {
                StartCoroutine(EatCookieAnimation());
            }
            else
            {
                StartCoroutine(SwingAttackAnimation());
            }
        }
    }

    public void EquipItem(string itemName)
    {
        if (isAnimating) return;

        if (currentHeldObject != null)
        {
            Destroy(currentHeldObject);
        }

        currentHeldItemName = itemName;

        ItemHandModel match = handModels.Find(m => m.itemName == itemName);
        if (match.modelPrefab != null && handHolder != null)
        {
            currentHeldObject = Instantiate(match.modelPrefab, handHolder);
            
            currentHeldObject.transform.localPosition = match.spawnPositionOffset;
            currentHeldObject.transform.localRotation = Quaternion.Euler(match.spawnRotationOffset);

            // Vypnutí fyziky v ruce
            Rigidbody rb = currentHeldObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }

            // Vypnutí kolizí v ruce
            Collider col = currentHeldObject.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }
        }

        Debug.Log($"Vzal jsi do ruky: {itemName}");
    }

    // --- ANIMACE 1: Úder / Švihnutí nástrojem (Golfová hůl, meč, nůž atd.) ---
    private IEnumerator SwingAttackAnimation()
    {
        if (currentHeldObject == null) yield break;

        isAnimating = true;

        Vector3 startPos = currentHeldObject.transform.localPosition;
        Quaternion startRot = currentHeldObject.transform.localRotation;

        // Definice pozice a rotace v nejzazším bodě úderu
        Vector3 swingPos = startPos + new Vector3(0.05f, -0.05f, 0.25f);
        Quaternion swingRot = startRot * Quaternion.Euler(45f, -25f, 15f);

        // 1. Rychlé švihnutí dopředu a dolů (0.08 s)
        float duration = 0.08f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            currentHeldObject.transform.localPosition = Vector3.Lerp(startPos, swingPos, t);
            currentHeldObject.transform.localRotation = Quaternion.Slerp(startRot, swingRot, t);
            yield return null;
        }

        // 2. Krátká výdrž v bodě dopadu
        yield return new WaitForSeconds(0.05f);

        // 3. Plynulý návrat do výchozí pozice v ruce (0.18 s)
        duration = 0.18f;
        elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            currentHeldObject.transform.localPosition = Vector3.Lerp(swingPos, startPos, t);
            currentHeldObject.transform.localRotation = Quaternion.Slerp(swingRot, startRot, t);
            yield return null;
        }

        // Ujistíme se, že předmět skončí na přesné původní pozici
        currentHeldObject.transform.localPosition = startPos;
        currentHeldObject.transform.localRotation = startRot;

        isAnimating = false;
    }

    // --- ANIMACE 2: Easter Egg - Snědení sušenky ---
    private IEnumerator EatCookieAnimation()
    {
        if (currentHeldObject == null) yield break;

        isAnimating = true;

        Vector3 startPos = currentHeldObject.transform.localPosition;
        Quaternion startRot = currentHeldObject.transform.localRotation;
        Vector3 startScale = currentHeldObject.transform.localScale;

        Vector3 mouthPos = startPos + new Vector3(0f, -0.1f, -0.3f);
        float duration = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            currentHeldObject.transform.localPosition = Vector3.Lerp(startPos, mouthPos, t);
            currentHeldObject.transform.localRotation = Quaternion.Lerp(startRot, Quaternion.Euler(20f, 45f, 0f), t);
            yield return null;
        }

        for (int i = 0; i < 3; i++)
        {
            currentHeldObject.transform.localScale = startScale * 0.8f;
            yield return new WaitForSeconds(0.08f);
            currentHeldObject.transform.localScale = startScale;
            yield return new WaitForSeconds(0.08f);
        }

        elapsed = 0f;
        duration = 0.2f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            currentHeldObject.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.RemoveItems("Cookies", 1);
        }

        Destroy(currentHeldObject);
        currentHeldItemName = "";
        isAnimating = false;
    }
}