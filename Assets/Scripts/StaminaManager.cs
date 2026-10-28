using UnityEngine;
using EasyPeasyFirstPersonController;

/// <summary>
/// Add this to the same GameObject as FirstPersonController.
/// Sprinting drains stamina; stamina regenerates after a short delay.
/// When stamina hits 0 the player is "exhausted": InputManager.sprint returns false
/// (via CanSprint) until stamina recovers above the recovery threshold.
/// </summary>
[RequireComponent(typeof(FirstPersonController))]
public class StaminaSystem : MonoBehaviour
{
    public static StaminaSystem Instance;

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float drainPerSecond = 20f;
    public float regenPerSecond = 15f;
    public float regenDelay = 1f;                 // seconds after sprinting before regen starts
    public float exhaustedRecoverThreshold = 20f; // stamina needed before sprinting works again

    public float Current { get; private set; }
    public float Normalized => maxStamina <= 0f ? 0f : Current / maxStamina;
    public bool IsExhausted { get; private set; }

    /// <summary>InputManager.sprint uses this: false while exhausted.</summary>
    public bool CanSprint => !IsExhausted && Current > 0f;

    private FirstPersonController fpc;
    private float regenTimer;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        fpc = GetComponent<FirstPersonController>();
        Current = maxStamina;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        if (IsSprinting() && CanSprint)
        {
            Current -= drainPerSecond * dt;
            regenTimer = regenDelay;

            if (Current <= 0f)
            {
                Current = 0f;
                IsExhausted = true; // from now on InputManager.sprint is false
            }
        }
        else
        {
            if (regenTimer > 0f) regenTimer -= dt;
            else Current = Mathf.Min(maxStamina, Current + regenPerSecond * dt);

            if (IsExhausted && Current >= exhaustedRecoverThreshold)
                IsExhausted = false;
        }
    }

    private bool IsSprinting()
    {
        if (!fpc.isGrounded || fpc.isInWater) return false;

        // Don't drain during slide / climb / swim / crouch states
        string state = fpc.CurrentState != null ? fpc.CurrentState.GetType().Name : "";
        if (state.Contains("Slide") || state.Contains("Climb") || state.Contains("Swim") || state.Contains("Crouch"))
            return false;

        // Sprinting = moving clearly faster than walking
        Vector3 v = fpc.characterController.velocity;
        v.y = 0f;
        float threshold = fpc.walkSpeed + (fpc.sprintSpeed - fpc.walkSpeed) * 0.5f;
        return v.magnitude > threshold;
    }

    public void AddStamina(float amount)
    {
        Current = Mathf.Clamp(Current + amount, 0f, maxStamina);
        if (IsExhausted && Current >= exhaustedRecoverThreshold) IsExhausted = false;
    }
}