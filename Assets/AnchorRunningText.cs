using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class AnchorRunningText : MonoBehaviour
{
    [Header("Text Settings")]
    [SerializeField] private string textContent = "Waypoint";
    [SerializeField] private float cylinderRadius = 0.125f;
    [SerializeField] private float textPadding = 0.02f;
    [SerializeField] private float textHeight = 0.1f;
    [SerializeField] private float characterSpacing = 0.02f;
    [SerializeField] private float rotationSpeed = 30f;

    [Header("Text Appearance")]
    [SerializeField] private int fontSize = 24;
    [SerializeField] private Color textColor = Color.white;

    private GameObject textContainer;
    private float currentRotation = 0f;

    void Start()
    {
        if (textContainer == null)
        {
            CreateCircularText();
        }
    }

    void Update()
    {
        AnimateText();
    }

    void OnValidate()
    {
        if (textContainer == null)
        {
            CreateCircularText();
        }
        else if (textContainer.transform.childCount > 0)
        {
            RepositionCharacters();
        }
    }

    void CreateCircularText()
    {
        GameObject existingContainer = FindExistingTextContainer();
        if (existingContainer != null)
        {
            textContainer = existingContainer;
            RepositionCharacters();
        }
        else if (!IsInPrefabMode())
        {
            textContainer = new GameObject("TextContainer");
            textContainer.transform.SetParent(transform);
            textContainer.transform.localPosition = Vector3.zero;
            CreateCharacters();
        }
    }

    void CreateCharacters()
    {
        float characterWidth = fontSize * 0.01f;
        float totalArcLength = textContent.Length * (characterWidth + characterSpacing);
        float anglePerCharacter = (totalArcLength / (cylinderRadius + textPadding)) * Mathf.Rad2Deg;

        for (int i = 0; i < textContent.Length; i++)
        {
            float angle = (textContent.Length - 1 - i) * anglePerCharacter;
            float radiusWithPadding = cylinderRadius + textPadding;
            float x = radiusWithPadding * Mathf.Cos(angle * Mathf.Deg2Rad);
            float z = radiusWithPadding * Mathf.Sin(angle * Mathf.Deg2Rad);

            GameObject charObject = new GameObject($"Char_{i}");
            charObject.transform.SetParent(textContainer.transform);
            charObject.transform.localPosition = new Vector3(x, textHeight, z);

            Vector3 direction = new Vector3(charObject.transform.localPosition.x, 0, charObject.transform.localPosition.z).normalized;
            charObject.transform.localRotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0, 180, 0);

            TextMeshPro textMesh = charObject.AddComponent<TextMeshPro>();
            textMesh.text = textContent[i].ToString();
            textMesh.fontSize = fontSize;
            textMesh.color = textColor;
            textMesh.alignment = TextAlignmentOptions.Center;
        }
    }

    void RepositionCharacters()
    {
        Transform[] children = textContainer.GetComponentsInChildren<Transform>();
        List<Transform> characterTransforms = new List<Transform>();

        foreach (Transform child in children)
        {
            if (child != textContainer.transform && child.name.StartsWith("Char_"))
            {
                characterTransforms.Add(child);
            }
        }

        float characterWidth = fontSize * 0.01f;
        float totalArcLength = textContent.Length * (characterWidth + characterSpacing);
        float anglePerCharacter = (totalArcLength / (cylinderRadius + textPadding)) * Mathf.Rad2Deg;

        for (int i = 0; i < characterTransforms.Count && i < textContent.Length; i++)
        {
            Transform charTransform = characterTransforms[i];

            float angle = (textContent.Length - 1 - i) * anglePerCharacter;
            float radiusWithPadding = cylinderRadius + textPadding;
            float x = radiusWithPadding * Mathf.Cos(angle * Mathf.Deg2Rad);
            float z = radiusWithPadding * Mathf.Sin(angle * Mathf.Deg2Rad);

            charTransform.localPosition = new Vector3(x, textHeight, z);

            Vector3 direction = new Vector3(charTransform.localPosition.x, 0, charTransform.localPosition.z).normalized;
            charTransform.localRotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0, 180, 0);

            TextMeshPro textMesh = charTransform.GetComponent<TextMeshPro>();
            if (textMesh != null)
            {
                textMesh.fontSize = fontSize;
                textMesh.color = textColor;
            }
        }
    }

    GameObject FindExistingTextContainer()
    {
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

    bool IsInPrefabMode()
    {
        return UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject) ||
               UnityEditor.PrefabUtility.IsPartOfPrefabInstance(gameObject);
    }

    void AnimateText()
    {
        if (textContainer == null) return;

        currentRotation += rotationSpeed * Time.deltaTime;
        textContainer.transform.localRotation = Quaternion.Euler(0, currentRotation, 0);
    }

    // Public methods
    public void SetText(string newText)
    {
        textContent = newText;
        if (textContainer != null && textContainer.transform.childCount > 0)
        {
            RepositionCharacters();
        }
    }

    public void SetRotationSpeed(float speed)
    {
        rotationSpeed = speed;
    }

    public void SetCylinderRadius(float radius)
    {
        cylinderRadius = radius;
        if (textContainer != null && textContainer.transform.childCount > 0)
        {
            RepositionCharacters();
        }
    }

    public void SetTextPadding(float padding)
    {
        textPadding = padding;
        if (textContainer != null && textContainer.transform.childCount > 0)
        {
            RepositionCharacters();
        }
    }

    void OnDestroy()
    {
        if (textContainer != null)
        {
            Destroy(textContainer);
        }
    }
}
