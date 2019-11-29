using System.IO;
using System.Text;
using UnityEngine;

namespace Digger.TerrainCutters
{
#if UNITY_2019_3_OR_NEWER
    public class TerrainCutter20193 : TerrainCutter
    {
        [SerializeField] private DiggerSystem digger;

        private bool needsSync;
        private bool[,] holes;

        public override void OnEnterPlayMode()
        {
            var transparencyMapPath = digger.GetTransparencyMapBackupPath("holes");
            SaveTo(transparencyMapPath);
        }

        public override void OnExitPlayMode()
        {
            var transparencyMapPath = digger.GetTransparencyMapBackupPath("holes");
            LoadFrom(transparencyMapPath);
        }

        public static TerrainCutter20193 CreateInstance(Terrain terrain, DiggerSystem digger)
        {
            var cutter = digger.gameObject.AddComponent<TerrainCutter20193>();
            cutter.digger = digger;
            cutter.Refresh();
            return cutter;
        }

        public override void Refresh()
        {
            // force initialisation of holes texture
            var tData = digger.Terrain.terrainData;
            tData.SetHoles(0, 0, tData.GetHoles(0, 0, 1, 1));
            holes = tData.GetHoles(0, 0, tData.holesResolution, tData.holesResolution);
        }

        public override void Cut(CutEntry cutEntry, bool cutDetails)
        {
            holes[cutEntry.Z, cutEntry.X] = false;
            needsSync = true;
        }

        public override void UnCut(int x, int z)
        {
            holes[z, x] = true;
            needsSync = true;
        }

        protected override void ApplyInternal(bool persist)
        {
            Utils.Profiler.BeginSample("[Dig] Cutter20193.Apply");
            if (needsSync) {
                needsSync = false;
                digger.Terrain.terrainData.SetHoles(0, 0, holes);
            }

            Utils.Profiler.EndSample();
        }

        public override void LoadFrom(string path)
        {
            if (!File.Exists(path))
                return;

            Refresh();
            var resolution = digger.Terrain.terrainData.holesResolution;
            using (Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                using (var reader = new BinaryReader(stream, Encoding.Default)) {
                    for (var x = 0; x < resolution; ++x) {
                        for (var z = 0; z < resolution; ++z) {
                            holes[z, x] = reader.ReadBoolean();
                        }
                    }
                }
            }

            digger.Terrain.terrainData.SetHoles(0, 0, holes);
        }

        public override void SaveTo(string path)
        {
            if (holes == null)
                return;

            if (File.Exists(path))
                File.Delete(path);

            var resolution = digger.Terrain.terrainData.holesResolution;
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write)) {
                using (var writer = new BinaryWriter(stream, Encoding.Default)) {
                    for (var x = 0; x < resolution; ++x) {
                        for (var z = 0; z < resolution; ++z) {
                            writer.Write(holes[z, x]);
                        }
                    }
                }
            }
        }

        public override void Clear()
        {
#if UNITY_EDITOR
            Utils.Profiler.BeginSample("[Dig] Cutter.Clear");
            var resolution = digger.Terrain.terrainData.holesResolution;
            for (var x = 0; x < resolution; ++x) {
                for (var z = 0; z < resolution; ++z) {
                    holes[z, x] = true;
                }
            }

            digger.Terrain.terrainData.SetHoles(0, 0, holes);
            Utils.Profiler.EndSample();
#endif
        }
    }
#endif
}