using UnityEngine;

public class BackpackScrollViewUI : MonoBehaviour
{
    [SerializeField]
    private float dragStartThresholdPixels = 8f;

    private Vector2 backpackScrollPosition;
    private GUIStyle backpackItemStyle;
    [SerializeField]
    private float backpackScreenWidthRatio = 0.22f;
    private Texture2D blueCircleIconTexture;
    private Texture2D redCircleIconTexture;
    private Texture2D whiteCircleIconTexture;
    private Texture2D blueTriangleIconTexture;
    private Texture2D redTriangleIconTexture;
    private Texture2D whiteTriangleIconTexture;
    private Texture2D blueSquareIconTexture;
    private Texture2D redSquareIconTexture;
    private Texture2D whiteSquareIconTexture;
    private Texture2D countPipTexture;
    private Texture2D holoSlotTexture;
    private Texture2D holoBackgroundTexture;
    private bool isBackpackPointerDown;
    private bool hasSpawnedDuringDrag;
    private int pointerDownEntryIndex = -1;
    private CircuitElementType pointerDownType;
    private Vector2 pointerDownMousePosition;

    private void OnGUI()
    {
        if (!UI.ShouldRenderBackpackUI)
        {
            ResetPointerState();
            return;
        }

        if (!UI.IsBackpackOpen)
        {
            ResetPointerState();
        }

        DrawBackpackScrollView();
    }

    private void DrawBackpackScrollView()
    {
        var panelRectScreen = GetBackpackPanelScreenRect();
        if (panelRectScreen.width < 20f || panelRectScreen.height < 20f)
        {
            return;
        }

        EnsureBackpackStyles();
        var oldColor = GUI.color;
        var visibility = Mathf.Clamp01(UI.BackpackOpenProgress);
        GUI.color = new Color(1f, 1f, 1f, visibility);
        GUI.DrawTexture(panelRectScreen, holoBackgroundTexture, ScaleMode.StretchToFill, true);
        var viewRect = new Rect(panelRectScreen.x + 4f, panelRectScreen.y + 4f, panelRectScreen.width - 8f, panelRectScreen.height - 8f);
        var entries = BackpackItemSpawner.GetInventoryEntries();
        var rowHeight = 130f;
        var contentHeight = Mathf.Max(viewRect.height, entries.Count * rowHeight + 4f);
        var contentRect = new Rect(0f, 0f, Mathf.Max(1f, viewRect.width), contentHeight);
        backpackScrollPosition = GUI.BeginScrollView(viewRect, backpackScrollPosition, contentRect, false, false);

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var rowRect = new Rect(2f, i * rowHeight + 2f, contentRect.width - 4f, rowHeight - 6f);
            GUI.Box(rowRect, GUIContent.none, backpackItemStyle);
            HandleBackpackRowInput(rowRect, i, entry.type);

            var iconSize = Mathf.Min(rowRect.height - 8f, rowRect.width - 12f);
            var iconRect = new Rect(
                rowRect.x + (rowRect.width - iconSize) * 0.5f,
                rowRect.y + (rowRect.height - iconSize) * 0.5f,
                iconSize,
                iconSize);
            GUI.DrawTexture(iconRect, GetInventoryIconTexture(entry.type), ScaleMode.ScaleToFit, true);
            DrawCountPips(rowRect, entry.count);
        }

        GUI.EndScrollView();
        GUI.color = oldColor;
    }

    private Rect GetBackpackPanelScreenRect()
    {
        var panelWidth = Mathf.Clamp(Screen.width * backpackScreenWidthRatio, 180f, Screen.width * 0.45f);
        var progress = Mathf.Clamp01(UI.BackpackOpenProgress);
        var panelHeight = Mathf.Max(0f, Screen.height * progress);
        return new Rect(0f, 0f, panelWidth, panelHeight);
    }

    private Vector3 GetFieldSpawnPosition()
    {
        if (Camera.main == null)
        {
            return Vector3.zero;
        }

        var spawnViewport = new Vector3(0.62f, 0.5f, -Camera.main.transform.position.z);
        var world = Camera.main.ViewportToWorldPoint(spawnViewport);
        world.z = 0f;
        return world;
    }

    private Vector3 GetMouseWorldPosition()
    {
        if (Camera.main == null)
        {
            return GetFieldSpawnPosition();
        }

        var mouse = Input.mousePosition;
        mouse.z = -Camera.main.transform.position.z;
        var world = Camera.main.ScreenToWorldPoint(mouse);
        world.z = 0f;
        return world;
    }

    private void HandleBackpackRowInput(Rect rowRect, int rowIndex, CircuitElementType type)
    {
        var currentEvent = Event.current;
        if (currentEvent == null || currentEvent.button != 0)
        {
            return;
        }

        if (currentEvent.type == EventType.MouseDown && rowRect.Contains(currentEvent.mousePosition))
        {
            isBackpackPointerDown = true;
            hasSpawnedDuringDrag = false;
            pointerDownEntryIndex = rowIndex;
            pointerDownType = type;
            pointerDownMousePosition = currentEvent.mousePosition;
            currentEvent.Use();
            return;
        }

        if (!isBackpackPointerDown)
        {
            return;
        }

        if (currentEvent.type == EventType.MouseDrag && !hasSpawnedDuringDrag)
        {
            var dragDelta = currentEvent.mousePosition - pointerDownMousePosition;
            var threshold = Mathf.Max(1f, dragStartThresholdPixels);
            if (dragDelta.sqrMagnitude >= threshold * threshold)
            {
                hasSpawnedDuringDrag = BackpackItemSpawner.TrySpawnFromInventory(
                    pointerDownType,
                    GetMouseWorldPosition(),
                    DraggablePlacedComponent.ExternalDragMode.HoldToRelease);
                isBackpackPointerDown = false;
                pointerDownEntryIndex = -1;
                currentEvent.Use();
            }

            return;
        }

        if (currentEvent.type != EventType.MouseUp)
        {
            return;
        }

        if (pointerDownEntryIndex != rowIndex)
        {
            return;
        }

        var isClickRelease = pointerDownEntryIndex == rowIndex && rowRect.Contains(currentEvent.mousePosition) && !hasSpawnedDuringDrag;
        if (isClickRelease)
        {
            BackpackItemSpawner.TrySpawnFromInventory(
                pointerDownType,
                GetMouseWorldPosition(),
                DraggablePlacedComponent.ExternalDragMode.StickyToCursor);
        }

        isBackpackPointerDown = false;
        hasSpawnedDuringDrag = false;
        pointerDownEntryIndex = -1;
        currentEvent.Use();
    }

    private void ResetPointerState()
    {
        isBackpackPointerDown = false;
        hasSpawnedDuringDrag = false;
        pointerDownEntryIndex = -1;
    }

    private void EnsureBackpackStyles()
    {
        if (backpackItemStyle != null)
        {
            return;
        }

        backpackItemStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 1
        };
        backpackItemStyle.normal.textColor = new Color(0f, 0f, 0f, 0f);
        backpackItemStyle.hover.textColor = new Color(0f, 0f, 0f, 0f);
        backpackItemStyle.active.textColor = new Color(0f, 0f, 0f, 0f);
        // 全息蓝半透明卡槽
        holoSlotTexture = CreateSolidTexture(new Color(0.15f, 0.45f, 1f, 0f));
        var holoSlotHover = CreateSolidTexture(new Color(0.25f, 0.6f, 1f, 0f));  // 完全透明， 不需要卡槽
        backpackItemStyle.normal.background = holoSlotTexture;
        backpackItemStyle.hover.background = holoSlotHover;
        backpackItemStyle.active.background = holoSlotHover;
        backpackItemStyle.focused.background = holoSlotTexture;
        // 全息蓝半透明背景
        holoBackgroundTexture = CreateSolidTexture(new Color(0.1f, 0.35f, 0.9f, 0.30f));
    }

    private Texture2D GetInventoryIconTexture(CircuitElementType type)
    {
        switch (type)
        {
            case CircuitElementType.SemiWaveReceiver:
                if (redCircleIconTexture == null)
                {
                    redCircleIconTexture = CreateCircleTexture(64, new Color(1f, 0.2f, 0.2f, 1f));
                }

                return redCircleIconTexture;
            case CircuitElementType.SemiWaveConverter:
                if (whiteCircleIconTexture == null)
                {
                    whiteCircleIconTexture = CreateCircleTexture(64, new Color(1f, 1f, 1f, 1f));
                }

                return whiteCircleIconTexture;
            case CircuitElementType.TriangleWaveGenerator:
                if (blueTriangleIconTexture == null)
                {
                    blueTriangleIconTexture = CreateTriangleTexture(64, new Color(0.2f, 0.45f, 1f, 1f));
                }

                return blueTriangleIconTexture;
            case CircuitElementType.TriangleWaveReceiver:
                if (redTriangleIconTexture == null)
                {
                    redTriangleIconTexture = CreateTriangleTexture(64, new Color(1f, 0.2f, 0.2f, 1f));
                }

                return redTriangleIconTexture;
            case CircuitElementType.TriangleWaveConverter:
                if (whiteTriangleIconTexture == null)
                {
                    whiteTriangleIconTexture = CreateTriangleTexture(64, new Color(1f, 1f, 1f, 1f));
                }

                return whiteTriangleIconTexture;
            case CircuitElementType.SquareWaveGenerator:
                if (blueSquareIconTexture == null)
                {
                    blueSquareIconTexture = CreateSquareTexture(64, new Color(0.2f, 0.45f, 1f, 1f));
                }

                return blueSquareIconTexture;
            case CircuitElementType.SquareWaveReceiver:
                if (redSquareIconTexture == null)
                {
                    redSquareIconTexture = CreateSquareTexture(64, new Color(1f, 0.2f, 0.2f, 1f));
                }

                return redSquareIconTexture;
            case CircuitElementType.SquareWaveConverter:
                if (whiteSquareIconTexture == null)
                {
                    whiteSquareIconTexture = CreateSquareTexture(64, new Color(1f, 1f, 1f, 1f));
                }

                return whiteSquareIconTexture;
            default:
                if (blueCircleIconTexture == null)
                {
                    blueCircleIconTexture = CreateCircleTexture(64, new Color(0.2f, 0.45f, 1f, 1f));
                }

                return blueCircleIconTexture;
        }
    }

    private void DrawCountPips(Rect rowRect, int count)
    {
        if (count <= 1)
        {
            return;
        }

        if (countPipTexture == null)
        {
            countPipTexture = CreateCircleTexture(16, new Color(1f, 0.95f, 0.3f, 1f));
        }

        var maxPips = 6;
        var pipCount = Mathf.Clamp(count - 1, 1, maxPips);
        var size = 8f;
        var spacing = 3f;
        var total = pipCount * size + (pipCount - 1) * spacing;
        var x = rowRect.xMax - total - 6f;
        var y = rowRect.yMax - size - 5f;
        for (var i = 0; i < pipCount; i++)
        {
            var pipRect = new Rect(x + i * (size + spacing), y, size, size);
            GUI.DrawTexture(pipRect, countPipTexture, ScaleMode.StretchToFill, true);
        }
    }

    private Texture2D CreateCircleTexture(int size, Color fillColor)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        var radius = (size - 1) * 0.5f;
        var center = new Vector2(radius, radius);
        var aaWidth = Mathf.Max(1.0f, size * 0.04f);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var delta = new Vector2(x, y) - center;
                var distance = delta.magnitude;
                if (distance > radius + 0.5f)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                var alpha = Mathf.SmoothStep(1f, 0f, (distance - (radius - aaWidth)) / aaWidth);
                var color = fillColor;
                color.a = fillColor.a * Mathf.Clamp01(alpha);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    private Texture2D CreateSquareTexture(int size, Color fillColor)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        var margin = Mathf.Max(2, Mathf.RoundToInt(size * 0.12f));
        var aaWidth = Mathf.Max(1.0f, size * 0.04f);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var inside = x >= margin && x < size - margin && y >= margin && y < size - margin;
                if (!inside)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                var edgeDist = Mathf.Min(
                    Mathf.Min(x - margin, size - margin - 1 - x),
                    Mathf.Min(y - margin, size - margin - 1 - y));
                var alpha = Mathf.SmoothStep(0f, 1f, edgeDist / aaWidth);
                var color = fillColor;
                color.a = fillColor.a * Mathf.Clamp01(alpha);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    private Texture2D CreateTriangleTexture(int size, Color fillColor)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        var p0 = new Vector2(size * 0.5f, size * 0.88f);
        var p1 = new Vector2(size * 0.12f, size * 0.18f);
        var p2 = new Vector2(size * 0.88f, size * 0.18f);
        var aaWidth = Mathf.Max(1.0f, size * 0.04f);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var point = new Vector2(x + 0.5f, y + 0.5f);
                if (!IsPointInTriangle(point, p0, p1, p2))
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                var edgeDistance = Mathf.Min(
                    DistancePointToLine(point, p0, p1),
                    Mathf.Min(DistancePointToLine(point, p1, p2), DistancePointToLine(point, p2, p0)));
                var alpha = Mathf.SmoothStep(0f, 1f, edgeDistance / aaWidth);
                var color = fillColor;
                color.a = fillColor.a * Mathf.Clamp01(alpha);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    private bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        var s1 = Sign(p, a, b);
        var s2 = Sign(p, b, c);
        var s3 = Sign(p, c, a);
        var hasNeg = (s1 < 0f) || (s2 < 0f) || (s3 < 0f);
        var hasPos = (s1 > 0f) || (s2 > 0f) || (s3 > 0f);
        return !(hasNeg && hasPos);
    }

    private float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    private float DistancePointToLine(Vector2 point, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var length = Mathf.Max(0.0001f, ab.magnitude);
        return Mathf.Abs(ab.y * point.x - ab.x * point.y + b.x * a.y - b.y * a.x) / length;
    }

    private Texture2D CreateSolidTexture(Color color)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 2; x++)
            {
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply(false, false);
        return texture;
    }
}
