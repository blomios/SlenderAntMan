using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Digger
{
    [CustomEditor(typeof(DiggerSystem))]
    public class DiggerSystemEditor : Editor
    {
        private DiggerSystem diggerSystem;
        private static readonly int TerrainWidthInvProperty = Shader.PropertyToID("_TerrainWidthInv");
        private static readonly int TerrainHeightInvProperty = Shader.PropertyToID("_TerrainHeightInv");
        private static readonly int SplatArrayProperty = Shader.PropertyToID("_SplatArray");
        private static readonly int NormalArrayProperty = Shader.PropertyToID("_NormalArray");
        private const string SplatPrefixProperty = "_Splat";
        private const string NormalPrefixProperty = "_Normal";

        public void OnEnable()
        {
            diggerSystem = (DiggerSystem) target;
            Init(diggerSystem, false);
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox($"Digger data for this terrain can be found in {diggerSystem.BasePathData}", MessageType.Info);
            EditorGUILayout.HelpBox($"Raw voxel data can be found in {diggerSystem.BasePathData}/.internal", MessageType.Info);
            EditorGUILayout.HelpBox("DO NOT CHANGE / RENAME / MOVE this folder.", MessageType.Warning);
            EditorGUILayout.HelpBox("Don\'t forget to backup this folder as well when you backup your project.", MessageType.Warning);

            EditorGUILayout.LabelField("Use Digger Master to start digging.");

            var showDebug = EditorGUILayout.Toggle("Show debug data", diggerSystem.ShowDebug);
            if (showDebug != diggerSystem.ShowDebug) {
                diggerSystem.ShowDebug = showDebug;
                foreach (Transform child in diggerSystem.transform) {
                    child.gameObject.hideFlags = showDebug ? HideFlags.None : HideFlags.HideInHierarchy | HideFlags.HideInInspector;
                }

                EditorApplication.DirtyHierarchyWindowSorting();
                EditorApplication.RepaintHierarchyWindow();
            }

            if (showDebug) {
                EditorGUILayout.LabelField($"GUID: {diggerSystem.Guid}");
                EditorGUILayout.LabelField($"Undo/redo stack version: {diggerSystem.Version}");

                DrawDefaultInspector();
            }
        }

        public static void Init(DiggerSystem diggerSystem, bool forceRefresh)
        {
            if (!forceRefresh && diggerSystem.IsInitialized)
                return;

            diggerSystem.PreInit(true);

            if (diggerSystem.Materials == null || forceRefresh)
                SetupMaterial(diggerSystem, forceRefresh);

            diggerSystem.Init(forceRefresh);
            if (forceRefresh) {
                diggerSystem.PersistDiggerVersion();
            }
        }

        private static void SetupMaterial(DiggerSystem diggerSystem, bool forceRefresh)
        {
            Utils.Profiler.BeginSample("[Dig] SetupMaterial");

            if (EditorUtils.CTSExists(diggerSystem.Terrain)) {
                diggerSystem.MaterialType = TerrainMaterialType.CTS;
                Debug.Log("Setting up Digger with CTS shaders");
                SetupCTSMaterial(diggerSystem);
            } else if (IsBuiltInURP()) {
                diggerSystem.MaterialType = TerrainMaterialType.URP;
                Debug.Log("Setting up Digger with URP shaders");
                SetupURPMaterials(diggerSystem, forceRefresh);
            } else if (IsBuiltInLWRP()) {
                diggerSystem.MaterialType = TerrainMaterialType.LWRP;
                Debug.Log("Setting up Digger with LWRP shaders");
                SetupLWRPMaterials(diggerSystem, forceRefresh);
            } else {
                diggerSystem.MaterialType = TerrainMaterialType.Standard;
                Debug.Log("Setting up Digger with standard shaders");

                if (SystemInfo.supports2DArrayTextures) {
                    SetupDefaultMaterialsWithTextureArray(diggerSystem, forceRefresh);
                } else {
                    SetupDefaultMaterials(diggerSystem, forceRefresh);
                }
            }

            Utils.Profiler.EndSample();
        }

        private static bool IsBuiltInLWRP()
        {
#if UNITY_2019_3_OR_NEWER
            return GraphicsSettings.currentRenderPipeline != null && GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial.shader.name == "Lightweight Render Pipeline/Terrain/Lit";
#elif UNITY_2019_1_OR_NEWER
            return GraphicsSettings.renderPipelineAsset != null && GraphicsSettings.renderPipelineAsset.defaultTerrainMaterial.shader.name == "Lightweight Render Pipeline/Terrain/Lit";
#else
            return GraphicsSettings.renderPipelineAsset != null && GraphicsSettings.renderPipelineAsset.GetDefaultTerrainMaterial().shader.name == "Lightweight Render Pipeline/Terrain/Lit";
#endif
        }

        private static bool IsBuiltInURP()
        {
#if UNITY_2019_3_OR_NEWER
            return GraphicsSettings.currentRenderPipeline != null && GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial.shader.name == "Universal Render Pipeline/Terrain/Lit";
#elif UNITY_2019_1_OR_NEWER
            return GraphicsSettings.renderPipelineAsset != null && GraphicsSettings.renderPipelineAsset.defaultTerrainMaterial.shader.name == "Universal Render Pipeline/Terrain/Lit";
#else
            return GraphicsSettings.renderPipelineAsset != null && GraphicsSettings.renderPipelineAsset.GetDefaultTerrainMaterial().shader.name == "Universal Render Pipeline/Terrain/Lit";
#endif
        }

        private static void SetupStandardTerrainMaterial(DiggerSystem diggerSystem, bool forceRefresh)
        {
            if (forceRefresh || !diggerSystem.Terrain.materialTemplate || diggerSystem.Terrain.materialTemplate.shader.name != "Nature/Terrain/Digger/Cuttable-Triplanar") {
#if !UNITY_2019_2_OR_NEWER
                diggerSystem.Terrain.materialType = Terrain.MaterialType.Custom;
#endif
                var terrainMaterial = new Material(Shader.Find("Nature/Terrain/Digger/Cuttable-Triplanar"));
                terrainMaterial = EditorUtils.CreateOrReplaceAsset(terrainMaterial, Path.Combine(diggerSystem.BasePathData, "terrainMaterial.mat"));
                terrainMaterial.SetFloat(TerrainWidthInvProperty, 1f / diggerSystem.Terrain.terrainData.size.x);
                terrainMaterial.SetFloat(TerrainHeightInvProperty, 1f / diggerSystem.Terrain.terrainData.size.z);
                diggerSystem.Terrain.materialTemplate = terrainMaterial;
            }

            if (diggerSystem.Terrain.materialTemplate.shader.name != "Nature/Terrain/Digger/Cuttable-Triplanar")
                Debug.LogWarning("Looks like terrain material doesn't match cave meshes material.");
        }

        private static void SetupDefaultMaterials(DiggerSystem diggerSystem, bool forceRefresh)
        {
            SetupStandardTerrainMaterial(diggerSystem, forceRefresh);

            const int txtCountPerPass = 4;
            const int maxPassCount = 4;

            var tData = diggerSystem.Terrain.terrainData;
            var passCount = Mathf.Min(tData.terrainLayers.Length / txtCountPerPass + 1, maxPassCount);
            if (diggerSystem.Materials == null || diggerSystem.Materials.Length != passCount) {
                diggerSystem.Materials = new Material[passCount];
            }

            var textures = new List<Texture2D>();
            for (var pass = 0; pass < passCount; ++pass) {
                SetupDefaultMaterial(pass, txtCountPerPass, diggerSystem, textures);
            }

            diggerSystem.TerrainTextures = textures.ToArray();
        }

        private static void SetupDefaultMaterial(int pass, int txtCountPerPass, DiggerSystem diggerSystem, List<Texture2D> textures)
        {
            var material = diggerSystem.Materials[pass];
            var expectedShaderName = $"Digger/Standard/Mesh-Pass{pass}";
            if (!material || material.shader.name != expectedShaderName) {
                material = new Material(Shader.Find(expectedShaderName));
            }

            var tData = diggerSystem.Terrain.terrainData;
            var offset = pass * txtCountPerPass;
            for (var i = 0; i + offset < tData.terrainLayers.Length && i < txtCountPerPass; i++) {
                var terrainLayer = tData.terrainLayers[i + offset];
                if (terrainLayer == null || terrainLayer.diffuseTexture == null)
                    continue;

                material.SetFloat($"_tiles{i}x", 1.0f / terrainLayer.tileSize.x);
                material.SetFloat($"_tiles{i}y", 1.0f / terrainLayer.tileSize.y);
                material.SetFloat($"_offset{i}x", terrainLayer.tileOffset.x);
                material.SetFloat($"_offset{i}y", terrainLayer.tileOffset.y);
                material.SetFloat($"_normalScale{i}", terrainLayer.normalScale);
                material.SetFloat($"_Metallic{i}", terrainLayer.metallic);
                material.SetFloat($"_Smoothness{i}", terrainLayer.smoothness);
                material.SetTexture(SplatPrefixProperty + i, terrainLayer.diffuseTexture);
                material.SetTexture(NormalPrefixProperty + i, terrainLayer.normalMapTexture);
                textures.Add(terrainLayer.diffuseTexture);
            }

            var matPath = Path.Combine(diggerSystem.BasePathData, $"meshMaterialPass{pass}.mat");
            material = EditorUtils.CreateOrReplaceAsset(material, matPath);
            AssetDatabase.ImportAsset(matPath, ImportAssetOptions.ForceUpdate);
            diggerSystem.Materials[pass] = material;
        }

        private static void SetupDefaultMaterialsWithTextureArray(DiggerSystem diggerSystem, bool forceRefresh)
        {
            SetupStandardTerrainMaterial(diggerSystem, forceRefresh);

            const int txtCountPerPass = 8;
            const int maxPassCount = 2;

            var tData = diggerSystem.Terrain.terrainData;
            var passCount = Mathf.Min(tData.terrainLayers.Length / txtCountPerPass + 1, maxPassCount);
            if (diggerSystem.Materials == null || diggerSystem.Materials.Length != passCount) {
                diggerSystem.Materials = new Material[passCount];
            }

            var showMissingNormalMapWarning = false;
            var textures = new List<Texture2D>();
            for (var pass = 0; pass < passCount; ++pass) {
                SetupDefaultMaterialWithTextureArray(pass, txtCountPerPass, diggerSystem, textures, forceRefresh, out showMissingNormalMapWarning);
            }

            diggerSystem.TerrainTextures = textures.ToArray();

            if (textures.Count == 0) {
                if (!EditorUtility.DisplayDialog("Warning!",
                                                 "Digger did not find any terrain layer with both a texture and a normal " +
                                                 "map. Digger meshes will be rendered completely white.\n\nPlease add some layers " +
                                                 "to your terrain and make sure they contain both a texture and a normal map. " +
                                                 "Once this is done, click on 'Sync & Refresh' button of DiggerMaster.",
                                                 "Ok", "What is a terrain layer?")) {
                    Application.OpenURL("https://docs.unity3d.com/Manual/class-TerrainLayer.html");
                }
            } else if (showMissingNormalMapWarning) {
                if (!EditorUtility.DisplayDialog("Warning!",
                                                 "Some terrain layers don't have a normal map. " +
                                                 "Digger will ignore them and won't be able to render them.\n\n" +
                                                 "It is recommended to set a normal map to all terrain layers.",
                                                 "Ok", "What is a terrain layer?")) {
                    Application.OpenURL("https://docs.unity3d.com/Manual/class-TerrainLayer.html");
                }
            }
        }

        private static void SetupDefaultMaterialWithTextureArray(int pass, int txtCountPerPass, DiggerSystem diggerSystem, List<Texture2D> allTextures, bool forceRefresh, out bool showMissingNormalMapWarning)
        {
            var material = diggerSystem.Materials[pass];
            var expectedShaderName = $"Digger/Standard/TextureArray/Mesh-Pass{pass}";
            if (!material || material.shader.name != expectedShaderName) {
                material = new Material(Shader.Find(expectedShaderName));
            }

            showMissingNormalMapWarning = false;
            var tData = diggerSystem.Terrain.terrainData;
            var offset = pass * txtCountPerPass;
            var textures = new List<Texture2D>();
            var normals = new List<Texture2D>();
            for (var i = 0; i + offset < tData.terrainLayers.Length && i < txtCountPerPass; i++) {
                var terrainLayer = tData.terrainLayers[i + offset];
                if (terrainLayer == null || terrainLayer.diffuseTexture == null)
                    continue;
                if (terrainLayer.normalMapTexture == null) {
                    Debug.LogWarning($"All terrain layers should have a normal map, but '{terrainLayer.name}' layer doesn't.");
                    showMissingNormalMapWarning = true;
                    continue;
                }

                textures.Add(terrainLayer.diffuseTexture);
                normals.Add(terrainLayer.normalMapTexture);

                material.SetFloat($"_tiles{i}x", 1.0f / terrainLayer.tileSize.x);
                material.SetFloat($"_tiles{i}y", 1.0f / terrainLayer.tileSize.y);
                material.SetFloat($"_offset{i}x", terrainLayer.tileOffset.x);
                material.SetFloat($"_offset{i}y", terrainLayer.tileOffset.y);
                material.SetFloat($"_normalScale{i}", terrainLayer.normalScale);
                material.SetFloat($"_Metallic{i}", terrainLayer.metallic);
                material.SetFloat($"_Smoothness{i}", terrainLayer.smoothness);
                allTextures.Add(terrainLayer.diffuseTexture);
            }

            SetTextureArraysToMaterial(material, textures, normals, forceRefresh);

            var matPath = Path.Combine(diggerSystem.BasePathData, $"meshMaterialPass{pass}.mat");
            material = EditorUtils.CreateOrReplaceAsset(material, matPath);
            AssetDatabase.ImportAsset(matPath, ImportAssetOptions.ForceUpdate);
            diggerSystem.Materials[pass] = material;
        }

        private static void SetTextureArraysToMaterial(Material material, List<Texture2D> textures, List<Texture2D> normals, bool forceRefresh)
        {
            if (!forceRefresh &&
                material.GetTexture(SplatArrayProperty) &&
                material.GetTexture(NormalArrayProperty))
                return;

            var texture2DArray = TextureArrayManager.GetCreateTexture2DArray(textures, false);
            var normal2DArray = TextureArrayManager.GetCreateTexture2DArray(normals, true);

            if (texture2DArray == null || normal2DArray == null)
                return;

            // Set the texture to material
            material.SetTexture(SplatArrayProperty, texture2DArray);
            material.SetTexture(NormalArrayProperty, normal2DArray);
        }

        #region LWRP

        private static void SetupLWRPTerrainMaterial(DiggerSystem diggerSystem, bool forceRefresh)
        {
            if (forceRefresh || !diggerSystem.Terrain.materialTemplate || diggerSystem.Terrain.materialTemplate.shader.name != "Digger/LWRP/Terrain/Lit") {
#if !UNITY_2019_2_OR_NEWER
                diggerSystem.Terrain.materialType = Terrain.MaterialType.Custom;
#endif
                var terrainMaterial = new Material(Shader.Find("Digger/LWRP/Terrain/Lit"));
                terrainMaterial = EditorUtils.CreateOrReplaceAsset(terrainMaterial, Path.Combine(diggerSystem.BasePathData, "terrainMaterial.mat"));
                terrainMaterial.SetFloat(TerrainWidthInvProperty, 1f / diggerSystem.Terrain.terrainData.size.x);
                terrainMaterial.SetFloat(TerrainHeightInvProperty, 1f / diggerSystem.Terrain.terrainData.size.z);
                diggerSystem.Terrain.materialTemplate = terrainMaterial;
            }

            if (diggerSystem.Terrain.materialTemplate.shader.name != "Digger/LWRP/Terrain/Lit")
                Debug.LogWarning("Looks like terrain material doesn't match cave meshes material.");
        }

        private static void SetupLWRPMaterials(DiggerSystem diggerSystem, bool forceRefresh)
        {
            SetupLWRPTerrainMaterial(diggerSystem, forceRefresh);

            const int txtCountPerPass = 4;
            const int maxPassCount = 4;

            var tData = diggerSystem.Terrain.terrainData;
            var passCount = Mathf.Min(tData.terrainLayers.Length / txtCountPerPass + 1, maxPassCount);
            if (diggerSystem.Materials == null || diggerSystem.Materials.Length != passCount) {
                diggerSystem.Materials = new Material[passCount];
            }

            var textures = new List<Texture2D>();
            for (var pass = 0; pass < passCount; ++pass) {
                SetupLWRPMaterial(pass, txtCountPerPass, diggerSystem, textures);
            }

            diggerSystem.TerrainTextures = textures.ToArray();
        }

        private static void SetupLWRPMaterial(int pass, int txtCountPerPass, DiggerSystem diggerSystem, List<Texture2D> textures)
        {
            var material = diggerSystem.Materials[pass];
            var expectedShaderName = $"Digger/LWRP/Mesh-Pass{pass}";
            if (!material || material.shader.name != expectedShaderName) {
                material = new Material(Shader.Find(expectedShaderName));
            }

            var tData = diggerSystem.Terrain.terrainData;
            var offset = pass * txtCountPerPass;
            for (var i = 0; i + offset < tData.terrainLayers.Length && i < txtCountPerPass; i++) {
                var terrainLayer = tData.terrainLayers[i + offset];
                if (terrainLayer == null || terrainLayer.diffuseTexture == null)
                    continue;

                material.SetFloat($"_tiles{i}x", 1.0f / terrainLayer.tileSize.x);
                material.SetFloat($"_tiles{i}y", 1.0f / terrainLayer.tileSize.y);
                material.SetFloat($"_offset{i}x", terrainLayer.tileOffset.x);
                material.SetFloat($"_offset{i}y", terrainLayer.tileOffset.y);
                material.SetFloat($"_normalScale{i}", terrainLayer.normalScale);
                material.SetFloat($"_Metallic{i}", terrainLayer.metallic);
                material.SetFloat($"_Smoothness{i}", terrainLayer.smoothness);
                material.SetTexture(SplatPrefixProperty + i, terrainLayer.diffuseTexture);
                material.SetTexture(NormalPrefixProperty + i, terrainLayer.normalMapTexture);
                textures.Add(terrainLayer.diffuseTexture);
            }

            var matPath = Path.Combine(diggerSystem.BasePathData, $"meshMaterialPass{pass}.mat");
            material = EditorUtils.CreateOrReplaceAsset(material, matPath);
            AssetDatabase.ImportAsset(matPath, ImportAssetOptions.ForceUpdate);
            diggerSystem.Materials[pass] = material;
        }

        #endregion


        #region URP

        private static void SetupURPTerrainMaterial(DiggerSystem diggerSystem, bool forceRefresh)
        {
            if (forceRefresh || !diggerSystem.Terrain.materialTemplate || diggerSystem.Terrain.materialTemplate.shader.name != "Digger/URP/Terrain/Lit") {
                var terrainMaterial = new Material(Shader.Find("Digger/URP/Terrain/Lit"));
                terrainMaterial = EditorUtils.CreateOrReplaceAsset(terrainMaterial, Path.Combine(diggerSystem.BasePathData, "terrainMaterial.mat"));
                terrainMaterial.SetFloat(TerrainWidthInvProperty, 1f / diggerSystem.Terrain.terrainData.size.x);
                terrainMaterial.SetFloat(TerrainHeightInvProperty, 1f / diggerSystem.Terrain.terrainData.size.z);
                diggerSystem.Terrain.materialTemplate = terrainMaterial;
            }

            if (diggerSystem.Terrain.materialTemplate.shader.name != "Digger/URP/Terrain/Lit")
                Debug.LogWarning("Looks like terrain material doesn't match cave meshes material.");
        }

        private static void SetupURPMaterials(DiggerSystem diggerSystem, bool forceRefresh)
        {
            SetupURPTerrainMaterial(diggerSystem, forceRefresh);

            const int txtCountPerPass = 4;
            const int maxPassCount = 4;

            var tData = diggerSystem.Terrain.terrainData;
            var passCount = Mathf.Min(tData.terrainLayers.Length / txtCountPerPass + 1, maxPassCount);
            if (diggerSystem.Materials == null || diggerSystem.Materials.Length != passCount) {
                diggerSystem.Materials = new Material[passCount];
            }

            var textures = new List<Texture2D>();
            for (var pass = 0; pass < passCount; ++pass) {
                SetupURPMaterial(pass, txtCountPerPass, diggerSystem, textures);
            }

            diggerSystem.TerrainTextures = textures.ToArray();
        }

        private static void SetupURPMaterial(int pass, int txtCountPerPass, DiggerSystem diggerSystem, List<Texture2D> textures)
        {
            var material = diggerSystem.Materials[pass];
            var expectedShaderName = $"Digger/URP/Mesh-Pass{pass}";
            if (!material || material.shader.name != expectedShaderName) {
                material = new Material(Shader.Find(expectedShaderName));
            }

            var tData = diggerSystem.Terrain.terrainData;
            var offset = pass * txtCountPerPass;
            for (var i = 0; i + offset < tData.terrainLayers.Length && i < txtCountPerPass; i++) {
                var terrainLayer = tData.terrainLayers[i + offset];
                if (terrainLayer == null || terrainLayer.diffuseTexture == null)
                    continue;

                material.SetFloat($"_tiles{i}x", 1.0f / terrainLayer.tileSize.x);
                material.SetFloat($"_tiles{i}y", 1.0f / terrainLayer.tileSize.y);
                material.SetFloat($"_offset{i}x", terrainLayer.tileOffset.x);
                material.SetFloat($"_offset{i}y", terrainLayer.tileOffset.y);
                material.SetFloat($"_normalScale{i}", terrainLayer.normalScale);
                material.SetFloat($"_Metallic{i}", terrainLayer.metallic);
                material.SetFloat($"_Smoothness{i}", terrainLayer.smoothness);
                material.SetTexture(SplatPrefixProperty + i, terrainLayer.diffuseTexture);
                material.SetTexture(NormalPrefixProperty + i, terrainLayer.normalMapTexture);
                textures.Add(terrainLayer.diffuseTexture);
            }

            var matPath = Path.Combine(diggerSystem.BasePathData, $"meshMaterialPass{pass}.mat");
            material = EditorUtils.CreateOrReplaceAsset(material, matPath);
            AssetDatabase.ImportAsset(matPath, ImportAssetOptions.ForceUpdate);
            diggerSystem.Materials[pass] = material;
        }

        #endregion


        #region CTS

        private static void SetupCTSMaterial(DiggerSystem diggerSystem)
        {
            if (!diggerSystem.Terrain.materialTemplate) {
                Debug.LogError("Could not setup CTS material for Digger because terrain.materialTemplate is null.");
                return;
            }

            if (diggerSystem.Materials == null || diggerSystem.Materials.Length != 1) {
                diggerSystem.Materials = new Material[1];
            }

            if (diggerSystem.Terrain.materialTemplate.shader.name.StartsWith("CTS/CTS Terrain Shader Basic")) {
                SetupCTSBasicMaterial(diggerSystem);
            } else if (diggerSystem.Terrain.materialTemplate.shader.name.StartsWith("CTS/CTS Terrain Shader Advanced Tess")) {
                SetupCTSAdvancedTessMaterial(diggerSystem);
            } else if (diggerSystem.Terrain.materialTemplate.shader.name.StartsWith("CTS/CTS Terrain Shader Advanced")) {
                SetupCTSAdvancedMaterial(diggerSystem);
            } else {
                Debug.LogError($"Could not setup CTS material for Digger because terrain shader was not a known CTS shader. Was {diggerSystem.Terrain.materialTemplate.shader.name}");
            }
        }

        private static void SetupCTSBasicMaterial(DiggerSystem diggerSystem)
        {
            if (!diggerSystem.Materials[0] || diggerSystem.Materials[0].shader.name != "CTS/CTS Terrain Shader Basic Mesh") {
                diggerSystem.Materials[0] = new Material(Shader.Find("CTS/CTS Terrain Shader Basic Mesh"));
            }

            if (!diggerSystem.Terrain.materialTemplate || !diggerSystem.Terrain.materialTemplate.shader.name.StartsWith("CTS/CTS Terrain Shader Basic")) {
                Debug.LogWarning($"Looks like terrain material doesn\'t match cave meshes material. " +
                                 $"Expected \'CTS/CTS Terrain Shader Basic CutOut\', was {diggerSystem.Terrain.materialTemplate.shader.name}. " +
                                 $"Please fix this by assigning the right material to the terrain.");
                return;
            }

            diggerSystem.Materials[0].CopyPropertiesFromMaterial(diggerSystem.Terrain.materialTemplate);

            var matPath = Path.Combine(diggerSystem.BasePathData, "meshMaterial.mat");
            diggerSystem.Materials[0] = EditorUtils.CreateOrReplaceAsset(diggerSystem.Materials[0], matPath);
            AssetDatabase.ImportAsset(matPath, ImportAssetOptions.ForceUpdate);
        }

        private static void SetupCTSAdvancedMaterial(DiggerSystem diggerSystem)
        {
            if (!diggerSystem.Materials[0] || diggerSystem.Materials[0].shader.name != "CTS/CTS Terrain Shader Advanced Mesh") {
                diggerSystem.Materials[0] = new Material(Shader.Find("CTS/CTS Terrain Shader Advanced Mesh"));
            }

            if (!diggerSystem.Terrain.materialTemplate || !diggerSystem.Terrain.materialTemplate.shader.name.StartsWith("CTS/CTS Terrain Shader Advanced")) {
                Debug.LogWarning($"Looks like terrain material doesn\'t match cave meshes material. " +
                                 $"Expected \'CTS/CTS Terrain Shader Advanced CutOut\', was {diggerSystem.Terrain.materialTemplate.shader.name}. " +
                                 $"Please fix this by assigning the right material to the terrain.");
                return;
            }

            diggerSystem.Materials[0].CopyPropertiesFromMaterial(diggerSystem.Terrain.materialTemplate);

            var matPath = Path.Combine(diggerSystem.BasePathData, "meshMaterial.mat");
            diggerSystem.Materials[0] = EditorUtils.CreateOrReplaceAsset(diggerSystem.Materials[0], matPath);
            AssetDatabase.ImportAsset(matPath, ImportAssetOptions.ForceUpdate);
        }

        private static void SetupCTSAdvancedTessMaterial(DiggerSystem diggerSystem)
        {
            if (!diggerSystem.Materials[0] || diggerSystem.Materials[0].shader.name != "CTS/CTS Terrain Shader Advanced Tess Mesh") {
                diggerSystem.Materials[0] = new Material(Shader.Find("CTS/CTS Terrain Shader Advanced Tess Mesh"));
            }

            if (!diggerSystem.Terrain.materialTemplate || !diggerSystem.Terrain.materialTemplate.shader.name.StartsWith("CTS/CTS Terrain Shader Advanced Tess")) {
                Debug.LogWarning($"Looks like terrain material doesn\'t match cave meshes material. " +
                                 $"Expected \'CTS/CTS Terrain Shader Advanced Tess CutOut\', was {diggerSystem.Terrain.materialTemplate.shader.name}. " +
                                 $"Please fix this by assigning the right material to the terrain.");
                return;
            }

            diggerSystem.Materials[0].CopyPropertiesFromMaterial(diggerSystem.Terrain.materialTemplate);

            var matPath = Path.Combine(diggerSystem.BasePathData, "meshMaterial.mat");
            diggerSystem.Materials[0] = EditorUtils.CreateOrReplaceAsset(diggerSystem.Materials[0], matPath);
            AssetDatabase.ImportAsset(matPath, ImportAssetOptions.ForceUpdate);
        }

        #endregion
    }
}