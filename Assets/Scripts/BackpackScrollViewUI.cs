using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BackpackScrollViewUI : MonoBehaviour
{
    [SerializeField]
    private float dragStartThresholdPixels = 8f;

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
    private Sprite blueCircleIconSprite;
    private Sprite redCircleIconSprite;
    private Sprite whiteCircleIconSprite;
    private Sprite blueTriangleIconSprite;
    private Sprite redTriangleIconSprite;
    private Sprite whiteTriangleIconSprite;
    private Sprite blueSquareIconSprite;
    private Sprite redSquareIconSprite;
    private Sprite whiteSquareIconSprite;
    private Sprite countPipSprite;
    private Sprite slotBackgroundSprite;

    private Canvas backpackCanvas;
    private RectTransform canvasRectTransform;
    private RectTransform panelRectTransform;
    private RectTransform viewportRectTransform;
    private RectTransform contentRectTransform;
    private ScrollRect scrollRect;
    private Image panelImage;
    private bool uiInitialized;
    private bool inventoryDirty = true;

    private readonly List<SlotView> slotViews = new List<SlotView>();
    private bool isBackpackPointerDown;
    private bool hasSpawnedDuringDrag;
    private SlotView pointerDownSlot;
    private Vector2 pointerDownMousePosition;

    private void Awake()
    {
        EnsureUiInitialized();
    }

    private void OnEnable()
    {
        BackpackItemSpawner.InventoryChanged += OnInventoryChanged;
        inventoryDirty = true;
    }

    private void OnDisable()
    {
        BackpackItemSpawner.InventoryChanged -= OnInventoryChanged;
        ResetPointerState();
    }

    private void OnDestroy()
    {
        BackpackItemSpawner.InventoryChanged -= OnInventoryChanged;
    }

    private void Update()
    {
        EnsureUiInitialized();
        UpdatePanelLayout();
        if (inventoryDirty)
        {
            RebuildSlots();
            inventoryDirty = false;
        }
    }

    private void OnInventoryChanged()
    {
        inventoryDirty = true;
    }

    private void EnsureUiInitialized()
    {
        if (uiInitialized)
        {
            return;
        }

        EnsureEventSystemExists();
        var canvasObject = new GameObject("BackpackRuntimeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        backpackCanvas = canvasObject.GetComponent<Canvas>();
        backpackCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        backpackCanvas.sortingOrder = 80;
        canvasRectTransform = canvasObject.GetComponent<RectTransform>();
        var canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.matchWidthOrHeight = 0f;

        var panelObject = new GameObject("BackpackPanel", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        panelObject.transform.SetParent(canvasObject.transform, false);
        panelRectTransform = panelObject.GetComponent<RectTransform>();
        panelRectTransform.anchorMin = new Vector2(0f, 1f);
        panelRectTransform.anchorMax = new Vector2(0f, 1f);
        panelRectTransform.pivot = new Vector2(0f, 1f);
        panelRectTransform.anchoredPosition = Vector2.zero;
        panelImage = panelObject.GetComponent<Image>();
        panelImage.type = Image.Type.Sliced;
        panelImage.color = new Color(0.1f, 0.35f, 0.9f, 0.3f);

        var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportObject.transform.SetParent(panelObject.transform, false);
        viewportRectTransform = viewportObject.GetComponent<RectTransform>();
        viewportRectTransform.anchorMin = Vector2.zero;
        viewportRectTransform.anchorMax = Vector2.one;
        viewportRectTransform.offsetMin = new Vector2(4f, 4f);
        viewportRectTransform.offsetMax = new Vector2(-4f, -4f);
        var viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        var mask = viewportObject.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        var contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        contentRectTransform = contentObject.GetComponent<RectTransform>();
        contentRectTransform.anchorMin = new Vector2(0f, 1f);
        contentRectTransform.anchorMax = new Vector2(1f, 1f);
        contentRectTransform.pivot = new Vector2(0.5f, 1f);
        contentRectTransform.anchoredPosition = Vector2.zero;
        contentRectTransform.sizeDelta = new Vector2(0f, 0f);
        var verticalLayout = contentObject.GetComponent<VerticalLayoutGroup>();
        verticalLayout.padding = new RectOffset(2, 2, 2, 2);
        verticalLayout.spacing = 4f;
        verticalLayout.childForceExpandWidth = true;
        verticalLayout.childForceExpandHeight = false;
        verticalLayout.childControlWidth = true;
        verticalLayout.childControlHeight = true;
        var fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect = panelObject.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRectTransform;
        scrollRect.content = contentRectTransform;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 25f;

        slotBackgroundSprite = CreateSprite(CreateSolidTexture(new Color(0.2f, 0.5f, 1f, 0.12f)));
        uiInitialized = true;
    }

    private void EnsureEventSystemExists()
    {
        var eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem != null)
        {
            return;
        }

        var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystemObject.transform.SetParent(transform.root, false);
    }

    private void UpdatePanelLayout()
    {
        if (backpackCanvas == null || panelRectTransform == null || canvasRectTransform == null)
        {
            return;
        }

        var shouldRender = UI.ShouldRenderBackpackUI;
        if (backpackCanvas.enabled != shouldRender)
        {
            backpackCanvas.enabled = shouldRender;
        }

        if (!shouldRender)
        {
            ResetPointerState();
            return;
        }

        if (!UI.IsBackpackOpen)
        {
            ResetPointerState();
        }

        var canvasSize = canvasRectTransform.rect.size;
        var baseWidth = Mathf.Max(1f, canvasSize.x);
        var baseHeight = Mathf.Max(1f, canvasSize.y);
        var panelWidth = Mathf.Clamp(baseWidth * backpackScreenWidthRatio, 180f, baseWidth * 0.45f);
        var progress = Mathf.Clamp01(UI.BackpackOpenProgress);
        var panelHeight = Mathf.Max(0f, baseHeight * progress);
        panelRectTransform.sizeDelta = new Vector2(panelWidth, panelHeight);
    }

    private void RebuildSlots()
    {
        if (contentRectTransform == null)
        {
            return;
        }

        var entries = BackpackItemSpawner.GetInventoryEntries();
        EnsureSlotPool(entries.Count);
        for (var i = 0; i < slotViews.Count; i++)
        {
            var active = i < entries.Count;
            slotViews[i].Root.gameObject.SetActive(active);
            if (!active)
            {
                continue;
            }

            ConfigureSlot(slotViews[i], entries[i]);
        }
    }

    private void EnsureSlotPool(int count)
    {
        while (slotViews.Count < count)
        {
            slotViews.Add(CreateSlotView(slotViews.Count));
        }
    }

    private SlotView CreateSlotView(int index)
    {
        var slotObject = new GameObject($"Slot_{index}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(BackpackSlotPointerHandler));
        slotObject.transform.SetParent(contentRectTransform, false);
        var slotRect = slotObject.GetComponent<RectTransform>();
        slotRect.anchorMin = new Vector2(0f, 1f);
        slotRect.anchorMax = new Vector2(1f, 1f);
        slotRect.pivot = new Vector2(0.5f, 1f);
        slotRect.sizeDelta = new Vector2(0f, 124f);
        var slotImage = slotObject.GetComponent<Image>();
        slotImage.sprite = slotBackgroundSprite;
        slotImage.type = Image.Type.Sliced;
        slotImage.color = Color.white;

        var button = slotObject.GetComponent<Button>();
        button.targetGraphic = slotImage;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
        colors.pressedColor = new Color(0.8f, 0.9f, 1f, 1f);
        button.colors = colors;

        var layoutElement = slotObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 124f;
        layoutElement.minHeight = 124f;

        var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(slotObject.transform, false);
        var iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(98f, 98f);
        var iconImage = iconObject.GetComponent<Image>();
        iconImage.preserveAspect = true;

        var pips = new List<Image>();
        var pipsObject = new GameObject("Pips", typeof(RectTransform));
        pipsObject.transform.SetParent(slotObject.transform, false);
        var pipsRect = pipsObject.GetComponent<RectTransform>();
        pipsRect.anchorMin = new Vector2(1f, 0f);
        pipsRect.anchorMax = new Vector2(1f, 0f);
        pipsRect.pivot = new Vector2(1f, 0f);
        pipsRect.anchoredPosition = new Vector2(-8f, 8f);
        pipsRect.sizeDelta = new Vector2(90f, 14f);

        for (var i = 0; i < 6; i++)
        {
            var pipObject = new GameObject($"Pip_{i}", typeof(RectTransform), typeof(Image));
            pipObject.transform.SetParent(pipsObject.transform, false);
            var pipRect = pipObject.GetComponent<RectTransform>();
            pipRect.anchorMin = new Vector2(1f, 0f);
            pipRect.anchorMax = new Vector2(1f, 0f);
            pipRect.pivot = new Vector2(1f, 0f);
            pipRect.sizeDelta = new Vector2(8f, 8f);
            pipRect.anchoredPosition = new Vector2(-i * 11f, 0f);
            var pipImage = pipObject.GetComponent<Image>();
            pipImage.enabled = false;
            pips.Add(pipImage);
        }

        var slotView = new SlotView
        {
            Root = slotRect,
            IconImage = iconImage,
            PipImages = pips
        };

        var pointerHandler = slotObject.GetComponent<BackpackSlotPointerHandler>();
        pointerHandler.OnPointerDownEvent = data => OnSlotPointerDown(slotView, data);
        pointerHandler.OnDragEvent = data => OnSlotDrag(slotView, data);
        pointerHandler.OnPointerUpEvent = data => OnSlotPointerUp(slotView, data);
        return slotView;
    }

    private void ConfigureSlot(SlotView slotView, BackpackItemSpawner.InventoryEntry entry)
    {
        slotView.Entry = entry;
        slotView.IconImage.sprite = GetInventoryIconSprite(entry.Type);
        var pipCount = Mathf.Clamp(entry.Count - 1, 0, 6);
        var pipSprite = GetCountPipSprite();
        for (var i = 0; i < slotView.PipImages.Count; i++)
        {
            var pip = slotView.PipImages[i];
            var enabled = i < pipCount;
            pip.enabled = enabled;
            if (enabled)
            {
                pip.sprite = pipSprite;
                pip.color = Color.white;
            }
        }
    }

    private void OnSlotPointerDown(SlotView slotView, PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        isBackpackPointerDown = true;
        hasSpawnedDuringDrag = false;
        pointerDownSlot = slotView;
        pointerDownMousePosition = eventData.position;
    }

    private void OnSlotDrag(SlotView slotView, PointerEventData eventData)
    {
        if (!isBackpackPointerDown || pointerDownSlot != slotView || hasSpawnedDuringDrag)
        {
            return;
        }

        var threshold = Mathf.Max(1f, dragStartThresholdPixels);
        var dragDelta = eventData.position - pointerDownMousePosition;
        if (dragDelta.sqrMagnitude < threshold * threshold)
        {
            return;
        }

        hasSpawnedDuringDrag = BackpackItemSpawner.TrySpawnFromInventory(
            slotView.Entry.Type,
            slotView.Entry.Length,
            slotView.Entry.Width,
            GetMouseWorldPosition(),
            DraggablePlacedComponent.ExternalDragMode.HoldToRelease);
        isBackpackPointerDown = false;
        pointerDownSlot = null;
    }

    private void OnSlotPointerUp(SlotView slotView, PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        if (!isBackpackPointerDown || pointerDownSlot != slotView || hasSpawnedDuringDrag)
        {
            ResetPointerState();
            return;
        }

        BackpackItemSpawner.TrySpawnFromInventory(
            slotView.Entry.Type,
            slotView.Entry.Length,
            slotView.Entry.Width,
            GetMouseWorldPosition(),
            DraggablePlacedComponent.ExternalDragMode.StickyToCursor);
        ResetPointerState();
    }

    private void ResetPointerState()
    {
        isBackpackPointerDown = false;
        hasSpawnedDuringDrag = false;
        pointerDownSlot = null;
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

    private Sprite GetInventoryIconSprite(CircuitElementType type)
    {
        switch (type)
        {
            case CircuitElementType.SemiWaveReceiver:
                if (redCircleIconSprite == null)
                {
                    redCircleIconTexture = CreateCircleTexture(64, new Color(1f, 0.2f, 0.2f, 1f));
                    redCircleIconSprite = CreateSprite(redCircleIconTexture);
                }

                return redCircleIconSprite;
            case CircuitElementType.SemiWaveConverter:
                if (whiteCircleIconSprite == null)
                {
                    whiteCircleIconTexture = CreateCircleTexture(64, new Color(1f, 1f, 1f, 1f));
                    whiteCircleIconSprite = CreateSprite(whiteCircleIconTexture);
                }

                return whiteCircleIconSprite;
            case CircuitElementType.TriangleWaveGenerator:
                if (blueTriangleIconSprite == null)
                {
                    blueTriangleIconTexture = CreateTriangleTexture(64, new Color(0.2f, 0.45f, 1f, 1f));
                    blueTriangleIconSprite = CreateSprite(blueTriangleIconTexture);
                }

                return blueTriangleIconSprite;
            case CircuitElementType.TriangleWaveReceiver:
                if (redTriangleIconSprite == null)
                {
                    redTriangleIconTexture = CreateTriangleTexture(64, new Color(1f, 0.2f, 0.2f, 1f));
                    redTriangleIconSprite = CreateSprite(redTriangleIconTexture);
                }

                return redTriangleIconSprite;
            case CircuitElementType.TriangleWaveConverter:
                if (whiteTriangleIconSprite == null)
                {
                    whiteTriangleIconTexture = CreateTriangleTexture(64, new Color(1f, 1f, 1f, 1f));
                    whiteTriangleIconSprite = CreateSprite(whiteTriangleIconTexture);
                }

                return whiteTriangleIconSprite;
            case CircuitElementType.SquareWaveGenerator:
                if (blueSquareIconSprite == null)
                {
                    blueSquareIconTexture = CreateSquareTexture(64, new Color(0.2f, 0.45f, 1f, 1f));
                    blueSquareIconSprite = CreateSprite(blueSquareIconTexture);
                }

                return blueSquareIconSprite;
            case CircuitElementType.SquareWaveReceiver:
                if (redSquareIconSprite == null)
                {
                    redSquareIconTexture = CreateSquareTexture(64, new Color(1f, 0.2f, 0.2f, 1f));
                    redSquareIconSprite = CreateSprite(redSquareIconTexture);
                }

                return redSquareIconSprite;
            case CircuitElementType.SquareWaveConverter:
                if (whiteSquareIconSprite == null)
                {
                    whiteSquareIconTexture = CreateSquareTexture(64, new Color(1f, 1f, 1f, 1f));
                    whiteSquareIconSprite = CreateSprite(whiteSquareIconTexture);
                }

                return whiteSquareIconSprite;
            default:
                if (blueCircleIconSprite == null)
                {
                    blueCircleIconTexture = CreateCircleTexture(64, new Color(0.2f, 0.45f, 1f, 1f));
                    blueCircleIconSprite = CreateSprite(blueCircleIconTexture);
                }

                return blueCircleIconSprite;
        }
    }

    private Sprite GetCountPipSprite()
    {
        if (countPipSprite != null)
        {
            return countPipSprite;
        }

        countPipTexture = CreateCircleTexture(16, new Color(1f, 0.95f, 0.3f, 1f));
        countPipSprite = CreateSprite(countPipTexture);
        return countPipSprite;
    }

    private static Sprite CreateSprite(Texture2D texture)
    {
        if (texture == null)
        {
            return null;
        }

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
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

    private static bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        var s1 = Sign(p, a, b);
        var s2 = Sign(p, b, c);
        var s3 = Sign(p, c, a);
        var hasNeg = (s1 < 0f) || (s2 < 0f) || (s3 < 0f);
        var hasPos = (s1 > 0f) || (s2 > 0f) || (s3 > 0f);
        return !(hasNeg && hasPos);
    }

    private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    private static float DistancePointToLine(Vector2 point, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var length = Mathf.Max(0.0001f, ab.magnitude);
        return Mathf.Abs(ab.y * point.x - ab.x * point.y + b.x * a.y - b.y * a.x) / length;
    }

    private static Texture2D CreateSolidTexture(Color color)
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

    private class SlotView
    {
        public RectTransform Root;
        public Image IconImage;
        public List<Image> PipImages;
        public BackpackItemSpawner.InventoryEntry Entry;
    }
}

public class BackpackSlotPointerHandler : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public System.Action<PointerEventData> OnPointerDownEvent;
    public System.Action<PointerEventData> OnDragEvent;
    public System.Action<PointerEventData> OnPointerUpEvent;

    public void OnPointerDown(PointerEventData eventData)
    {
        OnPointerDownEvent?.Invoke(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        OnDragEvent?.Invoke(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        OnPointerUpEvent?.Invoke(eventData);
    }
}
