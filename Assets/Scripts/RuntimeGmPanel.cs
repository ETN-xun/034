using UnityEngine;

public class RuntimeGmPanel : MonoBehaviour
{
    public static RuntimeGmPanel Instance { get; private set; }
    public static bool IsVisible => Instance != null && Instance.showGmPanel;

    [SerializeField]
    private CircuitElementType addType = CircuitElementType.SemiWaveGenerator;

    [SerializeField]
    private int addAmount = 1;

    [SerializeField]
    private string levelPrefabName = "Level1";

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
    private bool resizingPanel;
    private Vector2 resizeStartMouse;
    private Vector2 resizeStartSize;
    private const float ResizeHandleSize = 18f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F10))
        {
            showGmPanel = !showGmPanel;
        }

        HandleResizeInput();
    }

    [ContextMenu("GM/向背包添加配置数量元器件")]
    public void AddConfiguredItemsToBackpack()
    {
        EnsureRuntimeManager().AddItemsToBackpack(addType, addAmount);
    }

    [ContextMenu("GM/保存当前关卡为预制体")]
    public void SaveCurrentLevelAsPrefab()
    {
        EnsureRuntimeManager().SaveCurrentLevelAsPrefab(levelPrefabName);
    }

    public void ResetCurrentLevel()
    {
        EnsureRuntimeManager().ResetCurrentLevel();
    }

    public void ClearCurrentLevel()
    {
        EnsureRuntimeManager().ClearCurrentLevel();
    }

    private void OnGUI()
    {
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
                EnsureRuntimeManager().SetStatus("数量格式错误");
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
        GUILayout.Label(EnsureRuntimeManager().StatusText, GUILayout.Height(36f));
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

    private LevelRuntimeManager EnsureRuntimeManager()
    {
        var runtime = LevelRuntimeManager.Instance;
        if (runtime != null)
        {
            return runtime;
        }

        runtime = GetComponent<LevelRuntimeManager>();
        if (runtime != null)
        {
            return runtime;
        }

        runtime = gameObject.AddComponent<LevelRuntimeManager>();
        return runtime;
    }
}
