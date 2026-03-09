using UnityEngine;

[RequireComponent(typeof(CircuitElement))]
[RequireComponent(typeof(Renderer))]
public class SineElementSetup : MonoBehaviour
{
    [SerializeField]
    private float terminalScale = 0.16f;

    [SerializeField]
    private Vector3 generatorTerminalLocalPosition = new Vector3(0.66f, 0f, 0f);

    [SerializeField]
    private Vector3 receiverTerminalLocalPosition = new Vector3(-0.66f, 0f, 0f);

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

        ApplyBodyColor();
        EnsureTerminal();
    }

    private void ApplyBodyColor()
    {
        var rendererComponent = GetComponent<Renderer>();
        if (rendererComponent == null)
        {
            return;
        }

        var color = circuitElement.ElementType == CircuitElementType.SineGenerator
            ? new Color(0.2f, 0.45f, 1f, 1f)
            : new Color(1f, 0.2f, 0.2f, 1f);

        rendererComponent.material.color = color;
    }

    private void EnsureTerminal()
    {
        var existingTerminal = GetComponentInChildren<ConnectorTerminal>();
        if (existingTerminal != null)
        {
            existingTerminal.transform.localPosition = circuitElement.ElementType == CircuitElementType.SineGenerator
                ? generatorTerminalLocalPosition
                : receiverTerminalLocalPosition;
            existingTerminal.transform.localScale = Vector3.one * terminalScale;
            SetTerminalVisual(existingTerminal.gameObject);
            return;
        }

        var terminalObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        terminalObject.name = "Terminal";
        terminalObject.transform.SetParent(transform, false);
        terminalObject.transform.localScale = Vector3.one * terminalScale;
        terminalObject.transform.localPosition = circuitElement.ElementType == CircuitElementType.SineGenerator
            ? generatorTerminalLocalPosition
            : receiverTerminalLocalPosition;
        terminalObject.transform.localRotation = Quaternion.identity;
        terminalObject.AddComponent<ConnectorTerminal>();

        SetTerminalVisual(terminalObject);
    }

    private void SetTerminalVisual(GameObject terminalObject)
    {
        var rendererComponent = terminalObject.GetComponent<Renderer>();
        if (rendererComponent != null)
        {
            rendererComponent.material.color = new Color(1f, 0.92f, 0.1f, 1f);
        }
    }
}
