using UnityEngine;
using UnityEngine.AI;
using Study3D.Combat;

namespace Study3D.AI
{
    /// <summary>
    /// 敌人智能大脑（基于有限状态机 FSM 与 NavMesh 寻路）：
    /// 1. 待机（Idle）：静止侦测玩家进入警戒范围。
    /// 2. 追逐（Chase）：利用 NavMeshAgent 自动避障、沿地形起伏飞奔追逐目标。
    /// 3. 攻击（Attack）：进入攻击范围后停步，面朝玩家按攻速进行普攻。
    /// 4. 死亡（Dead）：生命值耗尽，立即停下寻路并退出战斗状态。
    /// 5. 受击仇恨联动：被子弹打中立即强行触发追击，实现敏锐受击感知。
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public class EnemyAI : MonoBehaviour
    {
        public enum EnemyState
        {
            Idle,
            Chase,
            Attack,
            Dead
        }

        [Header("感知与追击参数")]
        [Tooltip("玩家警戒发现半径（米）")]
        [SerializeField] private float detectionRadius = 8f;

        [Tooltip("近战攻击判定距离（米）")]
        [SerializeField] private float attackRange = 1.8f;

        [Tooltip("追击奔跑速度（米/秒）")]
        [SerializeField] private float chaseSpeed = 4.5f;

        [Header("攻击参数")]
        [Tooltip("每次攻击伤害数值")]
        [SerializeField] private int attackDamage = 10;

        [Tooltip("每秒攻击频率（次/秒）")]
        [SerializeField] private float attackRate = 1.0f;

        [Header("仇恨参数")]
        [Tooltip("受击激怒持续追击时间（秒）")]
        [SerializeField] private float damageAggroDuration = 6f;

        // 核心组件与目标缓存
        private NavMeshAgent _agent;
        private Health _health;
        private Transform _playerTransform;
        private IDamageable _playerDamageable;
        private EnemyState _currentState = EnemyState.Idle;
        private float _nextAttackTime;
        private float _aggroTimer;

        public EnemyState CurrentState => _currentState;

        /// <summary>
        /// AI 状态切换事件（参数为新状态）
        /// </summary>
        public event System.Action<EnemyState> OnStateChanged;

        /// <summary>
        /// AI 发动攻击事件
        /// </summary>
        public event System.Action OnAttack;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();

            // 初始配置寻路代理速度
            _agent.speed = chaseSpeed;
            _agent.stoppingDistance = attackRange * 0.8f;
        }

        private void Start()
        {
            FindPlayerTarget();
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.OnDamaged += HandleDamaged;
                _health.OnDeath += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDamaged -= HandleDamaged;
                _health.OnDeath -= HandleDeath;
            }
        }

        private void Update()
        {
            if (_currentState == EnemyState.Dead) return;

            // 递减仇恨激怒时间
            if (_aggroTimer > 0f)
            {
                _aggroTimer -= Time.deltaTime;
            }

            // 若丢失玩家引用，尝试重新寻找
            if (_playerTransform == null)
            {
                FindPlayerTarget();
                if (_playerTransform == null) return;
            }

            // 若玩家已阵亡，AI 放弃仇恨并停止追杀，重置回待机状态
            if (_playerDamageable != null && _playerDamageable.IsDead)
            {
                _aggroTimer = 0f;
                if (_currentState != EnemyState.Idle && _currentState != EnemyState.Dead)
                {
                    SwitchState(EnemyState.Idle);
                }
                return;
            }

            // 计算与玩家的水平平面距离（忽略 Y 轴高度差对判定的干扰）
            Vector3 diff = _playerTransform.position - transform.position;
            diff.y = 0f;
            float distanceToPlayer = diff.magnitude;

            // 状态机核心更新与流转
            switch (_currentState)
            {
                case EnemyState.Idle:
                    UpdateIdleState(distanceToPlayer);
                    break;

                case EnemyState.Chase:
                    UpdateChaseState(distanceToPlayer);
                    break;

                case EnemyState.Attack:
                    UpdateAttackState(distanceToPlayer, diff);
                    break;
            }
        }

        #region 状态更新逻辑 (FSM Updates)

        private void UpdateIdleState(float distanceToPlayer)
        {
            // 待机时停止寻路
            if (_agent.isOnNavMesh && !_agent.isStopped)
            {
                _agent.isStopped = true;
            }

            // 玩家进入警戒探测圈，或者处于受击激怒状态 -> 切换到追逐
            if (distanceToPlayer <= detectionRadius || _aggroTimer > 0f)
            {
                SwitchState(EnemyState.Chase);
            }
        }

        private void UpdateChaseState(float distanceToPlayer)
        {
            // 进入近战攻击范围 -> 切换到攻击
            if (distanceToPlayer <= attackRange)
            {
                SwitchState(EnemyState.Attack);
                return;
            }

            // 只有当没有受击激怒时，超出警戒范围 1.5 倍才会放弃追击；若在激怒期，哪怕距离较远也持续追赶！
            if (_aggroTimer <= 0f && distanceToPlayer > detectionRadius * 1.5f)
            {
                SwitchState(EnemyState.Idle);
                return;
            }

            // 追击行为：将导航终点实时对准玩家当前位置
            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.SetDestination(_playerTransform.position);
            }
        }

        private void UpdateAttackState(float distanceToPlayer, Vector3 horizontalDir)
        {
            // 攻击时停住脚步，专注出招
            if (_agent.isOnNavMesh && !_agent.isStopped)
            {
                _agent.isStopped = true;
            }

            // 玩家拉开距离超出攻击范围 -> 重新追赶
            if (distanceToPlayer > attackRange * 1.2f)
            {
                SwitchState(EnemyState.Chase);
                return;
            }

            // 平滑转身面向玩家
            if (horizontalDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(horizontalDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
            }

            // 执行周期性攻击判定
            if (Time.time >= _nextAttackTime)
            {
                _nextAttackTime = Time.time + (1f / attackRate);
                PerformAttack();
            }
        }

        private void PerformAttack()
        {
            if (_playerTransform == null) return;

            // 面向接口 IDamageable 对玩家造成伤害
            var playerDamageable = _playerTransform.GetComponentInParent<IDamageable>();
            if (playerDamageable != null && !playerDamageable.IsDead)
            {
                playerDamageable.TakeDamage(attackDamage, transform.position);
            }

            OnAttack?.Invoke();
        }

        #endregion

        #region 事件响应与状态转移 (Events & Transition)

        private void SwitchState(EnemyState newState)
        {
            if (_currentState == newState) return;
            Debug.Log($"<color=#FFA500>[{name}] 状态切换: {_currentState} -> {newState}</color>");
            _currentState = newState;
            OnStateChanged?.Invoke(newState);
        }

        /// <summary>
        /// 受击仇恨：在非死亡状态下被攻击，立即强制追击攻击者
        /// </summary>
        private void HandleDamaged(int damage, Vector3 hitPoint)
        {
            if (_currentState == EnemyState.Dead) return;

            // 刷新受击激怒时间（至少保持持续追杀）
            _aggroTimer = damageAggroDuration;

            Debug.Log($"<color=#FF4444>[{name}] 受到 {damage} 点伤害！进入仇恨激怒状态（持续 {damageAggroDuration} 秒），全力追杀玩家！</color>");

            // 立即切换为追逐状态
            SwitchState(EnemyState.Chase);
        }

        /// <summary>
        /// 死亡停机：关闭寻路与行为更新
        /// </summary>
        private void HandleDeath()
        {
            SwitchState(EnemyState.Dead);
            // 重置怒气
            _aggroTimer = 0;

            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                
            }
        }

        /// <summary>
        /// 供外部复活重置（如 TargetDummy 重生后调用）
        /// </summary>
        public void ResetAI()
        {
            SwitchState(EnemyState.Idle);
            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
        }

        private void FindPlayerTarget()
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
            {
                _playerTransform = playerGo.transform;
                _playerDamageable = playerGo.GetComponentInParent<IDamageable>();
            }
        }

        #endregion

        #region 编辑器可视化调试 (Gizmos)

        private void OnDrawGizmosSelected()
        {
            // 绘制绿色警戒感知范围
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);

            // 绘制红色攻击判定距离
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }

        #endregion
    }
}
