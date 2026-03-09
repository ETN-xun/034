using UnityEngine;

[RequireComponent(typeof(CircuitElement))]
[RequireComponent(typeof(Renderer))]
public class SemiCircleElementSetup : MonoBehaviour
{
    public const float DefaultBodyRadiusInGridUnits = 1f;
    private static readonly Color GeneratorColor = new Color(0.2f, 0.45f, 1f, 1f);
    private static readonly Color ReceiverColor = new Color(1f, 0.2f, 0.2f, 1f);
    private static readonly Color ConverterColor = new Color(1f, 1f, 1f, 1f);
    private static readonly Color TerminalColor = new Color(1f, 0.92f, 0.1f, 1f);
    private static Mesh circleMesh;
    private static Mesh triangleMesh;
    private static Mesh squareMesh;

    [SerializeField]
    private float bodyRadiusInGridUnits = DefaultBodyRadiusInGridUnits;

    [SerializeField]
    private float bodyDepth = 0.2f;

    [SerializeField]
    private float terminalScale = 0.22f;

    [SerializeField]
    private float gridSpacing = 1f;

    [SerializeField]
    private float terminalWorldZOffset = -0.05f;

    private static readonly Vector3[] FourWayTerminalDirections =
    {
        new Vector3(1f, 0f, 0f),
        new Vector3(0f, 1f, 0f),
        new Vector3(-1f, 0f, 0f),
        new Vector3(0f, -1f, 0f)
    };
    private static readonly Vector3[] ConverterTerminalOffsets =
    {
        new Vector3(-1f, 0f, 0f),
        new Vector3(1f, 0f, 0f)
    };
    private static readonly string[] ConverterTerminalNames =
    {
        "Terminal_Input",
        "Terminal_Output"
    };

    private CircuitElement circuitElement;

    private void Awake()
    {
        circuitElement = GetComponent<CircuitElement>();
        Apply();
    }

    private void OnValidate()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        circuitElement = GetComponent<CircuitElement>();
        if (circuitElement == null)
        {
            return;
        }

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
        EnsureBodyShape();
        ApplyBodyColor();
        EnsureTerminals();
        EnsureCollider();
    }

    private void ApplyBodyColor()
    {
        var rendererComponent = GetComponent<Renderer>();
        if (rendererComponent == null)
        {
            return;
        }

        var color = GetBodyColor(circuitElement.ElementType);

        rendererComponent.material.color = color;
    }

    private void EnsureTerminals()
    {
        var expectedNames = IsConverterType(circuitElement.ElementType)
            ? ConverterTerminalNames
            : BuildFourWayTerminalNames();
        for (var i = 0; i < expectedNames.Length; i++)
        {
            var terminalObject = transform.Find(expectedNames[i]);
            if (terminalObject == null)
            {
                var created = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                created.name = expectedNames[i];
                created.transform.SetParent(transform, false);
                terminalObject = created.transform;
            }

            var worldOffset = GetTerminalWorldOffset(i);
            terminalObject.localPosition = WorldOffsetToLocal(worldOffset);
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
            rendererComponent.material.color = TerminalColor;
            rendererComponent.sortingOrder = 10;
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

    private void EnsureBodyShape()
    {
        var meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }
        if (GetComponent<MeshRenderer>() == null)
        {
            gameObject.AddComponent<MeshRenderer>();
        }

        switch (GetShapeCategory(circuitElement.ElementType))
        {
            case ShapeCategory.Triangle:
                meshFilter.mesh = GetTriangleMesh();
                break;
            case ShapeCategory.Square:
                meshFilter.mesh = GetSquareMesh();
                break;
            default:
                meshFilter.mesh = GetCircleMesh();
                break;
        }
    }

    private void EnsureCollider()
    {
        var colliders = GetComponents<Collider>();
        for (var i = 0; i < colliders.Length; i++)
        {
            if (Application.isPlaying)
            {
                Destroy(colliders[i]);
            }
            else
            {
                DestroyImmediate(colliders[i]);
            }
        }

        var shape = GetShapeCategory(circuitElement.ElementType);
        if (shape == ShapeCategory.Circle)
        {
            var sphereCollider = gameObject.AddComponent<SphereCollider>();
            sphereCollider.center = Vector3.zero;
            sphereCollider.radius = 0.5f;
            return;
        }

        var meshCollider = gameObject.AddComponent<MeshCollider>();
        meshCollider.convex = false;
        meshCollider.sharedMesh = shape == ShapeCategory.Triangle ? GetTriangleMesh() : GetSquareMesh();
    }

    private Color GetBodyColor(CircuitElementType type)
    {
        if (IsConverterType(type))
        {
            return ConverterColor;
        }

        return IsReceiverType(type) ? ReceiverColor : GeneratorColor;
    }

    private Vector3 GetTerminalWorldOffset(int index)
    {
        var spacing = Mathf.Max(0.01f, gridSpacing);
        if (IsConverterType(circuitElement.ElementType))
        {
            var converterIndex = Mathf.Clamp(index, 0, ConverterTerminalOffsets.Length - 1);
            return ConverterTerminalOffsets[converterIndex] * spacing;
        }

        var radius = Mathf.Max(0.05f, bodyRadiusInGridUnits * spacing);
        var direction = FourWayTerminalDirections[Mathf.Clamp(index, 0, FourWayTerminalDirections.Length - 1)];
        return direction * radius;
    }

    private Vector3 WorldOffsetToLocal(Vector3 worldOffset)
    {
        var safeScale = new Vector3(
            Mathf.Max(0.0001f, transform.localScale.x),
            Mathf.Max(0.0001f, transform.localScale.y),
            Mathf.Max(0.0001f, transform.localScale.z));
        var localZ = terminalWorldZOffset / safeScale.z;
        return new Vector3(worldOffset.x / safeScale.x, worldOffset.y / safeScale.y, localZ);
    }

    private static string[] BuildFourWayTerminalNames()
    {
        return new[]
        {
            "Terminal_0",
            "Terminal_1",
            "Terminal_2",
            "Terminal_3"
        };
    }

    private static bool IsReceiverType(CircuitElementType type)
    {
        return type == CircuitElementType.SemiWaveReceiver
            || type == CircuitElementType.TriangleWaveReceiver
            || type == CircuitElementType.SquareWaveReceiver;
    }

    private static bool IsConverterType(CircuitElementType type)
    {
        return type == CircuitElementType.SemiWaveConverter
            || type == CircuitElementType.TriangleWaveConverter
            || type == CircuitElementType.SquareWaveConverter;
    }

    private static ShapeCategory GetShapeCategory(CircuitElementType type)
    {
        switch (type)
        {
            case CircuitElementType.TriangleWaveGenerator:
            case CircuitElementType.TriangleWaveReceiver:
            case CircuitElementType.TriangleWaveConverter:
                return ShapeCategory.Triangle;
            case CircuitElementType.SquareWaveGenerator:
            case CircuitElementType.SquareWaveReceiver:
            case CircuitElementType.SquareWaveConverter:
                return ShapeCategory.Square;
            default:
                return ShapeCategory.Circle;
        }
    }

    private static Mesh GetTriangleMesh()
    {
        if (triangleMesh != null)
        {
            return triangleMesh;
        }

        var height = Mathf.Sqrt(3f) * 0.5f;
        var verticalOffset = -0.08f;
        var mesh = new Mesh();
        mesh.name = "TriangleMesh";
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -height / 3f + verticalOffset, 0f),
            new Vector3(0.5f, -height / 3f + verticalOffset, 0f),
            new Vector3(0f, 2f * height / 3f + verticalOffset, 0f)
        };
        mesh.triangles = new[] { 0, 2, 1 };
        mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 1f)
        };
        mesh.RecalculateBounds();
        triangleMesh = mesh;
        return triangleMesh;
    }

    private static Mesh GetCircleMesh()
    {
        if (circleMesh != null)
        {
            return circleMesh;
        }

        var probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var filter = probe.GetComponent<MeshFilter>();
        circleMesh = filter != null ? filter.sharedMesh : null;
        if (Application.isPlaying)
        {
            Destroy(probe);
        }
        else
        {
            DestroyImmediate(probe);
        }

        return circleMesh;
    }

    private static Mesh GetSquareMesh()
    {
        if (squareMesh != null)
        {
            return squareMesh;
        }

        var mesh = new Mesh();
        mesh.name = "SquareMesh";
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f)
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        mesh.RecalculateBounds();
        squareMesh = mesh;
        return squareMesh;
    }

    private enum ShapeCategory
    {
        Circle,
        Triangle,
        Square
    }
}
