using System.Collections;
using Study3D.AI;
using UnityEngine;

namespace Study3D.Combat
{
    /// <summary>
    /// 靶子假人 / 基础敌人行为控制器。
    /// 核心职责：
    /// 1. 监听 Health.OnDeath 事件，处理“死亡表现”（禁用碰撞、变暗/倒下）。
    /// 2. 支持可配置的自动复活重置机制（方便教学与射击测试）。
    /// 3. 与 Health 组件保持事件解耦。
    /// </summary>
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(EnemyAI))]
    public class TargetDummy : MonoBehaviour
    {
        [Header("重生与销毁")]
        [Tooltip("死亡后是否自动复活（测试射击系统时建议开启）")]
        [SerializeField] private bool autoRespawn = true;

        [Tooltip("自动复活延迟时间（秒）")]
        [SerializeField] private float respawnDelay = 1.5f;

        private Health _health;
        private Collider _collider;
        private EnemyAI _enemyAI;
        private Renderer[] _renderers;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _collider = GetComponent<Collider>();
            _renderers = GetComponentsInChildren<Renderer>();
            _enemyAI = GetComponent<EnemyAI>();

            _originalPosition = transform.position;
            _originalRotation = transform.rotation;
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.OnDeath += HandleDeath;
                _health.OnDamaged += HandleDamaged;
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDeath -= HandleDeath;
                _health.OnDamaged -= HandleDamaged;
            }
        }

        private void HandleDamaged(int damage, Vector3 hitPoint)
        {
            Debug.Log($"[{name}] 受到伤害: {damage}, 剩余血量: {_health.CurrentHealth}/{_health.MaxHealth}");
        }

        private void HandleDeath()
        {
            Debug.Log($"[{name}] 已被击毁！");

            // 死亡时禁用碰撞体，防止子弹继续命中尸体
            if (_collider != null)
            {
                _collider.enabled = false;
            }

            if (autoRespawn)
            {
                StartCoroutine(RespawnRoutine());
            }
            else
            {
                StartCoroutine(RespawnTogether());
            }
        }

        private IEnumerator RespawnRoutine()
        {
            // 倒下隐藏
            // SetRenderersVisible(false);

            // 等待复活动画
            yield return new WaitForSeconds(respawnDelay);

            if (_enemyAI != null)
            {
                _enemyAI.TriggerRevive();
            }

            // 等待复活动画播完
            yield return new WaitForSeconds(5f);

            if (_enemyAI != null)
            {
                _enemyAI.ResetAI();
            }

            // 回满血量并恢复碰撞体
            _health.ResetHealth();

            if (_collider != null)
            {
                _collider.enabled = true;
            }

            Debug.Log($"[{name}] 靶子已原地复活！");
        }

        private IEnumerator RespawnTogether()
        {
            // 等待复活动画
            yield return new WaitForSeconds(respawnDelay);

           
        }

        private void SetRenderersVisible(bool visible)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].enabled = visible;
                }
            }
        }
    }
}
