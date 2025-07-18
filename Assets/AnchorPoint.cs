using UnityEngine;

public class AnchorPoint : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private Material defaultMaterial;
    [SerializeField] private Material selectedMaterial;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseIntensity = 0.3f;

    private Renderer pointRenderer;
    private Material originalMaterial;
    private bool isSelected = false;
    private float pulseTime = 0f;

    void Start()
    {
        // Get the renderer component
        pointRenderer = GetComponent<Renderer>();
        if (pointRenderer != null)
        {
            originalMaterial = pointRenderer.material;
        }

        // Add a collider if none exists
        if (GetComponent<Collider>() == null)
        {
            SphereCollider collider = gameObject.AddComponent<SphereCollider>();
            collider.radius = 0.05f;
            collider.isTrigger = true;
        }
    }

    void Update()
    {
        // Handle pulsing effect
        if (isSelected)
        {
            pulseTime += Time.deltaTime * pulseSpeed;
            float pulse = 1f + Mathf.Sin(pulseTime) * pulseIntensity;

            if (pointRenderer != null)
            {
                pointRenderer.material.SetFloat("_EmissionIntensity", pulse);
            }
        }
    }

    public void Select()
    {
        isSelected = true;
        if (pointRenderer != null && selectedMaterial != null)
        {
            pointRenderer.material = selectedMaterial;
        }
    }

    public void Deselect()
    {
        isSelected = false;
        if (pointRenderer != null)
        {
            pointRenderer.material = originalMaterial;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Optional: Add interaction feedback when controller approaches
        if (other.CompareTag("Controller"))
        {
            Select();
        }
    }

    void OnTriggerExit(Collider other)
    {
        // Optional: Remove interaction feedback when controller leaves
        if (other.CompareTag("Controller"))
        {
            Deselect();
        }
    }

    public void SetAnchorNumber(int number)
    {
        // Update the name to include the anchor number
        name = $"AnchorPoint_{number}";
    }
}
