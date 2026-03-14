using UnityEngine;
using System.Collections;
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

    [SerializeField]
    private bool cannotWin;

    [SerializeField]
    private float levelTransitionFadeOutDuration = 0.35f;

    [SerializeField]
    private float levelTransitionBlackHoldDuration = 0.1f;

    [SerializeField]
    private float levelTransitionFadeInDuration = 0.35f;

    private int currentLevelIndex;
    private float nextWinCheckTime;
    private readonly List<WireConnection> receiverWires = new List<WireConnection>();
    private bool waitingOverwriteConfirm;
    private string pendingOverwritePath = string.Empty;
    private string statusText = string.Empty;
    private Texture2D transitionOverlayTexture;
    private float transitionOverlayAlpha;
    private bool isTransitioningLevel;
    private Coroutine levelTransitionCoroutine;
    private const string LevelPrefix = "Level";
    private const string LevelResourceFolder = "Prefabs";
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

    public int CurrentLevelIndex => currentLevelIndex;
    public string StatusText => statusText;
    public bool CannotWin
    {
        get => cannotWin;
        set => cannotWin = value;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureTransitionOverlayTexture();
    }

    private void OnDestroy()
    {
        if (transitionOverlayTexture != null)
        {
            Destroy(transitionOverlayTexture);
            transitionOverlayTexture = null;
        }

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
        if (isTransitioningLevel)
        {
            return;
        }

        if (Time.time < nextWinCheckTime)
        {
            return;
        }

        nextWinCheckTime = Time.time + Mathf.Max(0.05f, winCheckInterval);
        if (!IsCurrentLevelWon())
        {
            return;
        }

        if (cannotWin)
        {
            SetStatus($"关卡 {BuildLevelName(currentLevelIndex)} 已满足获胜条件，但“无法获胜”已开启，保持当前关卡");
            return;
        }

        BeginNextLevelTransition();
    }

    private void OnGUI()
    {
        if (transitionOverlayAlpha <= 0.001f)
        {
            return;
        }

        EnsureTransitionOverlayTexture();
        if (transitionOverlayTexture == null)
        {
            return;
        }

        var previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(transitionOverlayAlpha));
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), transitionOverlayTexture);
        GUI.color = previousColor;
    }

    public void SetStatus(string message)
    {
        statusText = message ?? string.Empty;
    }

    public int AddItemsToBackpack(CircuitElementType type, int amount)
    {
        return AddItemsToBackpack(type, amount, CircuitElement.DefaultLength, CircuitElement.DefaultWidth);
    }

    public int AddItemsToBackpack(CircuitElementType type, int amount, int length, int width)
    {
        var safeAmount = Mathf.Max(0, amount);
        var safeLength = Mathf.Max(0, length);
        var safeWidth = Mathf.Max(0, width);
        var changed = BackpackItemSpawner.AddInventoryToType(type, safeAmount, safeLength, safeWidth);
        SetStatus($"已添加：{type} {safeLength}x{safeWidth} x {safeAmount}，影响槽位：{changed}");
        Debug.Log($"GM Add Backpack Items: type={type}, length={safeLength}, width={safeWidth}, amount={safeAmount}, affectedSpawners={changed}");
        return changed;
    }

    public void ResetCurrentLevel()
    {
        CancelLevelTransition();
        LoadLevel(currentLevelIndex);
        SetStatus($"已重置 {BuildLevelName(currentLevelIndex)}");
    }

    public void ClearCurrentLevel()
    {
        CancelLevelTransition();
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
            if (WiringManager.Instance != null)
            {
                WiringManager.Instance.RebuildConnectionsFromScene();
            }

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

        if (WiringManager.Instance != null)
        {
            WiringManager.Instance.RebuildConnectionsFromScene();
        }

        SetStatus($"已加载关卡：{levelName}");
    }

    private void BeginNextLevelTransition()
    {
        if (isTransitioningLevel)
        {
            return;
        }

        var previousLevelIndex = currentLevelIndex;
        var nextLevelIndex = previousLevelIndex + 1;
        levelTransitionCoroutine = StartCoroutine(PlayLevelTransition(previousLevelIndex, nextLevelIndex));
    }

    private IEnumerator PlayLevelTransition(int previousLevelIndex, int nextLevelIndex)
    {
        isTransitioningLevel = true;
        yield return FadeTransitionOverlay(1f, levelTransitionFadeOutDuration);
        LoadLevel(nextLevelIndex);
        SetStatus($"关卡 {BuildLevelName(previousLevelIndex)} 已获胜，已切换到 {BuildLevelName(currentLevelIndex)}");
        if (levelTransitionBlackHoldDuration > 0f)
        {
            yield return new WaitForSeconds(levelTransitionBlackHoldDuration);
        }

        yield return FadeTransitionOverlay(0f, levelTransitionFadeInDuration);
        isTransitioningLevel = false;
        levelTransitionCoroutine = null;
    }

    private IEnumerator FadeTransitionOverlay(float targetAlpha, float duration)
    {
        var startAlpha = transitionOverlayAlpha;
        if (duration <= 0f)
        {
            transitionOverlayAlpha = Mathf.Clamp01(targetAlpha);
            yield break;
        }

        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            transitionOverlayAlpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        transitionOverlayAlpha = Mathf.Clamp01(targetAlpha);
    }

    private void CancelLevelTransition()
    {
        if (levelTransitionCoroutine != null)
        {
            StopCoroutine(levelTransitionCoroutine);
            levelTransitionCoroutine = null;
        }

        isTransitioningLevel = false;
        transitionOverlayAlpha = 0f;
    }

    private void EnsureTransitionOverlayTexture()
    {
        if (transitionOverlayTexture != null)
        {
            return;
        }

        transitionOverlayTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        transitionOverlayTexture.SetPixel(0, 0, Color.black);
        transitionOverlayTexture.Apply();
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
        PrepareLevelMaterialsForExport(levelRoot);
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
        if (receiver.Width <= 0)
        {
            return IsZeroSignalReceiverMatched(receiver);
        }

        if (receiverWires.Count == 0)
        {
            return false;
        }

        BuildExpectedSignalParams(receiver, out var expectedAmplitude, out var expectedWavelength);

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

            if (!wire.TryGetSignalShapeParams(out var amplitude, out var wavelength, out _))
            {
                continue;
            }

            if (IsSignalTypeMatchReceiver(receiver.ElementType, signalType)
                && IsSignalSizeMatch(expectedAmplitude, expectedWavelength, amplitude, wavelength))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsZeroSignalReceiverMatched(CircuitElement receiver)
    {
        if (receiverWires.Count == 0)
        {
            return true;
        }

        for (var i = 0; i < receiverWires.Count; i++)
        {
            var wire = receiverWires[i];
            if (wire == null)
            {
                continue;
            }

            if (!wire.TryGetSignalShapeParams(out _, out _, out var frequency))
            {
                return true;
            }

            if (IsWireSignalZeroAtReceiver(wire, receiver, frequency))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWireSignalZeroAtReceiver(WireConnection wire, CircuitElement receiver, float frequency)
    {
        if (wire == null || receiver == null)
        {
            return false;
        }

        const int sampleCount = 9;
        const float zeroTolerance = 0.01f;
        var safeFrequency = Mathf.Max(0.01f, frequency);
        var period = 1f / safeFrequency;
        var startTime = Time.time;
        for (var i = 0; i < sampleCount; i++)
        {
            var t = startTime + period * i / (sampleCount - 1);
            if (!wire.TryGetSignalAtElement(receiver, t, out var signalValue))
            {
                return false;
            }

            if (Mathf.Abs(signalValue) > zeroTolerance)
            {
                return false;
            }
        }

        return true;
    }

    private static void BuildExpectedSignalParams(CircuitElement receiver, out float amplitude, out float wavelength)
    {
        var widthScale = receiver != null ? receiver.WidthScaleMultiplier : 1f;
        var lengthScale = receiver != null ? receiver.LengthScaleMultiplier : 1f;
        amplitude = 0.5f * Mathf.Max(0.01f, widthScale);
        wavelength = 2f * Mathf.Max(0.01f, lengthScale);
    }

    private static bool IsSignalSizeMatch(float expectedAmplitude, float expectedWavelength, float actualAmplitude, float actualWavelength)
    {
        return Mathf.Abs(expectedAmplitude - actualAmplitude) <= 0.01f
            && Mathf.Abs(expectedWavelength - actualWavelength) <= 0.01f;
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

#if UNITY_EDITOR
    private static void PrepareLevelMaterialsForExport(Transform levelRoot)
    {
        if (levelRoot == null)
        {
            return;
        }

        const string materialFolder = "Assets/Resources/Materials/Generated";
        EnsureFolderExists(materialFolder);

        var renderers = levelRoot.GetComponentsInChildren<Renderer>(true);
        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            var sharedMaterials = renderer.sharedMaterials;
            if (sharedMaterials == null || sharedMaterials.Length == 0)
            {
                var fallbackColor = GetRendererColor(renderer);
                renderer.sharedMaterial = GetOrCreateExportMaterial(materialFolder, null, fallbackColor);
                continue;
            }

            var updated = false;
            for (var materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
            {
                var source = sharedMaterials[materialIndex];
                if (source != null && AssetDatabase.Contains(source))
                {
                    continue;
                }

                var color = GetMaterialColor(renderer, source);
                if (source == null)
                {
                    color = GetRendererColor(renderer);
                }

                var exported = GetOrCreateExportMaterial(materialFolder, source, color);
                if (exported == null)
                {
                    continue;
                }

                sharedMaterials[materialIndex] = exported;
                updated = true;
            }

            if (updated)
            {
                renderer.sharedMaterials = sharedMaterials;
            }
        }
    }

    private static Material GetOrCreateExportMaterial(string folder, Material source, Color color)
    {
        var shader = source != null ? source.shader : null;
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            return source;
        }

        var shaderName = shader.name.Replace("/", "_");
        var colorKey = ColorUtility.ToHtmlStringRGBA(color);
        var assetPath = $"{folder}/Auto_{shaderName}_{colorKey}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (existing != null)
        {
            return existing;
        }

        var material = source != null ? new Material(source) : new Material(shader);
        material.shader = shader;
        if (material.HasProperty("_Color"))
        {
            material.color = color;
        }

        AssetDatabase.CreateAsset(material, assetPath);
        return material;
    }

    private static Color GetMaterialColor(Renderer renderer, Material material)
    {
        if (renderer != null)
        {
            var propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            if (propertyBlock.isEmpty == false)
            {
                if (material != null && material.HasProperty(ColorPropertyId))
                {
                    return propertyBlock.GetColor(ColorPropertyId);
                }

                if (material != null && material.HasProperty(BaseColorPropertyId))
                {
                    return propertyBlock.GetColor(BaseColorPropertyId);
                }
            }
        }

        if (material != null && material.HasProperty("_Color"))
        {
            return material.color;
        }

        if (material != null && material.HasProperty("_BaseColor"))
        {
            return material.GetColor("_BaseColor");
        }

        return Color.white;
    }

    private static Color GetRendererColor(Renderer renderer)
    {
        if (renderer is LineRenderer lineRenderer)
        {
            return lineRenderer.startColor;
        }

        return Color.white;
    }

    private static void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        var parts = folderPath.Replace("\\", "/").Split('/');
        if (parts.Length == 0)
        {
            return;
        }

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
#endif
}
