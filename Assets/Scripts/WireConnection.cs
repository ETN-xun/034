using UnityEngine;
using System.Collections.Generic;

public class WireConnection : MonoBehaviour
{
    private enum SignalWaveform
    {
        None,
        Semicircle,
        Triangle,
        Square
    }

    [SerializeField]
    private ConnectorTerminal terminalA;

    [SerializeField]
    private ConnectorTerminal terminalB;

    [SerializeField]
    private LineRenderer lineRenderer;

    [SerializeField]
    private List<Vector3> bendPoints = new List<Vector3>();

    [SerializeField]
    private bool isLocked;

    private LineRenderer outlineRenderer;
    private LineRenderer signalRenderer;
    private readonly List<Vector3> currentPolyline = new List<Vector3>();
    private readonly List<Vector3> routedPolyline = new List<Vector3>();
    private readonly List<float> segmentLengths = new List<float>();
    private readonly List<Vector3> signalPoints = new List<Vector3>();
    private float totalLength;
    private float signalAmplitude;
    private float signalWavelength;
    private float signalFrequency;
    private SignalWaveform signalWaveform;
    private bool hasSignal;
    public bool IsLocked => isLocked;

    public bool IsConnectedToElement(CircuitElement element)
    {
        if (element == null)
        {
            return false;
        }

        return (terminalA != null && terminalA.OwnerElement == element) || (terminalB != null && terminalB.OwnerElement == element);
    }

    public bool TryGetSignalShapeParams(out float amplitude, out float wavelength, out float frequency)
    {
        amplitude = signalAmplitude;
        wavelength = signalWavelength;
        frequency = signalFrequency;
        return hasSignal;
    }

    public bool TryGetSignalShape(out CircuitElementType sourceType)
    {
        sourceType = CircuitElementType.SemiWaveGenerator;
        if (!hasSignal)
        {
            return false;
        }

        sourceType = GetElementTypeForWaveform(signalWaveform);
        return true;
    }

    public bool TryGetSignalAtElement(CircuitElement element, float time, out float signalValue)
    {
        signalValue = 0f;
        if (!hasSignal || element == null)
        {
            return false;
        }

        var terminal = GetTerminalForElement(element);
        if (terminal == null)
        {
            return false;
        }

        BuildSegmentLengths();
        if (totalLength <= 0.001f)
        {
            return false;
        }

        var distance = terminal == terminalA ? 0f : totalLength;
        if (TryGetSignalSource(out _, out var sourceTerminal, out _))
        {
            var sourceIsA = sourceTerminal == terminalA;
            distance = sourceIsA
                ? (terminal == terminalA ? 0f : totalLength)
                : (terminal == terminalB ? 0f : totalLength);
        }

        signalValue = EvaluateWave(signalWaveform, distance, time, signalWavelength, signalFrequency, signalAmplitude);
        return true;
    }

    public void Initialize(
        ConnectorTerminal a,
        ConnectorTerminal b,
        LineRenderer line,
        Material lineMaterial,
        float width,
        Color color,
        Color outlineColor,
        IReadOnlyList<Vector3> initialBends,
        Color signalColor,
        float signalWidth,
        float frequency)
    {
        terminalA = a;
        terminalB = b;
        lineRenderer = line;
        bendPoints.Clear();
        if (initialBends != null)
        {
            for (var i = 0; i < initialBends.Count; i++)
            {
                bendPoints.Add(initialBends[i]);
            }
        }

        ConfigureLineRenderer(lineRenderer, lineMaterial, width, color, 10);
        var outlineObject = new GameObject("WireOutline");
        outlineObject.transform.SetParent(transform, false);
        outlineRenderer = outlineObject.AddComponent<LineRenderer>();
        ConfigureLineRenderer(outlineRenderer, lineMaterial, width + 0.08f, outlineColor, 9);
        outlineRenderer.enabled = false;

        var signalObject = new GameObject("SignalWave");
        signalObject.transform.SetParent(transform, false);
        signalRenderer = signalObject.AddComponent<LineRenderer>();
        ConfigureLineRenderer(signalRenderer, lineMaterial, signalWidth, signalColor, 12);
        signalRenderer.enabled = false;

        signalFrequency = Mathf.Max(0.01f, frequency);
        InitializeSignalParams();
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

        currentPolyline.Clear();
        var start = terminalA.Position;
        start.z = 0f;
        currentPolyline.Add(start);

        for (var i = 0; i < bendPoints.Count; i++)
        {
            var bend = bendPoints[i];
            bend.z = 0f;
            currentPolyline.Add(bend);
        }

        var end = terminalB.Position;
        end.z = 0f;
        currentPolyline.Add(end);

        routedPolyline.Clear();
        var routed = BuildOrthogonalPolyline(currentPolyline);
        routedPolyline.AddRange(routed);
        lineRenderer.positionCount = routedPolyline.Count;
        if (outlineRenderer != null)
        {
            outlineRenderer.positionCount = routedPolyline.Count;
        }

        for (var i = 0; i < routedPolyline.Count; i++)
        {
            lineRenderer.SetPosition(i, routedPolyline[i]);
            if (outlineRenderer != null)
            {
                outlineRenderer.SetPosition(i, routedPolyline[i]);
            }
        }

        UpdateSignalLine();
    }

    public void SetSelected(bool selected)
    {
        if (outlineRenderer != null)
        {
            outlineRenderer.enabled = selected;
        }
    }

    public void ToggleLock()
    {
        isLocked = !isLocked;
    }

    public float DistanceToPoint(Vector3 point)
    {
        if (lineRenderer == null || lineRenderer.positionCount < 2)
        {
            return float.MaxValue;
        }

        var minDistance = float.MaxValue;
        for (var i = 0; i < lineRenderer.positionCount - 1; i++)
        {
            var a = lineRenderer.GetPosition(i);
            var b = lineRenderer.GetPosition(i + 1);
            var distance = DistancePointToSegment(point, a, b);
            if (distance < minDistance)
            {
                minDistance = distance;
            }
        }

        return minDistance;
    }

    public static List<Vector3> BuildOrthogonalPolyline(IReadOnlyList<Vector3> anchors)
    {
        var result = new List<Vector3>();
        if (anchors == null || anchors.Count == 0)
        {
            return result;
        }

        result.Add(new Vector3(anchors[0].x, anchors[0].y, 0f));
        for (var i = 1; i < anchors.Count; i++)
        {
            var from = result[result.Count - 1];
            var to = new Vector3(anchors[i].x, anchors[i].y, 0f);
            AddOrthogonalSegment(result, from, to);
        }

        return result;
    }

    private void ConfigureLineRenderer(LineRenderer renderer, Material material, float width, Color color, int sortingOrder)
    {
        renderer.positionCount = 0;
        renderer.useWorldSpace = true;
        renderer.startWidth = width;
        renderer.endWidth = width;
        renderer.numCapVertices = 8;
        renderer.material = material;
        renderer.startColor = color;
        renderer.endColor = color;
        renderer.sortingOrder = sortingOrder;
    }

    private static void AddOrthogonalSegment(List<Vector3> points, Vector3 from, Vector3 to)
    {
        if (Mathf.Approximately(from.x, to.x) || Mathf.Approximately(from.y, to.y))
        {
            AddPointIfNeeded(points, to);
            return;
        }

        var corner = new Vector3(to.x, from.y, 0f);
        AddPointIfNeeded(points, corner);
        AddPointIfNeeded(points, to);
    }

    private static void AddPointIfNeeded(List<Vector3> points, Vector3 point)
    {
        if (points.Count == 0)
        {
            points.Add(point);
            return;
        }

        var last = points[points.Count - 1];
        if ((last - point).sqrMagnitude > 0.0001f)
        {
            points.Add(point);
        }
    }

    private static float DistancePointToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        var sqrLength = ab.sqrMagnitude;
        if (sqrLength <= 0.00001f)
        {
            return Vector3.Distance(point, a);
        }

        var t = Vector3.Dot(point - a, ab) / sqrLength;
        t = Mathf.Clamp01(t);
        var projection = a + t * ab;
        return Vector3.Distance(point, projection);
    }

    private void InitializeSignalParams()
    {
        if (!TryGetSignalSource(out var sourceElement, out var sourceTerminal, out signalWaveform))
        {
            hasSignal = false;
            signalAmplitude = 0f;
            signalWavelength = 1f;
            signalWaveform = SignalWaveform.None;
            return;
        }

        var rendererComponent = sourceElement.GetComponent<Renderer>();
        var radius = 0.5f;
        if (rendererComponent != null)
        {
            radius = Mathf.Max(0.05f, rendererComponent.bounds.extents.x);
        }

        signalAmplitude = radius;
        var diameter = radius * 2f;
        signalWavelength = Mathf.Max(0.1f, diameter * 2f);
        hasSignal = true;
    }

    private bool TryGetSignalSource(out CircuitElement sourceElement, out ConnectorTerminal sourceTerminal, out SignalWaveform waveform)
    {
        sourceElement = null;
        sourceTerminal = null;
        waveform = SignalWaveform.None;
        if (CanEmitSignal(terminalA, out var waveformA))
        {
            sourceElement = terminalA.OwnerElement;
            sourceTerminal = terminalA;
            waveform = waveformA;
            return true;
        }

        if (CanEmitSignal(terminalB, out var waveformB))
        {
            sourceElement = terminalB.OwnerElement;
            sourceTerminal = terminalB;
            waveform = waveformB;
            return true;
        }

        return false;
    }

    private ConnectorTerminal GetTerminalForElement(CircuitElement element)
    {
        if (terminalA != null && terminalA.OwnerElement == element)
        {
            return terminalA;
        }

        if (terminalB != null && terminalB.OwnerElement == element)
        {
            return terminalB;
        }

        return null;
    }

    private void UpdateSignalLine()
    {
        if (signalRenderer == null)
        {
            return;
        }

        InitializeSignalParams();
        if (!hasSignal || routedPolyline.Count < 2)
        {
            signalRenderer.enabled = false;
            signalRenderer.positionCount = 0;
            return;
        }

        BuildSegmentLengths();
        if (totalLength <= 0.001f)
        {
            signalRenderer.enabled = false;
            signalRenderer.positionCount = 0;
            return;
        }

        var sampleStep = 0.08f;
        var sampleCount = Mathf.Max(2, Mathf.CeilToInt(totalLength / sampleStep) + 1);
        signalPoints.Clear();
        var time = Time.time;
        for (var i = 0; i < sampleCount; i++)
        {
            var distance = totalLength * i / (sampleCount - 1);
            var sample = SampleAtDistance(distance);
            var offset = ComputeSignalOffset(distance, time);
            sample.point += sample.normal * offset;
            sample.point.z = 0f;
            signalPoints.Add(sample.point);
        }

        signalRenderer.enabled = true;
        signalRenderer.positionCount = signalPoints.Count;
        for (var i = 0; i < signalPoints.Count; i++)
        {
            signalRenderer.SetPosition(i, signalPoints[i]);
        }
    }

    private void BuildSegmentLengths()
    {
        segmentLengths.Clear();
        totalLength = 0f;
        for (var i = 0; i < routedPolyline.Count - 1; i++)
        {
            var length = Vector3.Distance(routedPolyline[i], routedPolyline[i + 1]);
            segmentLengths.Add(length);
            totalLength += length;
        }
    }

    private (Vector3 point, Vector3 normal) SampleAtDistance(float distance)
    {
        var remaining = Mathf.Clamp(distance, 0f, totalLength);
        for (var i = 0; i < segmentLengths.Count; i++)
        {
            var segLength = segmentLengths[i];
            if (remaining > segLength && i < segmentLengths.Count - 1)
            {
                remaining -= segLength;
                continue;
            }

            var a = routedPolyline[i];
            var b = routedPolyline[i + 1];
            var t = segLength <= 0.0001f ? 0f : remaining / segLength;
            var point = Vector3.Lerp(a, b, t);
            var tangent = (b - a).normalized;
            var normal = new Vector3(-tangent.y, tangent.x, 0f);
            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = Vector3.up;
            }

            return (point, normal);
        }

        var fallback = routedPolyline[routedPolyline.Count - 1];
        return (fallback, Vector3.up);
    }

    private float ComputeSignalOffset(float distance, float time)
    {
        return EvaluateWave(signalWaveform, distance, time, signalWavelength, signalFrequency, signalAmplitude);
    }

    private static float EvaluateWave(SignalWaveform waveform, float distance, float time, float wavelength, float frequency, float amplitude)
    {
        switch (waveform)
        {
            case SignalWaveform.Triangle:
                return EvaluateTriangleWave(distance, time, wavelength, frequency, amplitude);
            case SignalWaveform.Square:
                return EvaluateSquareWave(distance, time, wavelength, frequency, amplitude);
            default:
                return EvaluateSemicircleWave(distance, time, wavelength, frequency, amplitude);
        }
    }

    public static float EvaluateSemicircleWave(float distance, float time, float wavelength, float frequency, float amplitude)
    {
        var safeWavelength = Mathf.Max(0.001f, wavelength);
        var safeFrequency = Mathf.Max(0.01f, frequency);
        var safeAmplitude = Mathf.Max(0f, amplitude);
        var phaseDistance = distance - time * safeFrequency * safeWavelength;
        var local = Mathf.Repeat(phaseDistance, safeWavelength);
        var halfWave = safeWavelength * 0.5f;
        var segment = local < halfWave ? local : local - halfWave;
        var x = segment / Mathf.Max(0.0001f, halfWave);
        var circleX = x * 2f - 1f;
        var y = Mathf.Sqrt(Mathf.Max(0f, 1f - circleX * circleX));
        var sign = local < halfWave ? 1f : -1f;
        return y * safeAmplitude * sign;
    }

    public static float EvaluateTriangleWave(float distance, float time, float wavelength, float frequency, float amplitude)
    {
        var safeWavelength = Mathf.Max(0.001f, wavelength);
        var safeFrequency = Mathf.Max(0.01f, frequency);
        var safeAmplitude = Mathf.Max(0f, amplitude);
        var phaseDistance = distance - time * safeFrequency * safeWavelength;
        var normalized = Mathf.Repeat(phaseDistance / safeWavelength, 1f);
        var raw = 1f - 4f * Mathf.Abs(normalized - 0.5f);
        return raw * safeAmplitude;
    }

    public static float EvaluateSquareWave(float distance, float time, float wavelength, float frequency, float amplitude)
    {
        var safeWavelength = Mathf.Max(0.001f, wavelength);
        var safeFrequency = Mathf.Max(0.01f, frequency);
        var safeAmplitude = Mathf.Max(0f, amplitude);
        var phaseDistance = distance - time * safeFrequency * safeWavelength;
        var local = Mathf.Repeat(phaseDistance, safeWavelength);
        return local < safeWavelength * 0.5f ? safeAmplitude : -safeAmplitude;
    }

    private bool CanEmitSignal(ConnectorTerminal terminal, out SignalWaveform waveform)
    {
        waveform = SignalWaveform.None;
        if (terminal == null || terminal.OwnerElement == null)
        {
            return false;
        }

        var type = terminal.OwnerElement.ElementType;
        switch (type)
        {
            case CircuitElementType.SemiWaveGenerator:
                waveform = SignalWaveform.Semicircle;
                return true;
            case CircuitElementType.TriangleWaveGenerator:
                waveform = SignalWaveform.Triangle;
                return true;
            case CircuitElementType.SquareWaveGenerator:
                waveform = SignalWaveform.Square;
                return true;
            case CircuitElementType.SemiWaveConverter:
                if (!IsConverterOutputTerminal(terminal) || !HasConverterInputSignal(terminal.OwnerElement))
                {
                    return false;
                }

                waveform = SignalWaveform.Semicircle;
                return true;
            case CircuitElementType.TriangleWaveConverter:
                if (!IsConverterOutputTerminal(terminal) || !HasConverterInputSignal(terminal.OwnerElement))
                {
                    return false;
                }

                waveform = SignalWaveform.Triangle;
                return true;
            case CircuitElementType.SquareWaveConverter:
                if (!IsConverterOutputTerminal(terminal) || !HasConverterInputSignal(terminal.OwnerElement))
                {
                    return false;
                }

                waveform = SignalWaveform.Square;
                return true;
            default:
                return false;
        }
    }

    private bool HasConverterInputSignal(CircuitElement converter)
    {
        if (converter == null || WiringManager.Instance == null)
        {
            return false;
        }

        var inputTerminal = converter.transform.Find("Terminal_Input");
        if (inputTerminal == null)
        {
            return false;
        }

        var connections = new List<WireConnection>();
        WiringManager.Instance.GetConnectionsForElement(converter, connections);
        for (var i = 0; i < connections.Count; i++)
        {
            var connection = connections[i];
            if (connection == null || connection == this)
            {
                continue;
            }

            var converterTerminal = connection.GetTerminalForElement(converter);
            if (converterTerminal == null || converterTerminal.transform != inputTerminal)
            {
                continue;
            }

            var otherTerminal = connection.GetOppositeTerminal(converterTerminal);
            if (otherTerminal == null || otherTerminal.OwnerElement == null)
            {
                continue;
            }

            if (IsPotentialSignalSource(otherTerminal.OwnerElement.ElementType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsConverterOutputTerminal(ConnectorTerminal terminal)
    {
        return terminal != null && terminal.name == "Terminal_Output";
    }

    private ConnectorTerminal GetOppositeTerminal(ConnectorTerminal terminal)
    {
        if (terminal == terminalA)
        {
            return terminalB;
        }

        if (terminal == terminalB)
        {
            return terminalA;
        }

        return null;
    }

    private static bool IsPotentialSignalSource(CircuitElementType type)
    {
        switch (type)
        {
            case CircuitElementType.SemiWaveGenerator:
            case CircuitElementType.TriangleWaveGenerator:
            case CircuitElementType.SquareWaveGenerator:
            case CircuitElementType.SemiWaveConverter:
            case CircuitElementType.TriangleWaveConverter:
            case CircuitElementType.SquareWaveConverter:
                return true;
            default:
                return false;
        }
    }

    private static CircuitElementType GetElementTypeForWaveform(SignalWaveform waveform)
    {
        switch (waveform)
        {
            case SignalWaveform.Triangle:
                return CircuitElementType.TriangleWaveGenerator;
            case SignalWaveform.Square:
                return CircuitElementType.SquareWaveGenerator;
            default:
                return CircuitElementType.SemiWaveGenerator;
        }
    }
}
