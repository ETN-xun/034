using UnityEngine;
using System.Collections.Generic;

public class BackpackItemSpawner : MonoBehaviour
{
    private const string LevelRootName = "LevelRoot";
    private const string InventoryStateObjectName = "BackpackInventoryState";
    private static readonly Dictionary<CircuitElementType, int> RuntimeInventory = new Dictionary<CircuitElementType, int>();
    private static readonly Dictionary<CircuitElementType, string> RuntimePrefabPaths = new Dictionary<CircuitElementType, string>();
    private static Transform cachedLevelRoot;
    private static BackpackInventoryState cachedInventoryState;
    public static event System.Action InventoryChanged;

    [SerializeField]
    private string prefabResourcePath;

    [SerializeField]
    private CircuitElementType elementType;

    [SerializeField]
    private float gridSpacing = 1f;

    [SerializeField]
    private int initialCount;

    [SerializeField]
    private int currentCount;

    [SerializeField]
    private bool inventoryInitialized;

    private void Awake()
    {
        RegisterPrefabPath(elementType, prefabResourcePath);

        if (!inventoryInitialized)
        {
            currentCount = Mathf.Max(0, initialCount);
            inventoryInitialized = true;
        }
        else
        {
            currentCount = Mathf.Max(0, currentCount);
        }

        if (currentCount > 0)
        {
            AddInventoryToType(elementType, currentCount);
            currentCount = 0;
        }

        HideLegacySpawnerVisual();
    }

    private void OnEnable()
    {
        HideLegacySpawnerVisual();
    }

    public static int AddInventoryToType(CircuitElementType type, int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        EnsureInventoryStateExists();
        RuntimeInventory.TryGetValue(type, out var current);
        RuntimeInventory[type] = Mathf.Max(0, current + amount);
        PersistRuntimeInventory();
        InventoryChanged?.Invoke();
        return 1;
    }

    public static void ClearInventory()
    {
        EnsureInventoryStateExists();
        RuntimeInventory.Clear();
        PersistRuntimeInventory();
        InventoryChanged?.Invoke();
    }

    public static bool TryConsumeOne(CircuitElementType type)
    {
        EnsureInventoryStateExists();
        RuntimeInventory.TryGetValue(type, out var current);
        if (current <= 0)
        {
            return false;
        }

        var next = Mathf.Max(0, current - 1);
        if (next == 0)
        {
            RuntimeInventory.Remove(type);
        }
        else
        {
            RuntimeInventory[type] = next;
        }

        PersistRuntimeInventory();
        InventoryChanged?.Invoke();
        return true;
    }

    public static bool TrySpawnFromInventory(CircuitElementType type, Vector3 worldPosition)
    {
        if (!TryConsumeOne(type))
        {
            return false;
        }

        var prefabPath = GetPrefabPath(type);
        if (string.IsNullOrEmpty(prefabPath))
        {
            AddInventoryToType(type, 1);
            return false;
        }

        var placeablePrefab = Resources.Load<GameObject>(prefabPath);
        if (placeablePrefab == null)
        {
            AddInventoryToType(type, 1);
            return false;
        }

        var spacing = Mathf.Max(0.01f, GetGridSpacing());
        var spawnPosition = worldPosition;
        spawnPosition.x = Mathf.Round(spawnPosition.x / spacing) * spacing;
        spawnPosition.y = Mathf.Round(spawnPosition.y / spacing) * spacing;
        spawnPosition.z = 0f;
        var instance = Instantiate(placeablePrefab, spawnPosition, Quaternion.identity);
        AttachToLevelRoot(instance.transform);
        instance.transform.position = spawnPosition;

        var circuitElement = instance.GetComponent<CircuitElement>();
        if (circuitElement == null)
        {
            circuitElement = instance.AddComponent<CircuitElement>();
        }

        if (circuitElement != null)
        {
            circuitElement.SetType(type);
        }

        var elementSetup = instance.GetComponent<SemiCircleElementSetup>();
        if (elementSetup == null)
        {
            elementSetup = instance.AddComponent<SemiCircleElementSetup>();
        }

        if (elementSetup != null)
        {
            elementSetup.Apply();
        }

        var draggable = instance.GetComponent<DraggablePlacedComponent>();
        if (draggable == null)
        {
            draggable = instance.AddComponent<DraggablePlacedComponent>();
        }

        if (draggable != null)
        {
            draggable.BeginExternalDragAt(spawnPosition);
        }

        return true;
    }

    public static List<(CircuitElementType type, int count)> GetInventoryEntries()
    {
        EnsureInventoryStateExists();
        var result = new List<(CircuitElementType type, int count)>();
        foreach (var pair in RuntimeInventory)
        {
            if (pair.Value <= 0)
            {
                continue;
            }

            result.Add((pair.Key, pair.Value));
        }

        result.Sort((a, b) => ((int)a.type).CompareTo((int)b.type));
        return result;
    }

    public static int GetInventoryCount(CircuitElementType type)
    {
        EnsureInventoryStateExists();
        RuntimeInventory.TryGetValue(type, out var current);
        return Mathf.Max(0, current);
    }

    public static Transform GetOrCreateLevelRoot()
    {
        if (cachedLevelRoot != null)
        {
            return cachedLevelRoot;
        }

        var root = GameObject.Find(LevelRootName);
        if (root == null)
        {
            root = new GameObject(LevelRootName);
        }

        cachedLevelRoot = root.transform;
        return cachedLevelRoot;
    }

    public static void AttachToLevelRoot(Transform target)
    {
        if (target == null)
        {
            return;
        }

        var levelRoot = GetOrCreateLevelRoot();
        if (levelRoot == null || target == levelRoot)
        {
            return;
        }

        target.SetParent(levelRoot, true);
    }

    public static void EnsureSceneObjectsUnderLevelRoot()
    {
        var levelRoot = GetOrCreateLevelRoot();
        if (levelRoot == null)
        {
            return;
        }

        EnsureInventoryStateExists();
        var roots = levelRoot.gameObject.scene.GetRootGameObjects();
        for (var i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            if (root == null || root.transform == levelRoot)
            {
                continue;
            }

            if (!IsLevelRecordObject(root))
            {
                continue;
            }

            root.transform.SetParent(levelRoot, true);
        }
    }

    internal static void ApplyInventorySnapshot(List<BackpackInventoryState.InventoryRecord> records)
    {
        RuntimeInventory.Clear();
        if (records != null)
        {
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                if (record == null || record.count <= 0)
                {
                    continue;
                }

                RuntimeInventory[record.type] = Mathf.Max(0, record.count);
                RegisterPrefabPath(record.type, record.prefabResourcePath);
            }
        }

        InventoryChanged?.Invoke();
    }

    internal static List<BackpackInventoryState.InventoryRecord> BuildInventorySnapshot()
    {
        var list = new List<BackpackInventoryState.InventoryRecord>();
        foreach (var pair in RuntimeInventory)
        {
            if (pair.Value <= 0)
            {
                continue;
            }

            list.Add(new BackpackInventoryState.InventoryRecord
            {
                type = pair.Key,
                count = pair.Value,
                prefabResourcePath = GetPrefabPath(pair.Key)
            });
        }

        list.Sort((a, b) => ((int)a.type).CompareTo((int)b.type));
        return list;
    }

    private static bool IsLevelRecordObject(GameObject rootObject)
    {
        if (rootObject.GetComponent<BackpackInventoryState>() != null)
        {
            return true;
        }

        if (rootObject.GetComponent<CircuitElement>() != null)
        {
            return true;
        }

        return rootObject.GetComponent<WireConnection>() != null;
    }

    private static void EnsureInventoryStateExists()
    {
        var levelRoot = GetOrCreateLevelRoot();
        if (levelRoot == null)
        {
            return;
        }

        if (cachedInventoryState != null)
        {
            return;
        }

        cachedInventoryState = levelRoot.GetComponentInChildren<BackpackInventoryState>(true);
        if (cachedInventoryState != null)
        {
            return;
        }

        var stateObject = new GameObject(InventoryStateObjectName);
        stateObject.transform.SetParent(levelRoot, false);
        cachedInventoryState = stateObject.AddComponent<BackpackInventoryState>();
        PersistRuntimeInventory();
    }

    private static void PersistRuntimeInventory()
    {
        EnsureInventoryStateExists();
        if (cachedInventoryState == null)
        {
            return;
        }

        cachedInventoryState.SetInventory(BuildInventorySnapshot());
    }

    private void HideLegacySpawnerVisual()
    {
        var renderers = GetComponents<Renderer>();
        for (var i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = false;
        }

        var colliders = GetComponents<Collider>();
        for (var i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private static float GetGridSpacing()
    {
        var spawners = FindObjectsByType<BackpackItemSpawner>(FindObjectsSortMode.None);
        for (var i = 0; i < spawners.Length; i++)
        {
            var value = spawners[i].gridSpacing;
            if (value > 0.001f)
            {
                return value;
            }
        }

        return 1f;
    }

    private static void RegisterPrefabPath(CircuitElementType type, string path)
    {
        var value = string.IsNullOrEmpty(path) ? GetDefaultPrefabPath(type) : path;
        RuntimePrefabPaths[type] = value;
    }

    private static string GetPrefabPath(CircuitElementType type)
    {
        if (RuntimePrefabPaths.TryGetValue(type, out var path) && !string.IsNullOrEmpty(path))
        {
            return path;
        }

        path = GetDefaultPrefabPath(type);
        RuntimePrefabPaths[type] = path;
        return path;
    }

    private static string GetDefaultPrefabPath(CircuitElementType type)
    {
        switch (type)
        {
            case CircuitElementType.SemiWaveReceiver:
                return "Prefabs/SemiCircleReceiver";
            default:
                return "Prefabs/SemiCircleGenerator";
        }
    }
}

public class BackpackInventoryState : MonoBehaviour
{
    [System.Serializable]
    public class InventoryRecord
    {
        public CircuitElementType type;
        public int count;
        public string prefabResourcePath;
    }

    [SerializeField]
    private List<InventoryRecord> records = new List<InventoryRecord>();

    private void Awake()
    {
        if (records != null && records.Count > 0)
        {
            BackpackItemSpawner.ApplyInventorySnapshot(records);
        }
    }

    public void SetInventory(List<InventoryRecord> newRecords)
    {
        records = newRecords ?? new List<InventoryRecord>();
    }
}
