using UnityEngine;
using UnityEngine.Pool;
using Study3D.Player;

namespace Study3D.Combat
{
    /// <summary>
    /// 武器发射器与对象池管理器。
    /// 演示 Unity 官方工业级 UnityEngine.Pool.ObjectPool 的标准用法：
    /// 1. 0 垃圾回收（零 GC），告别运行时反复 Instantiate / Destroy 造成的掉帧。
    /// 2. 严密的池生命周期管控（创建、借出、归还、销毁）。
    /// 3. 控制射速与发射点计算。
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class WeaponShooter : MonoBehaviour
    {
        [Header("子弹配置")]
        [Tooltip("子弹预制体")]
        [SerializeField] private Projectile projectilePrefab;

        [Tooltip("子弹发射枪口挂点（若未指定则默认在角色前方略偏上）")]
        [SerializeField] private Transform firePoint;

        [Header("射击参数")]
        [Tooltip("每秒射击次数（发/秒）")]
        [SerializeField] private float fireRate = 3f;

        [Tooltip("是否支持按住左键连续连射")]
        [SerializeField] private bool autoFire = true;

        [Header("对象池配置")]
        [Tooltip("池子初始预热容量")]
        [SerializeField] private int defaultPoolCapacity = 20;

        [Tooltip("池子最大上限")]
        [SerializeField] private int maxPoolSize = 100;

        // 核心对象池接口
        private IObjectPool<Projectile> _projectilePool;
        private PlayerInputReader _inputReader;
        private float _nextFireTime;

        private void Awake()
        {
            _inputReader = GetComponent<PlayerInputReader>();

            // 初始化 Unity 官方内置的高性能对象池
            _projectilePool = new ObjectPool<Projectile>(
                createFunc: CreateProjectile,
                actionOnGet: OnGetFromPool,
                actionOnRelease: OnReleaseToPool,
                actionOnDestroy: OnDestroyPooledObject,
                collectionCheck: true, // 启用严格查重（防止同一个对象被反复 Release 两次导致逻辑损坏）
                defaultCapacity: defaultPoolCapacity,
                maxSize: maxPoolSize
            );
        }

        private void Update()
        {
            // 判定开火条件：支持单次点按或长按连射，且冷却时间已到
            bool wantsToFire = autoFire ? _inputReader.AttackHeld : _inputReader.AttackTriggered;

            if (wantsToFire && Time.time >= _nextFireTime)
            {
                _nextFireTime = Time.time + (1f / fireRate);
                Fire();
            }
        }

        private void Fire()
        {
            if (projectilePrefab == null)
            {
                Debug.LogWarning($"[{nameof(WeaponShooter)}] 未配置 Projectile Prefab，无法发射！");
                return;
            }

            // 计算发射起点与朝向
            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + transform.forward * 1.7f + Vector3.up * 0.7f + transform.right * 0.2f;
            Quaternion spawnRot = firePoint != null ? firePoint.rotation : transform.rotation;

            // 从池子中借出一颗子弹（如果池内有闲置直接复用，没有则自动触发 CreateProjectile）
            Projectile bullet = _projectilePool.Get();
            
            // 初始化子弹状态与位置
            bullet.Init(_projectilePool, spawnPos, spawnRot);
        }

        #region 对象池生命周期回调 (Pool Callbacks)

        /// <summary>
        /// 当池子空了且需要新实例时调用
        /// </summary>
        private Projectile CreateProjectile()
        {
            Projectile instance = Instantiate(projectilePrefab);
            // 保持层级视图整洁：可以统一挂在特定父节点下，或直接独立管理
            return instance;
        }

        /// <summary>
        /// 从池中借出时：激活物体
        /// </summary>
        private void OnGetFromPool(Projectile bullet)
        {
            bullet.gameObject.SetActive(true);
        }

        /// <summary>
        /// 归还到池中时：隐藏物体，停止其在场景中的渲染与物理消耗
        /// </summary>
        private void OnReleaseToPool(Projectile bullet)
        {
            bullet.gameObject.SetActive(false);
        }

        /// <summary>
        /// 当池子超出最大限制或场景销毁时清理
        /// </summary>
        private void OnDestroyPooledObject(Projectile bullet)
        {
            if (bullet != null)
            {
                Destroy(bullet.gameObject);
            }
        }

        #endregion
    }
}
