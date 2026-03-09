using UnityEngine;

public class GridRenderer2D : MonoBehaviour
{
    [SerializeField]
    private Camera targetCamera;

    [SerializeField]
    private float spacing = 1f;

    [SerializeField]
    private float backpackViewportWidth = 0.2f;

    [SerializeField]
    private float lineWidth = 0.02f;

    [SerializeField]
    private Color gridColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

    private Material gridMaterial;

    private void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        gridMaterial = new Material(Shader.Find("Sprites/Default"));
        gridMaterial.color = gridColor;

        BuildGrid();
    }

    private void BuildGrid()
    {
        var halfHeight = targetCamera.orthographicSize;
        var halfWidth = halfHeight * targetCamera.aspect;
        var left = -halfWidth;
        var right = halfWidth;
        var bottom = -halfHeight;
        var top = halfHeight;
        var fieldLeft = Mathf.Lerp(left, right, backpackViewportWidth);

        for (var x = Mathf.Ceil(fieldLeft / spacing) * spacing; x <= right; x += spacing)
        {
            CreateLine(new Vector3(x, bottom, 0f), new Vector3(x, top, 0f));
        }

        for (var y = Mathf.Ceil(bottom / spacing) * spacing; y <= top; y += spacing)
        {
            CreateLine(new Vector3(fieldLeft, y, 0f), new Vector3(right, y, 0f));
        }
    }

    private void CreateLine(Vector3 start, Vector3 end)
    {
        var lineObject = new GameObject("GridLine");
        lineObject.transform.SetParent(transform);
        var line = lineObject.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.material = gridMaterial;
        line.startColor = gridColor;
        line.endColor = gridColor;
        line.sortingOrder = -10;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }
}
