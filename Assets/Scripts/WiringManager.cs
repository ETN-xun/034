using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class WiringManager : MonoBehaviour
{
    public static WiringManager Instance { get; private set; }

    [SerializeField]
    private Camera targetCamera;

    [SerializeField]
    private float wireWidth = 0.05f;

    [SerializeField]
    private Color wireColor = Color.yellow;

    [SerializeField]
    private Color previewColor = new Color(1f, 0.92f, 0.1f, 0.8f);

    [SerializeField]
    private Color selectedWireOutlineColor = Color.cyan;

    [SerializeField]
    private float wireSelectDistance = 0.2f;

    [SerializeField]
    private float gridSpacing = 1f;

    [SerializeField]
    private Color signalColor = Color.white;

    [SerializeField]
    private float signalWidth = 0.03f;

    [SerializeField]
    private float signalFrequency = 0.8f;

    [SerializeField]
    private Texture2D defaultCursorWhite;

    [SerializeField]
    private Texture2D terminalHoverCursorYellow;

    [SerializeField]
    private Vector2 cursorHotspot = new Vector2(8f, 8f);

    [SerializeField]
    private float terminalPickRadius = 0.3f;

    [SerializeField]
    private float junctionPointScale = 0.22f;

    [SerializeField]
    private float junctionConnectDistance = 0.2f;

    [SerializeField]
    private float junctionZOffset = -0.04f;

    private ConnectorTerminal startTerminal;
    private LineRenderer previewLine;
    private readonly List<Vector3> previewWaypoints = new List<Vector3>();
    private readonly List<WireConnection> wireConnections = new List<WireConnection>();
    private readonly List<WireConnection> sharedConnectionsBuffer = new List<WireConnection>();
    private WireConnection selectedWire;
    private ConnectorTerminal hoveredTerminal;
    private int lastTerminalClickFrame;
    private bool cursorIsYellow;
    private int activeDragCount;

    private Material lineMaterial;
    public bool AreTerminalsVisible => startTerminal != null;
    public float GridSpacing => Mathf.Max(0.01f, gridSpacing);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        lineMaterial = new Material(Shader.Find("Sprites/Default"));
        lineMaterial.color = wireColor;
        EnsureCursorTexturesLoaded();
        SetCursorWhite();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SetCursorWhite();
            Instance = null;
        }
    }

    public void HandleTerminalClick(ConnectorTerminal terminal)
    {
        if (terminal == null)
        {
            return;
        }

        lastTerminalClickFrame = Time.frameCount;
        if (startTerminal == null)
        {
            startTerminal = terminal;
            previewWaypoints.Clear();
            CreatePreviewLine();
            return;
        }

        if (terminal == startTerminal)
        {
            CancelPreview();
            return;
        }

        CreateWire(startTerminal, terminal, previewWaypoints);
        CancelPreview();
    }

    public void GetConnectionsForElement(CircuitElement element, List<WireConnection> output)
    {
        if (output == null)
        {
            return;
        }

        output.Clear();
        if (element == null)
        {
            return;
        }

        CleanupDestroyedWires();
        for (var i = 0; i < wireConnections.Count; i++)
        {
            var wire = wireConnections[i];
            if (wire == null || !wire.IsConnectedToElement(element))
            {
                continue;
            }

            output.Add(wire);
        }
    }

    public void GetConnectionsForTerminal(ConnectorTerminal terminal, List<WireConnection> output, WireConnection exclude = null)
    {
        if (output == null)
        {
            return;
        }

        output.Clear();
        if (terminal == null)
        {
            return;
        }

        CleanupDestroyedWires();
        for (var i = 0; i < wireConnections.Count; i++)
        {
            var wire = wireConnections[i];
            if (wire == null || wire == exclude || !wire.IsConnectedToTerminal(terminal))
            {
                continue;
            }

            output.Add(wire);
        }
    }

    public void RemoveConnectionsForElement(CircuitElement element)
    {
        if (element == null)
        {
            return;
        }

        CleanupDestroyedWires();
        for (var i = wireConnections.Count - 1; i >= 0; i--)
        {
            var wire = wireConnections[i];
            if (wire == null || !wire.IsConnectedToElement(element))
            {
                continue;
            }

            DeleteWire(wire, true);
        }
    }

    public void NotifyElementDragStarted()
    {
        activeDragCount++;
        CancelPreview();
    }

    public void NotifyElementDragEnded()
    {
        activeDragCount = Mathf.Max(0, activeDragCount - 1);
    }

    private void Update()
    {
        CleanupDestroyedWires();
        if (activeDragCount > 0 && !Input.GetMouseButton(0))
        {
            activeDragCount = 0;
        }

        if (activeDragCount > 0)
        {
            hoveredTerminal = null;
            CancelPreview();
            UpdateCursorState();
            return;
        }

        hoveredTerminal = FindHoveredTerminal();
        UpdateCursorState();
        if (Input.GetMouseButtonDown(2) && RuntimeGmPanel.IsVisible)
        {
            ToggleLockAtMouse();
        }

        var handledTerminalClick = false;
        if (Input.GetMouseButtonDown(0) && hoveredTerminal != null)
        {
            HandleTerminalClick(hoveredTerminal);
            handledTerminalClick = true;
        }

        if (startTerminal != null && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)))
        {
            CancelPreview();
            return;
        }

        if (startTerminal != null && !handledTerminalClick && Input.GetMouseButtonDown(0) && Time.frameCount != lastTerminalClickFrame)
        {
            if (!TryCompleteWireIntoExistingWire())
            {
                AddBendPoint();
            }
        }

        if (startTerminal != null && previewLine != null)
        {
            UpdatePreviewLine();
            return;
        }

        if (!handledTerminalClick && Input.GetMouseButtonDown(0))
        {
            SelectWireAtMouse();
        }

        if (selectedWire != null && (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)))
        {
            DeleteSelectedWire();
        }
    }

    private void CreateWire(ConnectorTerminal from, ConnectorTerminal to, IReadOnlyList<Vector3> bendPoints)
    {
        var wireObject = new GameObject("Wire");
        BackpackItemSpawner.AttachToLevelRoot(wireObject.transform);
        var line = wireObject.AddComponent<LineRenderer>();
        line.gameObject.name = "WireLine";

        var connection = wireObject.AddComponent<WireConnection>();
        connection.Initialize(
            from,
            to,
            line,
            lineMaterial,
            wireWidth,
            wireColor,
            selectedWireOutlineColor,
            bendPoints,
            signalColor,
            signalWidth,
            signalFrequency);
        wireConnections.Add(connection);
    }

    private void CreatePreviewLine()
    {
        var previewObject = new GameObject("PreviewWire");
        previewLine = previewObject.AddComponent<LineRenderer>();
        ConfigureLine(previewLine, wireWidth, previewColor, 11);
        previewLine.gameObject.name = "PreviewWire";
    }

    private void ConfigureLine(LineRenderer line, float width, Color color, int sortingOrder)
    {
        line.positionCount = 0;
        line.startWidth = width;
        line.endWidth = width;
        line.useWorldSpace = true;
        line.numCapVertices = 8;
        line.material = lineMaterial;
        line.startColor = color;
        line.endColor = color;
        line.sortingOrder = sortingOrder;
    }

    private void CancelPreview()
    {
        startTerminal = null;
        previewWaypoints.Clear();

        if (previewLine != null)
        {
            Destroy(previewLine.gameObject);
            previewLine = null;
        }
    }

    private void AddBendPoint()
    {
        var bend = SnapToGrid(GetMouseWorldPosition());
        var reference = startTerminal.Position;
        if (previewWaypoints.Count > 0)
        {
            reference = previewWaypoints[previewWaypoints.Count - 1];
        }

        if ((bend - reference).sqrMagnitude < 0.0001f)
        {
            return;
        }

        previewWaypoints.Add(bend);
    }

    private bool TryCompleteWireIntoExistingWire()
    {
        if (startTerminal == null)
        {
            return false;
        }

        var targetWire = FindWireAtMouse();
        if (targetWire == null || targetWire.IsLocked)
        {
            return false;
        }

        if (!targetWire.TryGetJunctionPoint(GetMouseWorldPosition(), Mathf.Max(junctionConnectDistance, wireSelectDistance), GridSpacing, out var junctionPoint))
        {
            return false;
        }

        var junctionTerminal = GetOrCreateJunctionTerminal(junctionPoint);
        if (junctionTerminal == null || junctionTerminal == startTerminal)
        {
            return false;
        }

        if (!targetWire.IsConnectedToTerminal(junctionTerminal))
        {
            if (!SplitWireAtJunction(targetWire, junctionTerminal, junctionPoint))
            {
                return false;
            }
        }

        CreateWire(startTerminal, junctionTerminal, previewWaypoints);
        CancelPreview();
        return true;
    }

    private bool SplitWireAtJunction(WireConnection wire, ConnectorTerminal junctionTerminal, Vector3 junctionPoint)
    {
        if (wire == null || junctionTerminal == null)
        {
            return false;
        }

        if (!wire.TryBuildSplitBendPoints(junctionPoint, out var bendsFromA, out var bendsFromB))
        {
            return false;
        }

        var terminalA = wire.TerminalA;
        var terminalB = wire.TerminalB;
        if (terminalA == null || terminalB == null)
        {
            return false;
        }

        wireConnections.Remove(wire);
        if (selectedWire == wire)
        {
            selectedWire = null;
        }

        Destroy(wire.gameObject);
        CreateWire(terminalA, junctionTerminal, bendsFromA);
        CreateWire(junctionTerminal, terminalB, bendsFromB);
        return true;
    }

    private ConnectorTerminal GetOrCreateJunctionTerminal(Vector3 point)
    {
        var existing = FindJunctionTerminalAt(point);
        if (existing != null)
        {
            return existing;
        }

        var junction = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        junction.name = "WireJunction";
        BackpackItemSpawner.AttachToLevelRoot(junction.transform);
        var spacing = GridSpacing;
        junction.transform.position = new Vector3(point.x, point.y, junctionZOffset);
        junction.transform.localScale = Vector3.one * Mathf.Max(0.06f, junctionPointScale * spacing);

        var rendererComponent = junction.GetComponent<Renderer>();
        if (rendererComponent != null)
        {
            rendererComponent.material.color = wireColor;
            rendererComponent.sortingOrder = 13;
        }

        var terminal = junction.AddComponent<ConnectorTerminal>();
        terminal.ConfigureAsJunction();
        return terminal;
    }

    private ConnectorTerminal FindJunctionTerminalAt(Vector3 point)
    {
        var allTerminals = FindObjectsOfType<ConnectorTerminal>();
        var threshold = Mathf.Max(0.04f, GridSpacing * 0.1f);
        var thresholdSqr = threshold * threshold;
        for (var i = 0; i < allTerminals.Length; i++)
        {
            var terminal = allTerminals[i];
            if (terminal == null || terminal.OwnerElement != null)
            {
                continue;
            }

            var delta = terminal.Position - point;
            delta.z = 0f;
            if (delta.sqrMagnitude <= thresholdSqr)
            {
                return terminal;
            }
        }

        return null;
    }

    private void UpdatePreviewLine()
    {
        var anchors = new List<Vector3>();
        anchors.Add(startTerminal.Position);
        for (var i = 0; i < previewWaypoints.Count; i++)
        {
            anchors.Add(previewWaypoints[i]);
        }

        anchors.Add(SnapToGrid(GetMouseWorldPosition()));
        var obstacleRects = WireConnection.BuildElementObstacleRects();
        var terminalTouches = new List<Vector3>();
        if (startTerminal != null)
        {
            terminalTouches.Add(startTerminal.Position);
        }

        if (hoveredTerminal != null)
        {
            terminalTouches.Add(hoveredTerminal.Position);
        }

        var routed = WireConnection.BuildOrthogonalPolyline(anchors, obstacleRects, GridSpacing, terminalTouches);
        previewLine.positionCount = routed.Count;
        for (var i = 0; i < routed.Count; i++)
        {
            previewLine.SetPosition(i, routed[i]);
        }
    }

    private void SelectWireAtMouse()
    {
        var mouse = GetMouseWorldPosition();
        var closest = default(WireConnection);
        var closestDistance = wireSelectDistance;

        for (var i = 0; i < wireConnections.Count; i++)
        {
            var wire = wireConnections[i];
            if (wire == null)
            {
                continue;
            }

            var distance = wire.DistanceToPoint(mouse);
            if (distance <= closestDistance)
            {
                closest = wire;
                closestDistance = distance;
            }
        }

        SetSelectedWire(closest);
    }

    private void DeleteSelectedWire()
    {
        if (selectedWire == null)
        {
            return;
        }

        DeleteWire(selectedWire, false);
    }

    private void SetSelectedWire(WireConnection wire)
    {
        if (selectedWire != null)
        {
            selectedWire.SetSelected(false);
        }

        selectedWire = wire;
        if (selectedWire != null)
        {
            selectedWire.SetSelected(true);
        }
    }

    private void CleanupDestroyedWires()
    {
        for (var i = wireConnections.Count - 1; i >= 0; i--)
        {
            if (wireConnections[i] == null)
            {
                wireConnections.RemoveAt(i);
            }
        }

        if (selectedWire == null)
        {
            return;
        }

        var stillExists = false;
        for (var i = 0; i < wireConnections.Count; i++)
        {
            if (wireConnections[i] == selectedWire)
            {
                stillExists = true;
                break;
            }
        }

        if (!stillExists)
        {
            selectedWire = null;
        }
    }

    private void ToggleLockAtMouse()
    {
        var element = FindElementAtMouse();
        if (element != null)
        {
            element.ToggleLock();
            return;
        }

        var wire = FindWireAtMouse();
        if (wire != null)
        {
            wire.ToggleLock();
        }
    }

    private CircuitElement FindElementAtMouse()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return null;
        }

        if (hoveredTerminal != null && hoveredTerminal.OwnerElement != null)
        {
            return hoveredTerminal.OwnerElement;
        }

        var ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        var hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Collide);
        var bestDistance = float.PositiveInfinity;
        var bestElement = default(CircuitElement);
        for (var i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit.collider == null || hit.distance >= bestDistance)
            {
                continue;
            }

            var element = hit.collider.GetComponentInParent<CircuitElement>();
            if (element == null)
            {
                continue;
            }

            bestDistance = hit.distance;
            bestElement = element;
        }

        return bestElement;
    }

    private WireConnection FindWireAtMouse()
    {
        var mouse = GetMouseWorldPosition();
        var closest = default(WireConnection);
        var closestDistance = wireSelectDistance;

        for (var i = 0; i < wireConnections.Count; i++)
        {
            var wire = wireConnections[i];
            if (wire == null)
            {
                continue;
            }

            var distance = wire.DistanceToPoint(mouse);
            if (distance > closestDistance)
            {
                continue;
            }

            closest = wire;
            closestDistance = distance;
        }

        return closest;
    }

    private void DeleteWire(WireConnection wire, bool ignoreLock)
    {
        if (wire == null)
        {
            return;
        }

        if (!ignoreLock && wire.IsLocked)
        {
            return;
        }

        wireConnections.Remove(wire);
        if (selectedWire == wire)
        {
            selectedWire = null;
        }

        Destroy(wire.gameObject);
        CleanupDanglingJunctions();
    }

    private void CleanupDanglingJunctions()
    {
        var allTerminals = FindObjectsOfType<ConnectorTerminal>();
        for (var i = 0; i < allTerminals.Length; i++)
        {
            var terminal = allTerminals[i];
            if (terminal == null || terminal.OwnerElement != null)
            {
                continue;
            }

            GetConnectionsForTerminal(terminal, sharedConnectionsBuffer);
            if (sharedConnectionsBuffer.Count > 0)
            {
                continue;
            }

            Destroy(terminal.gameObject);
        }

        sharedConnectionsBuffer.Clear();
    }

    private Vector3 GetMouseWorldPosition()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        var mouse = Input.mousePosition;
        mouse.z = -targetCamera.transform.position.z;
        var world = targetCamera.ScreenToWorldPoint(mouse);
        world.z = 0f;
        return world;
    }

    private Vector3 SnapToGrid(Vector3 world)
    {
        var spacing = Mathf.Max(0.01f, gridSpacing);
        world.x = Mathf.Round(world.x / spacing) * spacing;
        world.y = Mathf.Round(world.y / spacing) * spacing;
        world.z = 0f;
        return world;
    }

    private void UpdateCursorState()
    {
        var shouldBeYellow = hoveredTerminal != null && terminalHoverCursorYellow != null;
        if (shouldBeYellow == cursorIsYellow)
        {
            return;
        }

        if (shouldBeYellow)
        {
            ApplyCursor(terminalHoverCursorYellow);
            cursorIsYellow = true;
            return;
        }

        SetCursorWhite();
    }

    private ConnectorTerminal FindHoveredTerminal()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return null;
        }

        var ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        var hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Collide);
        var best = default(ConnectorTerminal);
        var bestDistance = float.PositiveInfinity;
        for (var i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            var terminal = hit.collider == null ? null : hit.collider.GetComponent<ConnectorTerminal>();
            if (terminal == null || hit.distance >= bestDistance)
            {
                continue;
            }

            best = terminal;
            bestDistance = hit.distance;
        }

        if (best != null)
        {
            return best;
        }

        var rayToPlaneDistance = Mathf.Abs(ray.direction.z) > 0.0001f ? -ray.origin.z / ray.direction.z : 0f;
        if (rayToPlaneDistance < 0f)
        {
            return null;
        }

        var mouseWorld = ray.origin + ray.direction * rayToPlaneDistance;
        mouseWorld.z = 0f;
        var allTerminals = FindObjectsOfType<ConnectorTerminal>();
        var bestFallback = default(ConnectorTerminal);
        var bestFallbackDistance = terminalPickRadius * terminalPickRadius;
        for (var i = 0; i < allTerminals.Length; i++)
        {
            var terminal = allTerminals[i];
            if (terminal == null || !terminal.isActiveAndEnabled)
            {
                continue;
            }

            var collider = terminal.GetComponent<Collider>();
            if (collider == null)
            {
                continue;
            }

            var closest = collider.ClosestPoint(mouseWorld);
            closest.z = 0f;
            var distance = (closest - mouseWorld).sqrMagnitude;
            if (distance > bestFallbackDistance)
            {
                continue;
            }

            bestFallbackDistance = distance;
            bestFallback = terminal;
        }

        return bestFallback;
    }

    private void SetCursorWhite()
    {
        cursorIsYellow = false;
        ApplyCursor(defaultCursorWhite);
    }

    private void EnsureCursorTexturesLoaded()
    {
        defaultCursorWhite = EnsureValidCursorTexture(defaultCursorWhite, "white.png");
        terminalHoverCursorYellow = EnsureValidCursorTexture(terminalHoverCursorYellow, "yellow.png");
    }

    private Texture2D LoadCursorTextureFromFile(string fileName)
    {
        var path = Path.Combine(Application.dataPath, "Cursor", fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        var bytes = File.ReadAllBytes(path);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes, false))
        {
            Destroy(texture);
            return null;
        }

        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }

    private void ApplyCursor(Texture2D texture)
    {
        Cursor.SetCursor(texture, cursorHotspot, CursorMode.Auto);
    }

    private Texture2D EnsureValidCursorTexture(Texture2D configured, string fallbackFileName)
    {
        var source = configured != null ? configured : LoadCursorTextureFromFile(fallbackFileName);
        if (source == null)
        {
            return null;
        }

        var runtimeCopy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        var active = RenderTexture.active;
        var renderTexture = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        Graphics.Blit(source, renderTexture);
        RenderTexture.active = renderTexture;
        runtimeCopy.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, false);
        runtimeCopy.Apply(false, false);
        RenderTexture.active = active;
        RenderTexture.ReleaseTemporary(renderTexture);
        runtimeCopy.filterMode = FilterMode.Bilinear;
        runtimeCopy.wrapMode = TextureWrapMode.Clamp;
        return runtimeCopy;
    }
}
