using UnityEngine;
using System.Collections.Generic;

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

    private ConnectorTerminal startTerminal;
    private LineRenderer previewLine;
    private readonly List<Vector3> previewWaypoints = new List<Vector3>();
    private readonly List<WireConnection> wireConnections = new List<WireConnection>();
    private WireConnection selectedWire;
    private int lastTerminalClickFrame;

    private Material lineMaterial;

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

    private void Update()
    {
        CleanupDestroyedWires();

        if (startTerminal != null && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)))
        {
            CancelPreview();
            return;
        }

        if (startTerminal != null && Input.GetMouseButtonDown(0) && Time.frameCount != lastTerminalClickFrame)
        {
            AddBendPoint();
        }

        if (startTerminal != null && previewLine != null)
        {
            UpdatePreviewLine();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            SelectWireAtMouse();
        }

        if (selectedWire != null && Input.GetKeyDown(KeyCode.Delete))
        {
            DeleteSelectedWire();
        }
    }

    private void CreateWire(ConnectorTerminal from, ConnectorTerminal to, IReadOnlyList<Vector3> bendPoints)
    {
        var wireObject = new GameObject("Wire");
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

    private void UpdatePreviewLine()
    {
        var anchors = new List<Vector3>();
        anchors.Add(startTerminal.Position);
        for (var i = 0; i < previewWaypoints.Count; i++)
        {
            anchors.Add(previewWaypoints[i]);
        }

        anchors.Add(SnapToGrid(GetMouseWorldPosition()));
        var routed = WireConnection.BuildOrthogonalPolyline(anchors);
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

        wireConnections.Remove(selectedWire);
        Destroy(selectedWire.gameObject);
        selectedWire = null;
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
}
