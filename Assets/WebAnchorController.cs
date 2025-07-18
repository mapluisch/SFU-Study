using UnityEngine;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections;
using System.Linq;

public class WebAnchorController : MonoBehaviour
{
    [Header("Web Server Settings")]
    [SerializeField] private int port = 8080;
    [SerializeField] private bool autoStartServer = true;

    [Header("Anchor Management")]
    [SerializeField] private GameObject anchorPointPrefab;
    [SerializeField] private Transform anchorParent;

    private HttpListener httpListener;
    private bool isServerRunning = false;
    private CancellationTokenSource cancellationTokenSource;

    [System.Serializable]
    public class AnchorData
    {
        public string id;
        public Vector3Data position;
        public QuaternionData rotation;
        public bool isSelected;
        public ulong magicLeapId;
        public string trackingState;
        public string confidence;
    }

    [System.Serializable]
    public class Vector3Data
    {
        public float x;
        public float y;
        public float z;

        public Vector3Data(Vector3 vector)
        {
            x = vector.x;
            y = vector.y;
            z = vector.z;
        }
    }

    [System.Serializable]
    public class QuaternionData
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public QuaternionData(Quaternion quaternion)
        {
            x = quaternion.x;
            y = quaternion.y;
            z = quaternion.z;
            w = quaternion.w;
        }
    }

    [System.Serializable]
    public class AnchorUpdateRequest
    {
        public string id;
        public Vector3Data position;
        public QuaternionData rotation;
    }

    void Start()
    {
        // Set Unity to run in background
        Application.runInBackground = true;

        if (autoStartServer)
        {
            // Small delay to ensure UnityMainThreadDispatcher is initialized
            StartCoroutine(StartServerWithDelay());
        }
    }

    private IEnumerator StartServerWithDelay()
    {
        yield return new WaitForSeconds(0.1f);
        StartWebServer();
    }

    void OnDestroy()
    {
        StopWebServer();
    }

    public void StartWebServer()
    {
        if (isServerRunning) return;

        // Ensure UnityMainThreadDispatcher is available
        if (UnityMainThreadDispatcher.Instance == null)
        {
            Debug.LogError("UnityMainThreadDispatcher not found. Cannot start web server.");
            return;
        }

        httpListener = new HttpListener();
        httpListener.Prefixes.Add($"http://localhost:{port}/");
        httpListener.Start();
        isServerRunning = true;
        cancellationTokenSource = new CancellationTokenSource();

        Debug.Log($"Web server started on http://localhost:{port}");

        // Start listening for requests on background thread
        _ = Task.Run(() => HandleRequestsAsync(cancellationTokenSource.Token));
    }

    public void StopWebServer()
    {
        if (httpListener != null && isServerRunning)
        {
            cancellationTokenSource?.Cancel();
            httpListener.Stop();
            httpListener.Close();
            isServerRunning = false;
            Debug.Log("Web server stopped");
        }
    }



    private async Task HandleRequestsAsync(CancellationToken cancellationToken)
    {
        while (isServerRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Use async method to get context without blocking
                var context = await httpListener.GetContextAsync();
                _ = Task.Run(() => ProcessRequestAsync(context), cancellationToken);
            }
            catch (System.Exception e)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Debug.LogError($"Error handling request: {e.Message}");
                }
            }
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            switch (request.HttpMethod)
            {
                case "GET":
                    await HandleGetRequestAsync(request, response);
                    break;
                case "POST":
                    await HandlePostRequestAsync(request, response);
                    break;
                case "PUT":
                    await HandlePutRequestAsync(request, response);
                    break;
                case "DELETE":
                    await HandleDeleteRequestAsync(request, response);
                    break;
                default:
                    await SendResponseAsync(response, "Method not allowed", 405);
                    break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error processing request: {e.Message}");
            await SendResponseAsync(response, "Internal server error", 500);
        }
    }

    private async Task HandleGetRequestAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var path = request.Url.AbsolutePath;

        switch (path)
        {
            case "/":
            case "/index.html":
                await ServeWebInterfaceAsync(response);
                break;
            case "/api/anchors":
                await GetAnchorsAsync(response);
                break;
            case "/api/anchor":
                await GetAnchorAsync(request, response);
                break;
            default:
                await SendResponseAsync(response, "Not found", 404);
                break;
        }
    }

    private async Task HandlePostRequestAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var path = request.Url.AbsolutePath;

        switch (path)
        {
            case "/api/anchor":
                await CreateAnchorAsync(request, response);
                break;
            default:
                await SendResponseAsync(response, "Method not allowed", 405);
                break;
        }
    }

    private async Task HandlePutRequestAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var path = request.Url.AbsolutePath;

        if (path.StartsWith("/api/anchor/"))
        {
            await UpdateAnchorAsync(request, response);
        }
        else
        {
            await SendResponseAsync(response, "Not found", 404);
        }
    }

    private async Task HandleDeleteRequestAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var path = request.Url.AbsolutePath;

        if (path.StartsWith("/api/anchor/"))
        {
            await DeleteAnchorAsync(request, response);
        }
        else
        {
            await SendResponseAsync(response, "Not found", 404);
        }
    }

    private async Task ServeWebInterfaceAsync(HttpListenerResponse response)
    {
        var html = GetWebInterfaceHTML();
        var bytes = Encoding.UTF8.GetBytes(html);

        response.ContentType = "text/html";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        response.Close();
    }

    private async Task GetAnchorsAsync(HttpListenerResponse response)
    {
        var anchors = new List<AnchorData>();

        // Use Unity's main thread for accessing GameObjects
        await UnityMainThreadDispatcher.Instance.EnqueueAsync(() =>
        {
            if (FixedPath.Instance == null)
            {
                Debug.LogWarning("WebAnchorController: FixedPath.Instance is null");
                return;
            }

            var anchorComponents = FixedPath.Instance.GetAllAnchorComponents();
            Debug.Log($"WebAnchorController: Processing {anchorComponents.Count} anchors from FixedPath");

            for (int i = 0; i < anchorComponents.Count; i++)
            {
                var anchor = anchorComponents[i];
                if (anchor == null)
                {
                    Debug.LogWarning($"WebAnchorController: Anchor at index {i} is null, skipping");
                    continue;
                }

                var anchorData = new AnchorData
                {
                    id = $"anchor_{i}",
                    position = new Vector3Data(anchor.transform.position),
                    rotation = new QuaternionData(anchor.transform.rotation),
                    isSelected = false,
                    magicLeapId = anchor.GetMagicLeapAnchorId(),
                    trackingState = anchor.GetARAnchor()?.trackingState.ToString() ?? "None",
                    confidence = "Unknown"
                };
                anchors.Add(anchorData);
                Debug.Log($"WebAnchorController: Added anchor {i} at position {anchor.transform.position}");
            }
        });

        Debug.Log($"WebAnchorController: Returning {anchors.Count} anchors to web client");
        var json = JsonConvert.SerializeObject(anchors);
        await SendResponseAsync(response, json, 200, "application/json");
    }

    private async Task GetAnchorAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var id = request.QueryString["id"];
        if (string.IsNullOrEmpty(id))
        {
            await SendResponseAsync(response, "Anchor ID not provided", 400);
            return;
        }

        // Parse the anchor index from the ID (format: "anchor_X")
        if (!id.StartsWith("anchor_") || !int.TryParse(id.Substring("anchor_".Length), out int index))
        {
            await SendResponseAsync(response, "Invalid anchor ID format", 400);
            return;
        }

        AnchorData anchorData = null;
        await UnityMainThreadDispatcher.Instance.EnqueueAsync(() =>
        {
            if (FixedPath.Instance == null)
            {
                Debug.LogWarning("WebAnchorController: FixedPath.Instance is null");
                return;
            }

            var anchor = FixedPath.Instance.GetAnchorAtIndex(index);
            if (anchor == null)
            {
                Debug.LogWarning($"WebAnchorController: Anchor at index {index} not found");
                return;
            }

            var anchorComponent = anchor.GetComponent<AnchorPoint>();
            if (anchorComponent == null)
            {
                Debug.LogWarning($"WebAnchorController: AnchorPoint component not found on anchor at index {index}");
                return;
            }

            anchorData = new AnchorData
            {
                id = id,
                position = new Vector3Data(anchor.transform.position),
                rotation = new QuaternionData(anchor.transform.rotation),
                isSelected = false,
                magicLeapId = anchorComponent.GetMagicLeapAnchorId(),
                trackingState = anchorComponent.GetARAnchor()?.trackingState.ToString() ?? "None",
                confidence = "Unknown"
            };
        });

        if (anchorData == null)
        {
            await SendResponseAsync(response, "Anchor not found", 404);
            return;
        }

        var json = JsonConvert.SerializeObject(anchorData);
        await SendResponseAsync(response, json, 200, "application/json");
    }

    private async Task CreateAnchorAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var body = await ReadRequestBodyAsync(request);
        var anchorData = JsonConvert.DeserializeObject<AnchorData>(body);

        GameObject createdAnchor = null;
        await UnityMainThreadDispatcher.Instance.EnqueueAsync(() =>
        {
            if (FixedPath.Instance == null)
            {
                Debug.LogWarning("WebAnchorController: FixedPath.Instance is null, cannot create anchor");
                return;
            }

            // Convert Vector3Data and QuaternionData back to Unity types
            Vector3 position = new Vector3(anchorData.position.x, anchorData.position.y, anchorData.position.z);
            Quaternion rotation = new Quaternion(anchorData.rotation.x, anchorData.rotation.y, anchorData.rotation.z, anchorData.rotation.w);

            // Create anchor using FixedPath's prefab and parent
            var anchorPoint = Instantiate(FixedPath.Instance.anchorPointPrefab, position, rotation, FixedPath.Instance.anchorPointParent);
            anchorPoint.name = $"WebAnchor_{FixedPath.Instance.GetAnchorCount()}";

            // Add to FixedPath's lists
            FixedPath.Instance.AddAnchorPoint(anchorPoint, position);

            createdAnchor = anchorPoint;
        });

        if (createdAnchor == null)
        {
            await SendResponseAsync(response, "Failed to create anchor", 500);
            return;
        }

        var anchorComponent = createdAnchor.GetComponent<AnchorPoint>();
        var result = new AnchorData
        {
            id = $"anchor_{FixedPath.Instance.GetAnchorCount() - 1}",
            position = new Vector3Data(createdAnchor.transform.position),
            rotation = new QuaternionData(createdAnchor.transform.rotation),
            isSelected = false,
            magicLeapId = anchorComponent?.GetMagicLeapAnchorId() ?? 0,
            trackingState = anchorComponent?.GetARAnchor()?.trackingState.ToString() ?? "None",
            confidence = "Unknown"
        };

        var json = JsonConvert.SerializeObject(result);
        await SendResponseAsync(response, json, 201, "application/json");
    }

    private async Task UpdateAnchorAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var path = request.Url.AbsolutePath;
        var id = path.Substring("/api/anchor/".Length);

        // Parse the anchor index from the ID (format: "anchor_X")
        if (!id.StartsWith("anchor_") || !int.TryParse(id.Substring("anchor_".Length), out int index))
        {
            await SendResponseAsync(response, "Invalid anchor ID format", 400);
            return;
        }

        var body = await ReadRequestBodyAsync(request);
        var updateData = JsonConvert.DeserializeObject<AnchorUpdateRequest>(body);

        bool success = false;
        await UnityMainThreadDispatcher.Instance.EnqueueAsync(() =>
        {
            if (FixedPath.Instance == null)
            {
                Debug.LogWarning("WebAnchorController: FixedPath.Instance is null");
                return;
            }

            var anchor = FixedPath.Instance.GetAnchorAtIndex(index);
            if (anchor == null)
            {
                Debug.LogWarning($"WebAnchorController: Anchor at index {index} not found");
                return;
            }

            // Convert Vector3Data and QuaternionData back to Unity types
            Vector3 position = new Vector3(updateData.position.x, updateData.position.y, updateData.position.z);
            Quaternion rotation = new Quaternion(updateData.rotation.x, updateData.rotation.y, updateData.rotation.z, updateData.rotation.w);

            anchor.transform.position = position;
            anchor.transform.rotation = rotation;

            // Update the position in FixedPath's list
            if (index < FixedPath.Instance.GetAllAnchorPositions().Count)
            {
                FixedPath.Instance.UpdateAnchorPosition(index, position);
            }

            success = true;
        });

        if (!success)
        {
            await SendResponseAsync(response, "Anchor not found", 404);
            return;
        }

        await SendResponseAsync(response, "OK", 200);
    }

    private async Task DeleteAnchorAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var path = request.Url.AbsolutePath;
        var id = path.Substring("/api/anchor/".Length);

        // Parse the anchor index from the ID (format: "anchor_X")
        if (!id.StartsWith("anchor_") || !int.TryParse(id.Substring("anchor_".Length), out int index))
        {
            await SendResponseAsync(response, "Invalid anchor ID format", 400);
            return;
        }

        bool success = false;
        await UnityMainThreadDispatcher.Instance.EnqueueAsync(() =>
        {
            if (FixedPath.Instance == null)
            {
                Debug.LogWarning("WebAnchorController: FixedPath.Instance is null");
                return;
            }

            var anchor = FixedPath.Instance.GetAnchorAtIndex(index);
            if (anchor == null)
            {
                Debug.LogWarning($"WebAnchorController: Anchor at index {index} not found");
                return;
            }

            // Remove from FixedPath's lists
            FixedPath.Instance.RemoveAnchorAtIndex(index);

            // Destroy the GameObject
            Destroy(anchor);

            success = true;
        });

        if (!success)
        {
            await SendResponseAsync(response, "Anchor not found", 404);
            return;
        }

        await SendResponseAsync(response, "OK", 200);
    }



    private async Task<string> ReadRequestBodyAsync(HttpListenerRequest request)
    {
        using (var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding))
        {
            return await reader.ReadToEndAsync();
        }
    }

    private async Task SendResponseAsync(HttpListenerResponse response, string content, int statusCode, string contentType = "text/plain")
    {
        var bytes = Encoding.UTF8.GetBytes(content);

        response.StatusCode = statusCode;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        response.Close();
    }

    private string GetWebInterfaceHTML()
    {
        return @"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Anchor Point Controller</title>
    <style>
        body {
            font-family: Arial, sans-serif;
            margin: 0;
            padding: 20px;
            background-color: #f0f0f0;
        }
        .container {
            max-width: 1200px;
            margin: 0 auto;
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }
        h1 {
            color: #333;
            text-align: center;
            margin-bottom: 30px;
        }
        .controls {
            display: flex;
            gap: 20px;
            margin-bottom: 30px;
            flex-wrap: wrap;
        }
        .control-group {
            flex: 1;
            min-width: 200px;
        }
        .control-group label {
            display: block;
            margin-bottom: 5px;
            font-weight: bold;
            color: #555;
        }
        .control-group input {
            width: 100%;
            padding: 8px;
            border: 1px solid #ddd;
            border-radius: 4px;
            box-sizing: border-box;
        }
        button {
            background-color: #007bff;
            color: white;
            border: none;
            padding: 10px 20px;
            border-radius: 4px;
            cursor: pointer;
            font-size: 14px;
        }
        button:hover {
            background-color: #0056b3;
        }
        button.danger {
            background-color: #dc3545;
        }
        button.danger:hover {
            background-color: #c82333;
        }
        .anchors-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
            gap: 20px;
            margin-top: 20px;
        }
        .anchor-card {
            border: 1px solid #ddd;
            border-radius: 8px;
            padding: 15px;
            background: #f9f9f9;
        }
        .anchor-card h3 {
            margin: 0 0 10px 0;
            color: #333;
        }
        .anchor-info {
            margin-bottom: 10px;
        }
        .anchor-info span {
            font-weight: bold;
            color: #555;
        }
        .position-controls {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 10px;
            margin-top: 10px;
        }
        .position-controls input {
            padding: 5px;
            border: 1px solid #ddd;
            border-radius: 4px;
        }
        .status-indicator {
            display: inline-block;
            width: 10px;
            height: 10px;
            border-radius: 50%;
            margin-right: 5px;
        }
        .status-tracking { background-color: #28a745; }
        .status-limited { background-color: #ffc107; }
        .status-none { background-color: #dc3545; }
    </style>
</head>
<body>
    <div class=""container"">
        <h1>Magic Leap Anchor Point Controller</h1>
        
        <div class=""controls"">
            <div class=""control-group"">
                <label for=""xPos"">X Position:</label>
                <input type=""number"" id=""xPos"" step=""0.01"" value=""0"">
            </div>
            <div class=""control-group"">
                <label for=""yPos"">Y Position:</label>
                <input type=""number"" id=""yPos"" step=""0.01"" value=""0"">
            </div>
            <div class=""control-group"">
                <label for=""zPos"">Z Position:</label>
                <input type=""number"" id=""zPos"" step=""0.01"" value=""0"">
            </div>
            <div class=""control-group"">
                <label for=""xRot"">X Rotation:</label>
                <input type=""number"" id=""xRot"" step=""1"" value=""0"">
            </div>
            <div class=""control-group"">
                <label for=""yRot"">Y Rotation:</label>
                <input type=""number"" id=""yRot"" step=""1"" value=""0"">
            </div>
            <div class=""control-group"">
                <label for=""zRot"">Z Rotation:</label>
                <input type=""number"" id=""zRot"" step=""1"" value=""0"">
            </div>
        </div>
        
        <div style=""text-align: center; margin-bottom: 20px;"">
            <button onclick=""createAnchor()"">Create New Anchor</button>
            <button onclick=""refreshAnchors()"">Refresh Anchors</button>
        </div>
        
        <div id=""anchorsContainer"">
            <div style=""text-align: center; color: #666;"">Loading anchors...</div>
        </div>
    </div>

    <script>
        let anchors = [];
        
        async function refreshAnchors() {
            try {
                const response = await fetch('/api/anchors');
                anchors = await response.json();
                displayAnchors();
            } catch (error) {
                console.error('Error fetching anchors:', error);
                document.getElementById('anchorsContainer').innerHTML = 
                    '<div style=""text-align: center; color: #dc3545;"">Error loading anchors</div>';
            }
        }
        
        function displayAnchors() {
            const container = document.getElementById('anchorsContainer');
            
            if (anchors.length === 0) {
                container.innerHTML = '<div style=""text-align: center; color: #666;"">No anchors found</div>';
                return;
            }
            
            const anchorsHTML = anchors.map(anchor => `
                <div class=""anchor-card"">
                    <h3>Anchor ${anchor.id}</h3>
                    <div class=""anchor-info"">
                        <span>Magic Leap ID:</span> ${anchor.magicLeapId || 'N/A'}<br>
                        <span>Status:</span> 
                        <span class=""status-indicator status-${anchor.trackingState.toLowerCase()}""></span>
                        ${anchor.trackingState}<br>
                        <span>Confidence:</span> ${anchor.confidence}<br>
                    </div>
                    <div class=""position-controls"">
                        <input type=""number"" step=""0.01"" value=""${anchor.position.x.toFixed(3)}"" 
                               onchange=""updateAnchorPosition('${anchor.id}', 'x', this.value)"">
                        <input type=""number"" step=""0.01"" value=""${anchor.position.y.toFixed(3)}"" 
                               onchange=""updateAnchorPosition('${anchor.id}', 'y', this.value)"">
                        <input type=""number"" step=""0.01"" value=""${anchor.position.z.toFixed(3)}"" 
                               onchange=""updateAnchorPosition('${anchor.id}', 'z', this.value)"">
                        <input type=""number"" step=""1"" value=""${(Math.atan2(2 * (anchor.rotation.w * anchor.rotation.x + anchor.rotation.y * anchor.rotation.z), 1 - 2 * (anchor.rotation.x * anchor.rotation.x + anchor.rotation.y * anchor.rotation.y)) * 180 / Math.PI).toFixed(1)}"" 
                               onchange=""updateAnchorRotation('${anchor.id}', 'x', this.value)"">
                        <input type=""number"" step=""1"" value=""${(Math.asin(2 * (anchor.rotation.w * anchor.rotation.y - anchor.rotation.z * anchor.rotation.x)) * 180 / Math.PI).toFixed(1)}"" 
                               onchange=""updateAnchorRotation('${anchor.id}', 'y', this.value)"">
                        <input type=""number"" step=""1"" value=""${(Math.atan2(2 * (anchor.rotation.w * anchor.rotation.z + anchor.rotation.x * anchor.rotation.y), 1 - 2 * (anchor.rotation.y * anchor.rotation.y + anchor.rotation.z * anchor.rotation.z)) * 180 / Math.PI).toFixed(1)}"" 
                               onchange=""updateAnchorRotation('${anchor.id}', 'z', this.value)"">
                    </div>
                    <div style=""margin-top: 10px;"">
                        <button class=""danger"" onclick=""deleteAnchor('${anchor.id}')"">Delete Anchor</button>
                    </div>
                </div>
            `).join('');
            
            container.innerHTML = `<div class=""anchors-grid"">${anchorsHTML}</div>`;
        }
        
        async function createAnchor() {
            const x = parseFloat(document.getElementById('xPos').value);
            const y = parseFloat(document.getElementById('yPos').value);
            const z = parseFloat(document.getElementById('zPos').value);
            const xRot = parseFloat(document.getElementById('xRot').value);
            const yRot = parseFloat(document.getElementById('yRot').value);
            const zRot = parseFloat(document.getElementById('zRot').value);
            
            const anchorData = {
                position: { x, y, z },
                rotation: { x: xRot, y: yRot, z: zRot, w: 1 }
            };
            
            try {
                await fetch('/api/anchor', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(anchorData)
                });
                
                refreshAnchors();
            } catch (error) {
                console.error('Error creating anchor:', error);
            }
        }
        
        async function updateAnchorPosition(id, axis, value) {
            const anchor = anchors.find(a => a.id === id);
            if (!anchor) return;
            
            anchor.position[axis] = parseFloat(value);
            
            try {
                await fetch(`/api/anchor/${id}`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        id: id,
                        position: anchor.position,
                        rotation: anchor.rotation
                    })
                });
            } catch (error) {
                console.error('Error updating anchor position:', error);
            }
        }
        
        async function updateAnchorRotation(id, axis, value) {
            const anchor = anchors.find(a => a.id === id);
            if (!anchor) return;
            
            // Convert euler angles to quaternion
            const eulerX = axis === 'x' ? parseFloat(value) * Math.PI / 180 : 0;
            const eulerY = axis === 'y' ? parseFloat(value) * Math.PI / 180 : 0;
            const eulerZ = axis === 'z' ? parseFloat(value) * Math.PI / 180 : 0;
            
            // Simple euler to quaternion conversion
            const cy = Math.cos(eulerZ * 0.5);
            const sy = Math.sin(eulerZ * 0.5);
            const cp = Math.cos(eulerY * 0.5);
            const sp = Math.sin(eulerY * 0.5);
            const cr = Math.cos(eulerX * 0.5);
            const sr = Math.sin(eulerX * 0.5);
            
            anchor.rotation = {
                w: cr * cp * cy + sr * sp * sy,
                x: sr * cp * cy - cr * sp * sy,
                y: cr * sp * cy + sr * cp * sy,
                z: cr * cp * sy - sr * sp * cy
            };
            
            try {
                await fetch(`/api/anchor/${id}`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        id: id,
                        position: anchor.position,
                        rotation: anchor.rotation
                    })
                });
            } catch (error) {
                console.error('Error updating anchor rotation:', error);
            }
        }
        
        async function deleteAnchor(id) {
            if (!confirm('Are you sure you want to delete this anchor?')) return;
            
            try {
                await fetch(`/api/anchor/${id}`, { method: 'DELETE' });
                refreshAnchors();
            } catch (error) {
                console.error('Error deleting anchor:', error);
            }
        }
        
        // Auto-refresh every 10 seconds
        setInterval(refreshAnchors, 10000);
        
        // Initial load
        refreshAnchors();
    </script>
</body>
</html>";
    }
}