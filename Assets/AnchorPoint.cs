using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using MagicLeap.OpenXR.Subsystems;
using TMPro;

public class AnchorPoint : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private Material defaultMaterial;
    [SerializeField] private Material selectedMaterial;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseIntensity = 0.3f;

    [Header("Magic Leap Settings")]
    [SerializeField] private TextMeshPro statusDisplay;

    private Renderer pointRenderer;
    private Material originalMaterial;
    private bool isSelected = false;
    private float pulseTime = 0f;

    // Magic Leap components
    private ARAnchor arAnchor;
    private MLXrAnchorSubsystem activeSubsystem;
    private ulong magicLeapAnchorId;

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

        // Initialize Magic Leap spatial anchor
        InitializeMagicLeapAnchor();
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

        // Update status display text
        UpdateStatusDisplay();
    }

    private void InitializeMagicLeapAnchor()
    {
        // Add ARAnchor component if it doesn't exist
        arAnchor = GetComponent<ARAnchor>();
        if (arAnchor == null)
        {
            arAnchor = gameObject.AddComponent<ARAnchor>();
        }

        // Get the Magic Leap subsystem
        if (AreSubsystemsLoaded(out activeSubsystem))
        {
            Debug.Log($"Magic Leap spatial anchor initialized for {gameObject.name}");
        }
        else
        {
            Debug.LogWarning("Magic Leap spatial anchor subsystem not loaded");
        }
    }

    private bool AreSubsystemsLoaded(out MLXrAnchorSubsystem subsystem)
    {
        subsystem = null;
        if (XRGeneralSettings.Instance == null) return false;
        if (XRGeneralSettings.Instance.Manager == null) return false;
        var activeLoader = XRGeneralSettings.Instance.Manager.activeLoader;
        if (activeLoader == null) return false;
        subsystem = activeLoader.GetLoadedSubsystem<XRAnchorSubsystem>() as MLXrAnchorSubsystem;
        return subsystem != null;
    }



    private void UpdateStatusDisplay()
    {
        if (statusDisplay == null) return;

        // Update Magic Leap anchor ID if available
        if (activeSubsystem != null && arAnchor != null && arAnchor.trackingState == TrackingState.Tracking)
        {
            magicLeapAnchorId = activeSubsystem.GetAnchorId(arAnchor);
            MLXrAnchorSubsystem.AnchorConfidence confidence = activeSubsystem.GetAnchorConfidence(arAnchor);

            statusDisplay.text = $"ML Anchor ID: {magicLeapAnchorId}\nConfidence: {confidence}\nStatus: {arAnchor.trackingState}\nPending: {arAnchor.pending}";

            // Color code based on confidence
            switch (confidence)
            {
                case MLXrAnchorSubsystem.AnchorConfidence.High:
                    statusDisplay.color = Color.green;
                    break;
                case MLXrAnchorSubsystem.AnchorConfidence.Medium:
                    statusDisplay.color = Color.yellow;
                    break;
                case MLXrAnchorSubsystem.AnchorConfidence.Low:
                    statusDisplay.color = Color.red;
                    break;
                default:
                    statusDisplay.color = Color.white;
                    break;
            }
        }
        else if (arAnchor != null)
        {
            // Show tracking state while waiting for Magic Leap ID
            statusDisplay.text = $"Status: {arAnchor.trackingState}\nPending: {arAnchor.pending}\nML Anchor ID: Not Available";
            statusDisplay.color = Color.gray;
        }
        else
        {
            statusDisplay.text = "No AR Anchor\nStatus: Not Initialized\nPending: N/A\nML Anchor ID: N/A";
            statusDisplay.color = Color.red;
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

    // Getter for Magic Leap anchor ID
    public ulong GetMagicLeapAnchorId()
    {
        return magicLeapAnchorId;
    }

    // Getter for AR Anchor component
    public ARAnchor GetARAnchor()
    {
        return arAnchor;
    }

    // Method to check if anchor is properly tracked
    public bool IsAnchorTracked()
    {
        return arAnchor != null && arAnchor.trackingState == TrackingState.Tracking && !arAnchor.pending;
    }
}

// Simple Billboard script to make text face the camera
public class Billboard : MonoBehaviour
{
    void Update()
    {
        if (Camera.main != null)
        {
            transform.LookAt(Camera.main.transform);
            transform.Rotate(0, 180, 0); // Flip to face camera properly
        }
    }
}
