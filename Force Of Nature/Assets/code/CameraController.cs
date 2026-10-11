using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 自由飞行相机：不使用物理刚体，靠自己做球形扫描来防穿模。
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("移动设置")]
    public float normalSpeed = 10f;
    public float fastSpeed = 30f;
    public float verticalSpeed = 8f;

    [Header("视角旋转设置")]
    public float rotationSensitivity = 3f;
    public float minPitch = -89f;
    public float maxPitch = 89f;

    [Header("缩放设置")]
    public float zoomSpeed = 5f;
    public float minFov = 15f;
    public float maxFov = 90f;

    [Header("碰撞（防穿模）")]
    [Tooltip("相机的碰撞球半径，一般 0.3 ~ 0.5")]
    public float collisionRadius = 0.4f;
    [Tooltip("撞到东西后沿表面滑动，而不是硬停住")]
    public bool slideAlongSurface = true;
    [Tooltip("参与碰撞的层，默认全部")]
    public LayerMask collisionLayers = ~0;

    private float yaw = 0f;
    private float pitch = 0f;
    private Vector3 lastMousePos;
    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();

        // 关键：相机不需要 Rigidbody。
        // Kinematic Rigidbody 用 MovePosition 移动时，物理引擎不会做碰撞解算，
        // 会被地形直接穿过 —— 这正是之前穿模的原因。这里清掉残留组件。
        var rb = GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);

        var oldCol = GetComponent<CapsuleCollider>();
        if (oldCol != null) Destroy(oldCol);

        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
    }

    void Update()
    {
        HandleRotation();
        HandleZoom();
        HandleMovement();
    }

    void HandleMovement()
    {
        // ---------- 读取输入（兼容新旧输入系统）----------
        float h = 0f, v = 0f;
        bool fast = false, up = false, down = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.wKey.isPressed) v += 1f;
        if (kb.sKey.isPressed) v -= 1f;
        if (kb.aKey.isPressed) h -= 1f;
        if (kb.dKey.isPressed) h += 1f;
        fast = kb.leftCtrlKey.isPressed;
        up = kb.spaceKey.isPressed;
        down = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
#else
        if (Input.GetKey(KeyCode.W)) v += 1f;
        if (Input.GetKey(KeyCode.S)) v -= 1f;
        if (Input.GetKey(KeyCode.A)) h -= 1f;
        if (Input.GetKey(KeyCode.D)) h += 1f;
        fast = Input.GetKey(KeyCode.LeftControl);
        up = Input.GetKey(KeyCode.Space);
        down = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif

        // 水平移动只取相机朝向在水平面上的投影，避免"低头前进会钻地"
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = transform.right;
        Vector3 moveDir = forward * v + right * h;
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        float currentSpeed = fast ? fastSpeed : normalSpeed;

        Vector3 delta = moveDir * currentSpeed * Time.deltaTime;
        if (up) delta += Vector3.up * verticalSpeed * Time.deltaTime;
        if (down) delta += Vector3.down * verticalSpeed * Time.deltaTime;

        MoveWithCollision(delta);
    }

    /// <summary>带碰撞的位移：走得通就走，走不通就贴着表面滑。</summary>
    void MoveWithCollision(Vector3 delta)
    {
        float dist = delta.magnitude;
        if (dist < 1e-6f) return;

        Vector3 start = transform.position;
        Vector3 dir = delta / dist;
        float r = Mathf.Max(0.01f, collisionRadius - 0.02f);

        // 若当前位置已经陷在碰撞体里（例如手动把相机拖进了山体），
        // 就放开限制让你能飞出来，否则会永远卡死。
        if (Physics.CheckSphere(start, r, collisionLayers, QueryTriggerInteraction.Ignore))
        {
            transform.position = start + delta;
            return;
        }

        // 球形扫描：半径 r 的球从当前位置沿 dir 扫 dist 距离，会不会撞到东西
        if (!Physics.SphereCast(start, r, dir, out RaycastHit hit, dist,
                                collisionLayers, QueryTriggerInteraction.Ignore))
        {
            transform.position = start + delta;
            return;
        }

        // 撞上了：先走到碰撞点前方留一点余量
        float safe = Mathf.Max(0f, hit.distance - 0.01f);
        Vector3 pos = start + dir * safe;

        if (slideAlongSurface)
        {
            // 把剩余位移投影到碰撞面上 → 沿地形/墙面滑动
            Vector3 remain = dir * (dist - safe);
            Vector3 slide = Vector3.ProjectOnPlane(remain, hit.normal);
            if (slide.sqrMagnitude > 1e-8f &&
                !Physics.SphereCast(pos, r, slide.normalized, out _, slide.magnitude,
                                    collisionLayers, QueryTriggerInteraction.Ignore))
            {
                pos += slide;
            }
        }

        // 最终校验，杜绝漏网之鱼
        if (!Physics.CheckSphere(pos, r * 0.9f, collisionLayers, QueryTriggerInteraction.Ignore))
            transform.position = pos;
    }

    void HandleRotation()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.middleButton.wasPressedThisFrame)
            lastMousePos = mouse.position.ReadValue();

        if (mouse.middleButton.isPressed)
        {
            Vector3 delta = (Vector3)(mouse.position.ReadValue() - (Vector2)lastMousePos);
            yaw += delta.x * rotationSensitivity * 0.1f;
            pitch -= delta.y * rotationSensitivity * 0.1f;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);   // 不再用 rb.MoveRotation
            lastMousePos = mouse.position.ReadValue();
        }
#else
        if (Input.GetMouseButtonDown(2))
            lastMousePos = Input.mousePosition;

        if (Input.GetMouseButton(2))
        {
            Vector3 delta = Input.mousePosition - lastMousePos;
            yaw += delta.x * rotationSensitivity * 0.1f;
            pitch -= delta.y * rotationSensitivity * 0.1f;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            lastMousePos = Input.mousePosition;
        }
#endif
    }

    void HandleZoom()
    {
        float scroll = 0f;

#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null) scroll = mouse.scroll.ReadValue().y / 120f;
#else
        scroll = Input.GetAxis("Mouse ScrollWheel");
#endif

        if (Mathf.Abs(scroll) > 0.0001f && cam != null)
        {
            cam.fieldOfView -= scroll * zoomSpeed;
            cam.fieldOfView = Mathf.Clamp(cam.fieldOfView, minFov, maxFov);
        }
    }
}
