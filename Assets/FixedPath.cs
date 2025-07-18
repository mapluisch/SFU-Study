using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;
using System.IO;
using System.Linq;

public class FixedPath : MonoBehaviour
{
    // Singleton instance
    public static FixedPath Instance { get; private set; }

    [Header("Path Settings")]
    [SerializeField] public GameObject anchorPointPrefab;
    [SerializeField] public Transform anchorPointParent;
    [SerializeField] private Material pathLineMaterial;
    [SerializeField] private float lineWidth = 0.01f;

    [Header("Controller Settings")]
    [SerializeField] private ActionBasedController leftController;
    [SerializeField] private ActionBasedController rightController;
    [SerializeField] private bool useLeftController = true;

    [Header("Editor Testing")]
    [SerializeField] private bool enableKeyboardInput = true;
    [SerializeField] private float keyboardMovementSpeed = 5f;
    [SerializeField] private Transform editorCamera;

    [Header("Random Path Generator")]
    [SerializeField] private bool enableRandomPathGenerator = true;
    [SerializeField] private int randomPathLength = 10;
    [SerializeField] private float minDistanceBetweenAnchors = 1.0f;
    [SerializeField] private float maxDistanceBetweenAnchors = 5.0f;
    [SerializeField] private int maxAttemptsPerAnchor = 50;
    [SerializeField] private float minAngleDegrees = 85.0f;
    [SerializeField] private float maxAngleDegrees = 95.0f;

    private List<GameObject> anchorPoints = new List<GameObject>();
    private List<Vector3> anchorPositions = new List<Vector3>();
    private LineRenderer pathLine;
    private bool triggerPressed = false;
    private bool keyboardTriggerPressed = false;

    // Random path generator data
    private List<int> currentPath = new List<int>();
    private List<Vector3> pathPositions = new List<Vector3>();
    private LineRenderer randomPathLine;

    // File path for saving anchor data
    private string saveFilePath;

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        InitializeControllers();
        InitializePathLine();
        LoadSavedPath();

        // Set up editor camera if not assigned
        if (editorCamera == null && Application.isEditor)
        {
            editorCamera = Camera.main?.transform;
        }
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

        // Create LineRenderer for random path visualization
        GameObject randomPathObject = new GameObject("RandomPathLine");
        randomPathObject.transform.SetParent(transform);
        randomPathLine = randomPathObject.AddComponent<LineRenderer>();
        randomPathLine.material = pathLineMaterial;
        randomPathLine.startWidth = lineWidth * 2f; // Make it slightly thicker
        randomPathLine.endWidth = lineWidth * 2f;
        randomPathLine.positionCount = 0;
        randomPathLine.useWorldSpace = true;
        randomPathLine.startColor = Color.green;
        randomPathLine.endColor = Color.red;

        // Enable color gradient
        randomPathLine.colorGradient = new Gradient();
        GradientColorKey[] colorKeys = new GradientColorKey[2];
        colorKeys[0] = new GradientColorKey(Color.green, 0.0f);
        colorKeys[1] = new GradientColorKey(Color.red, 1.0f);

        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
        alphaKeys[0] = new GradientAlphaKey(1.0f, 0.0f);
        alphaKeys[1] = new GradientAlphaKey(1.0f, 1.0f);

        randomPathLine.colorGradient.SetKeys(colorKeys, alphaKeys);

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

        // Handle keyboard input for editor testing
        if (Application.isEditor && enableKeyboardInput)
        {
            HandleKeyboardInput();
        }

        // Update path line
        UpdatePathLine();
    }

    void HandleKeyboardInput()
    {
        if (editorCamera == null) return;

        // Move camera with WASD keys
        Vector3 movement = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) movement += editorCamera.forward;
        if (Input.GetKey(KeyCode.S)) movement -= editorCamera.forward;
        if (Input.GetKey(KeyCode.A)) movement -= editorCamera.right;
        if (Input.GetKey(KeyCode.D)) movement += editorCamera.right;
        if (Input.GetKey(KeyCode.Q)) movement += Vector3.up;
        if (Input.GetKey(KeyCode.E)) movement += Vector3.down;

        // Normalize and apply movement
        if (movement.magnitude > 0)
        {
            movement.Normalize();
            editorCamera.position += movement * keyboardMovementSpeed * Time.deltaTime;
        }

        // Rotate camera with mouse
        if (Input.GetMouseButton(1)) // Right mouse button
        {
            float mouseX = Input.GetAxis("Mouse X") * 2f;
            float mouseY = Input.GetAxis("Mouse Y") * 2f;

            editorCamera.Rotate(Vector3.up, mouseX, Space.World);
            editorCamera.Rotate(Vector3.right, -mouseY, Space.Self);
        }

        // Create anchor point with Space key (simulates trigger)
        if (Input.GetKeyDown(KeyCode.Space) && !keyboardTriggerPressed)
        {
            keyboardTriggerPressed = true;
            CreateAnchorPointFromCamera();
        }
        else if (Input.GetKeyUp(KeyCode.Space))
        {
            keyboardTriggerPressed = false;
        }

        // Clear path with C key (simulates bumper)
        if (Input.GetKeyDown(KeyCode.C))
        {
            ClearPath();
        }

        // Save path manually with S key
        if (Input.GetKeyDown(KeyCode.S) && Input.GetKey(KeyCode.LeftControl))
        {
            SavePath();
            Debug.Log("Path saved manually");
        }

        // Load path manually with L key
        if (Input.GetKeyDown(KeyCode.L) && Input.GetKey(KeyCode.LeftControl))
        {
            LoadSavedPath();
            Debug.Log("Path loaded manually");
        }

        // Random path generator controls
        if (enableRandomPathGenerator)
        {
            // Generate random path with R key
            if (Input.GetKeyDown(KeyCode.R))
            {
                GenerateRandomPath();
            }

            // Clear random path with T key
            if (Input.GetKeyDown(KeyCode.T))
            {
                ClearRandomPath();
            }
        }
    }

    void CreateAnchorPoint(ActionBasedController controller)
    {
        if (controller == null) return;

        // Get controller position and orientation
        Vector3 controllerPosition = controller.transform.position;
        Quaternion controllerRotation = controller.transform.rotation;

        // Create anchor point at controller position
        GameObject anchorPoint = Instantiate(anchorPointPrefab, controllerPosition, controllerRotation, anchorPointParent);
        anchorPoint.name = $"AnchorPoint_{anchorPoints.Count}";

        // Add to lists
        anchorPoints.Add(anchorPoint);
        anchorPositions.Add(controllerPosition);

        // Save the path after adding a new point
        SavePath();

        Debug.Log($"Created anchor point {anchorPoints.Count} at position: {controllerPosition}");
    }

    void CreateAnchorPointFromCamera()
    {
        if (editorCamera == null) return;

        // Create anchor point at camera position
        Vector3 cameraPosition = editorCamera.position;
        Quaternion cameraRotation = editorCamera.rotation;

        GameObject anchorPoint = Instantiate(anchorPointPrefab, cameraPosition, cameraRotation);
        anchorPoint.name = $"AnchorPoint_{anchorPoints.Count}";

        // Add to lists
        anchorPoints.Add(anchorPoint);
        anchorPositions.Add(cameraPosition);

        // Save the path after adding a new point
        SavePath();

        Debug.Log($"Created anchor point {anchorPoints.Count} at position: {cameraPosition}");
    }

    void UpdatePathLine()
    {
        if (pathLine == null || anchorPositions.Count < 2) return;

        pathLine.positionCount = anchorPositions.Count;
        pathLine.SetPositions(anchorPositions.ToArray());

        // Also update random path visualization if it exists
        if (HasRandomPath())
        {
            UpdateRandomPathVisualization();
        }
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

    // Public methods to access anchors
    public List<GameObject> GetAllAnchorPoints()
    {
        return new List<GameObject>(anchorPoints);
    }

    public List<Vector3> GetAllAnchorPositions()
    {
        return new List<Vector3>(anchorPositions);
    }

    public int GetAnchorCount()
    {
        return anchorPoints.Count;
    }

    public GameObject GetAnchorAtIndex(int index)
    {
        if (index >= 0 && index < anchorPoints.Count)
        {
            return anchorPoints[index];
        }
        return null;
    }

    public Vector3 GetAnchorPositionAtIndex(int index)
    {
        if (index >= 0 && index < anchorPositions.Count)
        {
            return anchorPositions[index];
        }
        return Vector3.zero;
    }

    public List<AnchorPoint> GetAllAnchorComponents()
    {
        List<AnchorPoint> anchorComponents = new List<AnchorPoint>();
        foreach (GameObject anchor in anchorPoints)
        {
            if (anchor != null)
            {
                AnchorPoint component = anchor.GetComponent<AnchorPoint>();
                if (component != null)
                {
                    anchorComponents.Add(component);
                }
            }
        }
        return anchorComponents;
    }

    // Public method to add anchor points from external sources (like WebAnchorController)
    public void AddAnchorPoint(GameObject anchorPoint, Vector3 position)
    {
        anchorPoints.Add(anchorPoint);
        anchorPositions.Add(position);

        // Save the path after adding a new point
        SavePath();

        Debug.Log($"Added anchor point {anchorPoints.Count} at position: {position}");
    }

    // Public method to update anchor position
    public void UpdateAnchorPosition(int index, Vector3 newPosition)
    {
        if (index >= 0 && index < anchorPositions.Count)
        {
            anchorPositions[index] = newPosition;
            SavePath();
            Debug.Log($"Updated anchor position at index {index} to {newPosition}");
        }
    }

    // Public method to remove anchor at specific index
    public void RemoveAnchorAtIndex(int index)
    {
        if (index >= 0 && index < anchorPoints.Count)
        {
            anchorPoints.RemoveAt(index);
            anchorPositions.RemoveAt(index);
            SavePath();
            Debug.Log($"Removed anchor at index {index}");
        }
    }

    // Random Path Generator Methods
    public void GenerateRandomPath()
    {
        if (anchorPoints.Count < 2)
        {
            Debug.LogWarning("Need at least 2 anchor points to generate a random path");
            return;
        }

        ClearRandomPath();

        // Generate a random path through the anchors
        List<int> path = GenerateRandomPathSequence();

        if (path != null && path.Count > 0)
        {
            currentPath = path;
            UpdateRandomPathVisualization();
            Debug.Log($"Generated random path with {path.Count} steps: {string.Join(" -> ", path)}");
        }
        else
        {
            Debug.LogWarning("Failed to generate a valid random path");
        }
    }

    private List<int> GenerateRandomPathSequence()
    {
        List<int> path = new List<int>();
        List<int> availableAnchors = new List<int>();

        // Initialize available anchors (all anchor indices)
        for (int i = 0; i < anchorPoints.Count; i++)
        {
            availableAnchors.Add(i);
        }

        // Start with a random anchor
        int currentAnchor = Random.Range(0, anchorPoints.Count);
        path.Add(currentAnchor);
        availableAnchors.Remove(currentAnchor);

        // Generate path sequence - continue until we have exactly randomPathLength segments
        int attempts = 0;
        int maxTotalAttempts = randomPathLength * maxAttemptsPerAnchor;

        while (path.Count < randomPathLength + 1 && attempts < maxTotalAttempts) // +1 because we need n+1 anchors for n segments
        {
            attempts++;
            int currentStep = path.Count; // Current step is the number of anchors we have

            // Find valid next anchors (with distance and angle constraints)
            List<int> validNextAnchors = GetValidNextAnchorsWithAngleConstraint(currentAnchor, availableAnchors, path);

            if (validNextAnchors.Count == 0)
            {
                // If no valid next anchors with angle constraints, try to find any anchor within range
                validNextAnchors = GetValidNextAnchorsWithAngleConstraint(currentAnchor, Enumerable.Range(0, anchorPoints.Count).ToList(), path);

                if (validNextAnchors.Count == 0)
                {
                    // If still no valid anchors, try to relax constraints or restart
                    Debug.LogWarning($"No valid next anchor found at step {currentStep} (attempt {attempts})");

                    // Try to find any anchor that meets basic distance constraints
                    List<int> basicValidAnchors = GetValidNextAnchors(currentAnchor, Enumerable.Range(0, anchorPoints.Count).ToList());

                    if (basicValidAnchors.Count > 0)
                    {
                        // Use basic distance-only validation
                        validNextAnchors = basicValidAnchors;
                    }
                    else
                    {
                        // If even basic constraints fail, try any anchor except current
                        validNextAnchors = Enumerable.Range(0, anchorPoints.Count).Where(i => i != currentAnchor).ToList();
                    }
                }
            }

            // Choose a random valid next anchor
            int nextAnchor = validNextAnchors[Random.Range(0, validNextAnchors.Count)];
            path.Add(nextAnchor);

            // Update current anchor and available anchors
            currentAnchor = nextAnchor;
            if (availableAnchors.Contains(nextAnchor))
            {
                availableAnchors.Remove(nextAnchor);
            }
        }

        if (path.Count < randomPathLength + 1)
        {
            Debug.LogWarning($"Failed to generate complete path. Generated {path.Count - 1} segments out of {randomPathLength} requested.");
        }

        return path;
    }

    private List<int> GetValidNextAnchors(int currentAnchor, List<int> candidateAnchors)
    {
        List<int> validAnchors = new List<int>();
        Vector3 currentPosition = anchorPositions[currentAnchor];

        foreach (int candidateIndex in candidateAnchors)
        {
            if (candidateIndex == currentAnchor) continue;

            Vector3 candidatePosition = anchorPositions[candidateIndex];
            float distance = Vector3.Distance(currentPosition, candidatePosition);

            if (distance >= minDistanceBetweenAnchors && distance <= maxDistanceBetweenAnchors)
            {
                validAnchors.Add(candidateIndex);
            }
        }

        return validAnchors;
    }

    private List<int> GetValidNextAnchorsWithAngleConstraint(int currentAnchor, List<int> candidateAnchors, List<int> pathHistory)
    {
        List<int> validAnchors = new List<int>();
        Vector3 currentPosition = anchorPositions[currentAnchor];

        foreach (int candidateIndex in candidateAnchors)
        {
            if (candidateIndex == currentAnchor) continue;

            Vector3 candidatePosition = anchorPositions[candidateIndex];
            float distance = Vector3.Distance(currentPosition, candidatePosition);

            // Check distance constraint
            if (distance >= minDistanceBetweenAnchors && distance <= maxDistanceBetweenAnchors)
            {
                // Check if this would create an invalid path (revisiting same anchor without others in between)
                if (WouldCreateInvalidPath(pathHistory, currentAnchor, candidateIndex))
                {
                    continue;
                }

                // Check angle constraint (only if we have at least 2 anchors in the path)
                if (pathHistory.Count >= 2)
                {
                    if (IsValidAngle(pathHistory, currentAnchor, candidateIndex))
                    {
                        validAnchors.Add(candidateIndex);
                    }
                }
                else
                {
                    // No angle constraint for the first two anchors
                    validAnchors.Add(candidateIndex);
                }
            }
        }

        return validAnchors;
    }

    private bool WouldCreateInvalidPath(List<int> pathHistory, int currentAnchor, int candidateAnchor)
    {
        // Check if we're trying to go back to the same anchor without visiting others in between
        // Look at the last few anchors in the path to see if we're creating a loop
        if (pathHistory.Count >= 1)
        {
            // Check if the last anchor in the path would be: [someAnchor] -> currentAnchor -> candidateAnchor
            // And if candidateAnchor is the same as someAnchor, that's invalid
            int lastAnchorInPath = pathHistory[pathHistory.Count - 1];
            if (candidateAnchor == lastAnchorInPath)
            {
                return true; // Invalid: going back to the same anchor immediately
            }
        }

        return false;
    }

    private bool IsValidAngle(List<int> pathHistory, int currentAnchor, int candidateAnchor)
    {
        // We need at least 2 anchors in the path to calculate an angle
        if (pathHistory.Count < 2) return true;

        // Get the three points: previous, current, and candidate
        // previousAnchor should be the second-to-last anchor in the path
        int previousAnchor = pathHistory[pathHistory.Count - 2];
        Vector3 previousPos = anchorPositions[previousAnchor];
        Vector3 currentPos = anchorPositions[currentAnchor];
        Vector3 candidatePos = anchorPositions[candidateAnchor];

        // Use X-Z positions for angle calculation (ignore Y)
        Vector3 previousPosXZ = new Vector3(previousPos.x, 0, previousPos.z);
        Vector3 currentPosXZ = new Vector3(currentPos.x, 0, currentPos.z);
        Vector3 candidatePosXZ = new Vector3(candidatePos.x, 0, candidatePos.z);

        // Calculate vectors in X-Z plane
        Vector3 vector1 = (currentPosXZ - previousPosXZ).normalized;
        Vector3 vector2 = (candidatePosXZ - currentPosXZ).normalized;

        // Calculate angle in degrees
        float angle = Vector3.Angle(vector1, vector2);

        // Check if angle is within the valid range
        return angle >= minAngleDegrees && angle <= maxAngleDegrees;
    }

    private void UpdateRandomPathVisualization()
    {
        if (randomPathLine == null || currentPath.Count < 2) return;

        // Create positions list for the path
        pathPositions.Clear();
        foreach (int anchorIndex in currentPath)
        {
            if (anchorIndex >= 0 && anchorIndex < anchorPositions.Count)
            {
                pathPositions.Add(anchorPositions[anchorIndex]);
            }
        }

        // Update the line renderer
        randomPathLine.positionCount = pathPositions.Count;
        randomPathLine.SetPositions(pathPositions.ToArray());

        // Apply color gradient from start to end
        ApplyColorGradientToPath();
    }

    private void ApplyColorGradientToPath()
    {
        if (randomPathLine == null || pathPositions.Count < 2) return;

        // Create color array for the gradient
        Color[] colors = new Color[pathPositions.Count];

        for (int i = 0; i < pathPositions.Count; i++)
        {
            // Calculate gradient from start (green) to end (red)
            float t = (float)i / (pathPositions.Count - 1);
            colors[i] = Color.Lerp(Color.green, Color.red, t);
        }

        // Apply colors to the line renderer
        randomPathLine.startColor = colors[0];
        randomPathLine.endColor = colors[colors.Length - 1];

        // Set individual colors for each point if the line renderer supports it
        if (randomPathLine.colorGradient != null)
        {
            Gradient gradient = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[2];
            colorKeys[0] = new GradientColorKey(Color.green, 0.0f);
            colorKeys[1] = new GradientColorKey(Color.red, 1.0f);

            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(1.0f, 0.0f);
            alphaKeys[1] = new GradientAlphaKey(1.0f, 1.0f);

            gradient.SetKeys(colorKeys, alphaKeys);
            randomPathLine.colorGradient = gradient;
        }
    }

    public void ClearRandomPath()
    {
        currentPath.Clear();
        pathPositions.Clear();

        if (randomPathLine != null)
        {
            randomPathLine.positionCount = 0;
        }

        Debug.Log("Random path cleared");
    }

    // Public methods to access random path data
    public List<int> GetCurrentRandomPath()
    {
        return new List<int>(currentPath);
    }

    public List<Vector3> GetRandomPathPositions()
    {
        return new List<Vector3>(pathPositions);
    }

    public bool HasRandomPath()
    {
        return currentPath.Count > 0;
    }

    // Method to get random path data with color information for web UI
    public string GetRandomPathDataForWeb()
    {
        if (currentPath.Count < 2) return "[]";

        var pathData = new List<object>();

        for (int i = 0; i < currentPath.Count; i++)
        {
            int anchorIndex = currentPath[i];
            if (anchorIndex >= 0 && anchorIndex < anchorPositions.Count)
            {
                Vector3 position = anchorPositions[anchorIndex];

                // Calculate color gradient from start (green) to end (red)
                float t = (float)i / (currentPath.Count - 1);
                Color color = Color.Lerp(Color.green, Color.red, t);

                var anchorData = new
                {
                    index = anchorIndex,
                    position = new { x = position.x, y = position.y, z = position.z },
                    color = new { r = color.r, g = color.g, b = color.b, a = color.a },
                    segmentIndex = i,
                    isStart = i == 0,
                    isEnd = i == currentPath.Count - 1
                };

                pathData.Add(anchorData);
            }
        }

        return JsonUtility.ToJson(pathData);
    }



    // Data structure for serialization
    [System.Serializable]
    public class PathData
    {
        public Vector3[] anchorPositions;
        public Quaternion[] anchorRotations;
    }
}
