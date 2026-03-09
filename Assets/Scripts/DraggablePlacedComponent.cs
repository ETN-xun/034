using UnityEngine;

public class DraggablePlacedComponent : MonoBehaviour
{
    [SerializeField]
    private Camera targetCamera;

    [SerializeField]
    private float backpackViewportWidth = 0.2f;

    [SerializeField]
    private bool restrictToFieldArea = true;

    private bool isDragging;
    private Vector3 dragOffset;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void OnMouseDown()
    {
        if (WiringManager.Instance != null)
        {
            BeginDrag();
        }
    }

    private void OnMouseDrag()
    {
        if (!isDragging)
        {
            return;
        }

        var world = GetMouseWorldPosition() + dragOffset;

        if (restrictToFieldArea)
        {
            var minFieldX = targetCamera.ViewportToWorldPoint(new Vector3(backpackViewportWidth, 0.5f, -targetCamera.transform.position.z)).x;
            world.x = Mathf.Max(minFieldX + 0.5f, world.x);
        }

        world.z = 0f;
        transform.position = world;
    }

    private void OnMouseUp()
    {
        isDragging = false;
    }

    public void BeginExternalDragAt(Vector3 worldPosition)
    {
        transform.position = worldPosition;
        BeginDrag();
        dragOffset = Vector3.zero;
    }

    private void BeginDrag()
    {
        isDragging = true;
        dragOffset = transform.position - GetMouseWorldPosition();
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
