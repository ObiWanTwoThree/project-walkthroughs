using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieRunner.Combat;
using ZombieRunner.Core;
using ZombieRunner.Enemies;
using ZombieRunner.Player;
using ZombieRunner.Pooling;
using ZombieRunner.Spawning;
using ZombieRunner.World;

namespace ZombieRunner.EditorTools
{
    /// <summary>
    /// Tools > Zombie Runner > Build Prototype Scene. Generates the materials, prefabs, Zombie layer and a
    /// wired, playable scene matching the prototype hierarchy. Safe to re-run: generated assets are
    /// overwritten in place.
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        private const string RootDir = "Assets/ZombieRunner";
        private const string GeneratedDir = RootDir + "/Generated";
        private const string ScenesDir = RootDir + "/Scenes";
        private const string ScenePath = ScenesDir + "/Prototype.unity";
        private const string ZombieLayerName = "Zombie";

        private const float SegmentLength = 60f;
        private const int SegmentCount = 3;
        private const float TrackWidth = 10f;

        private static readonly Color SkyColor = new Color(0.12f, 0.13f, 0.16f);

        [MenuItem("Tools/Zombie Runner/Build Prototype Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(RootDir);
            EnsureFolder(GeneratedDir);
            EnsureFolder(ScenesDir);

            int zombieLayer = EnsureLayer(ZombieLayerName);
            if (zombieLayer < 0)
            {
                EditorUtility.DisplayDialog("Zombie Runner", "No free user layer is left for 'Zombie'. Free one in Project Settings > Tags and Layers and run this again.", "OK");
                return;
            }

            if (GraphicsSettings.currentRenderPipeline == null)
            {
                Debug.LogWarning("[Zombie Runner] No render pipeline asset is assigned, so materials use the Built-in Standard shader. " +
                                 "Assign a URP asset in Project Settings > Graphics and re-run the builder to get URP Simple Lit materials.");
            }

            // Open the new scene before creating assets: switching scenes can unload assets loaded earlier,
            // which would leave the prefab references wired below pointing at nothing.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = SkyColor;
            RenderSettings.fogStartDistance = 35f;
            RenderSettings.fogEndDistance = 75f;

            Material squadMat = CreateMaterial("Squad", new Color(0.24f, 0.55f, 0.95f));
            Material zombieMat = CreateMaterial("Zombie", new Color(0.45f, 0.58f, 0.34f));
            Material flashMat = CreateMaterial("ZombieFlash", Color.white, Color.white);
            Material bulletMat = CreateMaterial("Bullet", new Color(1f, 0.86f, 0.35f), new Color(1f, 0.7f, 0.2f) * 2f);
            Material railMat = CreateMaterial("Rail", new Color(0.55f, 0.2f, 0.18f));
            Material groundMat = CreateGroundMaterial();

            Bullet bulletPrefab = CreateBulletPrefab(bulletMat);
            ZombieAgent zombiePrefab = CreateZombiePrefab(zombieMat, flashMat, zombieLayer);

            // [Managers]
            var managers = new GameObject("[Managers]");
            var poolerGo = CreateChild("ObjectPooler", managers.transform);
            var pooler = poolerGo.AddComponent<ObjectPooler>();
            SetReference(pooler, "bulletPrefab", bulletPrefab);
            SetReference(pooler, "zombiePrefab", zombiePrefab);
            CreateChild("PerformanceBootstrap", managers.transform).AddComponent<PerformanceBootstrap>();

            // PlayerSquad
            GameObject squad = CreateSquad(squadMat, zombieLayer);

            // [Spawners]
            var spawners = new GameObject("[Spawners]");
            var spawner = CreateChild("HordeSpawner", spawners.transform).AddComponent<HordeSpawner>();
            SetReference(spawner, "playerTransform", squad.transform);

            // Environment
            CreateTrack(squad.transform, groundMat, railMat);

            // Camera and light from the default scene setup
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out Camera cam)) ConfigureCamera(cam, squad.transform);
                if (root.TryGetComponent(out Light light) && light.type == LightType.Directional)
                {
                    light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                    light.shadows = LightShadows.Hard;
                }
            }

            if (!VerifyReferences(pooler, spawner, squad)) return;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = squad;
            Debug.Log($"[Zombie Runner] Prototype scene built at {ScenePath}. Press Play and drag to steer.");
        }

        // ---------- scene objects ----------

        private static GameObject CreateSquad(Material squadMat, int zombieLayer)
        {
            var squad = new GameObject("PlayerSquad") { tag = "Player" };

            var body = squad.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var trigger = squad.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.size = new Vector3(1.1f, 2f, 1.1f);

            GameObject visual = CreatePrimitive(PrimitiveType.Capsule, "Visual", squad.transform, squadMat, castShadows: true);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);

            Transform firePoint = CreateChild("FirePoint", squad.transform).transform;
            firePoint.localPosition = new Vector3(0f, 1f, 0.8f);

            squad.AddComponent<SquadMovement>();
            var shooter = squad.AddComponent<SquadShooter>();
            SetReference(shooter, "firePoint", firePoint);
            var so = new SerializedObject(shooter);
            so.FindProperty("zombieLayer").intValue = 1 << zombieLayer;
            so.ApplyModifiedPropertiesWithoutUndo();

            return squad;
        }

        private static void CreateTrack(Transform squad, Material groundMat, Material railMat)
        {
            var environment = new GameObject("Environment");
            var segments = new Transform[SegmentCount];

            for (int i = 0; i < SegmentCount; i++)
            {
                GameObject segment = CreatePrimitive(PrimitiveType.Plane, $"Ground_{i}", environment.transform, groundMat);
                // Unity's plane is 10 x 10 units; scale Z to the segment length.
                segment.transform.localScale = new Vector3(TrackWidth / 10f, 1f, SegmentLength / 10f);
                segment.transform.position = new Vector3(0f, 0f, -20f + SegmentLength * (i + 0.5f));

                // Lane barriers ride along with their segment. Local units are divided by the segment's scale.
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject rail = CreatePrimitive(PrimitiveType.Cube, side < 0 ? "Rail_L" : "Rail_R", segment.transform, railMat);
                    rail.transform.localPosition = new Vector3(side * (TrackWidth * 0.5f + 0.15f) / (TrackWidth / 10f), 0.2f, 0f);
                    rail.transform.localScale = new Vector3(0.3f / (TrackWidth / 10f), 0.4f, 10f);
                }
                segments[i] = segment.transform;
            }

            var scroller = environment.AddComponent<TrackScroller>();
            var so = new SerializedObject(scroller);
            so.FindProperty("target").objectReferenceValue = squad;
            so.FindProperty("segmentLength").floatValue = SegmentLength;
            SerializedProperty list = so.FindProperty("segments");
            list.arraySize = SegmentCount;
            for (int i = 0; i < SegmentCount; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = segments[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureCamera(Camera cam, Transform squad)
        {
            cam.transform.SetPositionAndRotation(new Vector3(0f, 11f, -8f), Quaternion.Euler(42f, 0f, 0f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = SkyColor;
            cam.farClipPlane = 90f;

            var follow = cam.gameObject.AddComponent<CameraFollow>();
            SetReference(follow, "target", squad);
        }

        // ---------- prefabs ----------

        private static Bullet CreateBulletPrefab(Material mat)
        {
            // Stretched sphere so the tracer reads along its flight path (the bullet faces its direction).
            GameObject go = CreatePrimitive(PrimitiveType.Sphere, "Bullet", null, mat);
            go.transform.localScale = new Vector3(0.14f, 0.14f, 0.5f);
            go.AddComponent<Bullet>();
            return SavePrefab<Bullet>(go, "Bullet");
        }

        private static ZombieAgent CreateZombiePrefab(Material bodyMat, Material flashMat, int layer)
        {
            var root = new GameObject("Zombie") { layer = layer };

            GameObject body = CreatePrimitive(PrimitiveType.Capsule, "Body", root.transform, bodyMat);
            body.layer = layer;
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.9f, 0f);
            col.height = 1.8f;
            col.radius = 0.4f;

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var agent = root.AddComponent<ZombieAgent>();
            var so = new SerializedObject(agent);
            so.FindProperty("bodyRenderer").objectReferenceValue = body.GetComponent<MeshRenderer>();
            so.FindProperty("flashMaterial").objectReferenceValue = flashMat;
            so.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab<ZombieAgent>(root, "Zombie");
        }

        private static T SavePrefab<T>(GameObject go, string name) where T : Component
        {
            string path = $"{GeneratedDir}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            // Reload from disk so the reference is to the saved asset, not an in-memory object.
            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<T>();
        }

        // ---------- materials ----------

        private static Material CreateMaterial(string name, Color color, Color? emission = null)
        {
            string path = $"{GeneratedDir}/M_{name}.mat";
            Shader shader = FindLitShader();

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateGroundMaterial()
        {
            string texPath = $"{GeneratedDir}/T_GroundChecker.png";
            if (!File.Exists(texPath))
            {
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
                var light = new Color(0.33f, 0.34f, 0.37f);
                var dark = new Color(0.27f, 0.28f, 0.31f);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, ((x / 32) + (y / 32)) % 2 == 0 ? light : dark);

                File.WriteAllBytes(texPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texPath);

                var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            var checker = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            Material mat = CreateMaterial("Ground", Color.white);

            // One texture repeat holds 2 x 2 squares; tile so each square is 2 m on a side.
            var tiling = new Vector2(TrackWidth / 4f, SegmentLength / 4f);
            foreach (string prop in new[] { "_BaseMap", "_MainTex" })
            {
                if (!mat.HasProperty(prop)) continue;
                mat.SetTexture(prop, checker);
                mat.SetTextureScale(prop, tiling);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Shader FindLitShader()
        {
            Shader shader = null;
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                // Simple Lit (Blinn-Phong) is noticeably cheaper than Lit on mobile GPUs.
                shader = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            return shader != null ? shader : Shader.Find("Standard");
        }

        // ---------- helpers ----------

        private static bool VerifyReferences(Object pooler, Object spawner, GameObject squad)
        {
            var missing = new System.Collections.Generic.List<string>();
            CheckReference(pooler, "bulletPrefab", "ObjectPooler > Bullet Prefab", missing);
            CheckReference(pooler, "zombiePrefab", "ObjectPooler > Zombie Prefab", missing);
            CheckReference(spawner, "playerTransform", "HordeSpawner > Player Transform", missing);
            CheckReference(squad.GetComponent<SquadShooter>(), "firePoint", "SquadShooter > Fire Point", missing);
            if (missing.Count == 0) return true;

            Debug.LogError("[Zombie Runner] The scene was built but these references are empty: " + string.Join(", ", missing) +
                           ". Assign them in the Inspector, or run the builder again.");
            return false;
        }

        private static void CheckReference(Object target, string property, string label, System.Collections.Generic.List<string> missing)
        {
            if (new SerializedObject(target).FindProperty(property).objectReferenceValue == null) missing.Add(label);
        }

        private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Material mat, bool castShadows = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // physics shapes are added explicitly where needed
            if (parent != null) go.transform.SetParent(parent, false);

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            // Only the squad casts a shadow; the ground receives it. Keeps shadow-pass draw calls flat as the horde grows.
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = type == PrimitiveType.Plane;
            return go;
        }

        private static GameObject CreateChild(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void SetReference(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++) // 0-7 are reserved by Unity
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;

                slot.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
            return -1;
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (EditorBuildSettingsScene s in scenes)
            {
                if (s.path == path) return;
            }

            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            updated[0] = new EditorBuildSettingsScene(path, true);
            scenes.CopyTo(updated, 1);
            EditorBuildSettings.scenes = updated;
        }
    }
}
