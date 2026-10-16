using UnityEngine;

namespace Study3D.Player
{
    /// <summary>
    /// 场景坠落防御与安全重生保障器 (Fall Safeguard & Out-of-Bounds Respawner)。
    /// 
    /// 架构设计与工程哲学：
    /// 1. 【防御性兜底设计 (Defensive Graceful Degradation)】：
    ///    3D 动作/射击游戏中无论物理碰撞多么严密，偶发的高速穿模或边界缝隙均可能导致角色坠入虚空（Void Fall）。
    ///    本组件提供低开销、零 GC 的保底监控，彻底避免玩家因坠出世界导致主循环或相机矩阵崩溃。
    /// 2. 【CharacterController 物理位移规约】：
    ///    Unity 中修改带有 CharacterController 的物体坐标前，必须先将 controller.enabled 置为 false，
    ///    设定新坐标后再重新启用，否则 CC 内部的缓存位移体会立即覆盖坐标，导致传送失效或产生物理撕裂。
    /// </summary>
    public class FallSafeguard : MonoBehaviour
    {
        [Header("安全阈值")]
        [Tooltip("触发安全重生的 Y 坐标下限阈值")]
        [SerializeField] private float fallThreshold = -5.0f;

        [Tooltip("安全重生锚点坐标")]
        [SerializeField] private Vector3 respawnPosition = new Vector3(0f, 1.5f, -15f);

        private CharacterController _characterController;
        private Rigidbody _rigidbody;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (transform.position.y < fallThreshold)
            {
                RespawnToSafePosition();
            }
        }

        /// <summary>
        /// 安全瞬移回指定出生点，并清除所有残存物理动能
        /// </summary>
        public void RespawnToSafePosition()
        {
            Debug.LogWarning($"[{nameof(FallSafeguard)}] 警告：实体 {name} 坠出场景边界 (Y={transform.position.y:F2} < {fallThreshold})，正在执行安全规避重生 -> {respawnPosition}");

            // 1. 若挂载了 CharacterController，需临时解禁后再传送
            if (_characterController != null)
            {
                _characterController.enabled = false;
                transform.position = respawnPosition;
                _characterController.enabled = true;
            }
            else
            {
                transform.position = respawnPosition;
            }

            // 2. 清除刚体惯性与残余加速度
            if (_rigidbody != null && !_rigidbody.isKinematic)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }

        private void OnDrawGizmosSelected()
        {
            // 在编辑器视口中直观绘制深渊坠落红色警戒网格平面
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
            Gizmos.DrawCube(new Vector3(0f, fallThreshold, 0f), new Vector3(100f, 0.1f, 100f));

            // 绘制安全重生锚点绿色线框球
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(respawnPosition, 0.8f);
        }
    }
}
