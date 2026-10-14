# 🎮 Study3D - 商业级 3D 俯视角动作射击演示项目

[![Unity](https://img.shields.io/badge/Unity-6000.0%2B-black?logo=unity)](https://unity.com/)
[![Render Pipeline](https://img.shields.io/badge/Render%20Pipeline-URP-blue)](https://unity.com/srp/universal-render-pipeline)
[![Input System](https://img.shields.io/badge/Input%20System-New%20Input%20System-orange)](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.20/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

一个基于 **Unity 6 (URP)** 打造的商业级 3D 俯视角（Top-Down）双摇杆射击与战斗系统学习范式工程。  
项目坚决摒弃“玩具代码”与面条式强耦合逻辑，严格遵循 **单一职责原则（SRP）**、**观察者模式（Observer Pattern）**、**零 GC 内存安全（Zero GC）** 与 **工业级防御性设计（Graceful Degradation）**。

---

## 🌟 核心特性速览

### 1. 角色移动与走打一体化瞄准 (Locomotion & Aiming)
* **CharacterController 物理位移**：无物理穿模、自带台阶跨越（Step Offset）与坡度限制（Slope Limit），结合物理重力公式 $v = \sqrt{2gh}$ 打造扎实跳跃手感。
* **双轴解耦走打机制**：
  * **键盘 WASD**：结合主相机水平投影向量控制三维平面位移。
  * **鼠标瞄准**：基于现代屏幕射线与 $XZ$ 纯数学平面（`UnityEngine.Plane`）无延迟求交，枪口与身躯时刻锁定光标，实现丝滑的“倒车开火（走 A）”。
* **动画阻尼混合树**：采用 4 参数带阻尼的 `animator.SetFloat(SpeedHash, targetSpeed, speedDampTime, Time.deltaTime)`，消除走跑切换时的生硬抽搐。
* **纯代码战术准星（CrosshairUI）**：利用 1×1 动态纯白纹理拉伸技术，零图片依赖实现像素级锐利的自适应屏幕动态准星。

### 2. 高性能战斗与零 GC 对象池 (Combat & Object Pool)
* **官方高性能对象池（`UnityEngine.Pool.IObjectPool`）**：子弹从创建、借出、回收至销毁全程闭环，彻底杜绝运行时频繁 `Instantiate / Destroy` 引发的 GC 卡顿与帧率骤降（Spike）。
* **拖尾拉丝保护**：子弹复用时自动调用 `TrailRenderer.Clear()`，防止对象池唤醒瞬移导致的穿屏光带 Bug。
* **面向接口受击契约（`IDamageable`）**：发射系统与受击实体完全解耦，无论是小怪、靶子还是未来破坏物，均通过统一契约进行伤害交互。

### 3. 智能敌人有限状态机与受击仇恨 (Enemy AI & Navigation)
* **基于 NavMesh 的 FSM 状态机**：拥有 `Idle`（待机）、`Chase`（追逐）、`Attack`（近战攻击）、`Dead`（死亡停机）四大清晰状态流转。
* **受击激怒保护（Damage Aggro Duration）**：被玩家子弹击中后激活持续 6 秒的受击仇恨锁定，彻底修复超视距攻击引发的“瞬间秒脱战”问题。
* **近战精准面向**：近战攻击停步时，自动通过 `Quaternion.LookRotation` + `Quaternion.Slerp` 接管身体朝向，锁定玩家方位。

### 4. 极致视觉反馈与防御性设计 (Visual Feedback & Robustness)
* **`MaterialPropertyBlock` 高性能受击闪白**：修改材质时绝对不调用 `renderer.material`，不产生任何克隆材质实例，**100% 保持 URP SRP Batcher 动态合批**。
* **HDR 过载发光（Overdriven Color）**：闪烁颜色乘以强度系数激发高动态光效，配合 URP Bloom 产生强烈受击打击感。
* **死亡倒地平滑降级（Fallback Death）**：在缺少特定骨骼死亡动画剪辑（Clip）时，代码通过 `Quaternion.RotateTowards` 自动驱动角色后仰 80 度平滑倒地，保证游戏永远具备“倒地-结算-重开”完整闭环。
* **即时死亡结算 HUD（OnGUI）**：零 Canvas 依赖，纯代码自适应绘制居中半透明暗红结算横幅，支持按 `[R]` 键极速重置关卡。

---

## 🏗️ 架构设计理念 (Architecture Guidelines)

```text
Study3D 核心模块分层
 ├── Input Layer (输入层)
 │    └── PlayerInputReader.cs     -> 隔离新版输入系统 InputActionAsset，对外提供只读状态
 │
 ├── Logic / Brain (逻辑层)
 │    ├── PlayerMovement.cs        -> 位移与相机朝向解耦
 │    ├── WeaponShooter.cs         -> 射速控制与 ObjectPool 调度
 │    ├── Health.cs                -> 数值维护，广播 event Action（OnDamaged / OnDeath）
 │    └── EnemyAI.cs               -> NavMeshAgent 驱动的有限状态机
 │
 └── Presentation / View (表现层)
      ├── PlayerAnimation.cs       -> 阻尼驱动动画混合树与开火 Trigger
      ├── DamageFlash.cs           -> MaterialPropertyBlock 调色滤镜，监听 Health 事件
      ├── CrosshairUI.cs           -> 即时像素级屏幕战术十字线
      └── PlayerDeathHandler.cs    -> 监听 OnDeath 广播，执行倒地兜底与 R 键重开流程
```

1. **逻辑与表现彻底解耦**：`Health` 组件负责数值计算，自身不持有任何音效、粒子或 UI 组件引用，全部通过 `event Action` 广播供外界观察者响应。
2. **零 GC 与内存安全**：杜绝字符串寻址，所有 Shader 属性与 Animator 参数均通过 `Shader.PropertyToID` 及 `Animator.StringToHash` 预存哈希；事件订阅严格在 `OnEnable` 与 `OnDisable` 之间成对注销，杜绝闭包引起的内存泄漏。
3. **防御性编程**：动态保护所有引用（如 `out var agent`、`TryGetComponent`、懒加载属性块），即使在模型动态换装或热重载时也能优雅自愈。

---

## 📂 项目工程结构

```text
Assets/_Project/
 ├── Animations/                   # 动画状态机控制器与混合树 (PlayerAnimatorController)
 ├── Art/                          # 角色模型材质、子弹发光材质与贴图
 ├── Materials/                    # 通用渲染管线材质库 (M_Bullet_Yellow, M_Bullet_Trail 等)
 └── Scripts/                      # 核心商业级脚本模块
      ├── AI/                      # 敌人寻路与行为树/状态机 (EnemyAI.cs)
      ├── Camera/                  # 丝滑平滑插值相机跟随 (CameraFollow.cs)
      ├── Combat/                  # 战斗、血量、受击闪白与对象池 (WeaponShooter, Projectile, Health...)
      └── Player/                  # 角色移动、瞄准、输入读取与动画驱动 (PlayerMovement, PlayerInputReader...)
```

---

## 🕹️ 游戏操作指南 (Controls)

| 键位 / 操作 | 动作效果 | 说明 |
| :--- | :--- | :--- |
| **`W / A / S / D`** | 水平移动 | 依据相机水平前方自动对齐方向 |
| **`Space` 空格** | 跳跃 | 具备着地吸附保护，起跳高度稳定 |
| **`鼠标移动`** | 3D 瞄准 | 角色身躯与枪口实时对齐地面投影光标 |
| **`鼠标左键 (LMB)`** | 开火射击 | 支持单发点射与长按全自动扫射 |
| **`R 键`** | 战局重开 | 阵亡后按下立即重新加载本关 |

---

## 🚀 快速上手与运行

1. **克隆本仓库**：
   ```bash
   git clone https://github.com/ZhouQuan20/Study3D.git
   ```
2. **打开工程**：
   * 启动 **Unity Hub**，选择 **Unity 6 (6000.x)** 或更新版本的 Universal Render Pipeline (URP) 编辑器打开项目。
3. **加载场景**：
   * 在 Project 窗口中定位至：`Assets/Scenes/SampleScene.unity` 并双击打开。
4. **运行游戏**：
   * 点击编辑器顶部的 **Play** 按钮，即可体验完整的双摇杆射击、敌人 AI 追击与受击反馈机制！

---

## 📄 开源许可证

本项目基于 [MIT License](LICENSE) 开源，欢迎自由学习、拓展与商业借鉴。

