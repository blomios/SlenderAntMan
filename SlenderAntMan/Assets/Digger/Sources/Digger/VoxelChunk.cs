using System;
using System.IO;
using System.Text;
using Digger.TerrainCutters;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR

#endif

namespace Digger
{
    public class VoxelChunk : MonoBehaviour
    {
        [SerializeField] private DiggerSystem digger;
        [SerializeField] private int sizeVox;
        [SerializeField] private int sizeOfMesh;
        [SerializeField] private Vector3i chunkPosition;
        [SerializeField] private Vector3i voxelPosition;
        [SerializeField] private Vector3 worldPosition;
        [SerializeField] private ChunkTriggerBounds bounds;

        [NonSerialized] private Voxel[] voxelArray;
        [NonSerialized] private float[] heightArray;
        [NonSerialized] private float[] verticalNormalArray;
        [NonSerialized] private float[] alphamapArray;
        [NonSerialized] private int3 alphamapArraySize;
        [NonSerialized] private int2 alphamapArrayOrigin;


        public ChunkTriggerBounds TriggerBounds => bounds;

        internal static VoxelChunk Create(DiggerSystem digger, Chunk chunk)
        {
            var go = new GameObject("VoxelChunk")
            {
                hideFlags = HideFlags.DontSaveInBuild
            };
            go.transform.parent = chunk.transform;
            go.transform.position = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var voxelChunk = go.AddComponent<VoxelChunk>();
            voxelChunk.digger = digger;
            voxelChunk.sizeVox = digger.SizeVox;
            voxelChunk.sizeOfMesh = digger.SizeOfMesh;
            voxelChunk.chunkPosition = chunk.ChunkPosition;
            voxelChunk.voxelPosition = chunk.VoxelPosition;
            voxelChunk.worldPosition = chunk.WorldPosition;
            voxelChunk.Load();

            return voxelChunk;
        }

        private static void FeedHeights(DiggerSystem digger, Vector3i chunkVoxelPosition, ref float[] heightArray, ref float[] verticalNormalArray)
        {
            var sizeVox = digger.SizeVox;
            if (heightArray == null)
                heightArray = new float[sizeVox * sizeVox];

            if (verticalNormalArray == null)
                verticalNormalArray = new float[sizeVox * sizeVox];

            var heightFeeder = digger.HeightFeeder;
            for (var xi = 0; xi < sizeVox; ++xi) {
                for (var zi = 0; zi < sizeVox; ++zi) {
                    heightArray[xi * sizeVox + zi] = heightFeeder.GetHeight(chunkVoxelPosition.x + (xi - 1), chunkVoxelPosition.z + (zi - 1));
                    verticalNormalArray[xi * sizeVox + zi] = heightFeeder.GetVerticalNormal(chunkVoxelPosition.x + (xi - 1), chunkVoxelPosition.z + (zi - 1));
                }
            }
        }

        private static void GenerateVoxels(DiggerSystem digger, float[] heightArray, int chunkAltitude, ref Voxel[] voxelArray)
        {
            Utils.Profiler.BeginSample("[Dig] VoxelChunk.GenerateVoxels");
            var sizeVox = digger.SizeVox;
            if (voxelArray == null)
                voxelArray = new Voxel[sizeVox * sizeVox * sizeVox];

            var heights = new NativeArray<float>(heightArray, Allocator.TempJob);
            var voxels = new NativeArray<Voxel>(sizeVox * sizeVox * sizeVox, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            // Set up the job data
            var jobData = new VoxelGenerationJob
            {
                ChunkAltitude = chunkAltitude,
                Heights = heights,
                Voxels = voxels,
                SizeVox = sizeVox,
                SizeVox2 = sizeVox * sizeVox
            };

            // Schedule the job
            var handle = jobData.Schedule(voxels.Length, 64);

            // Wait for the job to complete
            handle.Complete();

            voxels.CopyTo(voxelArray);
            heights.Dispose();
            voxels.Dispose();

            Utils.Profiler.EndSample();
        }

        public void DoOperation(BrushType brush, ActionType action, float intensity, Vector3 position,
                                float radius, float coneHeight, bool upsideDown, int textureIndex, bool cutDetails)
        {
            if (action == ActionType.Smooth) {
                DoKernelOperation(action, intensity, position, radius, cutDetails);
                return;
            }

            Utils.Profiler.BeginSample("[Dig] VoxelChunk.DoOperation");
            var heights = new NativeArray<float>(heightArray, Allocator.TempJob);
            var normals = new NativeArray<float>(verticalNormalArray, Allocator.TempJob);
            var voxels = new NativeArray<Voxel>(voxelArray, Allocator.TempJob);
            var toCut = new NativeQueue<CutEntry>(Allocator.TempJob);
#if !UNITY_2019_3_OR_NEWER
            var toTriggerBounds = new NativeQueue<float3>(Allocator.TempJob);
#endif

            var tData = digger.Terrain.terrainData;
            var hScale = digger.HeightmapScale;
            var holeScale = digger.HoleMapScale;
            var cutSizeX = Math.Max(1, (int) (hScale.x / holeScale.x));
            var cutSizeZ = Math.Max(1, (int) (hScale.z / holeScale.z));

            var jobData = new VoxelModificationJob
            {
                SizeVox = sizeVox,
                SizeVox2 = sizeVox * sizeVox,
                Brush = brush,
                Action = action,
                HeightmapScale = digger.HeightmapScale,
                ChunkAltitude = voxelPosition.y,
                Voxels = voxels,
                Heights = heights,
                VerticalNormals = normals,
                Intensity = intensity,
                Center = position,
                Radius = radius,
                ConeHeight = coneHeight,
                UpsideDown = upsideDown,
                RadiusWithMargin = radius + Math.Max(Math.Max(digger.CutMargin.x, digger.CutMargin.y), digger.CutMargin.z),
                TextureIndex = (sbyte) textureIndex,
                CutSize = new int2(cutSizeX, cutSizeZ),
                WorldPosition = worldPosition,
#if UNITY_2019_3_OR_NEWER
                TerrainRelativePositionToHolePosition = new float2(1f / tData.size.x * tData.holesResolution, 1f / tData.size.z * tData.holesResolution),
                ToCut = toCut.AsParallelWriter()
#elif UNITY_2019_1_OR_NEWER
                TerrainRelativePositionToHolePosition = new float2(1f / tData.size.x * tData.alphamapWidth, 1f / tData.size.z * tData.alphamapHeight),
                ToTriggerBounds = toTriggerBounds.AsParallelWriter(),
                ToCut = toCut.AsParallelWriter()
#else
                TerrainRelativePositionToHolePosition = new float2(1f / tData.size.x * tData.alphamapWidth, 1f / tData.size.z * tData.alphamapHeight),
                ToTriggerBounds = toTriggerBounds.ToConcurrent(),
                ToCut = toCut.ToConcurrent()
#endif
            };
            jobData.PostConstruct();

            // Schedule the job
            var handle = jobData.Schedule(voxels.Length, 64);

            // Wait for the job to complete
            handle.Complete();

            voxels.CopyTo(voxelArray);
            voxels.Dispose();
            normals.Dispose();
            heights.Dispose();

            //CutAndComputeTriggerBounds();
            while (toCut.Count > 0) {
                var cutEntry = toCut.Dequeue();
                digger.Cutter.Cut(cutEntry, cutDetails);
            }

            toCut.Dispose();

#if !UNITY_2019_3_OR_NEWER
            var triggerBounds = new ChunkTriggerBounds(digger.HeightmapScale, digger.SizeOfMesh);
            while (toTriggerBounds.Count > 0) {
                triggerBounds.ExtendIfNeeded(toTriggerBounds.Dequeue());
            }

            toTriggerBounds.Dispose();
            bounds = triggerBounds;
#endif

#if UNITY_EDITOR
            RecordUndoIfNeeded();
#endif
            digger.EnsureChunkWillBePersisted(this);

            Utils.Profiler.EndSample();
        }

        public void DoKernelOperation(ActionType action, float intensity, Vector3 position, float radius, bool cutDetails)
        {
#if UNITY_EDITOR
            Utils.Profiler.BeginSample("[Dig] VoxelChunk.DoKernelOperation");
            var voxels = new NativeArray<Voxel>(voxelArray, Allocator.TempJob);
            var voxelsOut = new NativeArray<Voxel>(voxelArray, Allocator.TempJob);

            var jobData = new VoxelKernelModificationJob
            {
                SizeVox = sizeVox,
                SizeVox2 = sizeVox * sizeVox,
                SizeOfMesh = sizeOfMesh,
                LowInd = sizeVox - 4,
                Action = action,
                HeightmapScale = digger.HeightmapScale,
                Voxels = voxels,
                VoxelsOut = voxelsOut,
                Intensity = intensity,
                Center = position,
                Radius = radius,

                NeighborVoxelsLBB = LoadVoxels(digger, chunkPosition + new Vector3i(-1, -1, -1)),
                NeighborVoxelsLBF = LoadVoxels(digger, chunkPosition + new Vector3i(-1, -1, +1)),
                NeighborVoxelsLB_ = LoadVoxels(digger, chunkPosition + new Vector3i(-1, -1, +0)),
                NeighborVoxels_BB = LoadVoxels(digger, chunkPosition + new Vector3i(+0, -1, -1)),
                NeighborVoxels_BF = LoadVoxels(digger, chunkPosition + new Vector3i(+0, -1, +1)),
                NeighborVoxels_B_ = LoadVoxels(digger, chunkPosition + new Vector3i(+0, -1, +0)),
                NeighborVoxelsRBB = LoadVoxels(digger, chunkPosition + new Vector3i(+1, -1, -1)),
                NeighborVoxelsRBF = LoadVoxels(digger, chunkPosition + new Vector3i(+1, -1, +1)),
                NeighborVoxelsRB_ = LoadVoxels(digger, chunkPosition + new Vector3i(+1, -1, +0)),
                NeighborVoxelsL_B = LoadVoxels(digger, chunkPosition + new Vector3i(-1, +0, -1)),
                NeighborVoxelsL_F = LoadVoxels(digger, chunkPosition + new Vector3i(-1, +0, +1)),
                NeighborVoxelsL__ = LoadVoxels(digger, chunkPosition + new Vector3i(-1, +0, +0)),
                NeighborVoxels__B = LoadVoxels(digger, chunkPosition + new Vector3i(+0, +0, -1)),

                NeighborVoxels__F = LoadVoxels(digger, chunkPosition + new Vector3i(+0, +0, +1)),
                NeighborVoxelsR_B = LoadVoxels(digger, chunkPosition + new Vector3i(+1, +0, -1)),
                NeighborVoxelsR_F = LoadVoxels(digger, chunkPosition + new Vector3i(+1, +0, +1)),
                NeighborVoxelsR__ = LoadVoxels(digger, chunkPosition + new Vector3i(+1, +0, +0)),
                NeighborVoxelsLUB = LoadVoxels(digger, chunkPosition + new Vector3i(-1, +1, -1)),
                NeighborVoxelsLUF = LoadVoxels(digger, chunkPosition + new Vector3i(-1, +1, +1)),
                NeighborVoxelsLU_ = LoadVoxels(digger, chunkPosition + new Vector3i(-1, +1, +0)),
                NeighborVoxels_UB = LoadVoxels(digger, chunkPosition + new Vector3i(+0, +1, -1)),
                NeighborVoxels_UF = LoadVoxels(digger, chunkPosition + new Vector3i(+0, +1, +1)),
                NeighborVoxels_U_ = LoadVoxels(digger, chunkPosition + new Vector3i(+0, +1, +0)),
                NeighborVoxelsRUB = LoadVoxels(digger, chunkPosition + new Vector3i(+1, +1, -1)),
                NeighborVoxelsRUF = LoadVoxels(digger, chunkPosition + new Vector3i(+1, +1, +1)),
                NeighborVoxelsRU_ = LoadVoxels(digger, chunkPosition + new Vector3i(+1, +1, +0)),
            };

            // Schedule the job
            var handle = jobData.Schedule(voxels.Length, 64);

            // Wait for the job to complete
            handle.Complete();
            jobData.DisposeNeighbors();
            voxels.Dispose();

            voxelsOut.CopyTo(voxelArray);
            voxelsOut.Dispose();

            RecordUndoIfNeeded();
            digger.EnsureChunkWillBePersisted(this);

            Utils.Profiler.EndSample();
#endif
        }

        public void UnCutAllVertically()
        {
            var hScale = digger.HeightmapScale;
            var tData = digger.Terrain.terrainData;
            for (var xi = 0; xi < sizeVox; ++xi) {
                for (var zi = 0; zi < sizeVox; ++zi) {
                    var pos = new Vector3((xi - 1) * hScale.x, 0, (zi - 1) * hScale.z);
                    var wpos = pos + worldPosition;
                    var p = TerrainUtils.TerrainRelativePositionToHolePosition(tData, wpos);
                    digger.Cutter.UnCut(p.x, p.z);
                }
            }
        }

        public Mesh BuildVisualMesh(int lod)
        {
            return BuildMesh(lod, 0f, true, false);
        }

#if !UNITY_2019_3_OR_NEWER
        public Mesh BuildCollisionMesh(int lod)
        {
            return BuildMesh(lod, 0f, false, true);
        }
#endif

        private Mesh BuildMesh(int lod, float isovalue, bool alteredOnly, bool colliderMesh)
        {
            Utils.Profiler.BeginSample("[Dig] VoxelChunk.BuildMesh");
            Utils.Profiler.BeginSample("[Dig] VoxelChunk.BuildMesh.AllocateNatives");
            Utils.Profiler.BeginSample("[Dig] AllocateTables");
            var edgeTable = MarchingCubesTables.NewEdgeTable();
            var triTable = MarchingCubesTables.NewTriTable();
            var corners = MarchingCubesTables.NewCorners();
            Utils.Profiler.EndSample();
            Utils.Profiler.BeginSample("[Dig] AllocVoxels");
            var voxels = new NativeArray<Voxel>(voxelArray, Allocator.TempJob);
            Utils.Profiler.EndSample();
            Utils.Profiler.BeginSample("[Dig] AllocAlphamaps");
            var alphamaps = new NativeArray<float>(alphamapArray, Allocator.TempJob);
            Utils.Profiler.EndSample();
            var o = MarchingCubesJob.Out.New(!colliderMesh);
            var vertexCounter = new NativeCounter(Allocator.TempJob);
            var triangleCounter = new NativeCounter(Allocator.TempJob, 3);
            Utils.Profiler.EndSample();

            var tData = digger.Terrain.terrainData;
            var alphamapsSize = new int2(tData.alphamapWidth, tData.alphamapHeight);
            var uvScale = new Vector2(1f / tData.size.x,
                                      1f / tData.size.z);
            var scale = new float3(digger.HeightmapScale) {y = 1f};

            // for retro-compatibility
            if (lod <= 0) lod = 1;

            // Set up the job data
            var jobData = new MarchingCubesJob(edgeTable,
                                               triTable,
                                               corners,
                                               vertexCounter.ToConcurrent(),
                                               triangleCounter.ToConcurrent(),
                                               voxels,
                                               alphamaps,

                                               // out params
                                               o,

                                               // misc
                                               scale,
                                               uvScale,
                                               worldPosition,
                                               lod,
                                               alphamapArrayOrigin,
                                               alphamapsSize,
                                               alphamapArraySize);

            jobData.SizeVox = sizeVox;
            jobData.SizeVox2 = sizeVox * sizeVox;
            jobData.Isovalue = isovalue;
            jobData.AlteredOnly = (byte) (alteredOnly ? 1 : 0);
            jobData.FullOutput = (byte) (colliderMesh ? 0 : 1);

            // Schedule the job
            var handle = jobData.Schedule(voxels.Length, 4);

            // Wait for the job to complete
            handle.Complete();

            var vertexCount = vertexCounter.Count;
            var triangleCount = triangleCounter.Count;

            edgeTable.Dispose();
            triTable.Dispose();
            corners.Dispose();
            voxels.Dispose();
            alphamaps.Dispose();
            vertexCounter.Dispose();
            triangleCounter.Dispose();

            Mesh mesh;
            if (colliderMesh) {
                mesh = ToMeshSimple(o, vertexCount, triangleCount);
            } else {
                mesh = ToMesh(o, vertexCount, triangleCount);
            }

            o.Dispose();

            Utils.Profiler.EndSample();
            return mesh;
        }

        private static Mesh ToMeshSimple(MarchingCubesJob.Out o, int vertexCount, int triangleCount)
        {
            if (vertexCount < 3 || triangleCount < 3)
                return null;

            Utils.Profiler.BeginSample("[Dig] VoxelChunk.ToMeshSimple");
            var mesh = new Mesh();
            NativeArray<Vector3>.Copy(o.outVertices, DirectMeshAccess.VertexArray, vertexCount);
            NativeArray<int>.Copy(o.outTriangles, DirectMeshAccess.TriangleArray, triangleCount);
            NativeArray<Vector3>.Copy(o.outNormals, DirectMeshAccess.NormalArray, vertexCount);
            DirectMeshAccess.DirectSet(mesh, vertexCount, triangleCount);
            mesh.RecalculateBounds();
            Utils.Profiler.EndSample();
            return mesh;
        }

        private Mesh ToMesh(MarchingCubesJob.Out o, int vertexCount, int triangleCount)
        {
            if (vertexCount < 3 || triangleCount < 1)
                return null;

            Utils.Profiler.BeginSample("[Dig] VoxelChunk.ToMesh");
            var tData = digger.Terrain.terrainData;

            var mesh = new Mesh();
            AddVertexData(mesh, o, vertexCount, triangleCount, tData);

            mesh.bounds = GetBounds();
            if (digger.MaterialType == TerrainMaterialType.CTS) {
                Utils.Profiler.BeginSample("[Dig] VoxelChunk.ToMesh.RecalculateTangents");
                mesh.RecalculateTangents();
                Utils.Profiler.EndSample();
            }

            Utils.Profiler.EndSample();
            return mesh;
        }

        private Bounds GetBounds()
        {
            return digger.GetChunkBounds();
        }

        private void AddVertexData(Mesh mesh, MarchingCubesJob.Out o, int vertexCount, int triangleCount, TerrainData tData)
        {
            NativeArray<Vector3>.Copy(o.outVertices, DirectMeshAccess.VertexArray, vertexCount);
            NativeArray<int>.Copy(o.outTriangles, DirectMeshAccess.TriangleArray, triangleCount);
            NativeArray<Vector3>.Copy(o.outNormals, DirectMeshAccess.NormalArray, vertexCount);
            NativeArray<Color>.Copy(o.outColors, DirectMeshAccess.ColorArray, vertexCount);
            NativeArray<Vector2>.Copy(o.outUV1s, DirectMeshAccess.Uv0Array, vertexCount);
            NativeArray<int>.Copy(o.outInfos, DirectMeshAccess.InfoArray, vertexCount);

            var uvs = DirectMeshAccess.Uv0Array;
            var normals = DirectMeshAccess.NormalArray;

            var infos = DirectMeshAccess.InfoArray;
            for (var i = 0; i < vertexCount; ++i) {
                var texInfo = infos[i];
                if (texInfo == 0 || texInfo == 1 || texInfo == -1) {
                    // near the terrain surface -> set same normal
                    var uv = uvs[i];
                    normals[i] = tData.GetInterpolatedNormal(uv.x, uv.y);
                }
            }

            DirectMeshAccess.DirectSetTotal(mesh, vertexCount, triangleCount);
            mesh.SetUVs(1, ListPool.ToVector4List(o.outUV2s, vertexCount));
            mesh.SetUVs(2, ListPool.ToVector4List(o.outUV3s, vertexCount));
            mesh.SetUVs(3, ListPool.ToVector4List(o.outUV4s, vertexCount));
        }

        private void RecordUndoIfNeeded()
        {
#if UNITY_EDITOR
            if (voxelArray == null || voxelArray.Length == 0) {
                Debug.LogError("Voxel array should not be null when recording undo");
                return;
            }

            Utils.Profiler.BeginSample("[Dig] VoxelChunk.RecordUndoIfNeeded");
            var path = digger.GetEditorOnlyPathVoxelFile(chunkPosition);

            var savePath = digger.GetPathVersionedVoxelFile(chunkPosition, digger.PreviousVersion);
            if (File.Exists(path) && !File.Exists(savePath)) {
                File.Copy(path, savePath);
            }

            var metadataPath = digger.GetEditorOnlyPathVoxelMetadataFile(chunkPosition);

            var saveMetadataPath = digger.GetPathVersionedVoxelMetadataFile(chunkPosition, digger.PreviousVersion);
            if (File.Exists(metadataPath) && !File.Exists(saveMetadataPath)) {
                File.Copy(metadataPath, saveMetadataPath);
            }

            Utils.Profiler.EndSample();
#endif
        }

        public void Persist()
        {
            if (voxelArray == null || voxelArray.Length == 0) {
                Debug.LogError("Voxel array should not be null in saving");
                return;
            }

            Utils.Profiler.BeginSample("[Dig] VoxelChunk.Persist");
            var path = digger.GetPathVoxelFile(chunkPosition, true);

            var voxels = new NativeArray<Voxel>(voxelArray, Allocator.Temp);
            var bytes = new NativeSlice<Voxel>(voxels).SliceConvert<byte>();
            File.WriteAllBytes(path, bytes.ToArray());
            voxels.Dispose();

            var metadataPath = digger.GetPathVoxelMetadataFile(chunkPosition, true);
            using (var stream = new FileStream(metadataPath, FileMode.Create, FileAccess.Write, FileShare.Write, 4096, FileOptions.Asynchronous)) {
                using (var writer = new BinaryWriter(stream, Encoding.Default)) {
                    writer.Write(bounds.IsVirgin);
                    writer.Write(bounds.Min.x);
                    writer.Write(bounds.Min.y);
                    writer.Write(bounds.Min.z);
                    writer.Write(bounds.Max.x);
                    writer.Write(bounds.Max.y);
                    writer.Write(bounds.Max.z);
                }
            }

#if UNITY_EDITOR
            var savePath = digger.GetPathVersionedVoxelFile(chunkPosition, digger.Version);
            File.Copy(path, savePath, true);
            var saveMetadataPath = digger.GetPathVersionedVoxelMetadataFile(chunkPosition, digger.Version);
            File.Copy(metadataPath, saveMetadataPath, true);
#endif
            Utils.Profiler.EndSample();
        }

        public void Load()
        {
            // Feed heights again in case they have been modified
            FeedHeights(digger, voxelPosition, ref heightArray, ref verticalNormalArray);
            FeedAlphamaps();

            var path = digger.GetPathVoxelFile(chunkPosition, false);
            if (!File.Exists(path)) {
                if (voxelArray == null) {
                    // If there is no persisted voxels but voxel array is null, then we fallback and (re)generate them.
                    GenerateVoxels(digger, heightArray, voxelPosition.y, ref voxelArray);
                    digger.EnsureChunkWillBePersisted(this);
                }

                return;
            }

            ReadVoxelFile(sizeVox, path, ref voxelArray);

            var hScale = digger.HeightmapScale;
            var metadataPath = digger.GetPathVoxelMetadataFile(chunkPosition, false);
            using (Stream stream = new FileStream(metadataPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                using (var reader = new BinaryReader(stream, Encoding.Default)) {
                    bounds = new ChunkTriggerBounds(
                        reader.ReadBoolean(),
                        new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                        new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                        hScale, sizeOfMesh);
                }
            }

            Utils.Profiler.EndSample();
        }

        private void FeedAlphamaps()
        {
            var tData = digger.Terrain.terrainData;
            var alphamapsSize = new int2(tData.alphamapWidth, tData.alphamapHeight);
            var uvScale = new Vector2(1f / tData.size.x,
                                      1f / tData.size.z);
            var uv00 = new Vector2((worldPosition.x + 0) * uvScale.x, (worldPosition.z + 0) * uvScale.y);
            var uv11 = new Vector2((worldPosition.x + sizeOfMesh * digger.HeightmapScale.x) * uvScale.x, (worldPosition.z + sizeOfMesh * digger.HeightmapScale.z) * uvScale.y);
            var a00 = new Vector2(uv00.x * (alphamapsSize.x - 0), uv00.y * (alphamapsSize.y - 0));
            var a11 = new Vector2(uv11.x * (alphamapsSize.x - 0), uv11.y * (alphamapsSize.y - 0));

            var a00I = new int2(Math.Min(Math.Max(Convert.ToInt32(Math.Floor(a00.x)) - 1, 0), alphamapsSize.x),
                                Math.Min(Math.Max(Convert.ToInt32(Math.Floor(a00.y)) - 1, 0), alphamapsSize.y));
            var a11I = new int2(Math.Min(Math.Max(Convert.ToInt32(Math.Ceiling(a11.x)) + 3, 0), alphamapsSize.x),
                                Math.Min(Math.Max(Convert.ToInt32(Math.Ceiling(a11.y)) + 3, 0), alphamapsSize.y));

            alphamapArray = digger.GrabAlphamaps(a00I, a11I, out var alphamapCount);
            alphamapArraySize.xy = a11I - a00I;
            alphamapArraySize.z = alphamapCount;
            alphamapArrayOrigin = a00I;
        }

        private static void ReadVoxelFile(int sizeVox, string path, ref Voxel[] voxelArray)
        {
            if (voxelArray == null)
                voxelArray = new Voxel[sizeVox * sizeVox * sizeVox];

            var voxelBytes = new NativeArray<byte>(File.ReadAllBytes(path), Allocator.Temp);
            var bytes = new NativeSlice<byte>(voxelBytes);
            var voxelSlice = bytes.SliceConvert<Voxel>();
            voxelSlice.CopyTo(voxelArray);
            voxelBytes.Dispose();
        }

#if UNITY_EDITOR
        private static NativeArray<Voxel> LoadVoxels(DiggerSystem digger, Vector3i chunkPosition)
        {
            Utils.Profiler.BeginSample("[Dig] VoxelChunk.LoadVoxels");

            if (!digger.IsChunkBelongingToMe(chunkPosition)) {
                var neighbor = digger.GetNeighborAt(chunkPosition);
                if (neighbor) {
                    var neighborChunkPosition = neighbor.ToChunkPosition(digger.ToWorldPosition(chunkPosition));
                    if (!neighbor.IsChunkBelongingToMe(neighborChunkPosition)) {
                        Debug.LogError($"neighborChunkPosition {neighborChunkPosition} should always belong to neighbor");
                        return new NativeArray<Voxel>(tmpLoadingVoxels, Allocator.TempJob);
                    }

                    return LoadVoxels(neighbor, neighborChunkPosition);
                }
            }

            var path = digger.GetPathVoxelFile(chunkPosition, false);
            if (!File.Exists(path)) {
                var voxelPosition = Chunk.GetVoxelPosition(digger, chunkPosition);
                FeedHeights(digger, voxelPosition, ref tmpLoadingHeightArray, ref tmpLoadingVerticalNormalArray);
                GenerateVoxels(digger, tmpLoadingHeightArray, chunkPosition.y, ref tmpLoadingVoxels);
            } else {
                ReadVoxelFile(digger.SizeVox, path, ref tmpLoadingVoxels);
            }

            Utils.Profiler.EndSample();
            return new NativeArray<Voxel>(tmpLoadingVoxels, Allocator.TempJob);
        }

        private static Voxel[] tmpLoadingVoxels;
        private static float[] tmpLoadingHeightArray;
        private static float[] tmpLoadingVerticalNormalArray;
#endif
    }
}