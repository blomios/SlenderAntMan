using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Digger.HeightFeeders;
using Digger.TerrainCutters;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
#if UNITY_EDITOR
using UnityEditor;

#endif

namespace Digger
{
    public class DiggerSystem : MonoBehaviour
    {
        public const int DiggerVersion = 8; // 10000 and above means "DiggerPRO"
        public const string VoxelFileExtension = "vox";
        private const string VoxelMetadataFileExtension = "vom";
        private const string VersionFileExtension = "ver";
        private const int UndoStackSize = 15;
        public const int MaxTextureCountSupported = 16;

        private Dictionary<Vector3i, Chunk> chunks;
        private IHeightFeeder heightFeeder;
        private HashSet<VoxelChunk> chunksToPersist;
        private Dictionary<Collider, int> colliderStates;
        private readonly HashSet<Vector3i> builtChunks = new HashSet<Vector3i>(new Vector3iComparer());
        private bool disablePersistence;
        private Bounds bounds;

        [SerializeField] private DiggerMaster master;
        [SerializeField] private string guid;
        [SerializeField] private long version = 1;

        [SerializeField] private TerrainCutter cutter;

        [SerializeField] private Vector3 heightmapScale;
        [SerializeField] private Vector3 holeMapScale;

        [SerializeField] public Terrain Terrain;
        [SerializeField] public Material[] Materials;
        [SerializeField] private TerrainMaterialType materialType;
        [SerializeField] private Texture2D[] terrainTextures;
        [SerializeField] public LayerMask Layer;
        [SerializeField] public bool ShowDebug;

        public string Guid => guid;

        public Vector3 HeightmapScale => heightmapScale;
        public Vector3 HoleMapScale => holeMapScale;

        public Vector3 CutMargin => new Vector3(Math.Max(2f, 2.1f * holeMapScale.x),
                                                Math.Max(2f, 2.1f * holeMapScale.y),
                                                Math.Max(2f, 2.1f * holeMapScale.z));

        public TerrainCutter Cutter => cutter;
        public IHeightFeeder HeightFeeder => heightFeeder;

        public Texture2D[] TerrainTextures {
            set => terrainTextures = value;
            get => terrainTextures;
        }

        public float ScreenRelativeTransitionHeightLod0 => master.ScreenRelativeTransitionHeightLod0;
        public float ScreenRelativeTransitionHeightLod1 => master.ScreenRelativeTransitionHeightLod1;
        public int ColliderLodIndex => master.ColliderLodIndex;
        public bool CreateLODs => master.CreateLODs;

        public int SizeOfMesh => master.SizeOfMesh;
        public int SizeVox => master.SizeVox;

        public int DefaultNavMeshArea { get; set; }

#if !UNITY_2019_3_OR_NEWER
        public Dictionary<Collider, int> ColliderStates => colliderStates;
#endif

        public TerrainMaterialType MaterialType {
            get => materialType;
            set => materialType = value;
        }

        private string BaseFolder => $"{guid}";
        public string BasePathData => Path.Combine(master.SceneDataPath, BaseFolder);
        private string InternalPathData => Path.Combine(BasePathData, ".internal");
        private string StreamingAssetsPathData => Path.Combine(Application.streamingAssetsPath, "DiggerData", BaseFolder);
        private string PersistentRuntimePathData => Path.Combine(Application.persistentDataPath, "DiggerData", BaseFolder);

        public long Version => version;
        public long PreviousVersion => version - 1;

        private int TerrainChunkWidth => Terrain.terrainData.heightmapResolution * master.ResolutionMult / SizeOfMesh - 1;
        private int TerrainChunkHeight => Terrain.terrainData.heightmapResolution * master.ResolutionMult / SizeOfMesh - 1;

        public bool IsInitialized => Terrain != null && chunks != null && cutter != null && heightFeeder != null && chunksToPersist != null;

        public Bounds Bounds => bounds;

#if UNITY_EDITOR
        private static bool PersistModificationsInPlayMode => EditorPrefs.GetBool(DiggerMaster.PersistModificationsInPlayModeEditorKey, true);
#endif

        private string GetPathDiggerVersionFile()
        {
            return Path.Combine(BasePathData, "digger_version.asset");
        }

        private string GetPathCurrentVersionFile()
        {
            return Path.Combine(BasePathData, "current_version.asset");
        }

        private string GetPathVersionFile(long v)
        {
            return Path.Combine(InternalPathData, $"version_{v}.{VersionFileExtension}");
        }

        public string GetEditorOnlyPathVoxelFile(Vector3i chunkPosition)
        {
            return Path.Combine(InternalPathData, $"{Chunk.GetName(chunkPosition)}.{VoxelFileExtension}");
        }

        public string GetPathVoxelFile(Vector3i chunkPosition, bool forPersistence)
        {
#if UNITY_EDITOR
            return Path.Combine(InternalPathData, $"{Chunk.GetName(chunkPosition)}.{VoxelFileExtension}");
#else
            if (forPersistence) {
                return Path.Combine(PersistentRuntimePathData, $"{Chunk.GetName(chunkPosition)}.{VoxelFileExtension}");
            }

            var path = Path.Combine(PersistentRuntimePathData, $"{Chunk.GetName(chunkPosition)}.{VoxelFileExtension}");
            if (!File.Exists(path)) {
                path = Path.Combine(StreamingAssetsPathData, $"{Chunk.GetName(chunkPosition)}.{VoxelFileExtension}");
            }
            return path;
#endif
        }

        public string GetPathVoxelMetadataFile(Vector3i chunkPosition, bool forPersistence)
        {
            return Path.ChangeExtension(GetPathVoxelFile(chunkPosition, forPersistence), VoxelMetadataFileExtension);
        }

        public string GetEditorOnlyPathVoxelMetadataFile(Vector3i chunkPosition)
        {
            return Path.ChangeExtension(GetEditorOnlyPathVoxelFile(chunkPosition), VoxelMetadataFileExtension);
        }

        public string GetPathVersionedVoxelFile(Vector3i chunkPosition, long v)
        {
            return Path.ChangeExtension(GetEditorOnlyPathVoxelFile(chunkPosition), $"vox_v{v}");
        }

        public string GetPathVersionedVoxelMetadataFile(Vector3i chunkPosition, long v)
        {
            return Path.ChangeExtension(GetEditorOnlyPathVoxelMetadataFile(chunkPosition), $"vom_v{v}");
        }

        public string GetTransparencyMapPath()
        {
            return Path.Combine(BasePathData, "TransparencyMap.asset");
        }

        public string GetTransparencyMapBackupPath(string ext = "asset")
        {
            return Path.Combine(BasePathData, $"TransparencyMapBackup.{ext}");
        }

        public string GetVersionedTransparencyMapPath(long v)
        {
            return Path.Combine(InternalPathData, $"TransparencyMap.asset_{v}");
        }

        public string GetTransparencyMapRuntimePath()
        {
            return Path.Combine(PersistentRuntimePathData, "TransparencyMap.asset");
        }

        public Bounds GetChunkBounds()
        {
            var worldSize = Vector3.one * SizeOfMesh;
            worldSize.x *= HeightmapScale.x;
            worldSize.z *= HeightmapScale.z;
            return new Bounds(worldSize * 0.5f, worldSize);
        }

        private void Awake()
        {
            colliderStates = new Dictionary<Collider, int>(new ColliderComparer());
        }

        public void PersistAndRecordUndo()
        {
#if UNITY_EDITOR
            CreateDirs();

            foreach (var chunkToPersist in chunksToPersist) {
                chunkToPersist.Persist();
            }

            chunksToPersist.Clear();

            DeleteOtherVersions(true, version - UndoStackSize);

            var versionInfo = new VersionInfo
            {
                Version = version,
                AliveChunks = chunks.Keys.ToList()
            };

            File.WriteAllText(GetPathVersionFile(version), JsonUtility.ToJson(versionInfo));

            cutter.SaveTo(GetVersionedTransparencyMapPath(version));

            Undo.RegisterCompleteObjectUndo(this, "Digger edit");
            ++version;
            PersistVersion();
#endif
        }

        public void PersistAtRuntime()
        {
            if (!Directory.Exists(PersistentRuntimePathData)) {
                Directory.CreateDirectory(PersistentRuntimePathData);
            }

            foreach (var chunkToPersist in chunksToPersist) {
                chunkToPersist.Persist();
            }

            chunksToPersist.Clear();

            cutter.SaveTo(GetTransparencyMapRuntimePath());
        }


        public void DoUndo()
        {
#if UNITY_EDITOR
            if (!Terrain || !cutter || !Directory.Exists(InternalPathData) || !File.Exists(GetPathVersionFile(PreviousVersion))) {
                ++version;
                PersistVersion();
                Undo.ClearUndo(this);
                return;
            }

            var versionInfo = JsonUtility.FromJson<VersionInfo>(File.ReadAllText(GetPathVersionFile(PreviousVersion)));

            UndoRedoFiles();
            Reload(true, false);
            SyncChunksWithVersion(versionInfo);
#endif
        }

        public void PersistDiggerVersion()
        {
#if UNITY_EDITOR
            CreateDirs();
            EditorUtils.CreateOrReplaceAsset(new TextAsset(DiggerVersion.ToString()), GetPathDiggerVersionFile());
#endif
        }

        public int GetDiggerVersion()
        {
#if UNITY_EDITOR
            var verAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(GetPathDiggerVersionFile());
            if (verAsset) {
                return Convert.ToInt32(verAsset.text);
            }
#endif
            return 0;
        }

        private void PersistVersion()
        {
#if UNITY_EDITOR
            CreateDirs();
            EditorUtils.CreateOrReplaceAsset(new TextAsset(version.ToString()), GetPathCurrentVersionFile());
#endif
        }

        public void ReloadVersion()
        {
#if UNITY_EDITOR
            var verAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(GetPathCurrentVersionFile());
            if (verAsset) {
                version = Convert.ToInt64(verAsset.text);
            }
#endif
        }

        private void UndoRedoFiles()
        {
            var dir = new DirectoryInfo(InternalPathData);
            foreach (var file in dir.EnumerateFiles($"*.vox_v{PreviousVersion}")) {
                var bytesFilePath = Path.ChangeExtension(file.FullName, VoxelFileExtension);
                File.Copy(file.FullName, bytesFilePath, true);
            }

            foreach (var file in dir.EnumerateFiles($"*.vom_v{PreviousVersion}")) {
                var bytesFilePath = Path.ChangeExtension(file.FullName, VoxelMetadataFileExtension);
                File.Copy(file.FullName, bytesFilePath, true);
            }

            cutter.LoadFrom(GetVersionedTransparencyMapPath(PreviousVersion));
        }

        private void SyncChunksWithVersion(VersionInfo versionInfo)
        {
            // Check for missing chunks
            foreach (var vChunk in versionInfo.AliveChunks) {
                if (!chunks.ContainsKey(vChunk)) {
                    Debug.LogError("Chunk is missing " + vChunk);
                }
            }

            // Remove chunks that don't exist in this version
            var chunksToRemove = new List<Chunk>();
            foreach (var chunk in chunks) {
                if (!versionInfo.AliveChunks.Contains(chunk.Key)) {
                    chunksToRemove.Add(chunk.Value);
                }
            }

            foreach (var chunk in chunksToRemove) {
                RemoveChunk(chunk);
            }

            ComputeBounds();
        }


        private void DeleteOtherVersions(bool lower, long comparandVersion)
        {
#if UNITY_EDITOR
            if (Application.isPlaying && !PersistModificationsInPlayMode)
                return;

            if (!Directory.Exists(InternalPathData))
                return;

            Utils.Profiler.BeginSample("[Dig] DeleteOtherVersions");

            var dir = new DirectoryInfo(InternalPathData);
            foreach (var verFile in dir.EnumerateFiles($"version_*.{VersionFileExtension}")) {
                long versionToRemove;
                if (long.TryParse(verFile.Name.Replace("version_", "").Replace($".{VersionFileExtension}", ""),
                                  out versionToRemove)
                    && (!lower && versionToRemove >= comparandVersion || lower && versionToRemove <= comparandVersion)) {
                    foreach (var voxFile in dir.EnumerateFiles($"*.vox_v{versionToRemove}")) {
                        voxFile.Delete();
                    }

                    foreach (var voxMetadataFile in dir.EnumerateFiles($"*.vom_v{versionToRemove}")) {
                        voxMetadataFile.Delete();
                    }

                    if (File.Exists(GetVersionedTransparencyMapPath(versionToRemove))) {
                        File.Delete(GetVersionedTransparencyMapPath(versionToRemove));
                    }

                    verFile.Delete();
                }
            }

            Utils.Profiler.EndSample();
#endif
        }

        /// <summary>
        /// PreInit setup mandatory fields Terrain, Master and Guid, and it create directories.
        /// This is idempotent and can be called several times.
        /// </summary>
        public void PreInit(bool enablePersistence)
        {
            this.disablePersistence = !enablePersistence;
            Terrain = transform.parent.GetComponent<Terrain>();
            if (!Terrain) {
                Debug.LogError("DiggerSystem component can only be added as a child of a terrain.");
                return;
            }

            master = FindObjectOfType<DiggerMaster>();
            if (!master) {
                Debug.LogError("A DiggerMaster is required in the scene.");
                return;
            }

#if UNITY_EDITOR
            if (string.IsNullOrEmpty(guid)) {
                guid = GUID.Generate().ToString();
            }
#endif

            CreateDirs();
        }

        /// <summary>
        /// Initialize Digger and eventually reloads chunks
        /// </summary>
        /// <param name="forceRefresh"></param>
        public void Init(bool forceRefresh)
        {
            var terrainData = Terrain.terrainData;
            heightmapScale = terrainData.heightmapScale / master.ResolutionMult;
            heightmapScale.y = 1f / master.ResolutionMult;
#if UNITY_2019_3_OR_NEWER
            holeMapScale = new Vector3(terrainData.size.x / terrainData.holesResolution, 1f, terrainData.size.z / terrainData.holesResolution);
#else
            holeMapScale = new Vector3(terrainData.size.x / terrainData.alphamapWidth, 1f, terrainData.size.z / terrainData.alphamapHeight);
#endif
            Reload(forceRefresh, forceRefresh);
        }


        public void Reload(bool rebuildMeshes, bool removeUselessChunks)
        {
            Utils.Profiler.BeginSample("[Dig] Reload");
            CreateDirs();

            // check terrain scale and Digger transform
            Terrain.transform.rotation = Quaternion.identity;
            Terrain.transform.localScale = Vector3.one;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            if (cutter && !TerrainCutter.IsGoodVersion(cutter)) {
                DestroyImmediate(cutter);
                cutter = null;
            }

            if (!cutter) {
                cutter = GetComponent<TerrainCutter>();
                if (!cutter) {
                    cutter = TerrainCutter.Create(Terrain, this);
                }
            }

            cutter.Refresh();
#if !UNITY_EDITOR
            if (File.Exists(GetTransparencyMapRuntimePath())) {
                cutter.LoadFrom(GetTransparencyMapRuntimePath());
            }
#endif
            cutter.Apply(true);
            chunks = new Dictionary<Vector3i, Chunk>(100, new Vector3iComparer());
            heightFeeder = new TerrainHeightFeeder(Terrain.terrainData, master.ResolutionMult);
            chunksToPersist = new HashSet<VoxelChunk>();
            var children = transform.Cast<Transform>().ToList();
            foreach (var child in children) {
                var chunk = child.GetComponent<Chunk>();
                if (chunk) {
                    if (chunk.Digger != this) {
                        Debug.LogError("Chunk is badly defined. Missing/wrong cutter and/or digger reference.");
                    }

                    if (!rebuildMeshes) {
                        chunks.Add(chunk.ChunkPosition, chunk);
                    } else {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }

            LoadChunks(rebuildMeshes);

            if (removeUselessChunks) {
                RemoveUselessChunks();
            }

            ComputeBounds();
            UpdateStaticEditorFlags();

            Utils.Profiler.EndSample();
        }

        public float[] GrabAlphamaps(int2 from, int2 to, out int alphamapCount)
        {
            var size = to - from;
            var tData = Terrain.terrainData;
            var tAlphamaps = tData.GetAlphamaps(from.x, from.y, size.x, size.y);

            var sx = tAlphamaps.GetLength(1);
            var sz = tAlphamaps.GetLength(0);
            alphamapCount = tAlphamaps.GetLength(2);
            var alphamaps = new float[size.x * size.y * alphamapCount];
            for (var x = 0; x < size.x; ++x) {
                for (var z = 0; z < size.y; ++z) {
                    for (var map = 0; map < alphamapCount; ++map) {
                        var a = x < sx && z < sz ? tAlphamaps[z, x, map] : 0f;
                        alphamaps[x * size.y * alphamapCount + z * alphamapCount + map] = a;
                    }
                }
            }

            return alphamaps;
        }

        internal void AddNavMeshSources(List<NavMeshBuildSource> sources)
        {
            foreach (var chunk in chunks) {
                var navSrc = chunk.Value.NavMeshBuildSource;
                if (navSrc.sourceObject) {
                    sources.Add(navSrc);
                }
            }
        }

        private bool GetOrCreateChunk(Vector3i position, out Chunk chunk)
        {
            if (!chunks.TryGetValue(position, out chunk)) {
                chunk = Chunk.CreateChunk(position, this, Terrain, Materials, Layer);
                chunks.Add(position, chunk);
                var b = GetChunkBounds();
                ExpandBounds(chunk.WorldPosition, chunk.WorldPosition + b.size);
                return false;
            }

            return true;
        }

        public bool Modify(BrushType brush, ActionType action, float intensity, Vector3 operationWorldPosition,
                           float radius, float coneHeight, bool upsideDown, int textureIndex, bool cutDetails)
        {
            Utils.Profiler.BeginSample("[Dig] Modify");
            CreateDirs();

            DeleteOtherVersions(false, version);

            var operationTerrainPosition = operationWorldPosition - Terrain.transform.position;
            var p = operationTerrainPosition;
            p.x /= heightmapScale.x;
            p.z /= heightmapScale.z;
            var voxelRadius = (int) ((radius + Math.Max(CutMargin.x, CutMargin.z)) / Math.Min(heightmapScale.x, heightmapScale.z)) + 1;
            var voxMargin = new Vector3i(voxelRadius, (int) (radius + CutMargin.y) + 1, voxelRadius);
            var voxMin = new Vector3i(p) - voxMargin;
            var voxMax = new Vector3i(p) + voxMargin;

            var minMaxHeight = GetMinMaxHeightWithin(voxMin, voxMax);
            voxMin.y = Math.Min(voxMin.y, (int) minMaxHeight.x - 1);
            voxMax.y = Math.Max(voxMax.y, (int) minMaxHeight.y + 1);

            var min = voxMin / SizeOfMesh;
            var max = voxMax / SizeOfMesh;
            if (voxMin.x < 0)
                min.x--;
            if (voxMin.y < 0)
                min.y--;
            if (voxMin.z < 0)
                min.z--;
            if (voxMax.x < 0)
                max.x--;
            if (voxMax.y < 0)
                max.y--;
            if (voxMax.z < 0)
                max.z--;

            if (max.x < 0 || max.z < 0 || min.x > TerrainChunkWidth || min.z > TerrainChunkHeight) {
                Utils.Profiler.EndSample();
                return false;
            }

            if (min.x < 0)
                min.x = 0;
            if (min.z < 0)
                min.z = 0;
            if (max.x > TerrainChunkWidth)
                max.x = TerrainChunkWidth;
            if (max.z > TerrainChunkHeight)
                max.z = TerrainChunkHeight;

            builtChunks.Clear();
            for (var x = min.x; x <= max.x; ++x) {
                for (var z = min.z; z <= max.z; ++z) {
                    var uncutDone = false;
                    for (var y = min.y; y <= max.y; ++y) {
                        var cp = new Vector3i(x, y, z);
                        if (builtChunks.Contains(cp))
                            continue;

                        builtChunks.Add(cp);
                        GetOrCreateChunk(cp, out var chunk);
                        if (!uncutDone && action == ActionType.Reset) {
                            uncutDone = true;
                            chunk.UnCutAllVertically();
                        }

                        chunk.Modify(brush, action, intensity, operationTerrainPosition, radius, coneHeight, upsideDown, textureIndex, cutDetails);
                    }
                }
            }

            if (action == ActionType.Reset) {
                RemoveUselessChunks();
            }

            cutter.Apply(true);
            Utils.Profiler.EndSample();
            return true;
        }

        public bool IsChunkBelongingToMe(Vector3i chunkPosition)
        {
            return chunkPosition.x >= 0 && chunkPosition.x <= TerrainChunkWidth &&
                   chunkPosition.z >= 0 && chunkPosition.z <= TerrainChunkHeight;
        }

        public DiggerSystem GetNeighborAt(Vector3i chunkPosition)
        {
            if (chunkPosition.x < 0) {
                if (chunkPosition.z < 0) {
                    if (Terrain.leftNeighbor)
                        return GetDiggerSystemOf(Terrain.leftNeighbor.bottomNeighbor);
                    if (Terrain.bottomNeighbor)
                        return GetDiggerSystemOf(Terrain.bottomNeighbor.leftNeighbor);
                } else if (chunkPosition.z > TerrainChunkHeight) {
                    if (Terrain.leftNeighbor)
                        return GetDiggerSystemOf(Terrain.leftNeighbor.topNeighbor);
                    if (Terrain.topNeighbor)
                        return GetDiggerSystemOf(Terrain.topNeighbor.leftNeighbor);
                } else {
                    return GetDiggerSystemOf(Terrain.leftNeighbor);
                }
            } else if (chunkPosition.x > TerrainChunkWidth) {
                if (chunkPosition.z < 0) {
                    if (Terrain.rightNeighbor)
                        return GetDiggerSystemOf(Terrain.rightNeighbor.bottomNeighbor);
                    if (Terrain.bottomNeighbor)
                        return GetDiggerSystemOf(Terrain.bottomNeighbor.rightNeighbor);
                } else if (chunkPosition.z > TerrainChunkHeight) {
                    if (Terrain.rightNeighbor)
                        return GetDiggerSystemOf(Terrain.rightNeighbor.topNeighbor);
                    if (Terrain.topNeighbor)
                        return GetDiggerSystemOf(Terrain.topNeighbor.rightNeighbor);
                } else {
                    return GetDiggerSystemOf(Terrain.rightNeighbor);
                }
            } else {
                if (chunkPosition.z < 0) {
                    return GetDiggerSystemOf(Terrain.bottomNeighbor);
                } else if (chunkPosition.z > TerrainChunkHeight) {
                    return GetDiggerSystemOf(Terrain.topNeighbor);
                } else {
                    return this;
                }
            }

            return null;
        }

        public Vector3i ToChunkPosition(Vector3 worldPosition)
        {
            var p = worldPosition - Terrain.transform.position;
            p.x /= heightmapScale.x;
            p.z /= heightmapScale.z;
            return new Vector3i(p) / SizeOfMesh;
        }

        public Vector3 ToWorldPosition(Vector3i chunkPosition)
        {
            Vector3 p = chunkPosition * SizeOfMesh;
            p.x *= heightmapScale.x;
            p.z *= heightmapScale.z;
            return p + Terrain.transform.position;
        }

        private static DiggerSystem GetDiggerSystemOf(Terrain terrain)
        {
            return !terrain ? null : terrain.GetComponentInChildren<DiggerSystem>();
        }

        public void RemoveTreesInSphere(Vector3 center, float radius)
        {
            var tData = Terrain.terrainData;
            for (var i = 0; i < tData.treeInstanceCount; ++i) {
                var treeInstance = tData.GetTreeInstance(i);
                var position = TerrainUtils.UVToWorldPosition(tData, treeInstance.position);
                if (Vector3.Distance(position, center) < radius) {
                    treeInstance.heightScale = 0f;
                    treeInstance.widthScale = 0f;
                    tData.SetTreeInstance(i, treeInstance);
                }
            }
        }

        private float2 GetMinMaxHeightWithin(Vector3i minVox, Vector3i maxVox)
        {
            var minMax = new float2(float.MaxValue, float.MinValue);
            for (var x = minVox.x; x <= maxVox.x; ++x) {
                for (var z = minVox.z; z <= maxVox.z; ++z) {
                    var h = heightFeeder.GetHeight(x, z);
                    if (h < minMax.x)
                        minMax.x = h;
                    if (h > minMax.y)
                        minMax.y = h;
                }
            }

            return minMax;
        }

        private void ComputeBounds()
        {
            var firstIteration = true;
            var b = GetChunkBounds();
            foreach (var chunk in chunks) {
                var min = chunk.Value.WorldPosition;
                var max = min + b.size;
                if (firstIteration) {
                    firstIteration = false;
                    bounds.SetMinMax(min, max);
                    continue;
                }

                ExpandBounds(min, max);
            }
        }

        private void ExpandBounds(Vector3 min, Vector3 max)
        {
            if (bounds.min.x < min.x) {
                min.x = bounds.min.x;
            }

            if (bounds.min.y < min.y) {
                min.y = bounds.min.y;
            }

            if (bounds.min.z < min.z) {
                min.z = bounds.min.z;
            }

            if (bounds.max.x > max.x) {
                max.x = bounds.max.x;
            }

            if (bounds.max.y > max.y) {
                max.y = bounds.max.y;
            }

            if (bounds.max.z > max.z) {
                max.z = bounds.max.z;
            }

            bounds.SetMinMax(min, max);
        }

        public void EnsureChunkExists(Vector3i chunkPosition)
        {
            if (chunkPosition.x < 0 || chunkPosition.z < 0 || chunkPosition.x >= TerrainChunkWidth || chunkPosition.z >= TerrainChunkHeight)
                return;

            Chunk chunk;
            if (!GetOrCreateChunk(chunkPosition, out chunk)) {
                chunk.CreateWithoutOperation();
            }
        }

        public void EnsureChunkWillBePersisted(VoxelChunk voxelChunk)
        {
            if (!disablePersistence) {
                chunksToPersist.Add(voxelChunk);
            }
        }

        private void RemoveUselessChunks()
        {
            var chunksToRemove = new List<Chunk>();
            foreach (var chunk in chunks) {
                if (IsUseless(chunk.Key)) {
                    Debug.Log("[Digger] Cleaning chunk at " + chunk.Key);
                    chunksToRemove.Add(chunk.Value);
                }
            }

            foreach (var chunk in chunksToRemove) {
                RemoveChunk(chunk);
            }

            ComputeBounds();
        }

        private void RemoveChunk(Chunk chunk)
        {
            chunks.Remove(chunk.ChunkPosition);
            var file = GetPathVoxelFile(chunk.ChunkPosition, true);
            if (File.Exists(file)) {
                File.Delete(file);
            }

            file = GetPathVoxelMetadataFile(chunk.ChunkPosition, true);
            if (File.Exists(file)) {
                File.Delete(file);
            }

            if (Application.isEditor) {
                DestroyImmediate(chunk.gameObject);
            } else {
                Destroy(chunk.gameObject);
            }
        }

        private bool IsUseless(Vector3i chunkPosition)
        {
            Chunk chunk;
            if (!chunks.TryGetValue(chunkPosition, out chunk))
                return false; // if it doesn't exist, it doesn't need to be removed
            if (chunk.HasVisualMesh)
                return false; // if it has a visual mesh, it must not be removed
            foreach (var direction in Vector3i.allDirections) {
                Chunk neighbour;
                if (!chunks.TryGetValue(chunkPosition + direction, out neighbour))
                    continue;
                if (neighbour.NeedsNeighbour(-direction))
                    return false; // if one of the chunk's neighbours need it, it must not be removed
            }

            return true;
        }

        private void LoadChunks(bool rebuildMeshes)
        {
#if UNITY_EDITOR
            var path = InternalPathData;
            if (!Directory.Exists(path))
                return;

            if (chunks == null) {
                Debug.LogError("Chunks dico should not be null in loading");
                return;
            }

            foreach (var chunk in chunks) {
                chunk.Value.Load(rebuildMeshes);
            }

            LoadChunksFromDir(rebuildMeshes, new DirectoryInfo(path));
#else
            if (!Directory.Exists(StreamingAssetsPathData) && !Directory.Exists(PersistentRuntimePathData))
                return;

            if (chunks == null) {
                Debug.LogError("Chunks dico should not be null in loading");
                return;
            }

            foreach (var chunk in chunks) {
                chunk.Value.Load(rebuildMeshes);
            }

            LoadChunksFromDir(rebuildMeshes, new DirectoryInfo(PersistentRuntimePathData));
            LoadChunksFromDir(rebuildMeshes, new DirectoryInfo(StreamingAssetsPathData));
#endif
        }

        private void LoadChunksFromDir(bool rebuildMeshes, DirectoryInfo dir)
        {
            if (!dir.Exists)
                return;

            foreach (var file in dir.EnumerateFiles($"*.{VoxelFileExtension}")) {
                var chunkPosition = Chunk.GetPositionFromName(file.Name);
                if (!chunks.ContainsKey(chunkPosition) && chunkPosition.x >= 0 && chunkPosition.z >= 0 && chunkPosition.x <= TerrainChunkWidth && chunkPosition.z <= TerrainChunkHeight) {
                    Chunk chunk;
                    GetOrCreateChunk(chunkPosition, out chunk);
                    chunk.Load(rebuildMeshes);
                }
            }
        }

        public void UpdateStaticEditorFlags()
        {
            if (chunks == null)
                return;

            foreach (var chunk in chunks) {
                chunk.Value.UpdateStaticEditorFlags();
            }
        }

        public void Clear()
        {
#if UNITY_EDITOR
            Utils.Profiler.BeginSample("[Dig] Clear");
            cutter.Clear();
            cutter = null;

            AssetDatabase.StartAssetEditing();
            if (Directory.Exists(BasePathData)) {
                Directory.Delete(BasePathData, true);
                AssetDatabase.DeleteAsset(BasePathData);
            }

            if (chunks != null) {
                foreach (var chunk in chunks) {
                    if (Application.isEditor) {
                        DestroyImmediate(chunk.Value.gameObject);
                    } else {
                        Destroy(chunk.Value.gameObject);
                    }
                }

                chunks = null;
            }

            chunksToPersist = null;
            Materials = null;

            version = 1;
            PersistVersion();
            AssetDatabase.StopAssetEditing();

#if UNITY_2019_3_OR_NEWER
            if (GraphicsSettings.currentRenderPipeline != null) {
                Terrain.materialTemplate = GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial;
            } else {
                Terrain.materialTemplate = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Terrain-Standard.mat");
            }
#elif UNITY_2019_1_OR_NEWER
            if (GraphicsSettings.renderPipelineAsset != null) {
                Terrain.materialTemplate = GraphicsSettings.renderPipelineAsset.defaultTerrainMaterial;
            } else {
                Terrain.materialTemplate = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Terrain-Standard.mat");
            }
#else
            if (GraphicsSettings.renderPipelineAsset != null) {
                Terrain.materialTemplate = GraphicsSettings.renderPipelineAsset.GetDefaultTerrainMaterial();
            } else {
                Terrain.materialType = Terrain.MaterialType.BuiltInStandard;
            }
#endif

            Undo.ClearAll();
            Utils.Profiler.EndSample();
#endif
        }

        public void CreateDirs()
        {
#if UNITY_EDITOR
            master.CreateDirs();

            if (!Directory.Exists(BasePathData)) {
                AssetDatabase.CreateFolder(master.SceneDataPath, BaseFolder);
            }

            if (!Directory.Exists(InternalPathData)) {
                Directory.CreateDirectory(InternalPathData);
            }
#endif
        }
        
    }
}