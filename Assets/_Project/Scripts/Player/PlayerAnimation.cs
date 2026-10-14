using UnityEngine;
using Study3D.Combat;

namespace Study3D.Player
{
    /// <summary>
    /// 角色动画驱动组件。
    /// 核心职责：
    /// 1. 采集移动输入与物理移动速度，通过工业级平滑阻尼 (dampTime) 驱动 1D 混合树 (Speed 参数)。
    /// 2. 监听开火事件，触发射击动作 (Shoot Trigger)。
    /// 3. 与动画状态机完全解耦，支持 Animator 挂在根物体或 Model 子物体上。
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerAnimation : MonoBehaviour
    {
        [Header("动画组件引用")]
        [Tooltip("角色的 Animator 组件（若留空则自动从自身或子物体查找）")]
        [SerializeField] private Animator animator;

        [Header("速度阻尼参数")]
        [Tooltip("速度插值平滑阻尼时间（秒），越小切换越快，典型设为 0.1~0.15s")]
        [SerializeField] private float speedDampTime = 0.1f;

        // 动画参数字符串哈希缓存（提升每帧性能，避免字符串比较开销）
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int ShootHash = Animator.StringToHash("Shoot");

        private PlayerInputReader _inputReader;
        private CharacterController _characterController;

        private void Awake()
        {
            _inputReader = GetComponent<PlayerInputReader>();
            _characterController = GetComponent<CharacterController>();

            // 自动容错查找：若未手动拖拽，则优先找自身，再找子物体中的 Animator
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        private void Update()
        {
            if (animator == null) return;

            HandleLocomotionAnimation();
            HandleShootAnimation();
        }

        /// <summary>
        /// 驱动待机与奔跑混合树
        /// </summary>
        private void HandleLocomotionAnimation()
        {
            // 获取输入强度 (0 ~ 1)
            float targetSpeed = _inputReader.MoveInput.magnitude;

            // 若有 CharacterController，可结合实际地面水平速度进行双重校验
            if (_characterController != null && !_characterController.isGrounded)
            {
                // 空中时可以适当降低奔跑权重，此处暂保持地面输入值
            }

            // 使用 Unity 内置带阻尼时间的 SetFloat，生成由走至跑的自然过渡，告别生硬突变
            animator.SetFloat(SpeedHash, targetSpeed, speedDampTime, Time.deltaTime);
        }

        /// <summary>
        /// 响应开火动作
        /// </summary>
        private void HandleShootAnimation()
        {
            if (_inputReader.AttackTriggered)
            {
                animator.SetTrigger(ShootHash);
            }
        }

        /// <summary>
        /// 供外部或换装时动态重绑定 Animator
        /// </summary>
        public void SetAnimator(Animator newAnimator)
        {
            animator = newAnimator;
        }
    }
}
