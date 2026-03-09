using UnityEngine;

public class WiringManager : MonoBehaviour
{
    public static WiringManager Instance { get; private set; }

    [SerializeField]
    private Camera targetCamera;

    [SerializeField]
    private float wireWidth = 0.05f;

    [SerializeField]
    private Color wireColor = Color.yellow;

    private ConnectorTerminal startTerminal;
    private LineRenderer previewLine;

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

        if (startTerminal == null)
        {
            startTerminal = terminal;
            CreatePreviewLine();
            return;
        }

        if (terminal == startTerminal)
        {
            CancelPreview();
            return;
        }

        CreateWire(startTerminal, terminal);
        CancelPreview();
    }

    private void Update()
    {
        if (startTerminal == null || previewLine == null)
        {
            return;
        }

        previewLine.SetPosition(0, startTerminal.Position);
        previewLine.SetPosition(1, GetMouseWorldPosition());
    }

    private void CreateWire(ConnectorTerminal from, ConnectorTerminal to)
    {
        var wireObject = new GameObject("Wire");
        var line = wireObject.AddComponent<LineRenderer>();
        ConfigureLine(line, "WireMaterial");

        var connection = wireObject.AddComponent<WireConnection>();
        connection.Initialize(from, to, line);
    }

    private void CreatePreviewLine()
    {
        var previewObject = new GameObject("PreviewWire");
        previewLine = previewObject.AddComponent<LineRenderer>();
        ConfigureLine(previewLine, "PreviewWireMaterial");
    }

    private void ConfigureLine(LineRenderer line, string materialName)
    {
        line.positionCount = 2;
        line.startWidth = wireWidth;
        line.endWidth = wireWidth;
        line.useWorldSpace = true;
        line.numCapVertices = 8;
        line.material = lineMaterial;
        line.startColor = wireColor;
        line.endColor = wireColor;
        line.sortingOrder = 10;
        line.gameObject.name = materialName.Replace("Material", string.Empty);
    }

    private void CancelPreview()
    {
        startTerminal = null;

        if (previewLine != null)
        {
            Destroy(previewLine.gameObject);
            previewLine = null;
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
}
