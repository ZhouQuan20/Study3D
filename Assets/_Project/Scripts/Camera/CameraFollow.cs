using UnityEngine;

namespace Study3D.CameraControl
{
    /// <summary>
    /// 平滑跟随目标的相机控制器。
    /// 【重点】必须放在 LateUpdate 中执行：
    /// 确保角色在 Update 中完成所有物理和位移计算后，相机再进行跟随，避免画面产生微抖（Jitter）。
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("跟随目标")]
        [SerializeField] private Transform target;

        [Header("位置偏移与平滑度")]
        [Tooltip("相对于目标的偏移向量（后上方俯视）")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 6f, -7f);

        [Tooltip("相机跟随延迟时间（越小跟随越紧，越大越平滑）")]
        [SerializeField] private float smoothTime = 0.15f;

        private Vector3 _currentVelocity;

        private void LateUpdate()
        {
            if (target == null) return;

            // 计算目标期望位置
            Vector3 targetPosition = target.position + offset;

            // 使用 Vector3.SmoothDamp 进行无突变的物理平滑过渡
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _currentVelocity, smoothTime);

            // 保持相机视线锁定角色偏上方（如胸部/头部）
            transform.LookAt(target.position + Vector3.up * 1.2f);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }
    }
}
