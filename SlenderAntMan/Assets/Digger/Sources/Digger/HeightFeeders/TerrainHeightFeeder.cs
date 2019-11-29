using System;
using UnityEngine;

namespace Digger.HeightFeeders
{
    public class TerrainHeightFeeder : IHeightFeeder
    {
        private readonly TerrainData terrainData;
        private readonly int resolution;
        private readonly float resolutionInv;

        public TerrainHeightFeeder(TerrainData terrainData, int resolution)
        {
            this.terrainData = terrainData;
            this.resolution = resolution;
            this.resolutionInv = 1f / resolution;
        }

        public float GetHeight(int x, int z)
        {
            if (resolution == 1)
                return terrainData.GetHeight(x, z);

            var xr = x / resolution;
            var zr = z / resolution;
            return Utils.BilinearInterpolate(terrainData.GetHeight(xr, zr),
                                             terrainData.GetHeight(xr, zr + 1),
                                             terrainData.GetHeight(xr + 1, zr),
                                             terrainData.GetHeight(xr + 1, zr + 1),
                                             x % resolution * resolutionInv,
                                             z % resolution * resolutionInv);
        }

        public float GetVerticalNormal(int x, int z)
        {
            var minNrmY = 1f;
            var xr = x / resolution;
            var zr = z / resolution;
            for (var xx = -1; xx <= 1; ++xx) {
                for (var zz = -1; zz <= 1; ++zz) {
                    var nrm = terrainData.GetInterpolatedNormal((float) (xr + xx) / terrainData.heightmapResolution, (float) (zr + zz) / terrainData.heightmapResolution);
                    minNrmY = Math.Min(minNrmY, Math.Abs(nrm.y));
                }
            }

            return minNrmY;
        }
    }
}