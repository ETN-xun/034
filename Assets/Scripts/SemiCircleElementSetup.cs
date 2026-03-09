using UnityEngine;

[RequireComponent(typeof(CircuitElement))]
[RequireComponent(typeof(Renderer))]
public class SemiCircleElementSetup : MonoBehaviour
{
    public const float DefaultBodyRadiusInGridUnits = 1f;

    [SerializeField]
    private float bodyRadiusInGridUnits = DefaultBodyRadiusInGridUnits;

    [SerializeField]
    private float bodyDepth = 0.2f;

    [SerializeField]
    private float terminalScale = 0.22f;

    [SerializeField]
    private float gridSpacing = 1f;

    private static readonly Vector3[] TerminalDirections =
    {
        new Vector3(1f, 0f, 0f),
        new Vector3(0f, 1f, 0f),
        new Vector3(-1f, 0f, 0f),
        new Vector3(0f, -1f, 0f)
    };

    private CircuitElement circuitElement;

    private void Awake()
    {
        circuitElement = GetComponent<CircuitElement>();
        Apply();
    }

    public void Apply()
    {
        if (circuitElement == null)
        {
            circuitElement = GetComponent<CircuitElement>();
        }

        var spacing = Mathf.Max(0.01f, gridSpacing);
        var radius = Mathf.Max(0.05f, bodyRadiusInGridUnits * spacing);
        var diameter = radius * 2f;
        transform.localScale = new Vector3(diameter, diameter, Mathf.Max(0.01f, bodyDepth));
        transform.position = SnapToGrid(transform.position);
        ApplyBodyColor();
        EnsureTerminals();
    }

    private void ApplyBodyColor()
    {
        var rendererComponent = GetComponent<Renderer>();
        if (rendererComponent == null)
        {
            return;
        }

        var color = circuitElement.ElementType == CircuitElementType.SemiWaveGenerator
            ? new Color(0.2f, 0.45f, 1f, 1f)
            : new Color(1f, 0.2f, 0.2f, 1f);

        rendererComponent.material.color = color;
    }

    private void EnsureTerminals()
    {
        var expectedNames = new string[TerminalDirections.Length];
        var spacing = Mathf.Max(0.01f, gridSpacing);
        var radius = Mathf.Max(0.05f, bodyRadiusInGridUnits * spacing);
        for (var i = 0; i < TerminalDirections.Length; i++)
        {
            expectedNames[i] = $"Terminal_{i}";
            var terminalObject = transform.Find(expectedNames[i]);
            if (terminalObject == null)
            {
                var created = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                created.name = expectedNames[i];
                created.transform.SetParent(transform, false);
                terminalObject = created.transform;
            }

            terminalObject.localPosition = TerminalDirections[i] * (radius / Mathf.Max(0.0001f, transform.localScale.x));
            terminalObject.localScale = Vector3.one * terminalScale;
            terminalObject.localRotation = Quaternion.identity;

            var terminal = terminalObject.GetComponent<ConnectorTerminal>();
            if (terminal == null)
            {
                terminalObject.gameObject.AddComponent<ConnectorTerminal>();
            }

            SetTerminalVisual(terminalObject.gameObject);
        }

        var allTerminals = GetComponentsInChildren<ConnectorTerminal>();
        foreach (var terminal in allTerminals)
        {
            var shouldKeep = false;
            foreach (var name in expectedNames)
            {
                if (terminal.name == name)
                {
                    shouldKeep = true;
                    break;
                }
            }

            if (!shouldKeep)
            {
                Destroy(terminal.gameObject);
            }
        }
    }

    private void SetTerminalVisual(GameObject terminalObject)
    {
        var rendererComponent = terminalObject.GetComponent<Renderer>();
        if (rendererComponent != null)
        {
            rendererComponent.material.color = new Color(1f, 0.92f, 0.1f, 1f);
        }
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
