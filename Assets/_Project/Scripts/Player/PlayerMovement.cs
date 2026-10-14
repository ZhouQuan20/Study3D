using Study3D.Combat;
using UnityEngine;

namespace Study3D.Player
{
    /// <summary>
    /// 核心角色移动逻辑：
    /// 1. 基于 CharacterController 进行无穿模、支持爬坡的位移
    /// 2. 相机视角对齐的 3D 平面位移（向相机前方移动）
    /// 3. 真实重力加速度与跳跃冲量数学计算
    /// 4. 平滑转向过渡
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("移动参数")]
        [Tooltip("基础移动速度（米/秒）")]
        [SerializeField] private float moveSpeed = 6f;

        [Tooltip("旋转平滑时间（越小转头越快）")]
        [SerializeField] private float rotationSmoothTime = 0.1f;

        [Header("跳跃与重力参数")]
        [Tooltip("重力加速度（负数，典型动作游戏常设为 -15 ~ -25 以获得紧凑扎实的手感）")]
        [SerializeField] private float gravity = -20f;

        [Tooltip("期望跳跃高度（米）")]
        [SerializeField] private float jumpHeight = 1.8f;

        [Tooltip("落地时保持的向下微小速度，防止下坡时浮空颠簸")]
        [SerializeField] private float groundedStickForce = -2f;

        [Header("瞄准与朝向")]
        [Tooltip("是否启用鼠标瞄准（开启后角色面朝鼠标位置，开枪更精准；关闭后面朝移动方向）")]
        [SerializeField] private bool useMouseAim = true;

        [Tooltip("瞄准旋转平滑速度")]
        [SerializeField] private float aimRotateSpeed = 20f;

        // 组件引用
        private CharacterController _controller;
        private PlayerInputReader _inputReader;
        private Transform _mainCameraTransform;
        private Health _health;


        // 运行时速度与旋转缓存
        private Vector3 _verticalVelocity;
        private float _currentTurnVelocity;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _inputReader = GetComponent<PlayerInputReader>();
            _health = GetComponent<Health>();

            if (Camera.main != null)
            {
                _mainCameraTransform = Camera.main.transform;
            }
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.OnDamaged += HandleDamaged;
            }
        }

        private void Update()
        {
            HandleMovement();
            HandleGravityAndJump();
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDamaged -= HandleDamaged;
            }
        }

        private void HandleDamaged(int damage, Vector3 hitPoint)
        {
            // 受击时可以添加一些反馈效果，例如屏幕闪白、受击动画等


            Debug.Log($"玩家受到伤害: {damage}, 剩余血量: {_health.CurrentHealth}/{_health.MaxHealth}");

        }

        /// <summary>
        /// 处理水平位移与平滑转向
        /// </summary>
        private void HandleMovement()
        {
            Vector2 input = _inputReader.MoveInput;
            Vector3 inputDirection = new Vector3(input.x, 0f, input.y).normalized;

            if (inputDirection.magnitude >= 0.1f)
            {
                // 计算相对于相机的世界移动方向（让 W 永远向屏幕上方走）
                Vector3 moveDirection = CalculateCameraRelativeDirection(inputDirection);

                // 若未启用鼠标瞄准，则角色朝向移动方向
                if (!useMouseAim)
                {
                    float targetAngle = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
                    float smoothAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _currentTurnVelocity, rotationSmoothTime);
                    transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f);
                }

                // 水平位移：速度 * 时间步长
                _controller.Move(moveDirection * (moveSpeed * Time.deltaTime));
            }

            // 处理鼠标瞄准旋转（无论移动与否均实时瞄准鼠标位置）
            if (useMouseAim)
            {
                HandleMouseAim();
            }
        }

        /// <summary>
        /// 基于鼠标在世界地面投影的平滑瞄准旋转
        /// </summary>
        private void HandleMouseAim()
        {
            if (_mainCameraTransform == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            // 获取当前鼠标屏幕像素坐标
            Vector2 mouseScreenPos;
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                mouseScreenPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            }
            else
            {
                mouseScreenPos = Input.mousePosition;
            }

            // 从摄像机发射一条穿透屏幕的射线
            Ray ray = cam.ScreenPointToRay(mouseScreenPos);
            // 构造过角色脚底高度的水平面，进行纯数学相交计算，免受物理碰撞干扰
            Plane groundPlane = new Plane(Vector3.up, transform.position);

            if (groundPlane.Raycast(ray, out float enterDistance))
            {
                Vector3 hitPoint = ray.GetPoint(enterDistance);
                Vector3 lookDirection = hitPoint - transform.position;
                lookDirection.y = 0f;

                if (lookDirection.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, aimRotateSpeed * Time.deltaTime);
                }
            }
        }

        /// <summary>
        /// 处理垂直方向的重力累积与跳跃判定
        /// </summary>
        private void HandleGravityAndJump()
        {
            // CharacterController 内置的着地检测
            if (_controller.isGrounded)
            {
                // 在地面时，垂直速度清为微弱负值，确保牢牢吸附地面
                if (_verticalVelocity.y < 0f)
                {
                    _verticalVelocity.y = groundedStickForce;
                }

                // 响应跳跃：利用物理公式 v = sqrt(2 * h * g)
                if (_inputReader.JumpTriggered)
                {
                    _verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }
            else
            {
                // 空中累积重力加速度: v += g * dt
                _verticalVelocity.y += gravity * Time.deltaTime;
            }

            // 应用垂直移动: deltaY = v * dt
            _controller.Move(_verticalVelocity * Time.deltaTime);
        }

        /// <summary>
        /// 根据相机朝向把平面的 2D 输入投影到 3D 地面空间
        /// </summary>
        private Vector3 CalculateCameraRelativeDirection(Vector3 inputDir)
        {
            if (_mainCameraTransform == null)
            {
                return inputDir;
            }

            // 获取相机在水平地面上的前方向量与右方向量（忽略 Y 轴高度倾角）
            Vector3 camForward = _mainCameraTransform.forward;
            Vector3 camRight = _mainCameraTransform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            // 组合成实际移动向量
            return camForward * inputDir.z + camRight * inputDir.x;
        }

        

    }
}
