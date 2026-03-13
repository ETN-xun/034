using UnityEngine;

public class DraggablePlacedComponent : MonoBehaviour
{
    public enum ExternalDragMode
    {
        HoldToRelease,
        StickyToCursor
    }

    [SerializeField]
    private Camera targetCamera;

    [SerializeField]
    private float backpackViewportWidth = 0.2f;

    [SerializeField]
    private bool restrictToFieldArea = true;

    [SerializeField]
    private float gridSpacing = 1f;

    [SerializeField]
    private float dragStartThresholdPixels = 8f;

    private bool isDragging;
    private bool isPointerDown;
    private Vector3 dragOffset;
    private Vector3 pointerDownScreenPosition;
    private ExternalDragMode currentDragMode = ExternalDragMode.HoldToRelease;
    private int dragStartedFrame;

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
        if (IsLocked())
        {
            return;
        }

        isPointerDown = true;
        pointerDownScreenPosition = Input.mousePosition;
    }

    private void Update()
    {
        if (!isDragging)
        {
            return;
        }

        var world = GetMouseWorldPosition() + dragOffset;
        transform.position = SnapToGrid(world);

        if (currentDragMode == ExternalDragMode.StickyToCursor)
        {
            if (Time.frameCount > dragStartedFrame && Input.GetMouseButtonDown(0))
            {
                CompleteDragPlacement();
            }

            return;
        }

        if (!Input.GetMouseButton(0))
        {
            CompleteDragPlacement();
        }
    }

    private void OnMouseDrag()
    {
        if (!isPointerDown)
        {
            return;
        }

        if (!isDragging)
        {
            var dragDelta = Input.mousePosition - pointerDownScreenPosition;
            var threshold = Mathf.Max(1f, dragStartThresholdPixels);
            if (dragDelta.sqrMagnitude < threshold * threshold)
            {
                return;
            }

            BeginDrag(ExternalDragMode.HoldToRelease);
        }
    }

    private void OnMouseUp()
    {
        CompleteDragPlacement();
    }

    private void CompleteDragPlacement()
    {
        isPointerDown = false;
        if (!EndDrag())
        {
            return;
        }

        var snapped = SnapToGrid(transform.position);
        if (UI.IsBackpackOpen && IsInsideBackpackArea(snapped))
        {
            ReclaimToBackpack();
            return;
        }

        if (restrictToFieldArea && UI.IsBackpackOpen)
        {
            var minFieldX = GetBackpackBoundaryX();
            snapped.x = Mathf.Max(minFieldX + 0.5f, snapped.x);
        }

        transform.position = snapped;
    }

    private void OnDisable()
    {
        isPointerDown = false;
        EndDrag();
    }

    public void BeginExternalDragAt(Vector3 worldPosition, ExternalDragMode dragMode)
    {
        if (IsLocked())
        {
            return;
        }

        transform.position = SnapToGrid(worldPosition);
        isPointerDown = true;
        pointerDownScreenPosition = Input.mousePosition;
        BeginDrag(dragMode);
        dragOffset = Vector3.zero;
    }

    private void BeginDrag(ExternalDragMode dragMode)
    {
        if (isDragging)
        {
            return;
        }

        currentDragMode = dragMode;
        dragStartedFrame = Time.frameCount;
        isDragging = true;
        dragOffset = transform.position - GetMouseWorldPosition();
        if (WiringManager.Instance != null)
        {
            WiringManager.Instance.NotifyElementDragStarted();
        }
    }

    private bool EndDrag()
    {
        if (!isDragging)
        {
            return false;
        }

        isDragging = false;
        currentDragMode = ExternalDragMode.HoldToRelease;
        if (WiringManager.Instance != null)
        {
            WiringManager.Instance.NotifyElementDragEnded();
        }

        return true;
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

    private float GetBackpackBoundaryX()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        return targetCamera.ViewportToWorldPoint(new Vector3(backpackViewportWidth, 0.5f, -targetCamera.transform.position.z)).x;
    }

    private bool IsInsideBackpackArea(Vector3 worldPosition)
    {
        return worldPosition.x <= GetBackpackBoundaryX();
    }

    private void ReclaimToBackpack()
    {
        var element = GetComponent<CircuitElement>();
        if (element == null)
        {
            return;
        }

        if (WiringManager.Instance != null)
        {
            WiringManager.Instance.RemoveConnectionsForElement(element);
        }

        BackpackItemSpawner.AddInventoryToType(element.ElementType, 1, element.Length, element.Width);
        Destroy(gameObject);
    }

    private bool IsLocked()
    {
        var element = GetComponent<CircuitElement>();
        return element != null && element.IsLocked;
    }
}
