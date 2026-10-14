using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Study3D.Combat
{
    /// <summary>
    /// 受击闪烁反馈组件。
    /// 核心亮点：
    /// 1. 【观察者模式】：主动监听 Health.OnDamaged 事件，Health 内部没有任何关于 Flash 的引用。
    /// 2. 【工业级性能】：使用 MaterialPropertyBlock 修改颜色，杜绝 Material 实例克隆造成的内存泄漏与合批破坏。
    /// 3. 【生命周期规范】：严格在 OnEnable 订阅、OnDisable 退订，防止内存泄漏。
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class DamageFlash : MonoBehaviour
    {
        [Header("闪烁参数")]
        [Tooltip("受击时瞬间变亮的闪烁颜色（对带贴图模型建议纯白或亮红色）")]
        [SerializeField] private Color flashColor = Color.white;

        [Tooltip("自发光发光强度（1~3倍，能让带贴图的人物模型产生极其明显的自发光高亮闪烁）")]
        [SerializeField] private float flashIntensity = 2.5f;

        [Tooltip("闪烁持续时间（秒）")]
        [SerializeField] private float flashDuration = 0.12f;

        // 缓存着色器属性 ID（静态只读，避免每帧字符串 Hash 计算）
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Health _health;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _propertyBlock;
        private Coroutine _flashCoroutine;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _renderers = GetComponentsInChildren<Renderer>();
            _propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                // 订阅受击事件
                _health.OnDamaged += OnDamaged;
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                // 规范退订事件：避免对象失活或销毁后产生悬空引用
                _health.OnDamaged -= OnDamaged;
            }

            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
                ResetColor();
            }
        }

        private void OnDamaged(int damageAmount, Vector3 hitPoint)
        {
            TriggerFlash();
        }

        /// <summary>
        /// 触发一次高光闪烁（支持 Inspector 右键菜单一键测试）
        /// </summary>
        [ContextMenu("Test Flash (测试闪烁)")]
        public void TriggerFlash()
        {
            // 如果已经在闪烁中，先停止旧协程再开启新协程
            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
            }

            // 动态防御：若渲染器列表为空，重新扫描子物体（防止模型动态加载后未捕获）
            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>();
            }

            Debug.Log($"[{name}] 收到受击通知，触发闪烁！(受影响渲染器数量: {(_renderers != null ? _renderers.Length : 0)})");

            _flashCoroutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            SetColor(flashColor);

            yield return new WaitForSeconds(flashDuration);

            ResetColor();
            _flashCoroutine = null;
        }

        private void SetColor(Color color)
        {
            if (_renderers == null) return;
            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            // 计算叠加光效：通过 Emission 和 overdriven BaseColor 穿透任何复杂贴图
            Color emissionColor = color * flashIntensity;
            Color overdrivenColor = color * flashIntensity;

            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;

                r.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(BaseColorId, overdrivenColor);
                _propertyBlock.SetColor(LegacyColorId, overdrivenColor);
                _propertyBlock.SetColor(EmissionColorId, emissionColor);
                r.SetPropertyBlock(_propertyBlock);
            }
        }

        private void ResetColor()
        {
            if (_renderers == null) return;
            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;

                // 清空 PropertyBlock，恢复材质球自带的原始贴图与色彩
                _propertyBlock.Clear();
                r.SetPropertyBlock(_propertyBlock);
            }
        }
    }
}
