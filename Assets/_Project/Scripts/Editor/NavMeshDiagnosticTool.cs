using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using UnityCliConnector;
using Newtonsoft.Json.Linq;

namespace Study3D.Editor
{
    [UnityCliTool(Name = "diagnose_navmesh", Description = "Diagnose NavMesh status and bake if needed", Group = "custom")]
    public static class NavMeshDiagnosticTool
    {
        [MenuItem("Tools/NavMesh/Diagnose")]
        public static void DiagnoseMenu()
        {
            var result = Diagnose();
            Debug.Log($"[NavMeshDiagnostic]\n{result}");
        }

        [MenuItem("Tools/NavMesh/One-Click Fix & Bake NavMesh")]
        public static void OneClickFixAndBakeMenu()
        {
            FixAndBakeNavMesh();
        }

        public static object HandleCommand(JObject parameters)
        {
            string action = parameters.Value<string>("action") ?? "diagnose";
            if (action == "bake")
            {
                FixAndBakeNavMesh();
                return new SuccessResponse("NavMesh baked successfully", Diagnose());
            }
            return new SuccessResponse("NavMesh diagnosis complete", Diagnose());
        }

        public static string Diagnose()
        {
            var sb = new System.Text.StringBuilder();

            var surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
            sb.AppendLine($"[NavMeshSurface 数量]: {surfaces.Length}");

            var agents = Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
            sb.AppendLine($"[NavMeshAgent 数量]: {agents.Length}");

            foreach (var agent in agents)
            {
                sb.AppendLine($"  - Agent: {agent.name}, isOnNavMesh: {agent.isOnNavMesh}, position: {agent.transform.position}");
                if (NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    sb.AppendLine($"    -> 附近有合法 NavMesh 坐标: {hit.position}, 距 Agent: {Vector3.Distance(agent.transform.position, hit.position):F2}米");
                }
                else
                {
                    sb.AppendLine($"    -> ❌ 附近 5 米内无任何合法 NavMesh！");
                }
            }

            return sb.ToString();
        }

        public static void FixAndBakeNavMesh()
        {
            // 1. 查找或创建挂载 NavMeshSurface 的物体
            var surface = Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface == null)
            {
                var groundGo = GameObject.Find("Plane") ?? GameObject.Find("Ground");
                if (groundGo != null)
                {
                    surface = groundGo.AddComponent<NavMeshSurface>();
                }
                else
                {
                    var navManager = new GameObject("NavMesh_Manager");
                    surface = navManager.AddComponent<NavMeshSurface>();
                }
            }

            surface.collectObjects = CollectObjects.All;
            surface.BuildNavMesh();

            // 2. 将场景里的 Agent 吸附回地面合法 NavMesh
            var agents = Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
            foreach (var agent in agents)
            {
                if (NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                {
                    agent.transform.position = hit.position;
                    Undo.RecordObject(agent.transform, "Snap Agent to NavMesh");
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();

            Debug.Log("<color=#00FF00><b>[NavMesh] 烘焙修复成功！NavMeshSurface 已生效，所有敌人已对齐合法导航地面！</b></color>");
        }
    }
}
