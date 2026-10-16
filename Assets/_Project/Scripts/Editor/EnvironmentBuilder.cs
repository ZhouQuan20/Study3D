using System.IO;
using Study3D.Combat;
using Study3D.Player;
using UnityEditor;
using UnityEngine;

namespace Study3D.Editor
{
    /// <summary>
    /// 商业级战术训练场环境构建器 (Tactical Training Ground Environment Builder)。
    /// 
    /// 核心架构设计与工程哲学：
    /// 1. 【模块化场景树组织 (Structured Hierarchy)】：
    ///    严禁零散对象平铺在 Hierarchy 根节点。构建规范的 --- ENVIRONMENT --- 分组，
    ///    严格划分为 Ground、Boundaries、ObservationPlatform、TacticalCovers、Props、Targets 模块。
    /// 2. 【静态合批与光照烘焙就绪 (Zero-DrawCall Static Batching)】：
    ///    所有静态环境建筑一律自动标记 StaticEditorFlags（BatchingStatic | OccludeeStatic | OccluderStatic），
    ///    使引擎能够进行静态合批与遮挡剔除，极大降低移动端/PC 端 DrawCall。
    /// 3. 【无穿模厚度底板设计 (Anti-Tunneling Solid Ground)】：
    ///    摒弃 0 厚度的简陋单面 Plane，采用 1 米厚度的实体 BoxCollider，杜绝角色控制器或高速刚体下坠穿模。
    /// 4. 【高低差战术动线 (Tactical Level Design)】：
    ///    搭建抬高 2.5 米的战术高台与平缓斜坡（倾角约 12°，远低于 CharacterController 45° 爬坡限制），
    ///    结合半身掩体、全高掩体箱与集装箱，构建经典的三路交火掩护动线。
    /// 5. 【防御性兜底保障 (Defensive Safeguard)】：
    ///    自动为 Player 挂载 FallSafeguard 坠落防御器，一旦发生异常穿模立即重置回安全出生点。
    /// </summary>
    public static class EnvironmentBuilder
    {
        private const string MaterialsPath = "Assets/_Project/Materials/Environment";
        private const string EnvironmentRootName = "--- ENVIRONMENT ---";

        [MenuItem("Study3D/构建战术训练场环境 (Build Training Ground)")]
        public static void BuildTrainingGround()
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Tactical Training Ground");

            // 1. 确保材质库完备
            EnsureMaterialsDirectory();
            var mats = LoadOrCreateMaterials();

            // 2. 清理旧的环境根节点或遗留的单面 Plane
            CleanUpOldEnvironment();

            // 3. 构建规范环境根节点
            var root = new GameObject(EnvironmentRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Environment Root");

            // 4. 构建各个子系统
            var groundGroup = CreateGroup("01_Ground", root.transform);
            var boundaryGroup = CreateGroup("02_Boundaries", root.transform);
            var platformGroup = CreateGroup("03_ObservationPlatform", root.transform);
            var coversGroup = CreateGroup("04_TacticalCovers", root.transform);
            var propsGroup = CreateGroup("05_PropsAndDressing", root.transform);
            var targetsGroup = CreateGroup("06_ShootingTargets", root.transform);

            // ==================== 01. 地面系统 ====================
            // 主地面：60m x 60m x 1m 实体厚底板，顶面刚好在 Y = 1.0f
            var mainGround = CreateBox("MainGround_60x60", groundGroup.transform,
                new Vector3(0f, 0.5f, 0f), new Vector3(60f, 1f, 60f), mats.GroundTarmac);

            // 射击工位区地坪分界线（微抬高 0.01m 消除 Z-Fighting）
            CreateBox("ShootingZone_Pavement", groundGroup.transform,
                new Vector3(0f, 1.01f, -13f), new Vector3(32f, 0.02f, 14f), mats.GroundShootingZone);

            // 战术射击白/黄标线
            CreateBox("FiringLine_Marker", groundGroup.transform,
                new Vector3(0f, 1.02f, -6f), new Vector3(30f, 0.02f, 0.35f), mats.WarningStripe);

            // ==================== 02. 场景边界实体围墙 ====================
            // 四周围墙：高 3.5m，厚 1.2m，全包围带碰撞，顶部配置警戒反光条
            // 北墙 (Z = +30)
            CreateWallWithCoping("PerimeterWall_North", boundaryGroup.transform,
                new Vector3(0f, 2.75f, 30f), new Vector3(61.2f, 3.5f, 1.2f), mats.PerimeterWall, mats.WarningStripe);
            // 南墙 (Z = -30)
            CreateWallWithCoping("PerimeterWall_South", boundaryGroup.transform,
                new Vector3(0f, 2.75f, -30f), new Vector3(61.2f, 3.5f, 1.2f), mats.PerimeterWall, mats.WarningStripe);
            // 东墙 (X = +30)
            CreateWallWithCoping("PerimeterWall_East", boundaryGroup.transform,
                new Vector3(30f, 2.75f, 0f), new Vector3(1.2f, 3.5f, 61.2f), mats.PerimeterWall, mats.WarningStripe);
            // 西墙 (X = -30)
            CreateWallWithCoping("PerimeterWall_West", boundaryGroup.transform,
                new Vector3(-30f, 2.75f, 0f), new Vector3(1.2f, 3.5f, 61.2f), mats.PerimeterWall, mats.WarningStripe);

            // ==================== 03. 战术观察高台与爬坡道 ====================
            // 高台平台板（长 14m，宽 10m，高 2.5m，台面位于 Y = 2.7m）
            Vector3 platformCenter = new Vector3(18f, 2.5f, 10f);
            var platformDeck = CreateBox("ObservationDeck", platformGroup.transform,
                platformCenter, new Vector3(10f, 0.4f, 14f), mats.PlatformSteel);

            // 高台 4 根承重立柱
            CreateBox("Pillar_NW", platformGroup.transform, new Vector3(13.5f, 1.75f, 16.5f), new Vector3(0.6f, 2.5f, 0.6f), mats.PerimeterWall);
            CreateBox("Pillar_NE", platformGroup.transform, new Vector3(22.5f, 1.75f, 16.5f), new Vector3(0.6f, 2.5f, 0.6f), mats.PerimeterWall);
            CreateBox("Pillar_SW", platformGroup.transform, new Vector3(13.5f, 1.75f, 3.5f), new Vector3(0.6f, 2.5f, 0.6f), mats.PerimeterWall);
            CreateBox("Pillar_SE", platformGroup.transform, new Vector3(22.5f, 1.75f, 3.5f), new Vector3(0.6f, 2.5f, 0.6f), mats.PerimeterWall);

            // 高台护栏（北面、东面、西面后半段）
            CreateBox("Railing_North", platformGroup.transform, new Vector3(18f, 3.3f, 16.8f), new Vector3(10f, 1.2f, 0.3f), mats.BarrierConcrete);
            CreateBox("Railing_East", platformGroup.transform, new Vector3(22.8f, 3.3f, 10f), new Vector3(0.3f, 1.2f, 14f), mats.BarrierConcrete);
            CreateBox("Railing_West_Back", platformGroup.transform, new Vector3(13.2f, 3.3f, 12f), new Vector3(0.3f, 1.2f, 10f), mats.BarrierConcrete);

            // 平缓爬坡道 (Ramp): 坡长 8.5m，抬升 1.7m，坡度约 12°，完全兼容 CharacterController 45° 爬坡
            var ramp = CreateBox("TacticalRamp", platformGroup.transform,
                new Vector3(18f, 1.85f, -0.7f), new Vector3(4.5f, 0.35f, 8.6f), mats.PlatformSteel);
            ramp.transform.rotation = Quaternion.Euler(11.5f, 0f, 0f);

            // ==================== 04. 战术掩体与交火动线 ====================
            // 4.1 中央战术掩体碉堡群 (Center Bunker)
            // 两道半身掩护混凝土矮墙（高 1.25m，玩家可露出上半身射击）
            CreateBox("LowWall_Center_Left", coversGroup.transform, new Vector3(-3.5f, 1.62f, 3f), new Vector3(4.5f, 1.25f, 0.6f), mats.BarrierConcrete);
            CreateBox("LowWall_Center_Right", coversGroup.transform, new Vector3(3.5f, 1.62f, 3f), new Vector3(4.5f, 1.25f, 0.6f), mats.BarrierConcrete);
            // 中央掩护沙袋支撑柱
            CreateBox("Center_Core_Block", coversGroup.transform, new Vector3(0f, 1.8f, 4.5f), new Vector3(2f, 1.6f, 2f), mats.CrateMilitary);

            // 4.2 左路集装箱与箱堆走廊 (Left Flank Logistics Corridor)
            // 大型军用集装箱 (高 2.6m，长 6.5m，宽 2.8m，全高掩体)
            CreateBox("ShippingContainer_Left_1", coversGroup.transform, new Vector3(-13f, 2.3f, 7f), new Vector3(2.8f, 2.6f, 6.5f), mats.CrateMilitary);
            CreateBox("ShippingContainer_Left_2", coversGroup.transform, new Vector3(-17f, 2.3f, 15f), new Vector3(6.5f, 2.6f, 2.8f), mats.PerimeterWall);

            // 阶梯式木箱堆（可作半身掩体与垫脚石）
            CreateBox("Crate_Stack_Base1", coversGroup.transform, new Vector3(-16f, 1.75f, 3f), new Vector3(1.6f, 1.5f, 1.6f), mats.CrateWood);
            CreateBox("Crate_Stack_Base2", coversGroup.transform, new Vector3(-14.3f, 1.75f, 3f), new Vector3(1.6f, 1.5f, 1.6f), mats.CrateWood);
            var topCrate = CreateBox("Crate_Stack_Top", coversGroup.transform, new Vector3(-15.1f, 3.05f, 3f), new Vector3(1.3f, 1.1f, 1.3f), mats.CrateWood);
            topCrate.transform.rotation = Quaternion.Euler(0f, 18f, 0f);

            // 4.3 右路斜向掩体排布 (Right Flank Tactical Berm)
            var rightBarrier1 = CreateBox("Barrier_Right_Angled", coversGroup.transform, new Vector3(9f, 1.62f, -1f), new Vector3(4f, 1.25f, 0.7f), mats.BarrierConcrete);
            rightBarrier1.transform.rotation = Quaternion.Euler(0f, 25f, 0f);

            var rightBarrier2 = CreateBox("Barrier_Right_Forward", coversGroup.transform, new Vector3(11f, 1.62f, 6f), new Vector3(4.5f, 1.25f, 0.7f), mats.BarrierConcrete);
            rightBarrier2.transform.rotation = Quaternion.Euler(0f, -15f, 0f);

            // ==================== 05. 场景基础物品与细节道具 ====================
            // 5.1 射击出发台与工位隔断 (Shooting Stalls)
            // 射击准备长条工作台
            CreateBox("ShootingBench_Left", propsGroup.transform, new Vector3(-4.5f, 1.45f, -8f), new Vector3(2.8f, 0.9f, 0.9f), mats.PlatformSteel);
            CreateBox("ShootingBench_Right", propsGroup.transform, new Vector3(4.5f, 1.45f, -8f), new Vector3(2.8f, 0.9f, 0.9f), mats.PlatformSteel);

            // 工作台上的弹药小箱
            CreateBox("AmmoBox_1", propsGroup.transform, new Vector3(-4.2f, 2.05f, -8f), new Vector3(0.7f, 0.3f, 0.45f), mats.CrateMilitary);
            CreateBox("AmmoBox_2", propsGroup.transform, new Vector3(4.8f, 2.05f, -8f), new Vector3(0.65f, 0.3f, 0.45f), mats.CrateWood);

            // 工位防流弹隔断板
            CreateBox("Partition_Left", propsGroup.transform, new Vector3(-7f, 2.1f, -10f), new Vector3(0.2f, 2.2f, 4f), mats.PerimeterWall);
            CreateBox("Partition_Mid", propsGroup.transform, new Vector3(0f, 2.1f, -10f), new Vector3(0.2f, 2.2f, 4f), mats.PerimeterWall);
            CreateBox("Partition_Right", propsGroup.transform, new Vector3(7f, 2.1f, -10f), new Vector3(0.2f, 2.2f, 4f), mats.PerimeterWall);

            // 5.2 工业防爆油桶群 (Industrial Fuel & Hazard Barrels)
            // 左侧集装箱角落油桶群（2 亮黄警戒桶 + 1 危险红桶）
            CreateCylinder("Barrel_Yellow_1", propsGroup.transform, new Vector3(-10.5f, 1.6f, 4f), new Vector3(0.85f, 1.2f, 0.85f), mats.BarrelYellow);
            CreateCylinder("Barrel_Yellow_2", propsGroup.transform, new Vector3(-9.6f, 1.6f, 4.2f), new Vector3(0.85f, 1.2f, 0.85f), mats.BarrelYellow);
            CreateCylinder("Barrel_Red_1", propsGroup.transform, new Vector3(-10f, 1.6f, 5.1f), new Vector3(0.85f, 1.2f, 0.85f), mats.BarrelRed);

            // 右侧斜墙背后的储备油桶群
            CreateCylinder("Barrel_Red_2", propsGroup.transform, new Vector3(8.5f, 1.6f, 1f), new Vector3(0.85f, 1.2f, 0.85f), mats.BarrelRed);
            CreateCylinder("Barrel_Yellow_3", propsGroup.transform, new Vector3(9.3f, 1.6f, 0.5f), new Vector3(0.85f, 1.2f, 0.85f), mats.BarrelYellow);

            // 高台下方物资角
            CreateCylinder("Barrel_Yellow_4", propsGroup.transform, new Vector3(14.5f, 1.6f, 2.5f), new Vector3(0.85f, 1.2f, 0.85f), mats.BarrelYellow);
            CreateBox("Pallet_Crates", propsGroup.transform, new Vector3(15f, 1.6f, 0.5f), new Vector3(1.8f, 1.2f, 1.8f), mats.CrateWood);

            // ==================== 06. 靶标系统与实体保障 ====================
            // 确保现有的 TargetDummy 就位，并在各射击距离新增战术靶标
            SetupShootingTargets(targetsGroup.transform, mats);

            // 为 Player 配置安全保底与初始朝向
            SetupPlayerSafeguard();

            // 标记场景已修改
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[EnvironmentBuilder] 战术训练场环境构建完毕！全部地形、掩体、高台、道具及边界已就绪。");
        }

        private static void SetupShootingTargets(Transform parent, EnvironmentMaterials mats)
        {
            // 寻找现有的 TargetDummy，将其归入管理
            var existingDummy = GameObject.Find("TargetDummy");
            if (existingDummy != null)
            {
                existingDummy.transform.SetParent(parent, true);
                existingDummy.transform.position = new Vector3(-0.3f, 2.0f, 7.5f); // 中近程靶标 (15m)
            }

            // 新增远距离靶标 (Far Target ~25m)
            CreatePracticeDummy("TargetDummy_Far", parent, new Vector3(0f, 2.0f, 20f), mats.BarrelRed);

            // 新增左路集装箱后掩护靶标 (Left Flank Target)
            CreatePracticeDummy("TargetDummy_LeftFlank", parent, new Vector3(-13f, 2.0f, 14f), mats.BarrelRed);

            // 新增高台狙击哨位靶标 (Elevated Platform Target ~2.7m 高)
            CreatePracticeDummy("TargetDummy_HighPlatform", parent, new Vector3(18f, 3.7f, 12f), mats.BarrelRed);
        }

        private static void CreatePracticeDummy(string name, Transform parent, Vector3 pos, Material mat)
        {
            var dummy = GameObject.Find(name);
            if (dummy == null)
            {
                dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                dummy.name = name;
                Undo.RegisterCreatedObjectUndo(dummy, "Create Dummy");
            }

            dummy.transform.SetParent(parent, false);
            dummy.transform.position = pos;
            dummy.transform.localScale = new Vector3(1f, 1f, 1f);

            var mr = dummy.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;

            // 赋予战斗契约组件
            var health = dummy.GetComponent<Health>();
            if (health == null) health = dummy.AddComponent<Health>();

            var flash = dummy.GetComponent<DamageFlash>();
            if (flash == null) flash = dummy.AddComponent<DamageFlash>();

            var dummyLogic = dummy.GetComponent<TargetDummy>();
            if (dummyLogic == null) dummyLogic = dummy.AddComponent<TargetDummy>();
        }

        private static void SetupPlayerSafeguard()
        {
            var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
            if (player == null) return;

            // 确保玩家挂载坠落保底机制
            var safeguard = player.GetComponent<FallSafeguard>();
            if (safeguard == null)
            {
                safeguard = Undo.AddComponent<FallSafeguard>(player);
            }

            // 出生点校准到训练场射击站位中心
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = new Vector3(0f, 2.0f, -12f);
            player.transform.rotation = Quaternion.Euler(0f, 0f, 0f); // 面朝正北靶区
            if (cc != null) cc.enabled = true;
        }

        private static void CleanUpOldEnvironment()
        {
            // 清理旧的环境根节点
            var oldRoot = GameObject.Find(EnvironmentRootName);
            if (oldRoot != null)
            {
                Undo.DestroyObjectImmediate(oldRoot);
            }

            // 清理旧的零散 Plane
            var oldPlane = GameObject.Find("Plane");
            if (oldPlane != null)
            {
                Undo.DestroyObjectImmediate(oldPlane);
            }
        }

        private static GameObject CreateGroup(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 localPos, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && material != null)
            {
                mr.sharedMaterial = material;
            }

            // 静态合批标记
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic);

            return go;
        }

        private static GameObject CreateCylinder(string name, Transform parent, Vector3 localPos, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && material != null)
            {
                mr.sharedMaterial = material;
            }

            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic);

            return go;
        }

        private static void CreateWallWithCoping(string name, Transform parent, Vector3 center, Vector3 size, Material wallMat, Material copingMat)
        {
            var wall = CreateBox(name, parent, center, size, wallMat);

            // 顶部警戒压顶 (Coping with Warning Stripe)
            Vector3 copingPos = center + new Vector3(0f, (size.y * 0.5f) + 0.15f, 0f);
            Vector3 copingSize = new Vector3(
                size.x > size.z ? size.x + 0.3f : size.x + 0.4f,
                0.3f,
                size.z > size.x ? size.z + 0.3f : size.z + 0.4f);

            CreateBox($"{name}_Coping", wall.transform, copingPos, copingSize, copingMat);
        }

        private static void EnsureMaterialsDirectory()
        {
            if (!AssetDatabase.IsValidFolder(MaterialsPath))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Materials"))
                {
                    AssetDatabase.CreateFolder("Assets/_Project", "Materials");
                }
                AssetDatabase.CreateFolder("Assets/_Project/Materials", "Environment");
            }
        }

        private static EnvironmentMaterials LoadOrCreateMaterials()
        {
            var mats = new EnvironmentMaterials();
            var urpLitShader = Shader.Find("Universal Render Pipeline/Lit");

            mats.GroundTarmac = GetOrCreateMaterial("M_Ground_Tarmac", urpLitShader,
                new Color(0.18f, 0.20f, 0.22f), smoothness: 0.15f, metallic: 0.05f);

            mats.GroundShootingZone = GetOrCreateMaterial("M_Ground_ShootingZone", urpLitShader,
                new Color(0.24f, 0.26f, 0.28f), smoothness: 0.2f, metallic: 0.1f);

            mats.BarrierConcrete = GetOrCreateMaterial("M_Barrier_Concrete", urpLitShader,
                new Color(0.55f, 0.55f, 0.54f), smoothness: 0.08f, metallic: 0.0f);

            mats.CrateMilitary = GetOrCreateMaterial("M_Crate_Military", urpLitShader,
                new Color(0.22f, 0.32f, 0.20f), smoothness: 0.25f, metallic: 0.2f);

            mats.CrateWood = GetOrCreateMaterial("M_Crate_Wood", urpLitShader,
                new Color(0.56f, 0.38f, 0.24f), smoothness: 0.18f, metallic: 0.0f);

            mats.BarrelYellow = GetOrCreateMaterial("M_Barrel_Yellow", urpLitShader,
                new Color(0.92f, 0.72f, 0.12f), smoothness: 0.6f, metallic: 0.45f);

            mats.BarrelRed = GetOrCreateMaterial("M_Barrel_Red", urpLitShader,
                new Color(0.85f, 0.18f, 0.15f), smoothness: 0.65f, metallic: 0.55f);

            mats.PlatformSteel = GetOrCreateMaterial("M_Platform_Steel", urpLitShader,
                new Color(0.28f, 0.30f, 0.33f), smoothness: 0.5f, metallic: 0.75f);

            mats.PerimeterWall = GetOrCreateMaterial("M_Perimeter_Wall", urpLitShader,
                new Color(0.35f, 0.36f, 0.38f), smoothness: 0.1f, metallic: 0.05f);

            mats.WarningStripe = GetOrCreateMaterial("M_Warning_Stripe", urpLitShader,
                new Color(0.95f, 0.78f, 0.1f), smoothness: 0.35f, metallic: 0.1f);

            AssetDatabase.SaveAssets();
            return mats;
        }

        private static Material GetOrCreateMaterial(string name, Shader shader, Color color, float smoothness = 0.5f, float metallic = 0.0f)
        {
            string assetPath = $"{MaterialsPath}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", color);
                }
                else if (mat.HasProperty("_Color"))
                {
                    mat.SetColor("_Color", color);
                }

                if (mat.HasProperty("_Smoothness"))
                {
                    mat.SetFloat("_Smoothness", smoothness);
                }

                if (mat.HasProperty("_Metallic"))
                {
                    mat.SetFloat("_Metallic", metallic);
                }

                AssetDatabase.CreateAsset(mat, assetPath);
            }
            return mat;
        }

        private class EnvironmentMaterials
        {
            public Material GroundTarmac;
            public Material GroundShootingZone;
            public Material BarrierConcrete;
            public Material CrateMilitary;
            public Material CrateWood;
            public Material BarrelYellow;
            public Material BarrelRed;
            public Material PlatformSteel;
            public Material PerimeterWall;
            public Material WarningStripe;
        }
    }
}
