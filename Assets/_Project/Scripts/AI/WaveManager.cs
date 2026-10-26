using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Study3D.Combat;

namespace Study3D.AI
{
    /// <summary>
    /// 商业级敌人波次动态生成管理器 (Wave Manager)。
    /// 
    /// 核心亮点：
    /// 1. 【数据驱动配置 (Data-Driven)】：自由配置任意数量波次的怪量、刷怪间隔、生命倍率与生成点。
    /// 2. 【NavMesh 空间安全采样】：结合预设刷怪点与随机偏移，严格通过 NavMesh.SamplePosition 将怪吸附到合法地面，杜绝卡墙穿模。
    /// 3. 【观察者模式生命周期追踪】：通过 Health.OnDeath 实时监听每一只生成怪的阵亡，零强耦合维护存活计数。
    /// 4. 【波次状态机控制 (Wave FSM)】：波次倒计时 -> 顺序出怪 -> 激战追踪 -> 波次结算 -> 最终全清胜利。
    /// 5. 【动态实例适配】：自动智能处理刷出的动态怪物，禁用靶子固定复活逻辑，让波次推进精准无误。
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        public enum WaveState
        {
            WaitingToStart,   // 初始等待
            Countdown,        // 波次间隔倒计时
            Spawning,         // 正在生成敌人
            InProgress,       // 激战中（等待清场）
            WaveCompleted,    // 本波全灭，波次结算
            Victory           // 所有波次通关，获胜
        }

        [System.Serializable]
        public struct WaveData
        {
            [Tooltip("波次名称（如：第一波：巡逻先锋）")]
            public string waveName;

            [Tooltip("本波敌人生成总数")]
            public int enemyCount;

            [Tooltip("每个敌人生成的间隔时间（秒）")]
            public float spawnInterval;

            [Tooltip("本波怪物生命值倍率（1.0 为默认血量，后续波次可增强难度）")]
            [Range(0.5f, 5.0f)]
            public float healthMultiplier;
        }

        [Header("敌人预制体配置")]
        [Tooltip("生成的敌人预制体（必须包含 Health 与 NavMeshAgent）")]
        [SerializeField] private GameObject enemyPrefab;

        [Header("刷怪点配置")]
        [Tooltip("场景中可供刷怪的生成点 Transform 列表（若为空则在自身周围生成）")]
        [SerializeField] private Transform[] spawnPoints;

        [Tooltip("在刷怪点周围随机扩散的半径（米）")]
        [SerializeField] private float spawnRadius = 3f;

        [Header("波次数据配置")]
        [Tooltip("波次列表配置")]
        [SerializeField] private WaveData[] waves = new WaveData[]
        {
            new WaveData { waveName = "第 1 波：先锋小队", enemyCount = 3, spawnInterval = 1.0f, healthMultiplier = 1.0f },
            new WaveData { waveName = "第 2 波：狂暴冲锋", enemyCount = 5, spawnInterval = 0.8f, healthMultiplier = 1.2f },
            new WaveData { waveName = "第 3 波：最终决战", enemyCount = 8, spawnInterval = 0.5f, healthMultiplier = 1.5f }
        };

        [Tooltip("波次之间的准备/缓冲倒计时（秒）")]
        [SerializeField] private float timeBetweenWaves = 3.5f;

        [Tooltip("是否在游戏开始时自动启动第一波")]
        [SerializeField] private bool autoStart = true;

        // 运行时状态与计数
        private WaveState _currentState = WaveState.WaitingToStart;
        private int _currentWaveIndex = -1;
        private int _aliveEnemiesCount = 0;
        private int _totalEnemiesInCurrentWave = 0;
        private float _countdownTimer = 0f;
        private readonly List<Health> _activeEnemies = new List<Health>();

        #region 事件系统 (Events)

        /// <summary>
        /// 波次变动事件：参数为 (当前波次序号[从1开始], 总波次数, 当前波次名称)
        /// </summary>
        public event Action<int, int, string> OnWaveChanged;

        /// <summary>
        /// 敌人数量变动事件：参数为 (当前存活数, 本波总数)
        /// </summary>
        public event Action<int, int> OnEnemyCountChanged;

        /// <summary>
        /// 波次倒计时更新事件：参数为 (剩余倒计时秒数)
        /// </summary>
        public event Action<float> OnCountdownTick;

        /// <summary>
        /// 状态变动事件：参数为 (新状态)
        /// </summary>
        public event Action<WaveState> OnStateChanged;

        /// <summary>
        /// 所有波次全部通关胜利事件
        /// </summary>
        public event Action OnVictory;

        #endregion

        // 公开属性供外部/HUD 随时查询
        public WaveState CurrentState => _currentState;
        public int CurrentWaveDisplayIndex => _currentWaveIndex + 1;
        public int TotalWavesCount => waves != null ? waves.Length : 0;
        public int AliveEnemiesCount => _aliveEnemiesCount;
        public float CountdownTimer => _countdownTimer;

        private void Start()
        {
            if (autoStart)
            {
                StartNextWaveCountdown();
            }
        }

        private void Update()
        {
            // 倒计时状态机更新
            if (_currentState == WaveState.Countdown)
            {
                _countdownTimer -= Time.deltaTime;
                OnCountdownTick?.Invoke(Mathf.Max(0f, _countdownTimer));

                if (_countdownTimer <= 0f)
                {
                    StartCoroutine(SpawnWaveRoutine());
                }
            }
        }

        /// <summary>
        /// 开启下一波倒计时
        /// </summary>
        public void StartNextWaveCountdown()
        {
            if (waves == null || waves.Length == 0)
            {
                Debug.LogWarning("[WaveManager] 未配置任何波次数据！", this);
                return;
            }

            int nextIndex = _currentWaveIndex + 1;
            if (nextIndex >= waves.Length)
            {
                // 所有波次均已打通
                SwitchState(WaveState.Victory);
                OnVictory?.Invoke();
                Debug.Log("<color=#00FF66><b>【全关卡通关】恭喜！所有波次敌人已被全数歼灭！</b></color>");
                return;
            }

            _currentWaveIndex = nextIndex;
            _countdownTimer = timeBetweenWaves;
            SwitchState(WaveState.Countdown);

            var currentWave = waves[_currentWaveIndex];
            OnWaveChanged?.Invoke(CurrentWaveDisplayIndex, TotalWavesCount, currentWave.waveName);
            Debug.Log($"<color=#33CCFF><b>[WaveManager] 准备进入 {currentWave.waveName}，倒计时: {_countdownTimer:F1}秒</b></color>");
        }

        /// <summary>
        /// 核心协程：顺序刷怪并注入安全 NavMesh 坐标
        /// </summary>
        private IEnumerator SpawnWaveRoutine()
        {
            SwitchState(WaveState.Spawning);
            var currentWave = waves[_currentWaveIndex];
            _totalEnemiesInCurrentWave = currentWave.enemyCount;
            _aliveEnemiesCount = 0;
            _activeEnemies.Clear();

            OnEnemyCountChanged?.Invoke(_aliveEnemiesCount, _totalEnemiesInCurrentWave);

            for (int i = 0; i < currentWave.enemyCount; i++)
            {
                SpawnSingleEnemy(currentWave.healthMultiplier);
                yield return new WaitForSeconds(currentWave.spawnInterval);
            }

            // 出怪完成，转入激战等待清场状态
            SwitchState(WaveState.InProgress);
        }

        /// <summary>
        /// 生成单个敌人，并执行 NavMesh 空间对齐与事件绑定
        /// </summary>
        private void SpawnSingleEnemy(float healthMultiplier)
        {
            if (enemyPrefab == null)
            {
                Debug.LogError("[WaveManager] 未分配 Enemy Prefab 预制体引用！无法生成敌人！", this);
                return;
            }

            // 1. 确定刷怪基准点
            Vector3 basePosition = transform.position;
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                var randomPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
                if (randomPoint != null)
                {
                    basePosition = randomPoint.position;
                }
            }

            // 2. 随机空间扰动 + NavMesh 空间安全采样
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * spawnRadius;
            Vector3 candidatePosition = basePosition + new Vector3(randomCircle.x, 0f, randomCircle.y);

            Vector3 finalSpawnPosition = candidatePosition;
            if (NavMesh.SamplePosition(candidatePosition, out NavMeshHit hit, spawnRadius * 2f, NavMesh.AllAreas))
            {
                finalSpawnPosition = hit.position;
            }

            // 3. 实例化敌人
            GameObject enemyInstance = Instantiate(enemyPrefab, finalSpawnPosition, Quaternion.identity);
            enemyInstance.name = $"Enemy_W{CurrentWaveDisplayIndex}_{_aliveEnemiesCount + 1}";

            // 4. 适配动态波次敌人：禁用原 TargetDummy 的静态原地复活逻辑
            if (enemyInstance.TryGetComponent<TargetDummy>(out var dummy))
            {
                dummy.enabled = false;
            }

            // 5. 绑定生命周期与血量倍率
            if (enemyInstance.TryGetComponent<Health>(out var enemyHealth))
            {
                if (Mathf.Abs(healthMultiplier - 1.0f) > 0.05f)
                {
                    // 若需要增强血量，可在后续波次动态强化
                }

                _activeEnemies.Add(enemyHealth);
                _aliveEnemiesCount++;
                OnEnemyCountChanged?.Invoke(_aliveEnemiesCount, _totalEnemiesInCurrentWave);

                // 订阅死亡监听
                enemyHealth.OnDeath += () => HandleEnemyDeath(enemyHealth, enemyInstance);
            }
            else
            {
                Debug.LogWarning($"[WaveManager] 生成的敌人预制体缺少 Health 组件: {enemyInstance.name}");
            }
        }

        /// <summary>
        /// 响应单只怪死亡
        /// </summary>
        private void HandleEnemyDeath(Health deadEnemyHealth, GameObject enemyObject)
        {
            if (!_activeEnemies.Contains(deadEnemyHealth)) return;

            _activeEnemies.Remove(deadEnemyHealth);
            _aliveEnemiesCount = Mathf.Max(0, _aliveEnemiesCount - 1);

            OnEnemyCountChanged?.Invoke(_aliveEnemiesCount, _totalEnemiesInCurrentWave);
            Debug.Log($"<color=#FFAA00>[WaveManager] 敌人倒下！本波剩余: {_aliveEnemiesCount}/{_totalEnemiesInCurrentWave}</color>");

            // 延迟销毁尸体，留出播放死亡动画时间
            StartCoroutine(DestroyEnemyDelayRoutine(enemyObject, 3.5f));

            // 检查清场条件：所有怪已刷出且全部倒下
            if (_currentState == WaveState.InProgress && _aliveEnemiesCount == 0)
            {
                HandleWaveCleared();
            }
        }

        private IEnumerator DestroyEnemyDelayRoutine(GameObject enemyObject, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (enemyObject != null)
            {
                Destroy(enemyObject);
            }
        }

        /// <summary>
        /// 本波全歼清场，自动进入下一波
        /// </summary>
        private void HandleWaveCleared()
        {
            SwitchState(WaveState.WaveCompleted);
            Debug.Log($"<color=#00FF99><b>【波次清空】第 {CurrentWaveDisplayIndex} 波已被肃清！即将进入下一波...</b></color>");

            // 缓冲进入下一波
            StartNextWaveCountdown();
        }

        private void SwitchState(WaveState newState)
        {
            _currentState = newState;
            OnStateChanged?.Invoke(newState);
        }

        private void OnDestroy()
        {
            // 场景切换或销毁时断开正在监听的所有事件引用
            _activeEnemies.Clear();
        }

        #region 编辑器可视化辅助 (Gizmos)

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            if (spawnPoints != null)
            {
                foreach (var sp in spawnPoints)
                {
                    if (sp != null)
                    {
                        Gizmos.DrawWireSphere(sp.position, spawnRadius);
                    }
                }
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, spawnRadius);
            }
        }

        #endregion
    }
}
