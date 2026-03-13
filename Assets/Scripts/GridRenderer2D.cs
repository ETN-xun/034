using UnityEngine;
using System.Collections.Generic;

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

    [SerializeField]
    private float zoomSpeed = 1.2f;

    [SerializeField]
    private float minOrthographicSize = 2f;

    [SerializeField]
    private float maxOrthographicSize = 20f;

    [SerializeField]
    private int maxLinesPerAxis = 240;

    [SerializeField]
    private float minLineWidthPixels = 1f;

    private Material gridMaterial;
    private readonly List<LineRenderer> linePool = new List<LineRenderer>();
    private Vector3 lastMiddleMouseWorldPosition;
    private bool isMiddleMouseDragging;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        gridMaterial = new Material(Shader.Find("Sprites/Default"));
        gridMaterial.color = gridColor;
    }

    private void Update()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                return;
            }
        }

        HandleZoomInput();
        HandleMiddleMousePan();
        RenderVisibleGrid();
    }

    private void OnDestroy()
    {
        if (gridMaterial != null)
        {
            Destroy(gridMaterial);
        }
    }

    private void HandleZoomInput()
    {
        var scrollDelta = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scrollDelta) <= Mathf.Epsilon)
        {
            return;
        }

        var beforeZoomMouseWorld = GetMouseWorldPosition();
        var zoomStep = scrollDelta * zoomSpeed;
        targetCamera.orthographicSize = Mathf.Clamp(
            targetCamera.orthographicSize - zoomStep,
            minOrthographicSize,
            maxOrthographicSize);

        var afterZoomMouseWorld = GetMouseWorldPosition();
        var cameraDelta = beforeZoomMouseWorld - afterZoomMouseWorld;
        var cameraPosition = targetCamera.transform.position + cameraDelta;
        cameraPosition.z = targetCamera.transform.position.z;
        targetCamera.transform.position = cameraPosition;
    }

    private void HandleMiddleMousePan()
    {
        if (Input.GetMouseButtonDown(2))
        {
            isMiddleMouseDragging = true;
            lastMiddleMouseWorldPosition = GetMouseWorldPosition();
            return;
        }

        if (!isMiddleMouseDragging || !Input.GetMouseButton(2))
        {
            if (Input.GetMouseButtonUp(2))
            {
                isMiddleMouseDragging = false;
            }

            return;
        }

        var currentMouseWorldPosition = GetMouseWorldPosition();
        var cameraDelta = lastMiddleMouseWorldPosition - currentMouseWorldPosition;
        var cameraPosition = targetCamera.transform.position + cameraDelta;
        cameraPosition.z = targetCamera.transform.position.z;
        targetCamera.transform.position = cameraPosition;
        lastMiddleMouseWorldPosition = GetMouseWorldPosition();
    }

    private void RenderVisibleGrid()
    {
        var baseSpacing = Mathf.Max(0.01f, spacing);
        var cameraZDistance = -targetCamera.transform.position.z;
        var leftBottom = targetCamera.ViewportToWorldPoint(new Vector3(0f, 0f, cameraZDistance));
        var rightTop = targetCamera.ViewportToWorldPoint(new Vector3(1f, 1f, cameraZDistance));
        var minX = Mathf.Min(leftBottom.x, rightTop.x);
        var maxX = Mathf.Max(leftBottom.x, rightTop.x);
        var minY = Mathf.Min(leftBottom.y, rightTop.y);
        var maxY = Mathf.Max(leftBottom.y, rightTop.y);
        var worldWidth = Mathf.Max(0f, maxX - minX);
        var worldHeight = Mathf.Max(0f, maxY - minY);
        var estimatedVerticalAtBase = Mathf.CeilToInt(worldWidth / baseSpacing) + 1;
        var estimatedHorizontalAtBase = Mathf.CeilToInt(worldHeight / baseSpacing) + 1;
        var safeMaxLinesPerAxis = Mathf.Max(16, maxLinesPerAxis);
        var dominantEstimatedCount = Mathf.Max(estimatedVerticalAtBase, estimatedHorizontalAtBase);
        var spacingMultiplier = Mathf.Max(1, Mathf.CeilToInt((float)dominantEstimatedCount / safeMaxLinesPerAxis));
        var spacingValue = baseSpacing * spacingMultiplier;

        var epsilon = spacingValue * 0.0001f;
        var startX = Mathf.Floor((minX - epsilon) / spacingValue) * spacingValue;
        var endX = Mathf.Ceil((maxX + epsilon) / spacingValue) * spacingValue;
        var startY = Mathf.Floor((minY - epsilon) / spacingValue) * spacingValue;
        var endY = Mathf.Ceil((maxY + epsilon) / spacingValue) * spacingValue;
        var verticalLineCount = Mathf.Max(0, Mathf.CeilToInt((endX - startX) / spacingValue) + 1);
        var horizontalLineCount = Mathf.Max(0, Mathf.CeilToInt((endY - startY) / spacingValue) + 1);
        var centerVerticalVisible = minX <= 0f && maxX >= 0f;
        var centerHorizontalVisible = minY <= 0f && maxY >= 0f;
        var centerTolerance = Mathf.Max(0.0001f, spacingValue * 0.001f);
        var requiredLineCount = verticalLineCount + horizontalLineCount;
        if (centerVerticalVisible)
        {
            requiredLineCount++;
        }

        if (centerHorizontalVisible)
        {
            requiredLineCount++;
        }

        var worldUnitsPerPixel = (targetCamera.orthographicSize * 2f) / Mathf.Max(1, Screen.height);
        var effectiveLineWidth = Mathf.Max(lineWidth, worldUnitsPerPixel * Mathf.Max(0.1f, minLineWidthPixels));
        EnsureLinePoolCapacity(requiredLineCount);

        var lineIndex = 0;
        for (var i = 0; i < verticalLineCount; i++)
        {
            var x = startX + i * spacingValue;
            if (Mathf.Abs(x) <= centerTolerance)
            {
                continue;
            }

            ConfigureLine(linePool[lineIndex], new Vector3(x, minY, 0f), new Vector3(x, maxY, 0f), effectiveLineWidth);
            lineIndex++;
        }

        for (var i = 0; i < horizontalLineCount; i++)
        {
            var y = startY + i * spacingValue;
            if (Mathf.Abs(y) <= centerTolerance)
            {
                continue;
            }

            ConfigureLine(linePool[lineIndex], new Vector3(minX, y, 0f), new Vector3(maxX, y, 0f), effectiveLineWidth);
            lineIndex++;
        }

        var centerLineWidth = effectiveLineWidth * 1.25f;
        if (centerVerticalVisible)
        {
            ConfigureLine(linePool[lineIndex], new Vector3(0f, minY, 0f), new Vector3(0f, maxY, 0f), centerLineWidth);
            lineIndex++;
        }

        if (centerHorizontalVisible)
        {
            ConfigureLine(linePool[lineIndex], new Vector3(minX, 0f, 0f), new Vector3(maxX, 0f, 0f), centerLineWidth);
            lineIndex++;
        }

        for (var i = lineIndex; i < linePool.Count; i++)
        {
            if (linePool[i] != null)
            {
                linePool[i].gameObject.SetActive(false);
            }
        }
    }

    private void EnsureLinePoolCapacity(int requiredCount)
    {
        while (linePool.Count < requiredCount)
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
            linePool.Add(line);
        }
    }

    private void ConfigureLine(LineRenderer line, Vector3 start, Vector3 end, float width)
    {
        if (line == null)
        {
            return;
        }

        line.gameObject.SetActive(true);
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = gridColor;
        line.endColor = gridColor;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    private Vector3 GetMouseWorldPosition()
    {
        var mouse = Input.mousePosition;
        mouse.z = -targetCamera.transform.position.z;
        var world = targetCamera.ScreenToWorldPoint(mouse);
        world.z = 0f;
        return world;
    }
}
