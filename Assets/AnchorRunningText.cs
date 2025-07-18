using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class AnchorRunningText : MonoBehaviour
{
    [Header("Text Settings")]
    [SerializeField] private string textContent = "Waypoint";
    [SerializeField] private float cylinderRadius = 0.125f; // Half of 0.25 scale
    [SerializeField] private float textPadding = 0.02f; // Padding from cylinder surface
    [SerializeField] private float textHeight = 0.1f;
    [SerializeField] private float characterSpacing = 0.02f;
    [SerializeField] private float rotationSpeed = 30f; // Degrees per second

    [Header("Text Appearance")]
    [SerializeField] private int fontSize = 24;
    [SerializeField] private Color textColor = Color.white;

    private List<TextMeshPro> textMeshes = new List<TextMeshPro>();
    private float currentRotation = 0f;
    private float totalArcLength;
    private GameObject textContainer;
    private bool needsUpdate = true;

    // Cached values for change detection
    private string cachedTextContent;
    private float cachedCylinderRadius;
    private float cachedTextPadding;
    private float cachedTextHeight;
    private float cachedCharacterSpacing;
    private int cachedFontSize;
    private Color cachedTextColor;

    void Start()
    {
        if (textContainer == null)
        {
            CreateCircularText();
        }
        CacheValues();
    }

    void Update()
    {
        // Check for real-time changes in the editor
        if (Application.isEditor && HasInspectorChanged())
        {
            needsUpdate = true;
            CacheValues();
        }

        // Update text if needed
        if (needsUpdate)
        {
            CreateCircularText();
            needsUpdate = false;
        }

        AnimateText();
    }

    void OnValidate()
    {
        // Create text if it doesn't exist (for initial setup)
        if (textContainer == null)
        {
            CreateCircularText();
        }
        else
        {
            // Mark that we need to update, but don't create objects here
            needsUpdate = true;
        }
    }

    void CacheValues()
    {
        cachedTextContent = textContent;
        cachedCylinderRadius = cylinderRadius;
        cachedTextPadding = textPadding;
        cachedTextHeight = textHeight;
        cachedCharacterSpacing = characterSpacing;
        cachedFontSize = fontSize;
        cachedTextColor = textColor;
    }

    bool HasInspectorChanged()
    {
        return cachedTextContent != textContent ||
               cachedCylinderRadius != cylinderRadius ||
               cachedTextPadding != textPadding ||
               cachedTextHeight != textHeight ||
               cachedCharacterSpacing != characterSpacing ||
               cachedFontSize != fontSize ||
               cachedTextColor != textColor;
    }

    void CreateCircularText()
    {
        // Find existing text container or create new one
        GameObject existingContainer = FindExistingTextContainer();
        if (existingContainer != null)
        {
            // Reuse existing container
            textContainer = existingContainer;
            ClearExistingCharacters();
        }
        else
        {
            // Create new container
            textContainer = new GameObject("TextContainer");
            textContainer.transform.SetParent(transform);
            textContainer.transform.localPosition = Vector3.zero;
        }

        // Calculate the total arc length needed for the text
        float characterWidth = fontSize * 0.01f; // Approximate character width
        totalArcLength = textContent.Length * (characterWidth + characterSpacing);

        // Calculate the angle per character
        float anglePerCharacter = (totalArcLength / (cylinderRadius + textPadding)) * Mathf.Rad2Deg;

        // Create a TextMeshPro for each character
        for (int i = 0; i < textContent.Length; i++)
        {
            // Calculate position on the cylinder with padding (reverse order)
            float angle = (textContent.Length - 1 - i) * anglePerCharacter;
            float radiusWithPadding = cylinderRadius + textPadding;
            float x = radiusWithPadding * Mathf.Cos(angle * Mathf.Deg2Rad);
            float z = radiusWithPadding * Mathf.Sin(angle * Mathf.Deg2Rad);

            // Create GameObject for this character
            GameObject charObject = new GameObject($"Char_{i}");
            charObject.transform.SetParent(textContainer.transform);
            charObject.transform.localPosition = new Vector3(x, textHeight, z);

            // Make the character face outward from the cylinder center (no X rotation)
            Vector3 direction = new Vector3(charObject.transform.localPosition.x, 0, charObject.transform.localPosition.z).normalized;
            charObject.transform.localRotation = Quaternion.LookRotation(direction, Vector3.up);

            // Flip the character by rotating 180 degrees around Y-axis
            charObject.transform.localRotation *= Quaternion.Euler(0, 180, 0);

            // Add TextMeshPro component
            TextMeshPro textMesh = charObject.AddComponent<TextMeshPro>();
            textMesh.text = textContent[i].ToString();
            textMesh.fontSize = fontSize;
            textMesh.color = textColor;
            textMesh.alignment = TextAlignmentOptions.Center;

            // Store reference
            textMeshes.Add(textMesh);
        }
    }

    GameObject FindExistingTextContainer()
    {
        // Look for existing TextContainer in children
        Transform[] children = GetComponentsInChildren<Transform>();
        foreach (Transform child in children)
        {
            if (child != transform && child.name == "TextContainer")
            {
                return child.gameObject;
            }
        }
        return null;
    }

    void ClearExistingCharacters()
    {
        // Clear the list
        textMeshes.Clear();

        // Remove all character children from the container
        // destory all children of this gameobject while there are still child count > 0
        while (transform.childCount > 0)
        {
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
        }
    }

    void AnimateText()
    {
        if (textContainer == null) return;

        currentRotation += rotationSpeed * Time.deltaTime;

        // Rotate the entire text container around the cylinder
        textContainer.transform.localRotation = Quaternion.Euler(0, currentRotation, 0);
    }

    // Public method to change text content
    public void SetText(string newText)
    {
        textContent = newText;
        needsUpdate = true;
        CacheValues();
    }

    // Public method to change rotation speed
    public void SetRotationSpeed(float speed)
    {
        rotationSpeed = speed;
    }

    // Public method to change cylinder radius
    public void SetCylinderRadius(float radius)
    {
        cylinderRadius = radius;
        needsUpdate = true;
        CacheValues();
    }

    // Public method to change text padding
    public void SetTextPadding(float padding)
    {
        textPadding = padding;
        needsUpdate = true;
        CacheValues();
    }

    void OnDestroy()
    {
        // Clean up when the component is destroyed
        if (textContainer != null)
        {
            Destroy(textContainer);
        }
    }
}
