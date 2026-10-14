using UnityEngine;
using UnityEngine.AI;

namespace Study3D.AI
{
    /// <summary>
    /// 敌人动画驱动与视觉反馈组件。
    /// 核心职责：
    /// 1. 【移动动画驱动】：自动提取 NavMeshAgent 的物理位移速度，通过阻尼平滑注入混合树（Speed 浮点参数），实现不滑步、不抽搐的拟真奔跑。
    /// 2. 【攻击动作联动】：解耦监听 EnemyAI.OnAttack 事件，语义化触发攻击动作（Attack 触发器，支持 Inspector 自由配置，并对历史 Shoot 进行友好向下兼容）。
    /// 3. 【无模型兜底机制】：在没有 Animator 的白模（如胶囊体）阶段，自动切换材质警示色反馈追击与待机状态，保障开发调试体验。
    /// </summary>
    public class EnemyAnimation : MonoBehaviour
    {
        [Header("动画组件引用")]
        [Tooltip("敌人的 Animator 组件（若留空则自动从自身或子物体查找）")]
        [SerializeField] private Animator animator;

        [Header("动画参数配置（解耦与语义化）")]
        [Tooltip("驱动移动混合树的浮点参数名称")]
        [SerializeField] private string speedParameterName = "Speed";

        [Tooltip("触发攻击动画的触发器名称（近战建议设为 Attack，远程射击可设为 Shoot，自爆可设为 Explode）")]
        [SerializeField] private string attackTriggerName = "Attack";

        [Header("速度阻尼参数")]
        [Tooltip("速度插值平滑阻尼时间（秒），避免瞬时速度跳变导致的动作抽搐")]
        [SerializeField] private float speedDampTime = 0.15f;

        [Tooltip("敌人参考最大奔跑速度（用于将实际速度归一化到 0~1）")]
        [SerializeField] private float maxExpectedSpeed = 4.5f;

        // 运行时哈希缓存（避免每帧字符串运算性能浪费）
        private int _speedHash;
        private int _attackHash;
        private bool _hasSpeedParam;
        private bool _hasAttackParam;

        private NavMeshAgent _agent;
        private EnemyAI _enemyAI;
        private Renderer _renderer;
        private Color _originalColor;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _enemyAI = GetComponent<EnemyAI>();

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            CacheAndValidateParameters();

            // 缓存渲染器与初始材质颜色（用于白模无动画时的变色兜底）
            _renderer = GetComponentInChildren<Renderer>();
            if (_renderer != null && _renderer.sharedMaterial != null && _renderer.sharedMaterial.HasProperty("_BaseColor"))
            {
                _originalColor = _renderer.sharedMaterial.GetColor("_BaseColor");
            }
        }

        private void OnEnable()
        {
            if (_enemyAI != null)
            {
                _enemyAI.OnAttack += HandleAttackAnimation;
                _enemyAI.OnStateChanged += HandleStateChanged;
            }
        }

        private void OnDisable()
        {
            if (_enemyAI != null)
            {
                _enemyAI.OnAttack -= HandleAttackAnimation;
                _enemyAI.OnStateChanged -= HandleStateChanged;
            }
        }

        private void Update()
        {
            if (_agent == null || animator == null || !_hasSpeedParam) return;

            // 提取物理寻路速度并归一化 (0 ~ 1)
            float currentSpeed = _agent.velocity.magnitude;
            float normalizedSpeed = Mathf.Clamp01(currentSpeed / maxExpectedSpeed);

            // 通过阻尼平滑注入 Animator 混合树
            animator.SetFloat(_speedHash, normalizedSpeed, speedDampTime, Time.deltaTime);
        }

        /// <summary>
        /// 响应 EnemyAI.OnAttack 广播，播放攻击动作
        /// </summary>
        private void HandleAttackAnimation()
        {
            if (animator != null && _hasAttackParam)
            {
                animator.SetTrigger(_attackHash);
            }
        }

        /// <summary>
        /// 响应状态切换：在没有骨骼动画的白模时期提供纯视觉变色反馈
        /// </summary>
        private void HandleStateChanged(EnemyAI.EnemyState state)
        {
            if (animator != null || _renderer == null) return;

            if (state == EnemyAI.EnemyState.Chase)
            {
                // 追逐状态亮起狂暴橙色
                _renderer.material.SetColor("_BaseColor", new Color(1f, 0.4f, 0f));
            }
            else if (state == EnemyAI.EnemyState.Idle)
            {
                // 待机恢复初始颜色
                _renderer.material.SetColor("_BaseColor", _originalColor);
            }
        }

        /// <summary>
        /// 初始化并校验 Animator 中的参数定义，兼具友好提示与向下兼容
        /// </summary>
        private void CacheAndValidateParameters()
        {
            _speedHash = Animator.StringToHash(speedParameterName);
            _attackHash = Animator.StringToHash(attackTriggerName);

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                _hasSpeedParam = false;
                _hasAttackParam = false;
                return;
            }

            _hasSpeedParam = HasParameter(animator, _speedHash);
            _hasAttackParam = HasParameter(animator, _attackHash);

            // 智能兼容：如果配置的 Attack 参数不存在，但发现了 Shoot 参数（例如借用了 Player 的动画控制器），自动回退并友好提示
            if (!_hasAttackParam)
            {
                int shootHash = Animator.StringToHash("Shoot");
                if (HasParameter(animator, shootHash))
                {
                    _attackHash = shootHash;
                    _hasAttackParam = true;
                    Debug.Log($"[EnemyAnimation] 当前 Animator 未找到 '{attackTriggerName}' 参数，但检测到 'Shoot'，已自动启用兼容回退。");
                }
            }
        }

        private static bool HasParameter(Animator targetAnimator, int paramHash)
        {
            if (targetAnimator == null || targetAnimator.runtimeAnimatorController == null) return false;
            foreach (var parameter in targetAnimator.parameters)
            {
                if (parameter.nameHash == paramHash) return true;
            }
            return false;
        }

        /// <summary>
        /// 外部或运行时动态替换模型时的配置接口
        /// </summary>
        public void SetAnimator(Animator newAnimator)
        {
            animator = newAnimator;
            CacheAndValidateParameters();
        }
    }
}
