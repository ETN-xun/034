using UnityEngine;

public class ConnectorTerminal : MonoBehaviour
{
    [field: SerializeField]
    public CircuitElement OwnerElement { get; private set; }

    [field: SerializeField]
    public bool AlwaysVisible { get; private set; }

    public Vector3 Position => transform.position;
    private Renderer cachedRenderer;

    private void Awake()
    {
        if (OwnerElement == null)
        {
            OwnerElement = GetComponentInParent<CircuitElement>();
        }

        cachedRenderer = GetComponent<Renderer>();
        ApplyVisibility();
    }

    private void LateUpdate()
    {
        ApplyVisibility();
    }

    public void ConfigureAsJunction()
    {
        AlwaysVisible = true;
        OwnerElement = null;
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        if (cachedRenderer == null)
        {
            return;
        }

        var visible = AlwaysVisible || (WiringManager.Instance != null && WiringManager.Instance.AreTerminalsVisible);
        if (cachedRenderer.enabled != visible)
        {
            cachedRenderer.enabled = visible;
        }
    }
}
