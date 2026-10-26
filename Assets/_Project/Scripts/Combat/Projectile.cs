using UnityEngine;
using UnityEngine.Pool;

namespace Study3D.Combat
{
    /// <summary>
    /// 子弹飞行与回收逻辑。
    /// 核心理念：
    /// 1. 不使用 Destroy()，生命周期结束或命中目标时归还给对象池（IObjectPool）。
    /// 2. 带有防双重回收标志位，确保状态安全。
    /// 3. 支持重置拖尾（TrailRenderer），避免复用时出现瞬移拉丝。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        [Header("飞行参数")]
        [SerializeField] private float speed = 15f;
        [SerializeField] private float lifetime = 3f;
        [SerializeField] private int damage = 10;

        private IObjectPool<Projectile> _pool;
        private float _currentLifeTimer;
        private bool _isReleased;
        private TrailRenderer _trailRenderer;

        private void Awake()
        {
            _trailRenderer = GetComponentInChildren<TrailRenderer>();
            
            // 确保 Collider 设置为 Trigger，避免与实体产生刚性物理推挤
            var col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            // 【关键物理机制】：Unity 要求发生 Trigger 碰撞的双方必须至少有一方带有 Rigidbody。
            // 将子弹设为 Kinematic + 无重力，既能触发静态物体的 OnTriggerEnter，又不会受重力下坠。
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        /// <summary>
        /// 从对象池取出时调用，重置子弹状态与出生位置
        /// </summary>
        public void Init(IObjectPool<Projectile> pool, Vector3 spawnPosition, Quaternion spawnRotation)
        {
            _pool = pool;
            transform.position = spawnPosition;
            transform.rotation = spawnRotation;
            
            _currentLifeTimer = 0f;
            _isReleased = false;

            // 关键细节：如果子弹有拖尾效果，复用时必须先清除上一局残留的历史轨迹
            if (_trailRenderer != null)
            {
                _trailRenderer.Clear();
            }
        }

        private void Update()
        {

            // 沿自身朝向直线飞行
            transform.position += transform.forward * (speed * Time.deltaTime);

            // 超时回收机制（飞出屏幕/距离过远）
            _currentLifeTimer += Time.deltaTime;
            if (_currentLifeTimer >= lifetime)
            {
                ReleaseToPool();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // 避免打中发射者自己（可通过 Layer 物理层或 Tag 过滤）
            if (other.CompareTag("Player"))
            {
                return;
            }

            // 查询目标（或其父节点）是否实现了通用受击契约 IDamageable
            var damageable = other.GetComponentInParent<IDamageable>();
            Debug.Log($"[{nameof(Projectile)}] 命中物体: {other.name}, Tag: {other.tag}, 契约检测: {(damageable != null ? "有效(IDamageable)" : "无契约")}");

            if (damageable != null && !damageable.IsDead)
            {
                damageable.TakeDamage(damage, transform.position);
            }

            // 命中后归还对象池
            ReleaseToPool();
        }

        /// <summary>
        /// 安全归还对象池
        /// </summary>
        private void ReleaseToPool()
        {
            if (_isReleased) return;
            _isReleased = true;

            if (_pool != null)
            {
                _pool.Release(this);
            }
            else
            {
                // 保底机制：若未通过对象池生成，回退到 Destroy
                Destroy(gameObject);
            }
        }
    }
}
