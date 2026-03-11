using UnityEngine;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class LevelRuntimeManager : MonoBehaviour
{
    public static LevelRuntimeManager Instance { get; private set; }

    [SerializeField]
    private int startLevelIndex = 1;

    [SerializeField]
    private float winCheckInterval = 0.2f;

    private int currentLevelIndex;
    private float nextWinCheckTime;
    private readonly List<WireConnection> receiverWires = new List<WireConnection>();
    private bool waitingOverwriteConfirm;
    private string pendingOverwritePath = string.Empty;
    private string statusText = string.Empty;
    private const string LevelPrefix = "Level";
    private const string LevelResourceFolder = "Prefabs";

    public int CurrentLevelIndex => currentLevelIndex;
    public string StatusText => statusText;

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

    private void Start()
    {
        currentLevelIndex = Mathf.Max(1, startLevelIndex);
        LoadLevel(currentLevelIndex);
    }

    private void Update()
    {
        if (Time.time < nextWinCheckTime)
        {
            return;
        }

        nextWinCheckTime = Time.time + Mathf.Max(0.05f, winCheckInterval);
        if (!IsCurrentLevelWon())
        {
            return;
        }

        var previous = currentLevelIndex;
        LoadLevel(previous + 1);
        SetStatus($"关卡 {BuildLevelName(previous)} 已获胜，已切换到 {BuildLevelName(currentLevelIndex)}");
    }

    public void SetStatus(string message)
    {
        statusText = message ?? string.Empty;
    }

    public int AddItemsToBackpack(CircuitElementType type, int amount)
    {
        var safeAmount = Mathf.Max(0, amount);
        var changed = BackpackItemSpawner.AddInventoryToType(type, safeAmount);
        SetStatus($"已添加：{type} x {safeAmount}，影响槽位：{changed}");
        Debug.Log($"GM Add Backpack Items: type={type}, amount={safeAmount}, affectedSpawners={changed}");
        return changed;
    }

    public void ResetCurrentLevel()
    {
        LoadLevel(currentLevelIndex);
        SetStatus($"已重置 {BuildLevelName(currentLevelIndex)}");
    }

    public void ClearCurrentLevel()
    {
        ClearLevelRootChildren();
        BackpackItemSpawner.ClearInventory();
        SetStatus($"已清空 {BuildLevelName(currentLevelIndex)}");
    }

    public void LoadLevel(int levelIndex)
    {
        var safeLevelIndex = Mathf.Max(1, levelIndex);
        var levelName = BuildLevelName(safeLevelIndex);
        var levelRoot = BackpackItemSpawner.GetOrCreateLevelRoot();
        if (levelRoot == null)
        {
            SetStatus("未找到关卡父物体");
            return;
        }

        ClearLevelRootChildren();
        BackpackItemSpawner.ClearInventory();
        var prefab = Resources.Load<GameObject>($"{LevelResourceFolder}/{levelName}");
        currentLevelIndex = safeLevelIndex;
        if (prefab == null)
        {
            SetStatus($"未找到关卡：{levelName}，当前为空关卡");
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

        SetStatus($"已加载关卡：{levelName}");
    }

    public bool SaveCurrentLevelAsPrefab(string levelPrefabName)
    {
#if UNITY_EDITOR
        var path = BuildLevelPrefabAssetPath(levelPrefabName);
        if (string.IsNullOrEmpty(path))
        {
            SetStatus("关卡名为空");
            return false;
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
            SetStatus("未找到关卡父物体");
            return false;
        }

        if (File.Exists(path) && (!waitingOverwriteConfirm || pendingOverwritePath != path))
        {
            waitingOverwriteConfirm = true;
            pendingOverwritePath = path;
            SetStatus($"已存在同名关卡，再按一次保存将覆盖：{Path.GetFileName(path)}");
            return false;
        }

        waitingOverwriteConfirm = false;
        pendingOverwritePath = string.Empty;
        PrefabUtility.SaveAsPrefabAsset(levelRoot.gameObject, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        SetStatus("关卡父物体预制体已保存：" + path);
        Debug.Log("GM Save Level Prefab: " + path);
        return true;
#else
        SetStatus("仅在编辑器下可保存关卡预制体");
        return false;
#endif
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

    private string BuildLevelPrefabAssetPath(string levelPrefabName)
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

        return $"Assets/Resources/Prefabs/{safeName}.prefab";
    }
}
