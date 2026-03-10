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

    [SerializeField]
    private float dragStartThresholdPixels = 8f;

    [Header("Rotation")]
    [SerializeField]
    private float rotationSpeed = 90f; // 每秒旋转的角度

    private bool isDragging;
    private bool isPointerDown;
    private bool isRightMouseDown;
    private bool isMouseOver;
    private Vector3 dragOffset;
    private Vector3 pointerDownScreenPosition;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        transform.position = SnapToGrid(transform.position);
    }

    private void Update()
    {
        // 检测右键按下/抬起（仅在鼠标悬停且未拖拽时）
        if (isMouseOver && !isDragging)
        {
            if (Input.GetMouseButtonDown(1))
            {
                if (!IsLocked() && !IsWiringActive())
                {
                    isRightMouseDown = true;
                }
            }

            if (Input.GetMouseButtonUp(1))
            {
                isRightMouseDown = false;
            }
        }
        else
        {
            isRightMouseDown = false;
        }

        // 长按右键持续旋转
        if (isRightMouseDown && !IsLocked())
        {
            float rotationAmount = rotationSpeed * Time.deltaTime;
            transform.Rotate(0f, 0f, rotationAmount);
        }
    }

    private void OnMouseEnter()
    {
        isMouseOver = true;
    }

    private void OnMouseExit()
    {
        isMouseOver = false;
        isRightMouseDown = false;
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

            BeginDrag();
        }

        var world = GetMouseWorldPosition() + dragOffset;
        transform.position = SnapToGrid(world);
    }

    private void OnMouseUp()
    {
        isPointerDown = false;
        EndDrag();
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        var snapped = SnapToGrid(transform.position);
        if (IsInsideBackpackArea(snapped))
        {
            ReclaimToBackpack();
            return;
        }

        if (restrictToFieldArea)
        {
            var minFieldX = GetBackpackBoundaryX();
            snapped.x = Mathf.Max(minFieldX + 0.5f, snapped.x);
        }

        transform.position = snapped;
    }

    private void OnDisable()
    {
        isPointerDown = false;
        isRightMouseDown = false;
        isMouseOver = false;
        EndDrag();
    }

    public void BeginExternalDragAt(Vector3 worldPosition)
    {
        if (IsLocked())
        {
            return;
        }

        transform.position = SnapToGrid(worldPosition);
        isPointerDown = true;
        pointerDownScreenPosition = Input.mousePosition;
        BeginDrag();
        dragOffset = Vector3.zero;
    }

    private void BeginDrag()
    {
        if (isDragging)
        {
            return;
        }

        isDragging = true;
        dragOffset = transform.position - GetMouseWorldPosition();
        if (WiringManager.Instance != null)
        {
            WiringManager.Instance.NotifyElementDragStarted();
        }
    }

    private void EndDrag()
    {
        if (!isDragging)
        {
            return;
        }

        if (WiringManager.Instance != null)
        {
            WiringManager.Instance.NotifyElementDragEnded();
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

        BackpackItemSpawner.AddInventoryToType(element.ElementType, 1);
        Destroy(gameObject);
    }

    private bool IsLocked()
    {
        var element = GetComponent<CircuitElement>();
        return element != null && element.IsLocked;
    }

    private bool IsWiringActive()
    {
        // 检测连线系统是否正在预览连线（避免右键冲突）
        return WiringManager.Instance != null && WiringManager.Instance.AreTerminalsVisible;
    }
}
