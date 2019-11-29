using System;
using Digger.TerrainCutters;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Digger
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
    public struct VoxelModificationJob : IJobParallelFor
    {
        public int SizeVox;
        public int SizeVox2;
        public BrushType Brush;
        public ActionType Action;
        public float3 HeightmapScale;
        public float3 Center;
        public float ConeHeight;
        public bool UpsideDown;
        public float Radius;
        public float RadiusWithMargin;
        public float Intensity;
        public int ChunkAltitude;
        public sbyte TextureIndex;
        public int2 CutSize;
        public float2 TerrainRelativePositionToHolePosition;
        public float3 WorldPosition;

        [ReadOnly] public NativeArray<float> Heights;
        [ReadOnly] public NativeArray<float> VerticalNormals;

        public NativeArray<Voxel> Voxels;
#if UNITY_2019_3_OR_NEWER
        [WriteOnly] public NativeQueue<CutEntry>.ParallelWriter ToCut;
#elif UNITY_2019_1_OR_NEWER
        [WriteOnly] public NativeQueue<CutEntry>.ParallelWriter ToCut;
        [WriteOnly] public NativeQueue<float3>.ParallelWriter ToTriggerBounds;
#else
        [WriteOnly] public NativeQueue<CutEntry>.Concurrent ToCut;
        [WriteOnly] public NativeQueue<float3>.Concurrent ToTriggerBounds;
#endif

        private double coneAngle;
        private float upsideDownSign;

        public void PostConstruct()
        {
            if (ConeHeight > 0.1f)
                coneAngle = Math.Atan((double) Radius / ConeHeight);
            upsideDownSign = UpsideDown ? -1f : 1f;
        }

        public void Execute(int index)
        {
            var xi = index / SizeVox2;
            var yi = (index - xi * SizeVox2) / SizeVox;
            var zi = index - xi * SizeVox2 - yi * SizeVox;

            var p = new float3((xi - 1) * HeightmapScale.x, (yi - 1), (zi - 1) * HeightmapScale.z);
            var terrainHeight = Heights[xi * SizeVox + zi];
            var terrainHeightValue = p.y + ChunkAltitude - terrainHeight;

            float2 distances;
            switch (Brush) {
                case BrushType.Sphere:
                    distances = ComputeSphereDistances(p);
                    break;
                case BrushType.HalfSphere:
                    distances = ComputeHalfSphereDistances(p);
                    break;
                case BrushType.RoundedCube:
                    distances = ComputeCubeDistances(p);
                    break;
                case BrushType.Stalagmite:
                    distances = ComputeConeDistances(p);
                    break;
                default:
                    return; // never happens
            }

            Voxel voxel;
            switch (Action) {
                case ActionType.Add:
                case ActionType.Dig:
                    voxel = ApplyDigAdd(index, Action == ActionType.Dig, distances.x, distances.y, Math.Max(1, Math.Abs(terrainHeightValue)));
                    break;
                case ActionType.Paint:
                    voxel = ApplyPaint(index, distances.x);
                    break;
                case ActionType.Reset:
                    voxel = ApplyResetBrush(index, xi, zi, p);
                    break;
                default:
                    return; // never happens
            }


            if (voxel.Altered != 0) {
                var absAltered = Math.Abs(voxel.Altered);
                if (Math.Abs(terrainHeightValue) <= 1f && Math.Abs(voxel.Value - terrainHeightValue) < 0.08f) {
                    absAltered = 1;
                } else if (absAltered >= Voxel.TextureOffset) {
                    var terrainNrm = VerticalNormals[xi * SizeVox + zi];
                    if (Math.Abs(terrainHeightValue) <= 1f / Math.Max(terrainNrm, 0.001f) + 0.5f) {
                        absAltered = (sbyte) (absAltered - Voxel.TextureOffset + Voxel.TextureNearSurfaceOffset);
                    }
                }

                if (voxel.Value > terrainHeightValue) {
                    voxel.Altered = (sbyte) -absAltered;
                } else {
                    voxel.Altered = absAltered;
                }
            }

            if (voxel.IsAlteredNearBelowSurface || voxel.IsAlteredNearAboveSurface) {
                var pos = new float3((xi - 1) * HeightmapScale.x, (yi - 1), (zi - 1) * HeightmapScale.z);
#if !UNITY_2019_3_OR_NEWER
                ToTriggerBounds.Enqueue(pos);
#endif
                var wpos = pos + WorldPosition;
                var pCut = new int3((int) (wpos.x * TerrainRelativePositionToHolePosition.x), (int) wpos.y, (int) (wpos.z * TerrainRelativePositionToHolePosition.y));
                for (var x = -CutSize.x; x < CutSize.x; ++x) {
                    for (var z = -CutSize.y; z < CutSize.y; ++z) {
                        ToCut.Enqueue(new CutEntry(
                                          pCut.x + x,
                                          pCut.z + z,
                                          voxel.IsAlteredNearAboveSurface
                                      ));
                    }
                }
            }


            Voxels[index] = voxel;
        }

        private float2 ComputeSphereDistances(float3 p)
        {
            var vec = p - Center;
            var distance = (float) Math.Sqrt(vec.x * vec.x + vec.y * vec.y + vec.z * vec.z);
            var flatDistance = (float) Math.Sqrt(vec.x * vec.x + vec.z * vec.z);
            return new float2(Radius - distance, RadiusWithMargin - flatDistance);
        }

        private float2 ComputeHalfSphereDistances(float3 p)
        {
            var vec = p - Center;
            var distance = (float) Math.Sqrt(vec.x * vec.x + vec.y * vec.y + vec.z * vec.z);
            var flatDistance = (float) Math.Sqrt(vec.x * vec.x + vec.z * vec.z);
            return new float2(Math.Min(Radius - distance, vec.y), RadiusWithMargin - flatDistance);
        }

        private float2 ComputeCubeDistances(float3 p)
        {
            var vec = p - Center;
            var flatDistance = Math.Min(RadiusWithMargin - Math.Abs(vec.x), RadiusWithMargin - Math.Abs(vec.z));
            return new float2(Math.Min(Math.Min(Radius - Math.Abs(vec.x), Radius - Math.Abs(vec.y)), Radius - Math.Abs(vec.z)), flatDistance);
        }

        private float2 ComputeConeDistances(float3 p)
        {
            var coneVertex = Center + new float3(0, upsideDownSign * ConeHeight * 0.95f, 0);
            var vec = p - coneVertex;
            var distance = (float) Math.Sqrt(vec.x * vec.x + vec.y * vec.y + vec.z * vec.z);
            var flatDistance = (float) Math.Sqrt(vec.x * vec.x + vec.z * vec.z);
            var pointAngle = Math.Asin((double) flatDistance / distance);
            var d = -distance * Math.Sin(Math.Abs(pointAngle - coneAngle)) * Math.Sign(pointAngle - coneAngle);
            return new float2(Math.Min(Math.Min((float) d, ConeHeight + upsideDownSign * vec.y), -upsideDownSign * vec.y), RadiusWithMargin - flatDistance);
        }

        private Voxel ApplyDigAdd(int index, bool dig, float distance, float flatDistance, float absTerrainHeightValue)
        {
            var voxel = Voxels[index];
            var currentValF = voxel.Value;

            if (dig) {
                voxel = new Voxel(Math.Max(currentValF, currentValF + Intensity * absTerrainHeightValue * distance), voxel.Altered);
            } else {
                voxel = new Voxel(Math.Min(currentValF, currentValF - Intensity * absTerrainHeightValue * distance), voxel.Altered);
            }

            var currentAlteredAbs = Math.Abs(voxel.Altered);
            if (distance >= 0) {
                voxel.Altered = (sbyte) (Voxel.TextureOffset + TextureIndex);
            } else if (flatDistance > 0 && currentAlteredAbs < 3) {
                voxel.Altered = 1;
            }

            return voxel;
        }

        private Voxel ApplyPaint(int index, float distance)
        {
            var voxel = Voxels[index];

            if (distance >= 0) {
                var currentAlteredAbs = Math.Abs(voxel.Altered);
                if (currentAlteredAbs >= Voxel.TextureOffset) {
                    voxel.Altered = (sbyte) (Voxel.TextureOffset + TextureIndex);
                } else if (currentAlteredAbs >= Voxel.TextureNearSurfaceOffset) {
                    voxel.Altered = (sbyte) (Voxel.TextureNearSurfaceOffset + TextureIndex);
                }
            }

            return voxel;
        }

        private Voxel ApplyResetBrush(int index, int xi, int zi, float3 p)
        {
            var vec = p - Center;
            var flatDistance = (float) Math.Sqrt(vec.x * vec.x + vec.z * vec.z);
            if (flatDistance <= RadiusWithMargin) {
                var height = Heights[xi * SizeVox + zi];
                var voxel = Voxels[index];
                sbyte newAltered;
                if (voxel.Altered == 0 || flatDistance < Radius) {
                    newAltered = 0;
                } else {
                    newAltered = 1;
                }

                return new Voxel(p.y + ChunkAltitude - height, newAltered);
            }

            return Voxels[index];
        }
    }
}