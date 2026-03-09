using UnityEngine;

public class ConnectorTerminal : MonoBehaviour
{
    [field: SerializeField]
    public CircuitElement OwnerElement { get; private set; }

    public Vector3 Position => transform.position;

    private void Awake()
    {
        if (OwnerElement == null)
        {
            OwnerElement = GetComponentInParent<CircuitElement>();
        }
    }

    private void OnMouseDown()
    {
        if (WiringManager.Instance == null)
        {
            return;
        }

        WiringManager.Instance.HandleTerminalClick(this);
    }
}
