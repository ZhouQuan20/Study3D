using System;
using UnityEngine;

namespace Study3D.Combat
{
    /// <summary>
    /// 通用血量与生命周期管理组件。
    /// 遵循观察者模式（Observer Pattern）与单一职责原则（SRP）：
    /// 1. 负责血量数值增减与死亡状态维护。
    /// 2. 不直接引用任何 UI、特效、音频组件。
    /// 3. 通过 C# event Action 广播状态变动，外部组件自由订阅响应。
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [Header("血量配置")]
        [Tooltip("最大生命值")]
        [SerializeField] private int maxHealth = 100;

        [Tooltip("初始生命值（默认为 0 则自动同步为最大生命值）")]
        [SerializeField] private int startingHealth = 0;

        // 核心血量属性
        public int MaxHealth => maxHealth;
        public int CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0;

        #region 事件系统 (Events)

        /// <summary>
        /// 血量发生改变时触发：参数为 (当前血量, 最大血量)
        /// 典型用途：UI 血条更新、数值文本刷新
        /// </summary>
        public event Action<int, int> OnHealthChanged;

        /// <summary>
        /// 受到伤害时触发：参数为 (扣除血量, 命中位置坐标)
        /// 典型用途：受击闪白 (DamageFlash)、受击硬直动画、伤害飘字
        /// </summary>
        public event Action<int, Vector3> OnDamaged;

        /// <summary>
        /// 生命值耗尽死亡时触发
        /// 典型用途：播放死亡动画、击杀计数、掉落金币道具、销毁/回收物体
        /// </summary>
        public event Action OnDeath;

        #endregion

        private void Awake()
        {
            ResetHealth();
        }

        /// <summary>
        /// 承受伤害（实现 IDamageable 接口）
        /// </summary>
        public void TakeDamage(int damageAmount, Vector3 hitPoint = default)
        {
            // 已死亡或伤害值不合法时忽略
            if (IsDead || damageAmount <= 0)
            {
                Debug.LogWarning($"[{name}] 无效伤害请求：IsDead={IsDead}, damageAmount={damageAmount}");
                return;
            }

            // 扣除血量并钳制在 [0, maxHealth] 范围内
            CurrentHealth = Mathf.Max(0, CurrentHealth - damageAmount);

            // 广播受击事件
            OnDamaged?.Invoke(damageAmount, hitPoint);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

            // 检查死亡
            if (CurrentHealth <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// 恢复生命值
        /// </summary>
        public void Heal(int healAmount)
        {
            if (IsDead || healAmount <= 0)
            {
                return;
            }

            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + healAmount);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        /// <summary>
        /// 重置并回满生命值（常用于对象池复用或实体复活）
        /// </summary>
        public void ResetHealth()
        {
            CurrentHealth = startingHealth > 0 ? Mathf.Min(startingHealth, maxHealth) : maxHealth;
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        private void Die()
        {
            OnDeath?.Invoke();
        }
    }
}
