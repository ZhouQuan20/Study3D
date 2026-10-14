using UnityEngine;

namespace Study3D.Player
{
    /// <summary>
    /// 即开即用的战术瞄准准星组件（Crosshair UI）。
    /// 优势：
    /// 1. 零资源依赖（0 图片、0 Canvas 配置成本），代码自动在屏幕上绘制准星。
    /// 2. 实时跟随鼠标物理位置，完美契合俯视/第三人称瞄准射击。
    /// 3. 可自定义准星颜色、大小、间距与中心准心红点。
    /// 4. 自动隐藏系统原生指针，营造浓厚射击沉浸感。
    /// </summary>
    public class CrosshairUI : MonoBehaviour
    {
        [Header("准星外观配置")]
        [Tooltip("准星颜色（经典战术绿或亮红）")]
        [SerializeField] private Color crosshairColor = new Color(0.2f, 1f, 0.3f, 0.9f);

        [Tooltip("中心小红点颜色")]
        [SerializeField] private Color centerDotColor = Color.red;

        [Tooltip("准星十字线臂长（像素）")]
        [SerializeField] private float armLength = 10f;

        [Tooltip("十字线粗细（像素）")]
        [SerializeField] private float thickness = 2f;

        [Tooltip("中心留空半径（像素）")]
        [SerializeField] private float centerGap = 6f;

        [Tooltip("中心瞄准点大小（0 表示不显示）")]
        [SerializeField] private float dotSize = 3f;

        [Header("光标交互")]
        [Tooltip("运行时是否隐藏 Windows 原生鼠标指针")]
        [SerializeField] private bool hideSystemCursor = true;

        private Texture2D _pixelTexture;

        private void Awake()
        {
            // 创建纯色 1x1 纹理用于绘制像素线条
            _pixelTexture = new Texture2D(1, 1);
            _pixelTexture.SetPixel(0, 0, Color.white);
            _pixelTexture.Apply();
        }

        private void OnEnable()
        {
            if (hideSystemCursor)
            {
                Cursor.visible = false;
            }
        }

        private void OnDisable()
        {
            Cursor.visible = true;
        }

        private void OnDestroy()
        {
            if (_pixelTexture != null)
            {
                Destroy(_pixelTexture);
            }
        }

        private void OnGUI()
        {
            // 获取鼠标屏幕位置（GUI 坐标系 Y 轴向下，需要做翻转）
            Vector2 mousePos;
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            }
            else
            {
                mousePos = Input.mousePosition;
            }

            float cx = mousePos.x;
            float cy = Screen.height - mousePos.y; // 翻转 Y

            GUI.color = crosshairColor;

            // 左横线
            GUI.DrawTexture(new Rect(cx - centerGap - armLength, cy - thickness * 0.5f, armLength, thickness), _pixelTexture);
            // 右横线
            GUI.DrawTexture(new Rect(cx + centerGap, cy - thickness * 0.5f, armLength, thickness), _pixelTexture);
            // 上竖线
            GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy - centerGap - armLength, thickness, armLength), _pixelTexture);
            // 下竖线
            GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy + centerGap, thickness, armLength), _pixelTexture);

            // 中心瞄准点
            if (dotSize > 0f)
            {
                GUI.color = centerDotColor;
                GUI.DrawTexture(new Rect(cx - dotSize * 0.5f, cy - dotSize * 0.5f, dotSize, dotSize), _pixelTexture);
            }
        }
    }
}
