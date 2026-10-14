using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Study3D.Combat;

namespace Study3D.Player
{
    /// <summary>
    /// 玩家死亡与游戏重开协调组件 (Player Death & Game Over Coordinator)。
    /// 
    /// 核心设计思想：
    /// 1. 【单一职责 (SRP)】：将死亡逻辑从 PlayerMovement 和 WeaponShooter 中完全剥离，避免逻辑污染。
    /// 2. 【控制权剥夺】：监听 Health.OnDeath，第一时间禁用移动、瞄准、跳跃与武器开火，杜绝“死后滑步漂移”与“尸体射击”。
    /// 3. 【物理与仇恨隔离】：禁用 CharacterController 物理碰撞，让场景中所有敌人停止攻击已死玩家。
    /// 4. 【优雅降级表现】：自动检测 Animator 是否有 "Die" 参数；若有则播放死亡动作，若无（白模或当前缺少死亡动作）则执行平滑倒地后仰，确保无报错、画面反馈到位。
    /// 5. 【轻量流程闭环】：死亡后弹出屏幕 Game Over 提示，并在倒计时结束后支持按 [R] 键快速重置场景。
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerDeathHandler : MonoBehaviour
    {
        [Header("重开与延迟配置")]
        [Tooltip("死亡后等待多少秒允许按 [R] 重新开始（给玩家留出观察死亡表现的缓冲时间）")]
        [SerializeField] private float restartDelay = 1.0f;

        [Header("死亡动画参数")]
        [Tooltip("触发死亡动作的 Trigger 参数名")]
        [SerializeField] private string deathTriggerName = "Die";

        [Header("白模倒地兜底")]
        [Tooltip("若无死亡动画时，倒地后仰的旋转速度")]
        [SerializeField] private float fallbackFallSpeed = 120f;

        private Health _health;
        private PlayerMovement _movement;
        private WeaponShooter _shooter;
        private CharacterController _characterController;
        private Animator _animator;
        private PlayerInputReader _inputReader;

        private bool _isDead = false;
        private bool _canRestart = false;
        private int _deathHash;
        private bool _hasDeathAnimParam = false;
        private bool _isFallingOver = false;
        private Quaternion _targetDeathRotation;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _movement = GetComponent<PlayerMovement>();
            _shooter = GetComponent<WeaponShooter>();
            _characterController = GetComponent<CharacterController>();
            _animator = GetComponentInChildren<Animator>();
            _inputReader = GetComponent<PlayerInputReader>();

            _deathHash = Animator.StringToHash(deathTriggerName);

            // 检查 Animator 是否配置了 Die 参数
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                foreach (var param in _animator.parameters)
                {
                    if (param.nameHash == _deathHash)
                    {
                        _hasDeathAnimParam = true;
                        break;
                    }
                }
            }
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.OnDeath += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDeath -= HandleDeath;
            }
        }

        private void Update()
        {
            // 兜底表现：平滑倒地后仰（无动画夹持时的视觉反馈）
            if (_isFallingOver)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, _targetDeathRotation, fallbackFallSpeed * Time.deltaTime);
                if (Quaternion.Angle(transform.rotation, _targetDeathRotation) < 1f)
                {
                    _isFallingOver = false;
                }
            }

            // 死亡倒计时就绪后，检测重开按键
            if (_isDead && _canRestart)
            {
                if (_inputReader != null && _inputReader.ReviveTriggered)
                {
                    RestartLevel();
                }

            }
        }

        /// <summary>
        /// 核心死亡处理逻辑
        /// </summary>
        private void HandleDeath()
        {
            if (_isDead) return;
            _isDead = true;

            Debug.Log("<color=red><b>[PlayerDeathHandler] 玩家生命值耗尽，已阵亡！</b></color>");

            // 1. 剥夺操作控制权（禁用位移、旋转和武器开火）
            if (_movement != null) _movement.enabled = false;
            if (_shooter != null) _shooter.enabled = false;

            // 2. 禁用物理控制器（防止推挤、并让敌人 AI 无法再对死尸进行碰撞判定）
            if (_characterController != null) _characterController.enabled = false;

            // 3. 驱动视觉死亡表现（动画 vs 物理倒地兜底）
            if (_hasDeathAnimParam && _animator != null)
            {
                _animator.SetTrigger(_deathHash);
            }
            else
            {
                // 优雅降级：向后倾倒 80 度平躺在地面
                _targetDeathRotation = Quaternion.Euler(-80f, transform.eulerAngles.y, 0f);
                if (_animator != null) _animator.enabled = false; // 若有 Animator，禁用它以避免动画干扰倒地旋转
                _isFallingOver = true;
            }

            // 4. 开启重开缓冲倒计时
            StartCoroutine(EnableRestartRoutine());
        }

        private IEnumerator EnableRestartRoutine()
        {
            yield return new WaitForSeconds(restartDelay);
            _canRestart = true;
        }

        private void RestartLevel()
        {
            // 恢复时间刻度（若后续有慢镜头）并重新加载当前场景
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// 商业级内建即时 GUI 反馈（无需提前在场景摆放复杂的 Canvas/UI 预制体，即开即用）
        /// </summary>
        private void OnGUI()
        {
            if (!_isDead) return;

            // 屏幕居中半透明深红警示横幅
            float boxWidth = 360f;
            float boxHeight = 110f;
            float posX = (Screen.width - boxWidth) * 0.5f;
            float posY = (Screen.height - boxHeight) * 0.4f;

            GUI.color = new Color(0.1f, 0.02f, 0.02f, 0.85f);
            GUI.Box(new Rect(posX, posY, boxWidth, boxHeight), GUIContent.none);

            // 标题样式
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = new Color(1f, 0.2f, 0.2f);

            // 提示样式
            GUIStyle tipStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Normal
            };
            tipStyle.normal.textColor = Color.white;

            GUI.color = Color.white;
            GUI.Label(new Rect(posX, posY + 12f, boxWidth, 40f), "GAME OVER", titleStyle);

            string promptText = _canRestart ? "按  [ R ]  键重新开始" : "正在阵亡结算中...";
            GUI.Label(new Rect(posX, posY + 58f, boxWidth, 30f), promptText, tipStyle);
        }
    }
}
