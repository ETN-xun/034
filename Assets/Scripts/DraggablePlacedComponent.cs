using UnityEngine;

public class DraggablePlacedComponent : MonoBehaviour
{
    [SerializeField]
    private Camera targetCamera;

    [SerializeField]
    private float backpackViewportWidth = 0.2f;

    [SerializeField]
    private bool restrictToFieldArea = true;

    [SerializeField]
    private float gridSpacing = 1f;

    private bool isDragging;
    private Vector3 dragOffset;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        transform.position = SnapToGrid(transform.position);
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

        transform.position = SnapToGrid(world);
    }

    private void OnMouseUp()
    {
        isDragging = false;
        transform.position = SnapToGrid(transform.position);
    }

    public void BeginExternalDragAt(Vector3 worldPosition)
    {
        transform.position = SnapToGrid(worldPosition);
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

    private Vector3 SnapToGrid(Vector3 world)
    {
        var spacing = Mathf.Max(0.01f, gridSpacing);
        world.x = Mathf.Round(world.x / spacing) * spacing;
        world.y = Mathf.Round(world.y / spacing) * spacing;
        world.z = 0f;
        return world;
    }
}
