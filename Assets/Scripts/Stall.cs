using UnityEngine;
using UnityEngine.InputSystem;

public class Stall : MonoBehaviour
{
    public float interactDistance = 4.0f;

    private void Update()
    {
        // Reaguje na kliknutí levého tlačítka myši (případně změň na E nebo pravé tlačítko)
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryInteract();
        }
    }

    private void TryInteract()
    {
        if (Camera.main == null) return;

        // Vystřelí paprsek přesně ze středu kamery (tam, kam se dívá křížek)
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            // Pokud paprsek trefil tento stánek (nebo některou z jeho částí)
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                if (StallManager.Instance != null)
                {
                    StallManager.Instance.OpenStall();
                }
            }
        }
    }
}