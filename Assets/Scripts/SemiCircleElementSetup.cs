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
    private static readonly Color ConverterInputTerminalColor = new Color(0.2f, 0.45f, 1f, 1f);
    private static readonly Color ConverterOutputTerminalColor = new Color(1f, 0.2f, 0.2f, 1f);
    private const float DefaultTerminalScaleInGridUnits = 0.1f;
    private const float JunctionVisualScaleInGridUnits = 0.12f;
    private static Mesh circleMesh;
    private static Mesh triangleMesh;
    private static Mesh squareMesh;
    private static Material sharedVisualMaterial;
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

    [SerializeField]
    private float bodyRadiusInGridUnits = DefaultBodyRadiusInGridUnits;

    [SerializeField]
    private float bodyDepth = 0.2f;

    private float terminalScale = DefaultTerminalScaleInGridUnits;

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
        var useJunctionVisual = IsJunctionVisualMode();
        var lengthScale = circuitElement != null ? circuitElement.LengthScaleMultiplier : 1f;
        var widthScale = circuitElement != null ? circuitElement.WidthScaleMultiplier : 1f;
        var bodyScaleX = useJunctionVisual
            ? Mathf.Max(0.06f, JunctionVisualScaleInGridUnits * spacing)
            : Mathf.Max(0.05f, diameter * lengthScale);
        var bodyScaleY = useJunctionVisual
            ? Mathf.Max(0.06f, JunctionVisualScaleInGridUnits * spacing)
            : Mathf.Max(0.05f, diameter * widthScale);
        transform.localScale = new Vector3(
            bodyScaleX,
            bodyScaleY,
            Mathf.Max(0.01f, bodyDepth));
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
        ApplyRendererColor(rendererComponent, color);
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

            SetTerminalVisual(terminalObject.gameObject, expectedNames[i]);
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

    private void SetTerminalVisual(GameObject terminalObject, string terminalName)
    {
        var rendererComponent = terminalObject.GetComponent<Renderer>();
        if (rendererComponent != null)
        {
            ApplyRendererColor(rendererComponent, GetTerminalColor(terminalName));
            rendererComponent.sortingOrder = 10;
        }
    }

    private static void ApplyRendererColor(Renderer rendererComponent, Color color)
    {
        if (rendererComponent == null)
        {
            return;
        }

        var material = GetOrCreateSharedVisualMaterial();
        if (material != null)
        {
            rendererComponent.sharedMaterial = material;
        }

        var propertyBlock = new MaterialPropertyBlock();
        rendererComponent.GetPropertyBlock(propertyBlock);
        if (material != null && material.HasProperty(ColorPropertyId))
        {
            propertyBlock.SetColor(ColorPropertyId, color);
        }
        else
        {
            propertyBlock.SetColor(BaseColorPropertyId, color);
        }
        rendererComponent.SetPropertyBlock(propertyBlock);
    }

    private static Material GetOrCreateSharedVisualMaterial()
    {
        if (sharedVisualMaterial != null)
        {
            return sharedVisualMaterial;
        }

        var shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            return null;
        }

        sharedVisualMaterial = new Material(shader);
        return sharedVisualMaterial;
    }

    private Color GetTerminalColor(string terminalName)
    {
        if (!IsConverterType(circuitElement.ElementType))
        {
            return TerminalColor;
        }

        if (terminalName == "Terminal_Input")
        {
            return ConverterInputTerminalColor;
        }

        if (terminalName == "Terminal_Output")
        {
            return ConverterOutputTerminalColor;
        }

        return TerminalColor;
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

        var shape = IsJunctionVisualMode() ? ShapeCategory.Circle : GetShapeCategory(circuitElement.ElementType);
        switch (shape)
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

        var shape = IsJunctionVisualMode() ? ShapeCategory.Circle : GetShapeCategory(circuitElement.ElementType);
        if (shape == ShapeCategory.Circle)
        {
            var circleCollider = gameObject.AddComponent<MeshCollider>();
            circleCollider.convex = false;
            circleCollider.sharedMesh = GetCircleMesh();
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
        var halfExtents = GetHalfExtentsInWorld();
        if (IsConverterType(circuitElement.ElementType))
        {
            var converterIndex = Mathf.Clamp(index, 0, ConverterTerminalOffsets.Length - 1);
            var x = converterIndex == 0 ? -halfExtents.x : halfExtents.x;
            return new Vector3(x, 0f, 0f);
        }

        var directionIndex = Mathf.Clamp(index, 0, FourWayTerminalDirections.Length - 1);
        switch (directionIndex)
        {
            case 0:
                return new Vector3(halfExtents.x, 0f, 0f);
            case 1:
                return new Vector3(0f, halfExtents.y, 0f);
            case 2:
                return new Vector3(-halfExtents.x, 0f, 0f);
            default:
                return new Vector3(0f, -halfExtents.y, 0f);
        }
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

    private Vector2 GetHalfExtentsInWorld()
    {
        var spacing = Mathf.Max(0.01f, gridSpacing);
        var baseExtent = Mathf.Max(0.05f, bodyRadiusInGridUnits * spacing);
        var lengthScale = circuitElement != null ? circuitElement.LengthScaleMultiplier : 1f;
        var widthScale = circuitElement != null ? circuitElement.WidthScaleMultiplier : 1f;
        return new Vector2(baseExtent * lengthScale, baseExtent * widthScale);
    }

    private bool IsJunctionVisualMode()
    {
        return circuitElement != null && (circuitElement.Length <= 0 || circuitElement.Width <= 0);
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

        var mesh = new Mesh();
        mesh.name = "TriangleMesh";
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0f, 0.5f, 0f)
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
