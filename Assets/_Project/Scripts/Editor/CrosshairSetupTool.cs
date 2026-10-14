using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityCliConnector;
using Newtonsoft.Json.Linq;

namespace Study3D.Editor
{
    [UnityCliTool(Name = "setup_crosshair", Description = "Create Crosshair GameObject and Material in active scene", Group = "custom")]
    public static class CrosshairSetupTool
    {
        [MenuItem("Tools/Setup Crosshair")]
        public static void CreateCrosshairMenuItem()
        {
            SetupCrosshair();
        }

        public static object HandleCommand(JObject parameters)
        {
            var result = SetupCrosshair();
            return new SuccessResponse("Crosshair created successfully", result);
        }

        public static object SetupCrosshair()
        {
            // 1. Ensure Materials folder exists
            string materialDir = "Assets/_Project/Materials";
            if (!AssetDatabase.IsValidFolder(materialDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project"))
                {
                    AssetDatabase.CreateFolder("Assets", "_Project");
                }
                AssetDatabase.CreateFolder("Assets/_Project", "Materials");
            }

            string matPath = $"{materialDir}/M_Crosshair.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("URP/Unlit");
                }
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                }
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                mat = new Material(shader);
                // Set bright fluorescent green color
                Color brightGreen = new Color(0.1f, 1f, 0.2f, 1f);
                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", brightGreen);
                }
                if (mat.HasProperty("_Color"))
                {
                    mat.SetColor("_Color", brightGreen);
                }

                AssetDatabase.CreateAsset(mat, matPath);
                AssetDatabase.SaveAssets();
            }

            // 2. Find or create Crosshair GameObject in active scene
            GameObject crosshair = GameObject.Find("Crosshair");
            if (crosshair == null)
            {
                // Create Cylinder
                crosshair = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                crosshair.name = "Crosshair";
                Undo.RegisterCreatedObjectUndo(crosshair, "Create Crosshair");
            }

            // Scale: X: 0.5, Y: 0.01, Z: 0.5
            crosshair.transform.localScale = new Vector3(0.5f, 0.01f, 0.5f);
            // Ground Plane is at y = 1, so place crosshair slightly above the floor at y = 1.01
            crosshair.transform.position = new Vector3(0f, 1.01f, -2f);

            // Remove Collider
            Collider col = crosshair.GetComponent<Collider>();
            if (col != null)
            {
                Object.DestroyImmediate(col);
            }

            // Assign Material
            MeshRenderer renderer = crosshair.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = mat;
            }

            // Select it in editor
            Selection.activeGameObject = crosshair;

            // Mark scene dirty and save
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            return new
            {
                name = crosshair.name,
                scale = new { x = 0.5f, y = 0.01f, z = 0.5f },
                material = matPath,
                hasCollider = crosshair.GetComponent<Collider>() != null
            };
        }
    }
}
