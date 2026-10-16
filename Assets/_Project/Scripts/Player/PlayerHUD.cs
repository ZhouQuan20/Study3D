using UnityEngine;
using UnityEngine.UI;
using Study3D.Combat;

namespace Study3D.Player
{
    /// <summary>
    /// 商业级玩家战斗 HUD 控制器 (Player Combat HUD Controller)。
    /// 
    /// 核心亮点：
    /// 1. 【观察者模式解耦】：严格订阅 Health.OnHealthChanged 与 Health.OnDamaged，数据层与表现层互不强引用。
    /// 2. 【3A 级双层缓冲血条 (Dual-Layer Lerp Bar)】：
    ///    - 顶层主血条：受击瞬时扣减，直接反映真实残存血量。
    ///    - 底层残影条：受击后在原位置停留缓冲（Delay），随后平滑追赶（Lerp Catch-up），视觉冲击感拉满。
    /// 3. 【濒死危机心跳 (Low Health Pulse)】：血量低于 25% 时触发正弦波心跳闪烁，营造绝境紧迫感。
    /// 4. 【双模自适应渲染 (Hybrid Adaptive)】：
    ///    - 零门槛自绘模式：无需在场景里手动搭建繁琐的 Canvas，挂载脚本即刻在左下角渲染现代科技风战术血条。
    ///    - 商业 UGUI 插槽：若未来拖入美术精致 UI（Slider/Image/Text），自动无缝切换为 UGUI 驱动，支持零代码重构升级。
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerHUD : MonoBehaviour
    {
        [Header("双层缓冲血条参数")]
        [Tooltip("受击后残影条在原位置停留等待的时间（秒）")]
        [SerializeField] private float ghostDelay = 0.4f;

        [Tooltip("残影条平滑追赶主血条的衰减速度")]
        [SerializeField] private float ghostCatchupSpeed = 2.0f;

        [Tooltip("濒死危机警报血量阈值（比例 0~1）")]
        [Range(0.1f, 0.5f)]
        [SerializeField] private float lowHealthThreshold = 0.25f;

        [Header("外观配置（纯代码自绘模式）")]
        [Tooltip("血条屏幕左下角偏移坐标")]
        [SerializeField] private Vector2 screenOffset = new Vector2(30f, 30f);

        [Tooltip("血条宽度（像素）")]
        [SerializeField] private float barWidth = 260f;

        [Tooltip("血条高度（像素）")]
        [SerializeField] private float barHeight = 22f;

        [Tooltip("健康主血条颜色")]
        [SerializeField] private Color healthColor = new Color(0.2f, 0.85f, 0.35f, 0.95f);

        [Tooltip("残影缓冲条颜色（经典亮黄或战术白）")]
        [SerializeField] private Color ghostColor = new Color(1f, 0.85f, 0.3f, 0.85f);

        [Tooltip("背景底槽颜色")]
        [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.8f);

        [Header("可选 UGUI 插槽（若有美术制作的 UI 则自动优先使用）")]
        [SerializeField] private Image uguiPrimaryBar;
        [SerializeField] private Image uguiGhostBar;
        [SerializeField] private Text uguiHealthText;

        // 核心数值与插值状态
        private Health _health;
        private float _currentHealthRatio = 1f;
        private float _ghostHealthRatio = 1f;
        private float _ghostDelayTimer = 0f;
        private int _cachedCurrentHp = 100;
        private int _cachedMaxHp = 100;

        // 自绘纯色纹理缓存（零垃圾回收）
        private Texture2D _whitePixelTexture;
        private GUIStyle _textStyle;

        private void Awake()
        {
            _health = GetComponent<Health>();

            // 创建 1x1 共享单像素纯色纹理，用于 GUI.DrawTexture 绘制无锯齿色块
            _whitePixelTexture = new Texture2D(1, 1);
            _whitePixelTexture.SetPixel(0, 0, Color.white);
            _whitePixelTexture.Apply();

            _cachedCurrentHp = _health.CurrentHealth;
            _cachedMaxHp = _health.MaxHealth;
            _currentHealthRatio = _cachedMaxHp > 0 ? (float)_cachedCurrentHp / _cachedMaxHp : 1f;
            _ghostHealthRatio = _currentHealthRatio;
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.OnHealthChanged += HandleHealthChanged;
                _health.OnDamaged += HandleDamaged;
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnHealthChanged -= HandleHealthChanged;
                _health.OnDamaged -= HandleDamaged;
            }
        }

        private void Update()
        {
            // 驱动残影缓冲条的平滑追赶
            if (_ghostDelayTimer > 0f)
            {
                _ghostDelayTimer -= Time.deltaTime;
            }
            else if (_ghostHealthRatio > _currentHealthRatio)
            {
                // 以平滑插值 Lerp 追赶主血条
                _ghostHealthRatio = Mathf.MoveTowards(_ghostHealthRatio, _currentHealthRatio, ghostCatchupSpeed * Time.deltaTime);
            }
            else
            {
                _ghostHealthRatio = _currentHealthRatio;
            }

            // 若配置了 UGUI，则同步更新 UGUI 组件
            UpdateUGUI();
        }

        /// <summary>
        /// 响应血量变动：主血条瞬时扣除，更新文本
        /// </summary>
        private void HandleHealthChanged(int current, int max)
        {
            _cachedCurrentHp = current;
            _cachedMaxHp = max;

            float newRatio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;

            // 如果是回血，残影条立刻同步跟上
            if (newRatio >= _currentHealthRatio)
            {
                _ghostHealthRatio = newRatio;
                _ghostDelayTimer = 0f;
            }

            _currentHealthRatio = newRatio;
        }

        /// <summary>
        /// 响应受击事件：重置残影条的停留缓冲计时器
        /// </summary>
        private void HandleDamaged(int damage, Vector3 hitPoint)
        {
            _ghostDelayTimer = ghostDelay;
        }

        private void UpdateUGUI()
        {
            if (uguiPrimaryBar != null)
            {
                uguiPrimaryBar.fillAmount = _currentHealthRatio;
            }
            if (uguiGhostBar != null)
            {
                uguiGhostBar.fillAmount = _ghostHealthRatio;
            }
            if (uguiHealthText != null)
            {
                uguiHealthText.text = $"{_cachedCurrentHp} / {_cachedMaxHp}";
            }
        }

        /// <summary>
        /// 零门槛即开即用的战术科技风自绘 HUD
        /// </summary>
        private void OnGUI()
        {
            // 若用户已经绑定了完整 UGUI，则关闭自绘以防重叠
            if (uguiPrimaryBar != null) return;

            // 确保样式初始化
            if (_textStyle == null)
            {
                _textStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 14,
                    alignment = TextAnchor.MiddleLeft
                };
            }

            // 计算左下角绝对像素坐标
            float startX = screenOffset.x;
            float startY = Screen.height - screenOffset.y - barHeight;

            // 1. 绘制暗色外底框与背景槽
            Rect bgRect = new Rect(startX - 2f, startY - 2f, barWidth + 4f, barHeight + 4f);
            DrawRect(bgRect, new Color(0f, 0f, 0f, 0.6f));
            DrawRect(new Rect(startX, startY, barWidth, barHeight), backgroundColor);

            // 2. 绘制底层残影缓冲条 (Ghost Bar)
            if (_ghostHealthRatio > 0.001f)
            {
                Rect ghostRect = new Rect(startX, startY, barWidth * _ghostHealthRatio, barHeight);
                DrawRect(ghostRect, ghostColor);
            }

            // 3. 绘制顶层主血条 (Primary Bar，低血量时有心跳红光呼吸)
            Color activeColor = healthColor;
            if (_currentHealthRatio <= lowHealthThreshold && _currentHealthRatio > 0f)
            {
                // 正弦波心跳闪烁 (8Hz 快速搏动)
                float pulse = (Mathf.Sin(Time.time * 8f) + 1f) * 0.5f;
                activeColor = Color.Lerp(new Color(0.9f, 0.15f, 0.15f), new Color(1f, 0.4f, 0.4f), pulse);
            }

            if (_currentHealthRatio > 0.001f)
            {
                Rect primaryRect = new Rect(startX, startY, barWidth * _currentHealthRatio, barHeight);
                DrawRect(primaryRect, activeColor);
            }

            // 4. 绘制文字信息 (HP: 85 / 100)
            _textStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(startX + 8f, startY, barWidth, barHeight), $"HP  {_cachedCurrentHp} / {_cachedMaxHp}", _textStyle);
        }

        private void DrawRect(Rect rect, Color color)
        {
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _whitePixelTexture);
            GUI.color = oldColor;
        }

        private void OnDestroy()
        {
            if (_whitePixelTexture != null)
            {
                Destroy(_whitePixelTexture);
            }
        }
    }
}
