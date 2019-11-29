using System;
using Digger.TerrainCutters;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Digger
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
    public struct VoxelKernelModificationJob : IJobParallelFor
    {
        public int SizeVox;
        public int SizeOfMesh;
        public int SizeVox2;
        public int LowInd;
        public ActionType Action;
        public float3 HeightmapScale;
        public float3 Center;
        public float Radius;
        public float Intensity;

        [ReadOnly] public NativeArray<Voxel> Voxels;
        [WriteOnly] public NativeArray<Voxel> VoxelsOut;

        // Smooth action only
        [ReadOnly] public NativeArray<Voxel> NeighborVoxelsLBB;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxelsLBF;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxelsLB_;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxels_BB;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxels_BF;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxels_B_;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxelsRBB;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxelsRBF;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxelsRB_;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxelsL_B;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxelsL_F;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxelsL__;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxels__B;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxels__F;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxelsR_B;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxelsR_F;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxelsR__;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxelsLUB;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxelsLUF;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxelsLU_;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxels_UB;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxels_UF;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxels_U_;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxelsRUB;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxelsRUF;
        [ReadOnly] public NativeArray<Voxel> NeighborVoxelsRU_;


        public void Execute(int index)
        {
            var xi = index / SizeVox2;
            var yi = (index - xi * SizeVox2) / SizeVox;
            var zi = index - xi * SizeVox2 - yi * SizeVox;

            var p = new float3((xi - 1) * HeightmapScale.x, (yi - 1), (zi - 1) * HeightmapScale.z);

            // Always use a spherical brush
            if (ComputeSphereDistance(p) < 0) {
                VoxelsOut[index] = Voxels[index];
                return;
            }

            Voxel voxel;
            switch (Action) {
                case ActionType.Smooth:
                    voxel = ApplySmooth(index, xi, yi, zi);
                    break;
                default:
                    return; // never happens
            }

            VoxelsOut[index] = voxel;
        }

        public void DisposeNeighbors()
        {
            NeighborVoxelsLBB.Dispose();
            NeighborVoxelsLBF.Dispose();
            NeighborVoxelsLB_.Dispose();
            NeighborVoxels_BB.Dispose();
            NeighborVoxels_BF.Dispose();
            NeighborVoxels_B_.Dispose();
            NeighborVoxelsRBB.Dispose();
            NeighborVoxelsRBF.Dispose();
            NeighborVoxelsRB_.Dispose();
            NeighborVoxelsL_B.Dispose();
            NeighborVoxelsL_F.Dispose();
            NeighborVoxelsL__.Dispose();
            NeighborVoxels__B.Dispose();
            NeighborVoxels__F.Dispose();
            NeighborVoxelsR_B.Dispose();
            NeighborVoxelsR_F.Dispose();
            NeighborVoxelsR__.Dispose();
            NeighborVoxelsLUB.Dispose();
            NeighborVoxelsLUF.Dispose();
            NeighborVoxelsLU_.Dispose();
            NeighborVoxels_UB.Dispose();
            NeighborVoxels_UF.Dispose();
            NeighborVoxels_U_.Dispose();
            NeighborVoxelsRUB.Dispose();
            NeighborVoxelsRUF.Dispose();
            NeighborVoxelsRU_.Dispose();
        }

        private float ComputeSphereDistance(float3 p)
        {
            var vec = p - Center;
            var distance = (float) Math.Sqrt(vec.x * vec.x + vec.y * vec.y + vec.z * vec.z);
            return Radius - distance;
        }

        private Voxel ApplySmooth(int index, int xi, int yi, int zi)
        {
            var voxel = Voxels[index];
            if (!voxel.IsAltered)
                return voxel;

            var voxelValue = 0f;
            for (var x = xi - 1; x <= xi + 1; ++x) {
                for (var y = yi - 1; y <= yi + 1; ++y) {
                    for (var z = zi - 1; z <= zi + 1; ++z) {
                        voxelValue += GetVoxelAt(x, y, z).Value;
                    }
                }
            }

            const float by27 = 1f / 27f;
            voxel.Value = Mathf.Lerp(voxel.Value, voxelValue * by27, Intensity);
            return voxel;
        }

        private Voxel GetVoxelAt(int x, int y, int z)
        {
            // [x * SizeVox * SizeVox + y * SizeVox + z]
            if (x == -1) {
                if (y == -1) {
                    if (z == -1) {
                        return NeighborVoxelsLBB[LowInd * SizeVox2 + LowInd * SizeVox + LowInd];
                    } else if (z > SizeOfMesh) {
                        return NeighborVoxelsLBF[LowInd * SizeVox2 + LowInd * SizeVox + (z - SizeOfMesh)];
                    } else {
                        return NeighborVoxelsLB_[LowInd * SizeVox2 + LowInd * SizeVox + z];
                    }
                } else if (y > SizeOfMesh) {
                    if (z == -1) {
                        return NeighborVoxelsLUB[LowInd * SizeVox2 + (y - SizeOfMesh) * SizeVox + LowInd];
                    } else if (z > SizeOfMesh) {
                        return NeighborVoxelsLUF[LowInd * SizeVox2 + (y - SizeOfMesh) * SizeVox + (z - SizeOfMesh)];
                    } else {
                        return NeighborVoxelsLU_[LowInd * SizeVox2 + (y - SizeOfMesh) * SizeVox + z];
                    }
                } else {
                    if (z == -1) {
                        return NeighborVoxelsL_B[LowInd * SizeVox2 + y * SizeVox + LowInd];
                    } else if (z > SizeOfMesh) {
                        return NeighborVoxelsL_F[LowInd * SizeVox2 + y * SizeVox + (z - SizeOfMesh)];
                    } else {
                        return NeighborVoxelsL__[LowInd * SizeVox2 + y * SizeVox + z];
                    }
                }
            } else if (x > SizeOfMesh) {
                if (y == -1) {
                    if (z == -1) {
                        return NeighborVoxelsRBB[(x - SizeOfMesh) * SizeVox2 + LowInd * SizeVox + LowInd];
                    } else if (z > SizeOfMesh) {
                        return NeighborVoxelsRBF[(x - SizeOfMesh) * SizeVox2 + LowInd * SizeVox + (z - SizeOfMesh)];
                    } else {
                        return NeighborVoxelsRB_[(x - SizeOfMesh) * SizeVox2 + LowInd * SizeVox + z];
                    }
                } else if (y > SizeOfMesh) {
                    if (z == -1) {
                        return NeighborVoxelsRUB[(x - SizeOfMesh) * SizeVox2 + (y - SizeOfMesh) * SizeVox + LowInd];
                    } else if (z > SizeOfMesh) {
                        return NeighborVoxelsRUF[(x - SizeOfMesh) * SizeVox2 + (y - SizeOfMesh) * SizeVox + (z - SizeOfMesh)];
                    } else {
                        return NeighborVoxelsRU_[(x - SizeOfMesh) * SizeVox2 + (y - SizeOfMesh) * SizeVox + z];
                    }
                } else {
                    if (z == -1) {
                        return NeighborVoxelsR_B[(x - SizeOfMesh) * SizeVox2 + y * SizeVox + LowInd];
                    } else if (z > SizeOfMesh) {
                        return NeighborVoxelsR_F[(x - SizeOfMesh) * SizeVox2 + y * SizeVox + (z - SizeOfMesh)];
                    } else {
                        return NeighborVoxelsR__[(x - SizeOfMesh) * SizeVox2 + y * SizeVox + z];
                    }
                }
            } else {
                if (y == -1) {
                    if (z == -1) {
                        return NeighborVoxels_BB[x * SizeVox2 + LowInd * SizeVox + LowInd];
                    } else if (z > SizeOfMesh) {
                        return NeighborVoxels_BF[x * SizeVox2 + LowInd * SizeVox + (z - SizeOfMesh)];
                    } else {
                        return NeighborVoxels_B_[x * SizeVox2 + LowInd * SizeVox + z];
                    }
                } else if (y > SizeOfMesh) {
                    if (z == -1) {
                        return NeighborVoxels_UB[x * SizeVox2 + (y - SizeOfMesh) * SizeVox + LowInd];
                    } else if (z > SizeOfMesh) {
                        return NeighborVoxels_UF[x * SizeVox2 + (y - SizeOfMesh) * SizeVox + (z - SizeOfMesh)];
                    } else {
                        return NeighborVoxels_U_[x * SizeVox2 + (y - SizeOfMesh) * SizeVox + z];
                    }
                } else {
                    if (z == -1) {
                        return NeighborVoxels__B[x * SizeVox2 + y * SizeVox + LowInd];
                    } else if (z > SizeOfMesh) {
                        return NeighborVoxels__F[x * SizeVox2 + y * SizeVox + (z - SizeOfMesh)];
                    } else {
                        return Voxels[x * SizeVox2 + y * SizeVox + z];
                    }
                }
            }
        }

        private Voxel GetVoxelAtDebug(int x, int y, int z)
        {
            x = Mathf.Max(0, Mathf.Min(x, LowInd));
            y = Mathf.Max(0, Mathf.Min(y, LowInd));
            z = Mathf.Max(0, Mathf.Min(z, LowInd));
            return Voxels[x * SizeVox2 + y * SizeVox + z];
        }
    }
}