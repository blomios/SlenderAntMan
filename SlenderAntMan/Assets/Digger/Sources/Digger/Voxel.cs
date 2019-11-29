using System;

namespace Digger
{
    public struct Voxel
    {
        public const sbyte TextureOffset = 50;
        public const sbyte TextureNearSurfaceOffset = 10;

        public float Value;

        /// <summary>
        /// 0 => not altered, no visual mesh generated.
        /// 1 => not altered but visual mesh must be generated as terrain surface.
        /// [10, 50[ => altered, near terrain surface. Texture index is given by Altered - 10.
        /// [50, 90[ => altered, not near terrain surface. Texture index is given by Altered - 50.
        /// </summary>
        public sbyte Altered;

        public Voxel(float value, sbyte altered)
        {
            Value = value;
            Altered = altered;
        }

        public bool IsInside => Value < 0;

        public bool IsAlteredNearBelowSurface => Altered <= -TextureNearSurfaceOffset && Altered > -TextureOffset;

        public bool IsAlteredNearAboveSurface => Altered >= TextureNearSurfaceOffset && Altered < TextureOffset;

        public bool IsAltered => Altered >= TextureOffset || Altered <= -TextureOffset;

        public static int GetTextureIndex(int altered)
        {
            var absAltered = Math.Abs(altered);
            if (absAltered >= TextureNearSurfaceOffset && absAltered < TextureOffset)
                return absAltered - TextureNearSurfaceOffset;

            if (absAltered >= TextureOffset)
                return absAltered - TextureOffset;

            return -1;
        }
    }
}