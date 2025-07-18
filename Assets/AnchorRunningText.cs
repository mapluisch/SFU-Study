using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnchorRunningText : MonoBehaviour
{
    [Header("Text Settings")]
    [SerializeField] private string textContent = "Waypoint";
    [SerializeField] private float cylinderRadius = 0.125f; // Half of 0.25 scale
    [SerializeField] private float textHeight = 0.1f;
    [SerializeField] private float characterSpacing = 0.02f;
    [SerializeField] private float rotationSpeed = 30f; // Degrees per second

    [Header("Text Appearance")]
    [SerializeField] private Font textFont;
    [SerializeField] private int fontSize = 24;
    [SerializeField] private Color textColor = Color.white;

    private List<TextMesh> textMeshes = new List<TextMesh>();
    private float currentRotation = 0f;
    private float totalArcLength;

    void Start()
    {
        CreateCircularText();
    }

    void Update()
    {
        AnimateText();
    }

    void CreateCircularText()
    {
        // Calculate the total arc length needed for the text
        float characterWidth = fontSize * 0.01f; // Approximate character width
        totalArcLength = textContent.Length * (characterWidth + characterSpacing);

        // Calculate the angle per character
        float anglePerCharacter = (totalArcLength / cylinderRadius) * Mathf.Rad2Deg;

        // Create a TextMesh for each character
        for (int i = 0; i < textContent.Length; i++)
        {
            // Calculate position on the cylinder
            float angle = i * anglePerCharacter;
            float x = cylinderRadius * Mathf.Cos(angle * Mathf.Deg2Rad);
            float z = cylinderRadius * Mathf.Sin(angle * Mathf.Deg2Rad);

            // Create GameObject for this character
            GameObject charObject = new GameObject($"Char_{i}");
            charObject.transform.SetParent(transform);
            charObject.transform.localPosition = new Vector3(x, textHeight, z);

            // Make the character face outward from the cylinder
            Vector3 direction = charObject.transform.localPosition.normalized;
            charObject.transform.localRotation = Quaternion.LookRotation(direction, Vector3.up);

            // Add TextMesh component
            TextMesh textMesh = charObject.AddComponent<TextMesh>();
            textMesh.text = textContent[i].ToString();
            textMesh.font = textFont;
            textMesh.fontSize = fontSize;
            textMesh.color = textColor;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;

            // Store reference
            textMeshes.Add(textMesh);
        }
    }

    void AnimateText()
    {
        currentRotation += rotationSpeed * Time.deltaTime;

        // Rotate all characters around the cylinder
        for (int i = 0; i < textMeshes.Count; i++)
        {
            if (textMeshes[i] != null)
            {
                // Calculate new position
                float anglePerCharacter = (totalArcLength / cylinderRadius) * Mathf.Rad2Deg;
                float angle = (i * anglePerCharacter) + currentRotation;

                float x = cylinderRadius * Mathf.Cos(angle * Mathf.Deg2Rad);
                float z = cylinderRadius * Mathf.Sin(angle * Mathf.Deg2Rad);

                // Update position
                textMeshes[i].transform.localPosition = new Vector3(x, textHeight, z);

                // Update rotation to face outward
                Vector3 direction = textMeshes[i].transform.localPosition.normalized;
                textMeshes[i].transform.localRotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }
    }

    // Public method to change text content
    public void SetText(string newText)
    {
        textContent = newText;
        ClearText();
        CreateCircularText();
    }

    // Public method to change rotation speed
    public void SetRotationSpeed(float speed)
    {
        rotationSpeed = speed;
    }

    void ClearText()
    {
        foreach (TextMesh textMesh in textMeshes)
        {
            if (textMesh != null)
            {
                DestroyImmediate(textMesh.gameObject);
            }
        }
        textMeshes.Clear();
    }

    void OnDestroy()
    {
        ClearText();
    }
}
