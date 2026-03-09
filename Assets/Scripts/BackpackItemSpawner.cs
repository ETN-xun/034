using UnityEngine;

public class BackpackItemSpawner : MonoBehaviour
{
    [SerializeField]
    private Camera targetCamera;

    [SerializeField]
    private string prefabResourcePath;

    [SerializeField]
    private CircuitElementType elementType;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void OnMouseDown()
    {
        var placeablePrefab = Resources.Load<GameObject>(prefabResourcePath);
        if (placeablePrefab == null)
        {
            return;
        }

        var spawnPosition = GetMouseWorldPosition();
        var instance = Instantiate(placeablePrefab, spawnPosition, Quaternion.identity);
        var circuitElement = instance.GetComponent<CircuitElement>();
        if (circuitElement == null)
        {
            circuitElement = instance.AddComponent<CircuitElement>();
        }

        if (circuitElement != null)
        {
            circuitElement.SetType(elementType);
        }

        var elementSetup = instance.GetComponent<SineElementSetup>();
        if (elementSetup == null)
        {
            elementSetup = instance.AddComponent<SineElementSetup>();
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
    }

    private Vector3 GetMouseWorldPosition()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        var mouse = Input.mousePosition;
        mouse.z = -targetCamera.transform.position.z;
        var world = targetCamera.ScreenToWorldPoint(mouse);
        world.z = 0f;
        return world;
    }
}
