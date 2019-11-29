using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Digger
{
    public class ListPool
    {
        private static readonly Vector4[] Vector4Array = new Vector4[65536];
        private static readonly List<Vector4> Vector4List = new List<Vector4>(65536);

        public static List<Vector4> ToVector4List(NativeArray<Vector4> src, int length)
        {
            NativeArray<Vector4>.Copy(src, Vector4Array, length);
            Vector4List.Clear();
            for (var i = 0; i < length; ++i) {
                Vector4List.Add(Vector4Array[i]);
            }

            return Vector4List;
        }
    }
}