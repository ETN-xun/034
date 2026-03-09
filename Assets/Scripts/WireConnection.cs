using UnityEngine;

public class WireConnection : MonoBehaviour
{
    [SerializeField]
    private ConnectorTerminal terminalA;

    [SerializeField]
    private ConnectorTerminal terminalB;

    [SerializeField]
    private LineRenderer lineRenderer;

    public void Initialize(ConnectorTerminal a, ConnectorTerminal b, LineRenderer line)
    {
        terminalA = a;
        terminalB = b;
        lineRenderer = line;
        UpdateLine();
    }

    private void LateUpdate()
    {
        UpdateLine();
    }

    private void UpdateLine()
    {
        if (terminalA == null || terminalB == null || lineRenderer == null)
        {
            return;
        }

        lineRenderer.SetPosition(0, terminalA.Position);
        lineRenderer.SetPosition(1, terminalB.Position);
    }
}
