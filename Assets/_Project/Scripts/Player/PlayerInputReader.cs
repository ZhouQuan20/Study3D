using UnityEngine;
using UnityEngine.InputSystem;

namespace Study3D.Player
{
    /// <summary>
    /// 负责接收输入设备的数据，对外提供读取接口。
    /// 与具体的移动/战斗表现完全解耦。
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("输入监听配置")]
        [SerializeField] private InputActionAsset inputActions;

        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _attackAction;
        private InputAction _reviveAction;

        public Vector2 MoveInput { get; private set; }
        public bool JumpTriggered { get; private set; }
        public bool AttackTriggered { get; private set; }
        public bool AttackHeld { get; private set; }
        public bool ReviveTriggered { get; private set; }


        private void Awake()
        {
            if (inputActions == null)
            {
                Debug.LogError($"[{nameof(PlayerInputReader)}] 未关联 InputActionAsset，请在 Inspector 中拖入 InputSystem_Actions！");
                return;
            }

            // 获取默认 Player Action Map 中的 Move、Jump 和 Attack
            var playerMap = inputActions.FindActionMap("Player");
            if (playerMap != null)
            {
                _moveAction = playerMap.FindAction("Move");
                _jumpAction = playerMap.FindAction("Jump");
                _attackAction = playerMap.FindAction("Attack");
                // 如果资产里配了就用资产里的；如果没配，代码自动创建独立的 Action 并默认绑定 R 键！
                _reviveAction = playerMap.FindAction("Revive");
                if (_reviveAction == null)
                {
                    // 使用独立的 InputAction，绝不强行修改已经激活的 InputActionAsset 资源
                    _reviveAction = new InputAction("Revive", InputActionType.Button, "<Keyboard>/r");
                    _reviveAction.AddBinding("<Gamepad>/buttonNorth");
                }
            }
        }

        private void OnEnable()
        {
            _moveAction?.Enable();
            _jumpAction?.Enable();
            _attackAction?.Enable();
            _reviveAction?.Enable();
        }

        private void OnDisable()
        {
            _moveAction?.Disable();
            _jumpAction?.Disable();
            _attackAction?.Disable();
            _reviveAction?.Disable();
        }

        private void Update()
        {
            if (_moveAction != null)
            {
                MoveInput = _moveAction.ReadValue<Vector2>();
            }

            if (_jumpAction != null)
            {
                JumpTriggered = _jumpAction.WasPressedThisFrame();
            }

            if (_attackAction != null)
            {
                AttackTriggered = _attackAction.WasPressedThisFrame();
                AttackHeld = _attackAction.IsPressed();
            }

            if (_reviveAction != null)
            {
                // 这里可以添加对复活按键的处理逻辑
                ReviveTriggered = _reviveAction.WasPressedThisFrame();

            }
        }
    }
}
