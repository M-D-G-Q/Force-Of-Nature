using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("移动设置")]
    public float normalSpeed = 10f;      // 正常移动速度
    public float fastSpeed = 30f;        // 加速移动速度
    public float verticalSpeed = 8f;     // 垂直移动速度

    [Header("视角旋转设置")]
    public float rotationSensitivity = 3f; // 鼠标旋转灵敏度
    public float minPitch = -89f;          // 最小俯仰角
    public float maxPitch = 89f;           // 最大俯仰角

    [Header("缩放设置")]
    public float zoomSpeed = 5f;         // 滚轮缩放速度
    public float minFov = 15f;           // 最小视野
    public float maxFov = 90f;           // 最大视野

    // 内部变量
    private float yaw = 0f;      // 水平旋转角度
    private float pitch = 0f;    // 垂直旋转角度
    private Vector3 lastMousePos;

    void Start()
    {
        // 初始化角度，保持摄像机当前朝向
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
    }

    void Update()
    {
        HandleMovement();
        HandleRotation();
        HandleZoom();
    }

    /// <summary>
    /// WASD 移动 + 空格上升 + Shift 下降 + 左Ctrl加速
    /// </summary>
    void HandleMovement()
    {
        // 选择当前速度
        float currentSpeed = Input.GetKey(KeyCode.LeftControl) ? fastSpeed : normalSpeed;

        // 获取水平输入 (基于世界坐标)
        float h = 0f;
        float v = 0f;

        if (Input.GetKey(KeyCode.W)) v += 1f;
        if (Input.GetKey(KeyCode.S)) v -= 1f;
        if (Input.GetKey(KeyCode.A)) h -= 1f;
        if (Input.GetKey(KeyCode.D)) h += 1f;

        // 摄像机前方/右方 (在世界平面上的投影)
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        Vector3 moveDir = (forward * v + right * h).normalized;
        Vector3 moveDelta = moveDir * currentSpeed * Time.deltaTime;

        // 垂直移动 (空格上升 / Shift下降)
        if (Input.GetKey(KeyCode.Space))
        {
            moveDelta += Vector3.up * verticalSpeed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            moveDelta += Vector3.down * verticalSpeed * Time.deltaTime;
        }

        transform.position += moveDelta;
    }

    /// <summary>
    /// 按住鼠标中键拖动视角
    /// </summary>
    void HandleRotation()
    {
        if (Input.GetMouseButtonDown(2))
        {
            lastMousePos = Input.mousePosition;
        }

        if (Input.GetMouseButton(2))
        {
            Vector3 delta = Input.mousePosition - lastMousePos;
            yaw += delta.x * rotationSensitivity * 0.1f;
            pitch -= delta.y * rotationSensitivity * 0.1f;

            // 限制俯仰角，防止翻转
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            lastMousePos = Input.mousePosition;
        }
    }

    /// <summary>
    /// 鼠标滚轮缩放 (通过调整 FOV 实现)
    /// </summary>
    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            Camera cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.fieldOfView -= scroll * zoomSpeed;
                cam.fieldOfView = Mathf.Clamp(cam.fieldOfView, minFov, maxFov);
            }
        }
    }
}