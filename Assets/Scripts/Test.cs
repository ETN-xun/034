using UnityEngine;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Test : MonoBehaviour
{
    public static Test Instance { get; private set; }
    public static bool IsGmPanelVisible => Instance != null && Instance.showGmPanel;

    [SerializeField]
    private CircuitElementType addType = CircuitElementType.SemiWaveGenerator;

    [SerializeField]
    private int addAmount = 1;

    [SerializeField]
    private string levelPrefabName = "Level1";

    [SerializeField]
    private int startLevelIndex = 1;

    [SerializeField]
    private float winTolerance = 0.08f;

    [SerializeField]
    private int winSampleCount = 24;

    [SerializeField]
    private bool showGmPanel;

    [SerializeField]
    private Rect panelRect = new Rect(40f, 40f, 420f, 240f);

    [SerializeField]
    private Vector2 minPanelSize = new Vector2(340f, 200f);

    [SerializeField]
    private Vector2 maxPanelSize = new Vector2(900f, 650f);

    [SerializeField]
    private bool showTypeDropdown;

    private string addAmountText = "1";
    private string statusText = string.Empty;
    private bool resizingPanel;
    private Vector2 resizeStartMouse;
    private Vector2 resizeStartSize;
    private bool waitingOverwriteConfirm;
    private string pendingOverwritePath = string.Empty;
    private int currentLevelIndex;
    private float nextWinCheckTime;
    private readonly List<WireConnection> receiverWires = new List<WireConnection>();
    private Vector2 backpackScrollPosition;
    private GUIStyle backpackItemStyle;
    private Renderer backpackPanelRenderer;
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
    private Texture2D whiteButtonTexture;
    private const float ResizeHandleSize = 18f;
    private const float WinCheckInterval = 0.2f;
    private const string LevelPrefix = "Level";
    private const string LevelResourceFolder = "Prefabs";

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        currentLevelIndex = Mathf.Max(1, startLevelIndex);
        LoadLevel(currentLevelIndex);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F10))
        {
            showGmPanel = !showGmPanel;
        }

        HandleResizeInput();
        if (Time.time < nextWinCheckTime)
        {
            return;
        }

        nextWinCheckTime = Time.time + WinCheckInterval;
        if (IsCurrentLevelWon())
        {
            var nextLevel = currentLevelIndex + 1;
            LoadLevel(nextLevel);
            statusText = $"关卡 {BuildLevelName(currentLevelIndex - 1)} 已获胜，已切换到 {BuildLevelName(currentLevelIndex)}";
        }
    }

    [ContextMenu("GM/向背包添加配置数量元器件")]
    public void AddConfiguredItemsToBackpack()
    {
        var changed = BackpackItemSpawner.AddInventoryToType(addType, addAmount);
        statusText = $"已添加：{addType} x {addAmount}，影响槽位：{changed}";
        Debug.Log($"GM Add Backpack Items: type={addType}, amount={addAmount}, affectedSpawners={changed}");
    }

    public void AddItemsToBackpack(CircuitElementType type, int amount)
    {
        var changed = BackpackItemSpawner.AddInventoryToType(type, amount);
        statusText = $"已添加：{type} x {amount}，影响槽位：{changed}";
        Debug.Log($"GM Add Backpack Items: type={type}, amount={amount}, affectedSpawners={changed}");
    }

    [ContextMenu("GM/保存当前关卡为预制体")]
    public void SaveCurrentLevelAsPrefab()
    {
#if UNITY_EDITOR
        var path = BuildLevelPrefabAssetPath();
        if (string.IsNullOrEmpty(path))
        {
            statusText = "关卡名为空";
            return;
        }

        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
        {
            var parts = folder.Replace("\\", "/").Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        BackpackItemSpawner.EnsureSceneObjectsUnderLevelRoot();
        var levelRoot = BackpackItemSpawner.GetOrCreateLevelRoot();
        if (levelRoot == null)
        {
            statusText = "未找到关卡父物体";
            return;
        }

        if (File.Exists(path) && (!waitingOverwriteConfirm || pendingOverwritePath != path))
        {
            waitingOverwriteConfirm = true;
            pendingOverwritePath = path;
            statusText = $"已存在同名关卡，再按一次保存将覆盖：{Path.GetFileName(path)}";
            return;
        }

        waitingOverwriteConfirm = false;
        pendingOverwritePath = string.Empty;
        PrefabUtility.SaveAsPrefabAsset(levelRoot.gameObject, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        statusText = "关卡父物体预制体已保存：" + path;
        Debug.Log("GM Save Level Prefab: " + path);
#else
        statusText = "仅在编辑器下可保存关卡预制体";
#endif
    }

    public void ResetCurrentLevel()
    {
        LoadLevel(currentLevelIndex);
        statusText = $"已重置 {BuildLevelName(currentLevelIndex)}";
    }

    public void ClearCurrentLevel()
    {
        ClearLevelRootChildren();
        BackpackItemSpawner.ClearInventory();
        statusText = $"已清空 {BuildLevelName(currentLevelIndex)}";
    }

    private void OnGUI()
    {
        DrawBackpackScrollView();

        if (!showGmPanel)
        {
            return;
        }

        panelRect = GUI.Window(3109, panelRect, DrawGmWindow, "GM 面板");
    }

    private void DrawGmWindow(int id)
    {
        GUILayout.BeginVertical();
        GUILayout.Label("按 F10 显示/隐藏");

        var typeNames = System.Enum.GetNames(typeof(CircuitElementType));
        var selectedIndex = Mathf.Clamp((int)addType, 0, typeNames.Length - 1);
        var currentLabel = typeNames.Length > 0 ? typeNames[selectedIndex] : addType.ToString();
        if (GUILayout.Button($"元器件：{currentLabel} ▼", GUILayout.Height(28f)))
        {
            showTypeDropdown = !showTypeDropdown;
        }

        if (showTypeDropdown)
        {
            for (var i = 0; i < typeNames.Length; i++)
            {
                if (!GUILayout.Button(typeNames[i], GUILayout.Height(24f)))
                {
                    continue;
                }

                selectedIndex = i;
                showTypeDropdown = false;
                break;
            }
        }

        addType = (CircuitElementType)selectedIndex;

        GUILayout.BeginHorizontal();
        GUILayout.Label("数量", GUILayout.Width(40f));
        addAmountText = GUILayout.TextField(addAmountText, GUILayout.Width(120f));
        if (GUILayout.Button("添加到背包", GUILayout.Height(28f)))
        {
            if (int.TryParse(addAmountText, out var amount))
            {
                addAmount = Mathf.Max(0, amount);
                AddConfiguredItemsToBackpack();
            }
            else
            {
                statusText = "数量格式错误";
            }
        }

        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.Label("保存关卡名");
        levelPrefabName = GUILayout.TextField(levelPrefabName);
        if (GUILayout.Button("保存当前关卡为预制体", GUILayout.Height(28f)))
        {
            SaveCurrentLevelAsPrefab();
        }

        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("一键重置关卡", GUILayout.Height(28f)))
        {
            ResetCurrentLevel();
        }

        if (GUILayout.Button("一键清除关卡", GUILayout.Height(28f)))
        {
            ClearCurrentLevel();
        }

        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.Label(statusText, GUILayout.Height(36f));
        GUILayout.EndVertical();

        DrawResizeHandle();
        GUI.DragWindow(new Rect(0f, 0f, panelRect.width - ResizeHandleSize, 24f));
    }

    private void DrawResizeHandle()
    {
        var handleRect = new Rect(panelRect.width - ResizeHandleSize, panelRect.height - ResizeHandleSize, ResizeHandleSize, ResizeHandleSize);
        GUI.Box(handleRect, "◢");
    }

    private void HandleResizeInput()
    {
        if (!showGmPanel)
        {
            resizingPanel = false;
            return;
        }

        var mouse = Input.mousePosition;
        mouse.y = Screen.height - mouse.y;
        var handleRect = new Rect(panelRect.xMax - ResizeHandleSize, panelRect.yMax - ResizeHandleSize, ResizeHandleSize, ResizeHandleSize);

        if (Input.GetMouseButtonDown(0) && handleRect.Contains(mouse))
        {
            resizingPanel = true;
            resizeStartMouse = mouse;
            resizeStartSize = new Vector2(panelRect.width, panelRect.height);
        }

        if (resizingPanel && Input.GetMouseButton(0))
        {
            var delta = mouse - (Vector3)resizeStartMouse;
            var width = Mathf.Clamp(resizeStartSize.x + delta.x, minPanelSize.x, maxPanelSize.x);
            var height = Mathf.Clamp(resizeStartSize.y + delta.y, minPanelSize.y, maxPanelSize.y);
            panelRect.width = width;
            panelRect.height = height;
        }

        if (Input.GetMouseButtonUp(0))
        {
            resizingPanel = false;
        }
    }

    private bool IsCurrentLevelWon()
    {
        var levelRoot = BackpackItemSpawner.GetOrCreateLevelRoot();
        if (levelRoot == null)
        {
            return false;
        }

        var elements = levelRoot.GetComponentsInChildren<CircuitElement>(true);
        var placedReceiverCount = 0;
        var matchedReceiverCount = 0;
        for (var i = 0; i < elements.Length; i++)
        {
            var element = elements[i];
            if (element == null || !IsReceiverType(element.ElementType))
            {
                continue;
            }

            placedReceiverCount++;
            if (IsReceiverMatched(element))
            {
                matchedReceiverCount++;
            }
        }

        var backpackReceiverCount =
            BackpackItemSpawner.GetInventoryCount(CircuitElementType.SemiWaveReceiver)
            + BackpackItemSpawner.GetInventoryCount(CircuitElementType.TriangleWaveReceiver)
            + BackpackItemSpawner.GetInventoryCount(CircuitElementType.SquareWaveReceiver);
        var totalReceiverCount = placedReceiverCount + backpackReceiverCount;
        if (totalReceiverCount <= 0)
        {
            return false;
        }

        return matchedReceiverCount == totalReceiverCount;
    }

    private bool IsReceiverMatched(CircuitElement receiver)
    {
        if (receiver == null || WiringManager.Instance == null)
        {
            return false;
        }

        WiringManager.Instance.GetConnectionsForElement(receiver, receiverWires);
        if (receiverWires.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < receiverWires.Count; i++)
        {
            var wire = receiverWires[i];
            if (wire == null)
            {
                continue;
            }

            if (!wire.TryGetSignalShape(out var signalType))
            {
                continue;
            }

            if (!wire.TryGetSignalShapeParams(out _, out _, out _))
            {
                continue;
            }

            if (IsSignalTypeMatchReceiver(receiver.ElementType, signalType))
            {
                return true;
            }
        }

        return false;
    }

    private void LoadLevel(int levelIndex)
    {
        var safeLevelIndex = Mathf.Max(1, levelIndex);
        var levelName = BuildLevelName(safeLevelIndex);
        var levelRoot = BackpackItemSpawner.GetOrCreateLevelRoot();
        if (levelRoot == null)
        {
            statusText = "未找到关卡父物体";
            return;
        }

        ClearLevelRootChildren();
        BackpackItemSpawner.ClearInventory();
        var prefab = Resources.Load<GameObject>($"{LevelResourceFolder}/{levelName}");
        currentLevelIndex = safeLevelIndex;
        if (prefab == null)
        {
            statusText = $"未找到关卡：{levelName}，当前为空关卡";
            return;
        }

        var loaded = Instantiate(prefab);
        if (loaded.transform.childCount == 0)
        {
            BackpackItemSpawner.AttachToLevelRoot(loaded.transform);
        }
        else
        {
            var children = new List<Transform>();
            for (var i = 0; i < loaded.transform.childCount; i++)
            {
                children.Add(loaded.transform.GetChild(i));
            }

            for (var i = 0; i < children.Count; i++)
            {
                children[i].SetParent(levelRoot, true);
            }

            Destroy(loaded);
        }

        statusText = $"已加载关卡：{levelName}";
    }

    private void DrawBackpackScrollView()
    {
        var panelRectScreen = GetBackpackPanelScreenRect();
        if (panelRectScreen.width < 20f || panelRectScreen.height < 20f)
        {
            return;
        }

        EnsureBackpackStyles();
        var viewRect = new Rect(panelRectScreen.x + 4f, panelRectScreen.y + 4f, panelRectScreen.width - 8f, panelRectScreen.height - 8f);
        var entries = BackpackItemSpawner.GetInventoryEntries();
        var rowHeight = 58f;
        var contentHeight = Mathf.Max(viewRect.height, entries.Count * rowHeight + 4f);
        var contentRect = new Rect(0f, 0f, Mathf.Max(1f, viewRect.width), contentHeight);
        backpackScrollPosition = GUI.BeginScrollView(viewRect, backpackScrollPosition, contentRect, false, false);

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var rowRect = new Rect(2f, i * rowHeight + 2f, contentRect.width - 4f, rowHeight - 6f);
            if (GUI.Button(rowRect, GUIContent.none, backpackItemStyle))
            {
                BackpackItemSpawner.TrySpawnFromInventory(entry.type, GetFieldSpawnPosition());
            }

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
    }

    private Rect GetBackpackPanelScreenRect()
    {
        if (backpackPanelRenderer == null)
        {
            var panel = GameObject.Find("BackpackPanel");
            if (panel != null)
            {
                backpackPanelRenderer = panel.GetComponent<Renderer>();
            }
        }

        if (backpackPanelRenderer == null || Camera.main == null)
        {
            var fallbackWidth = Screen.width * 0.22f;
            return new Rect(0f, 0f, fallbackWidth, Screen.height);
        }

        var bounds = backpackPanelRenderer.bounds;
        var points = new Vector3[4]
        {
            new Vector3(bounds.min.x, bounds.min.y, 0f),
            new Vector3(bounds.min.x, bounds.max.y, 0f),
            new Vector3(bounds.max.x, bounds.min.y, 0f),
            new Vector3(bounds.max.x, bounds.max.y, 0f)
        };

        var xMin = float.PositiveInfinity;
        var xMax = float.NegativeInfinity;
        var yMin = float.PositiveInfinity;
        var yMax = float.NegativeInfinity;
        for (var i = 0; i < points.Length; i++)
        {
            var screen = Camera.main.WorldToScreenPoint(points[i]);
            xMin = Mathf.Min(xMin, screen.x);
            xMax = Mathf.Max(xMax, screen.x);
            yMin = Mathf.Min(yMin, screen.y);
            yMax = Mathf.Max(yMax, screen.y);
        }

        var left = Mathf.Clamp(xMin, 0f, Screen.width);
        var right = Mathf.Clamp(xMax, 0f, Screen.width);
        var top = Mathf.Clamp(Screen.height - yMax, 0f, Screen.height);
        var bottom = Mathf.Clamp(Screen.height - yMin, 0f, Screen.height);
        return Rect.MinMaxRect(left, top, right, bottom);
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
        whiteButtonTexture = CreateSolidTexture(new Color(1f, 1f, 1f, 1f));
        backpackItemStyle.normal.background = whiteButtonTexture;
        backpackItemStyle.hover.background = whiteButtonTexture;
        backpackItemStyle.active.background = whiteButtonTexture;
        backpackItemStyle.focused.background = whiteButtonTexture;
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

    private bool IsReceiverType(CircuitElementType type)
    {
        return type == CircuitElementType.SemiWaveReceiver
            || type == CircuitElementType.TriangleWaveReceiver
            || type == CircuitElementType.SquareWaveReceiver;
    }

    private bool IsSignalTypeMatchReceiver(CircuitElementType receiverType, CircuitElementType signalType)
    {
        switch (receiverType)
        {
            case CircuitElementType.TriangleWaveReceiver:
                return signalType == CircuitElementType.TriangleWaveGenerator;
            case CircuitElementType.SquareWaveReceiver:
                return signalType == CircuitElementType.SquareWaveGenerator;
            default:
                return signalType == CircuitElementType.SemiWaveGenerator;
        }
    }

    private float EvaluateReceiverTargetWave(
        CircuitElementType receiverType,
        float distance,
        float time,
        float wavelength,
        float frequency,
        float amplitude)
    {
        switch (receiverType)
        {
            case CircuitElementType.TriangleWaveReceiver:
                return WireConnection.EvaluateTriangleWave(distance, time, wavelength, frequency, amplitude);
            case CircuitElementType.SquareWaveReceiver:
                return WireConnection.EvaluateSquareWave(distance, time, wavelength, frequency, amplitude);
            default:
                return WireConnection.EvaluateSemicircleWave(distance, time, wavelength, frequency, amplitude);
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
        var edge = Mathf.Max(1f, size * 0.06f);
        var outline = new Color(0f, 0f, 0f, 0.75f);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var delta = new Vector2(x, y) - center;
                var distance = delta.magnitude;
                if (distance > radius)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                var color = distance >= radius - edge ? outline : fillColor;
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
        var outline = new Color(0f, 0f, 0f, 0.75f);
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

                var border = x <= margin + 1 || x >= size - margin - 2 || y <= margin + 1 || y >= size - margin - 2;
                texture.SetPixel(x, y, border ? outline : fillColor);
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
        var outline = new Color(0f, 0f, 0f, 0.75f);
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
                texture.SetPixel(x, y, edgeDistance <= 1.6f ? outline : fillColor);
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

    private void ClearLevelRootChildren()
    {
        var levelRoot = BackpackItemSpawner.GetOrCreateLevelRoot();
        if (levelRoot == null)
        {
            return;
        }

        for (var i = levelRoot.childCount - 1; i >= 0; i--)
        {
            var child = levelRoot.GetChild(i);
            if (child == null)
            {
                continue;
            }

            Destroy(child.gameObject);
        }
    }

    private string BuildLevelName(int levelIndex)
    {
        return $"{LevelPrefix}{Mathf.Max(1, levelIndex)}";
    }

    private string BuildLevelPrefabAssetPath()
    {
        var rawName = levelPrefabName == null ? string.Empty : levelPrefabName.Trim();
        if (string.IsNullOrEmpty(rawName))
        {
            return string.Empty;
        }

        var safeName = Path.GetFileNameWithoutExtension(rawName);
        if (string.IsNullOrEmpty(safeName))
        {
            return string.Empty;
        }

        levelPrefabName = safeName;
        return $"Assets/Resources/Prefabs/{safeName}.prefab";
    }
}
