using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;
using System.IO;

public class FixedPath : MonoBehaviour
{
    [Header("Path Settings")]
    [SerializeField] private GameObject anchorPointPrefab;
    [SerializeField] private Material pathLineMaterial;
    [SerializeField] private float lineWidth = 0.01f;

    [Header("Controller Settings")]
    [SerializeField] private ActionBasedController leftController;
    [SerializeField] private ActionBasedController rightController;
    [SerializeField] private bool useLeftController = true;

    private List<GameObject> anchorPoints = new List<GameObject>();
    private List<Vector3> anchorPositions = new List<Vector3>();
    private LineRenderer pathLine;
    private bool triggerPressed = false;

    // File path for saving anchor data
    private string saveFilePath;

    void Start()
    {
        InitializeControllers();
        InitializePathLine();
        LoadSavedPath();
    }

    void InitializeControllers()
    {
        // Find controllers in the scene
        if (leftController == null)
        {
            leftController = FindObjectOfType<ActionBasedController>();
        }

        if (rightController == null)
        {
            ActionBasedController[] controllers = FindObjectsOfType<ActionBasedController>();
            foreach (ActionBasedController controller in controllers)
            {
                if (controller != leftController)
                {
                    rightController = controller;
                    break;
                }
            }
        }

        Debug.Log("OpenXR Controllers initialized");
    }

    void InitializePathLine()
    {
        // Create LineRenderer for path visualization
        pathLine = gameObject.AddComponent<LineRenderer>();
        pathLine.material = pathLineMaterial;
        pathLine.startWidth = lineWidth;
        pathLine.endWidth = lineWidth;
        pathLine.positionCount = 0;
        pathLine.useWorldSpace = true;

        // Set up save file path
        saveFilePath = Path.Combine(Application.persistentDataPath, "spatial_path.json");
    }

    void Update()
    {
        // Check for trigger input
        ActionBasedController activeController = useLeftController ? leftController : rightController;

        if (activeController != null)
        {
            // Check if trigger is pressed
            if (activeController.activateAction.action.ReadValue<float>() > 0.5f && !triggerPressed)
            {
                triggerPressed = true;
                CreateAnchorPoint(activeController);
            }
            else if (activeController.activateAction.action.ReadValue<float>() <= 0.5f)
            {
                triggerPressed = false;
            }

            // Check for bumper button to clear path
            if (activeController.selectAction.action.WasPressedThisFrame())
            {
                ClearPath();
            }
        }

        // Update path line
        UpdatePathLine();
    }

    void CreateAnchorPoint(ActionBasedController controller)
    {
        if (controller == null) return;

        // Get controller position and orientation
        Vector3 controllerPosition = controller.transform.position;
        Quaternion controllerRotation = controller.transform.rotation;

        // Create anchor point at controller position
        GameObject anchorPoint = Instantiate(anchorPointPrefab, controllerPosition, controllerRotation);
        anchorPoint.name = $"AnchorPoint_{anchorPoints.Count}";

        // Add to lists
        anchorPoints.Add(anchorPoint);
        anchorPositions.Add(controllerPosition);

        // Save the path after adding a new point
        SavePath();

        Debug.Log($"Created anchor point {anchorPoints.Count} at position: {controllerPosition}");
    }

    void UpdatePathLine()
    {
        if (pathLine == null || anchorPositions.Count < 2) return;

        pathLine.positionCount = anchorPositions.Count;
        pathLine.SetPositions(anchorPositions.ToArray());
    }

    void SavePath()
    {
        try
        {
            PathData pathData = new PathData
            {
                anchorPositions = anchorPositions.ToArray(),
                anchorRotations = new Quaternion[anchorPoints.Count]
            };

            // Save rotations for each anchor point
            for (int i = 0; i < anchorPoints.Count; i++)
            {
                if (anchorPoints[i] != null)
                {
                    pathData.anchorRotations[i] = anchorPoints[i].transform.rotation;
                }
            }

            string json = JsonUtility.ToJson(pathData, true);
            File.WriteAllText(saveFilePath, json);

            Debug.Log($"Path saved with {anchorPositions.Count} anchor points");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error saving path: {e.Message}");
        }
    }

    void LoadSavedPath()
    {
        if (!File.Exists(saveFilePath)) return;

        try
        {
            string json = File.ReadAllText(saveFilePath);
            PathData pathData = JsonUtility.FromJson<PathData>(json);

            // Clear existing points
            ClearPath();

            // Load anchor points
            for (int i = 0; i < pathData.anchorPositions.Length; i++)
            {
                Vector3 position = pathData.anchorPositions[i];
                Quaternion rotation = pathData.anchorRotations[i];

                GameObject anchorPoint = Instantiate(anchorPointPrefab, position, rotation);
                anchorPoint.name = $"AnchorPoint_{i}";

                anchorPoints.Add(anchorPoint);
                anchorPositions.Add(position);
            }

            Debug.Log($"Loaded path with {anchorPositions.Count} anchor points");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading path: {e.Message}");
        }
    }

    void ClearPath()
    {
        // Destroy all anchor point GameObjects
        foreach (GameObject anchor in anchorPoints)
        {
            if (anchor != null)
            {
                DestroyImmediate(anchor);
            }
        }

        anchorPoints.Clear();
        anchorPositions.Clear();

        // Clear the line renderer
        if (pathLine != null)
        {
            pathLine.positionCount = 0;
        }

        // Delete save file
        if (File.Exists(saveFilePath))
        {
            File.Delete(saveFilePath);
        }

        Debug.Log("Path cleared");
    }

    // Data structure for serialization
    [System.Serializable]
    public class PathData
    {
        public Vector3[] anchorPositions;
        public Quaternion[] anchorRotations;
    }
}
