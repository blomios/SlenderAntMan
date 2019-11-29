using Digger.TerrainCutters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Digger
{
    [CustomEditor(typeof(DiggerMaster))]
    public class DiggerMasterEditor : Editor
    {
        private DiggerMaster master;
        private DiggerSystem[] diggerSystems;

        private bool clicking;

        private GameObject reticleSphere;
        private GameObject reticleHalfSphere;
        private GameObject reticleCube;
        private GameObject reticleCone;
        private GameObject reticleCylinder;

        private BrushType brush {
            get => (BrushType) EditorPrefs.GetInt("diggerMaster_brush", 0);
            set => EditorPrefs.SetInt("diggerMaster_brush", (int) value);
        }

        private ActionType action {
            get => (ActionType) EditorPrefs.GetInt("diggerMaster_action", 0);
            set => EditorPrefs.SetInt("diggerMaster_action", (int) value);
        }

        private float opacity {
            get => EditorPrefs.GetFloat("diggerMaster_opacity", 0.3f);
            set => EditorPrefs.SetFloat("diggerMaster_opacity", value);
        }

        private float size {
            get => EditorPrefs.GetFloat("diggerMaster_size", 3f);
            set => EditorPrefs.SetFloat("diggerMaster_size", value);
        }

        private float depth {
            get => EditorPrefs.GetFloat("diggerMaster_depth", 0f);
            set => EditorPrefs.SetFloat("diggerMaster_depth", value);
        }

        private float coneHeight {
            get => EditorPrefs.GetFloat("diggerMaster_coneHeight", 6f);
            set => EditorPrefs.SetFloat("diggerMaster_coneHeight", value);
        }

        private bool upsideDown {
            get => EditorPrefs.GetBool("diggerMaster_upsideDown", false);
            set => EditorPrefs.SetBool("diggerMaster_upsideDown", value);
        }

        private int textureIndex {
            get => EditorPrefs.GetInt("diggerMaster_textureIndex", 0);
            set => EditorPrefs.SetInt("diggerMaster_textureIndex", value);
        }

        private bool cutDetails {
            get => EditorPrefs.GetBool("diggerMaster_cutDetails", true);
            set => EditorPrefs.SetBool("diggerMaster_cutDetails", value);
        }

        private static bool PersistModificationsInPlayMode {
            get => EditorPrefs.GetBool(DiggerMaster.PersistModificationsInPlayModeEditorKey, true);
            set => EditorPrefs.SetBool(DiggerMaster.PersistModificationsInPlayModeEditorKey, value);
        }

        private int activeTab {
            get => EditorPrefs.GetInt("diggerMaster_activeTab", 0);
            set => EditorPrefs.SetInt("diggerMaster_activeTab", value);
        }

        private bool UseSRP => GraphicsSettings.renderPipelineAsset != null;

        private GameObject ReticleSphere {
            get {
                if (!reticleSphere) {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        UseSRP ? "Assets/Digger/Misc/SRP/SphereReticleSRP.prefab" : "Assets/Digger/Misc/SphereReticle.prefab");
                    reticleSphere = Instantiate(prefab);
                    reticleSphere.hideFlags = HideFlags.HideAndDontSave;
                }

                return reticleSphere;
            }
        }

        private GameObject ReticleCube {
            get {
                if (!reticleCube) {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        UseSRP ? "Assets/Digger/Misc/SRP/CubeReticleSRP.prefab" : "Assets/Digger/Misc/CubeReticle.prefab");
                    reticleCube = Instantiate(prefab);
                    reticleCube.hideFlags = HideFlags.HideAndDontSave;
                }

                return reticleCube;
            }
        }

        private GameObject ReticleHalfSphere {
            get {
                if (!reticleHalfSphere) {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        UseSRP ? "Assets/Digger/Misc/SRP/HalfSphereReticleSRP.prefab" : "Assets/Digger/Misc/HalfSphereReticle.prefab");
                    reticleHalfSphere = Instantiate(prefab);
                    reticleHalfSphere.hideFlags = HideFlags.HideAndDontSave;
                }

                return reticleHalfSphere;
            }
        }

        private GameObject ReticleCone {
            get {
                if (!reticleCone) {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        UseSRP ? "Assets/Digger/Misc/SRP/ConeReticleSRP.prefab" : "Assets/Digger/Misc/ConeReticle.prefab");
                    reticleCone = Instantiate(prefab);
                    reticleCone.hideFlags = HideFlags.HideAndDontSave;
                }

                return reticleCone;
            }
        }

        private GameObject ReticleCylinder {
            get {
                if (!reticleCylinder) {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        UseSRP ? "Assets/Digger/Misc/SRP/CylinderReticleSRP.prefab" : "Assets/Digger/Misc/CylinderReticle.prefab");
                    reticleCylinder = Instantiate(prefab);
                    reticleCylinder.hideFlags = HideFlags.HideAndDontSave;
                }

                return reticleCylinder;
            }
        }

        private GameObject Reticle {
            get {
                if (action == ActionType.Reset) {
                    if (reticleSphere)
                        DestroyImmediate(reticleSphere);
                    if (reticleCube)
                        DestroyImmediate(reticleCube);
                    if (reticleHalfSphere)
                        DestroyImmediate(reticleHalfSphere);
                    if (reticleCone)
                        DestroyImmediate(reticleCone);
                    return ReticleCylinder;
                }

                switch (brush) {
                    case BrushType.HalfSphere:
                        if (reticleSphere)
                            DestroyImmediate(reticleSphere);
                        if (reticleCube)
                            DestroyImmediate(reticleCube);
                        if (reticleCylinder)
                            DestroyImmediate(reticleCylinder);
                        if (reticleCone)
                            DestroyImmediate(reticleCone);
                        return ReticleHalfSphere;
                    case BrushType.RoundedCube:
                        if (reticleSphere)
                            DestroyImmediate(reticleSphere);
                        if (reticleHalfSphere)
                            DestroyImmediate(reticleHalfSphere);
                        if (reticleCylinder)
                            DestroyImmediate(reticleCylinder);
                        if (reticleCone)
                            DestroyImmediate(reticleCone);
                        return ReticleCube;
                    case BrushType.Stalagmite:
                        if (reticleSphere)
                            DestroyImmediate(reticleSphere);
                        if (reticleHalfSphere)
                            DestroyImmediate(reticleHalfSphere);
                        if (reticleCylinder)
                            DestroyImmediate(reticleCylinder);
                        if (reticleCube)
                            DestroyImmediate(reticleCube);
                        return ReticleCone;
                    case BrushType.Sphere:
                    default:
                        if (reticleHalfSphere)
                            DestroyImmediate(reticleHalfSphere);
                        if (reticleCube)
                            DestroyImmediate(reticleCube);
                        if (reticleCylinder)
                            DestroyImmediate(reticleCylinder);
                        if (reticleCone)
                            DestroyImmediate(reticleCone);
                        return ReticleSphere;
                }
            }
        }

        public void OnEnable()
        {
            master = (DiggerMaster) target;
            CheckDiggerVersion();
            diggerSystems = FindObjectsOfType<DiggerSystem>();
            foreach (var diggerSystem in diggerSystems) {
                DiggerSystemEditor.Init(diggerSystem, false);
            }

#if UNITY_2019_1_OR_NEWER
            SceneView.duringSceneGui -= OnScene;
            SceneView.duringSceneGui += OnScene;
#else
            SceneView.onSceneGUIDelegate -= OnScene;
            SceneView.onSceneGUIDelegate += OnScene;
#endif
            Undo.undoRedoPerformed -= UndoCallback;
            Undo.undoRedoPerformed += UndoCallback;
        }

        public void OnDisable()
        {
            Undo.undoRedoPerformed -= UndoCallback;
#if UNITY_2019_1_OR_NEWER
            SceneView.duringSceneGui -= OnScene;
#else
            SceneView.onSceneGUIDelegate -= OnScene;
#endif
            if (reticleSphere)
                DestroyImmediate(reticleSphere);
            if (reticleHalfSphere)
                DestroyImmediate(reticleHalfSphere);
            if (reticleCube)
                DestroyImmediate(reticleCube);
            if (reticleCone)
                DestroyImmediate(reticleCone);
            if (reticleCylinder)
                DestroyImmediate(reticleCylinder);
        }

        private static void UndoCallback()
        {
            var diggers = FindObjectsOfType<DiggerSystem>();
            foreach (var digger in diggers) {
                digger.DoUndo();
            }
        }

        public override void OnInspectorGUI()
        {
            activeTab = GUILayout.Toolbar(activeTab, new[]
            {
                EditorGUIUtility.TrTextContentWithIcon("Edit", "d_TerrainInspector.TerrainToolSplat"),
                EditorGUIUtility.TrTextContentWithIcon("Settings", "d_TerrainInspector.TerrainToolSettings"),
                EditorGUIUtility.TrTextContentWithIcon("Help", "_Help")
            });
            switch (activeTab) {
                case 0:
                    OnInspectorGUIEditTab();
                    break;
                case 1:
                    OnInspectorGUISettingsTab();
                    break;
                case 2:
                    OnInspectorGUIHelpTab();
                    break;
                default:
                    activeTab = 0;
                    break;
            }
        }

        public void OnInspectorGUIHelpTab()
        {
            EditorGUILayout.HelpBox("Thanks for using Digger!\n\n" +
                                    "Need help? Checkout the documentation and join us on Discord to get support!\n\n" +
                                    "Want to help the developer and support the project? Please write a review on the Asset Store!", MessageType.Info);


            if (GUILayout.Button("Open documentation")) {
                Application.OpenURL("https://ofux.github.io/Digger-Documentation/");
            }

            #region DiggerPRO

            if (GUILayout.Button("Write a review")) {
                Application.OpenURL("https://assetstore.unity.com/packages/tools/terrain/digger-caves-overhangs-135178");
            }

            #endregion

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Discord:", GUILayout.Width(60));
            var style = new GUIStyle(EditorStyles.textField);
            EditorGUILayout.SelectableLabel("https://discord.gg/C2X6C6s", style, GUILayout.Height(18));
            EditorGUILayout.EndHorizontal();
        }

        public void OnInspectorGUISettingsTab()
        {
            EditorGUILayout.LabelField("Global Settings", EditorStyles.boldLabel);
            master.SceneDataFolder = EditorGUILayout.TextField("Scene data folder", master.SceneDataFolder);
            EditorGUILayout.HelpBox($"Digger data for this scene can be found in {master.SceneDataPath}", MessageType.Info);
            EditorGUILayout.HelpBox("Don\'t forget to backup this folder as well when you backup your project.", MessageType.Warning);
            EditorGUILayout.Space();

            var showUnderlyingObjects = EditorGUILayout.Toggle("Show underlying objects", master.ShowUnderlyingObjects);
            if (showUnderlyingObjects != master.ShowUnderlyingObjects) {
                master.ShowUnderlyingObjects = showUnderlyingObjects;
                var diggers = FindObjectsOfType<DiggerSystem>();
                foreach (var digger in diggers) {
                    foreach (Transform child in digger.transform) {
                        child.gameObject.hideFlags = showUnderlyingObjects ? HideFlags.None : HideFlags.HideInHierarchy | HideFlags.HideInInspector;
                    }
                }

                EditorApplication.DirtyHierarchyWindowSorting();
                EditorApplication.RepaintHierarchyWindow();
                if (showUnderlyingObjects) {
                    EditorUtility.DisplayDialog("Please reload scene",
                                                "You need to reload the scene (or restart Unity) in order for this change to take full effect.", "Ok");
                }
            }

            EditorGUILayout.HelpBox("Enable this to reveal all objects created by Digger in the hierarchy. Digger creates objects as children of your terrain(s).", MessageType.Info);
            EditorGUILayout.Space();

            var newChunkSize = EditorGUILayout.IntPopup("Chunk size", master.ChunkSize, new[] {"16", "32", "64"}, new[] {17, 33, 65});
            EditorGUILayout.HelpBox("Lowering the size of chunks improves real-time editing performance, but also creates more meshes.", MessageType.Info);
            if (newChunkSize != master.ChunkSize && EditorUtility.DisplayDialog("Change chunk size & clear everything",
                                                                                "All modifications must be cleared for new chunk size to take effect.\n\n" +
                                                                                "THIS WILL CLEAR ALL MODIFICATIONS MADE WITH DIGGER.\n" +
                                                                                "This operation CANNOT BE UNDONE.\n\n" +
                                                                                "Are you sure you want to proceed?", "Yes, clear it", "Cancel")) {
                master.ChunkSize = newChunkSize;
                DoClear();
            }

            var newResolutionMult = EditorGUILayout.IntPopup("Resolution", master.ResolutionMult, new[] {"x1", "x2", "x4", "x8"}, new[] {1, 2, 4, 8});
            if (newResolutionMult != master.ResolutionMult && EditorUtility.DisplayDialog("Change resolution & clear everything",
                                                                                          "All modifications must be cleared for new resolution to take effect.\n\n" +
                                                                                          "THIS WILL CLEAR ALL MODIFICATIONS MADE WITH DIGGER.\n" +
                                                                                          "This operation CANNOT BE UNDONE.\n\n" +
                                                                                          "Are you sure you want to proceed?", "Yes, clear it", "Cancel")) {
                master.ResolutionMult = newResolutionMult;
                DoClear();
            }

            EditorGUILayout.HelpBox("If your heightmaps have a low resolution, you might want to set this to x2, x4 or x8 to generate " +
                                    "meshes with higher resolution and finer details. " +
                                    "However, keep in mind that the higher the resolution is, the more performance will be impacted.", MessageType.Info);
            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("LOD Settings", EditorStyles.boldLabel);
            var newCreateLODs = EditorGUILayout.Toggle("Enable LODs generation", master.CreateLODs);
            if (newCreateLODs != master.CreateLODs && EditorUtility.DisplayDialog($"{(newCreateLODs ? "Enable" : "Disable")} LODs generation",
                                                                                  "Digger must recompute all modifications for the new LODs generation setting to take effect.\n\n" +
                                                                                  "This operation is not destructive, but can be long.\n\n" +
                                                                                  "Do you want to proceed?", "Yes", "Cancel")) {
                master.CreateLODs = newCreateLODs;
                DoReload();
            }

            if (master.CreateLODs) {
                EditorGUILayout.LabelField("Screen Relative Transition Height of LODs:");
                master.ScreenRelativeTransitionHeightLod0 = EditorGUILayout.Slider("    LOD 0", master.ScreenRelativeTransitionHeightLod0, 0f, 1f);
                master.ScreenRelativeTransitionHeightLod1 = EditorGUILayout.Slider("    LOD 1", master.ScreenRelativeTransitionHeightLod1, 0f, master.ScreenRelativeTransitionHeightLod0);
                master.ColliderLodIndex = EditorGUILayout.IntSlider(
                    new GUIContent("Collider LOD", "LOD that will hold the collider. Increasing it will produce mesh colliders with fewer vertices but also less accuracy."),
                    master.ColliderLodIndex, 0, 2);
            }

            EditorGUILayout.Space();
            OnInspectorGUIClearButtons();
        }

        public void OnInspectorGUIEditTab()
        {
            var diggerSystem = FindObjectOfType<DiggerSystem>();
            if (diggerSystem) {
                EditorGUILayout.Space();
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Editing", EditorStyles.boldLabel);

                action = (ActionType) EditorGUILayout.EnumPopup("Action", action);

                if (action != ActionType.Reset && action != ActionType.Smooth) {
                    brush = (BrushType) EditorGUILayout.EnumPopup("Brush", brush);
                } else if (action == ActionType.Smooth) {
                    brush = BrushType.Sphere;
                }

                size = EditorGUILayout.Slider("Brush Size", size, 0.5f, 20f);

                if (action != ActionType.Reset && action != ActionType.Smooth && brush == BrushType.Stalagmite) {
                    coneHeight = EditorGUILayout.Slider("Stalagmite Height", coneHeight, 1f, 10f);
                    upsideDown = EditorGUILayout.Toggle("Upside Down", upsideDown);
                }

                if (action != ActionType.Reset) {
                    opacity = EditorGUILayout.Slider("Opacity", opacity, 0f, 1f);
                    depth = EditorGUILayout.Slider("Depth", depth, -size, size);
                }

                if (action != ActionType.Reset && action != ActionType.Smooth) {
                    GUIStyle gridList = "GridList";
                    var errorMessage = new GUIContent("No texture to display.\n\n" +
                                                      "You have to add some layers to the terrain with " +
                                                      "BOTH a texture and a normal map. Then, click on 'Sync & Refresh'.");
                    textureIndex = EditorUtils.AspectSelectionGrid(textureIndex, diggerSystem.TerrainTextures, 64, gridList, errorMessage);

                    if (diggerSystem.Terrain.terrainData.terrainLayers.Length > DiggerSystem.MaxTextureCountSupported) {
                        EditorGUILayout.HelpBox($"Digger shader supports a maximum of {DiggerSystem.MaxTextureCountSupported} textures. " +
                                                $"Consequently, only the first {DiggerSystem.MaxTextureCountSupported} terrain layers can be used.", MessageType.Warning);
                    }
                } else if (action == ActionType.Smooth) {
                    EditorGUILayout.HelpBox("Smooth action can only be done click-by-click and is slow to compute, so please be patient.\n" +
                                            "Smooth action is slow because it needs to load all neighbors of each chunk, which basically means 27 times more things to load/compute.\n\n" +
                                            "For this reason, smooth action is not supported at runtime (even with Digger PRO).", MessageType.Warning);
                }

#if UNITY_2019_3_OR_NEWER
                cutDetails = false; // handled by terrain holes feature
#else
                cutDetails = EditorGUILayout.ToggleLeft("Auto-remove terrain details", cutDetails);
#endif

                GUI.enabled = !Application.isPlaying;
                PersistModificationsInPlayMode = EditorGUILayout.ToggleLeft(Application.isPlaying ? "Persist modifications in Play Mode (stop game to edit)" : "Persist modifications in Play Mode", PersistModificationsInPlayMode);
                GUI.enabled = true;
            }

            EditorGUILayout.Space();
            OnInspectorGUIClearButtons();
        }

        private void OnInspectorGUIClearButtons()
        {
            EditorGUILayout.LabelField("Utils", EditorStyles.boldLabel);

            var doClear = GUILayout.Button("Clear") && EditorUtility.DisplayDialog("Clear",
                                                                                   "This will clear all modifications made with Digger.\n" +
                                                                                   "This operation CANNOT BE UNDONE.\n\n" +
                                                                                   "Are you sure you want to proceed?", "Yes, clear it", "Cancel");
            if (doClear) {
                DoClear();
            }

            var doReload = GUILayout.Button("Sync & Refresh") && EditorUtility.DisplayDialog("Sync & Refresh",
                                                                                             "This will recompute all modifications made with Digger. " +
                                                                                             "This operation is not destructive, but can be long.\n\n" +
                                                                                             "Are you sure you want to proceed?",
                                                                                             "Yes, go ahead", "Cancel");
            if (doReload) {
                DoReload();
            }
        }

        private static void DoClear()
        {
            var diggers = FindObjectsOfType<DiggerSystem>();
            foreach (var digger in diggers) {
                digger.Clear();
            }

            AssetDatabase.Refresh();

            foreach (var digger in diggers) {
                DiggerSystemEditor.Init(digger, true);
                Undo.ClearUndo(digger);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            GUIUtility.ExitGUI();
        }

        private static void DoReload()
        {
            var diggers = FindObjectsOfType<DiggerSystem>();
            foreach (var digger in diggers) {
                DiggerSystemEditor.Init(digger, true);
                Undo.ClearUndo(digger);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            GUIUtility.ExitGUI();
        }

        private void OnScene(SceneView sceneview)
        {
            var controlId = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current;
            if (e.type == EventType.Layout || e.type == EventType.Repaint) {
                HandleUtility.AddDefaultControl(controlId);
                return;
            }

            if (!clicking && !e.alt && e.type == EventType.MouseDown && e.button == 0) {
                clicking = true;
            } else if (clicking && (e.type == EventType.MouseUp || e.type == EventType.MouseLeaveWindow || e.isKey || e.alt || EditorWindow.mouseOverWindow == null || EditorWindow.mouseOverWindow.GetType() != typeof(SceneView))) {
                clicking = false;
                if (!Application.isPlaying || PersistModificationsInPlayMode) {
                    foreach (var diggerSystem in diggerSystems) {
                        diggerSystem.PersistAndRecordUndo();
                    }
                }
            }

            var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            var hit = GetIntersectionWithTerrain(ray);

            if (hit.HasValue) {
                var p = hit.Value.point + depth * ray.direction.normalized;
                Reticle.transform.position = p;
                Reticle.transform.localScale = 1.9f * size * Vector3.one;
                Reticle.transform.rotation = Quaternion.identity;
                if (action == ActionType.Reset) {
                    Reticle.transform.localScale += 1000f * Vector3.up;
                } else if (brush == BrushType.Stalagmite) {
                    Reticle.transform.localScale = new Vector3(2f * size, 1f * coneHeight, 2f * size);
                    if (upsideDown) {
                        Reticle.transform.rotation = Quaternion.AngleAxis(180f, Vector3.right);
                    }
                }

                if (clicking) {
                    if (action == ActionType.Smooth && Application.isPlaying) {
                        EditorUtility.DisplayDialog("Warning", "Smooth action cannot be used while Playing.", "Ok");
                    } else {
                        foreach (var diggerSystem in diggerSystems) {
                            diggerSystem.Modify(brush, action, opacity, p, size, coneHeight, upsideDown, textureIndex, cutDetails);
                        }

                        if (action == ActionType.Smooth) {
                            clicking = false;
                            foreach (var diggerSystem in diggerSystems) {
                                diggerSystem.PersistAndRecordUndo();
                            }
                        }
                    }
                }

                HandleUtility.Repaint();
            }
        }

        private static RaycastHit? GetIntersectionWithTerrain(Ray ray)
        {
            if (DiggerPhysics.Raycast(ray, out var hit, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) {
                return hit;
            }

            return null;
        }

        [MenuItem("Tools/Digger/Setup terrains")]
        public static void SetupTerrains()
        {
            if (!FindObjectOfType<DiggerMaster>()) {
                var goMaster = new GameObject("Digger Master");
                goMaster.transform.localPosition = Vector3.zero;
                goMaster.transform.localRotation = Quaternion.identity;
                goMaster.transform.localScale = Vector3.one;
                var master = goMaster.AddComponent<DiggerMaster>();
                master.CreateDirs();
            }

            var isCTS = false;
            var lightmapStaticWarn = false;
            var terrains = FindObjectsOfType<Terrain>();
            foreach (var terrain in terrains) {
                if (!terrain.gameObject.GetComponentInChildren<DiggerSystem>()) {
                    var go = new GameObject("Digger");
                    go.transform.parent = terrain.transform;
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = Vector3.one;
                    var digger = go.AddComponent<DiggerSystem>();
                    DiggerSystemEditor.Init(digger, true);
                    isCTS = isCTS || digger.MaterialType == TerrainMaterialType.CTS;
#if UNITY_2019_2_OR_NEWER
                    lightmapStaticWarn = lightmapStaticWarn || GameObjectUtility.GetStaticEditorFlags(terrain.gameObject).HasFlag(StaticEditorFlags.ContributeGI);
#else
                    lightmapStaticWarn = lightmapStaticWarn || GameObjectUtility.GetStaticEditorFlags(terrain.gameObject).HasFlag(StaticEditorFlags.LightmapStatic);
#endif
                }
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            if (lightmapStaticWarn) {
                if (!EditorUtility.DisplayDialog("Warning - Lightmapping", "It is recommended to disable lightmapping on terrains " +
                                                                           "when using Digger. Otherwise there might be a visual difference between " +
                                                                           "Digger meshes and the terrains.\n\n" +
                                                                           "To disable lightmapping on a terrain, go to terrain settings and disable " +
                                                                           "'Lightmap Static' toggle.", "Ok", "Terrain settings?")) {
                    Application.OpenURL("https://docs.unity3d.com/Manual/terrain-OtherSettings.html");
                }
            }

            if (isCTS) {
                EditorUtility.DisplayDialog("Warning - CTS", "Digger has detected CTS on your terrain(s) and has been setup accordingly.\n\n" +
                                                             "You may have to close the scene and open it again (or restart Unity) to " +
                                                             "force it to refresh before using Digger.", "Ok");
            }
        }

        [MenuItem("Tools/Digger/Remove Digger from the scene")]
        public static void RemoveDiggerFromTerrains()
        {
            var confirm = EditorUtility.DisplayDialog("Remove Digger from the scene",
                                                      "You are about to completely remove Digger from the scene and clear all related Digger data.\n\n" +
                                                      "This operation CANNOT BE UNDONE.\n\n" +
                                                      "Are you sure you want to proceed?", "Yes, remove Digger", "Cancel");
            if (!confirm)
                return;

            var terrains = FindObjectsOfType<Terrain>();
            foreach (var terrain in terrains) {
                var digger = terrain.gameObject.GetComponentInChildren<DiggerSystem>();
                if (digger) {
                    digger.Clear();
                    DestroyImmediate(digger.gameObject);
                }
            }

            var diggerMaster = FindObjectOfType<DiggerMaster>();
            if (diggerMaster) {
                DestroyImmediate(diggerMaster.gameObject);
            }

            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        public static void LoadAllChunks()
        {
            var diggers = FindObjectsOfType<DiggerSystem>();
            foreach (var digger in diggers) {
                digger.ReloadVersion();
                digger.Reload(true, true);
                digger.PersistAndRecordUndo();
                Undo.ClearUndo(digger);
            }
        }

        public static void OnEnterPlayMode()
        {
            if (!PersistModificationsInPlayMode) {
                var diggers = FindObjectsOfType<DiggerSystem>();
                foreach (var digger in diggers) {
                    Undo.ClearUndo(digger);
                }

                var cutters = FindObjectsOfType<TerrainCutter>();
                foreach (var cutter in cutters) {
                    cutter.OnEnterPlayMode();
                }
            }
        }

        public static void OnExitPlayMode()
        {
            if (!PersistModificationsInPlayMode) {
                var cutters = FindObjectsOfType<TerrainCutter>();
                foreach (var cutter in cutters) {
                    cutter.OnExitPlayMode();
                }
            }

            LoadAllChunks();
        }

        private static void CheckDiggerVersion()
        {
            var warned = false;
            var diggers = FindObjectsOfType<DiggerSystem>();
            foreach (var digger in diggers) {
                if (digger.GetDiggerVersion() != DiggerSystem.DiggerVersion) {
                    if (!warned) {
                        warned = true;
                        EditorUtility.DisplayDialog("New Digger version",
                                                    "Looks like Digger was updated. Digger is going to synchronize and reload all its data " +
                                                    "to ensure compatibility. This may take a while.\n\nDon't forget to save your scene once this is done.", "Ok");
                    }

                    DiggerSystemEditor.Init(digger, true);
                    Undo.ClearUndo(digger);
                }
            }

            if (warned) {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Undo.ClearAll();
            }
        }
    }
}