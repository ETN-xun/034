using UnityEngine;
using System.Collections.Generic;

public class BackpackItemSpawner : MonoBehaviour
{
    public struct InventoryEntry
    {
        public CircuitElementType Type;
        public int Length;
        public int Width;
        public int Count;
    }

    private struct InventoryKey : System.IEquatable<InventoryKey>
    {
        public CircuitElementType Type;
        public int Length;
        public int Width;

        public bool Equals(InventoryKey other)
        {
            return Type == other.Type && Length == other.Length && Width == other.Width;
        }

        public override bool Equals(object obj)
        {
            return obj is InventoryKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Type;
                hash = (hash * 397) ^ Length;
                hash = (hash * 397) ^ Width;
                return hash;
            }
        }
    }

    private const string LevelRootName = "LevelRoot";
    private const string InventoryStateObjectName = "BackpackInventoryState";
    private static readonly Dictionary<InventoryKey, int> RuntimeInventory = new Dictionary<InventoryKey, int>();
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
            AddInventoryToType(elementType, currentCount, CircuitElement.DefaultLength, CircuitElement.DefaultWidth);
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
        return AddInventoryToType(type, amount, CircuitElement.DefaultLength, CircuitElement.DefaultWidth);
    }

    public static int AddInventoryToType(CircuitElementType type, int amount, int length, int width)
    {
        if (amount <= 0)
        {
            return 0;
        }

        var normalizedLength = NormalizeDimension(length);
        var normalizedWidth = NormalizeDimension(width);
        var key = new InventoryKey
        {
            Type = type,
            Length = normalizedLength,
            Width = normalizedWidth
        };
        EnsureInventoryStateExists();
        RuntimeInventory.TryGetValue(key, out var current);
        RuntimeInventory[key] = Mathf.Max(0, current + amount);
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
        return TryConsumeOne(type, CircuitElement.DefaultLength, CircuitElement.DefaultWidth);
    }

    public static bool TrySpawnFromInventory(CircuitElementType type, Vector3 worldPosition)
    {
        return TrySpawnFromInventory(
            type,
            CircuitElement.DefaultLength,
            CircuitElement.DefaultWidth,
            worldPosition,
            DraggablePlacedComponent.ExternalDragMode.HoldToRelease);
    }

    public static bool TrySpawnFromInventory(CircuitElementType type, Vector3 worldPosition, DraggablePlacedComponent.ExternalDragMode dragMode)
    {
        return TrySpawnFromInventory(
            type,
            CircuitElement.DefaultLength,
            CircuitElement.DefaultWidth,
            worldPosition,
            dragMode);
    }

    public static bool TrySpawnFromInventory(CircuitElementType type, int length, int width, Vector3 worldPosition, DraggablePlacedComponent.ExternalDragMode dragMode)
    {
        var normalizedLength = NormalizeDimension(length);
        var normalizedWidth = NormalizeDimension(width);
        if (!TryConsumeOne(type, normalizedLength, normalizedWidth))
        {
            return false;
        }

        var prefabPath = GetPrefabPath(type);
        if (string.IsNullOrEmpty(prefabPath))
        {
            AddInventoryToType(type, 1, normalizedLength, normalizedWidth);
            return false;
        }

        var placeablePrefab = Resources.Load<GameObject>(prefabPath);
        if (placeablePrefab == null)
        {
            AddInventoryToType(type, 1, normalizedLength, normalizedWidth);
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
            circuitElement.SetSize(normalizedLength, normalizedWidth);
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
            draggable.BeginExternalDragAt(spawnPosition, dragMode);
        }

        return true;
    }

    public static List<InventoryEntry> GetInventoryEntries()
    {
        EnsureInventoryStateExists();
        var result = new List<InventoryEntry>();
        foreach (var pair in RuntimeInventory)
        {
            if (pair.Value <= 0)
            {
                continue;
            }

            result.Add(new InventoryEntry
            {
                Type = pair.Key.Type,
                Length = pair.Key.Length,
                Width = pair.Key.Width,
                Count = pair.Value
            });
        }

        result.Sort((a, b) =>
        {
            var typeCompare = ((int)a.Type).CompareTo((int)b.Type);
            if (typeCompare != 0)
            {
                return typeCompare;
            }

            var lengthCompare = a.Length.CompareTo(b.Length);
            if (lengthCompare != 0)
            {
                return lengthCompare;
            }

            return a.Width.CompareTo(b.Width);
        });
        return result;
    }

    public static int GetInventoryCount(CircuitElementType type)
    {
        EnsureInventoryStateExists();
        var total = 0;
        foreach (var pair in RuntimeInventory)
        {
            if (pair.Key.Type != type)
            {
                continue;
            }

            total += Mathf.Max(0, pair.Value);
        }

        return total;
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

                var key = new InventoryKey
                {
                    Type = record.type,
                    Length = NormalizeDimension(record.length),
                    Width = NormalizeDimension(record.width)
                };
                RuntimeInventory[key] = Mathf.Max(0, record.count);
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
                type = pair.Key.Type,
                count = pair.Value,
                prefabResourcePath = GetPrefabPath(pair.Key.Type),
                length = pair.Key.Length,
                width = pair.Key.Width
            });
        }

        list.Sort((a, b) =>
        {
            var typeCompare = ((int)a.type).CompareTo((int)b.type);
            if (typeCompare != 0)
            {
                return typeCompare;
            }

            var lengthCompare = a.length.CompareTo(b.length);
            if (lengthCompare != 0)
            {
                return lengthCompare;
            }

            return a.width.CompareTo(b.width);
        });
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
            case CircuitElementType.SemiWaveGenerator:
                return "Prefabs/SemiCircleGenerator";
            case CircuitElementType.SemiWaveReceiver:
                return "Prefabs/SemiCircleReceiver";
            case CircuitElementType.TriangleWaveGenerator:
                return "Prefabs/TriangleWaveGenerator";
            case CircuitElementType.TriangleWaveReceiver:
                return "Prefabs/TriangleWaveReceiver";
            case CircuitElementType.SquareWaveGenerator:
                return "Prefabs/SquareWaveGenerator";
            case CircuitElementType.SquareWaveReceiver:
                return "Prefabs/SquareWaveReceiver";
            case CircuitElementType.SemiWaveConverter:
                return "Prefabs/SemiWaveConverter";
            case CircuitElementType.TriangleWaveConverter:
                return "Prefabs/TriangleWaveConverter";
            case CircuitElementType.SquareWaveConverter:
                return "Prefabs/SquareWaveConverter";
            default:
                return "Prefabs/SemiCircleGenerator";
        }
    }

    private static bool TryConsumeOne(CircuitElementType type, int length, int width)
    {
        EnsureInventoryStateExists();
        var key = new InventoryKey
        {
            Type = type,
            Length = NormalizeDimension(length),
            Width = NormalizeDimension(width)
        };
        RuntimeInventory.TryGetValue(key, out var current);
        if (current <= 0)
        {
            return false;
        }

        var next = Mathf.Max(0, current - 1);
        if (next == 0)
        {
            RuntimeInventory.Remove(key);
        }
        else
        {
            RuntimeInventory[key] = next;
        }

        PersistRuntimeInventory();
        InventoryChanged?.Invoke();
        return true;
    }

    private static int NormalizeDimension(int value)
    {
        return Mathf.Max(1, value);
    }
}
