using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Digger
{
    public static class TextureArrayManager
    {
        private const string ParentFolder = DiggerMaster.ParentFolder;
        private const string TextureArraysFolder = "TextureArrays";
        private const string CachedTexturesFolder = "CachedTextures";

        private const string TextureAssetLabel = "t";
        private const string NormalAssetLabel = "n";

        private static string ParentPath => Path.Combine("Assets", ParentFolder);
        private static string TextureArraysPath => Path.Combine(ParentPath, TextureArraysFolder);
        private static string CachedTexturesPath => Path.Combine(TextureArraysPath, CachedTexturesFolder);

        public static Texture2DArray GetCreateTexture2DArray(List<Texture2D> sourceTextures, bool isNormalMap)
        {
            Utils.Profiler.BeginSample("[Dig] TextureArrayManager.GetCreateTexture2DArray");
            CreateDirs();
            var foundTextureArray = FindTexture2DArray(sourceTextures, isNormalMap);
            if (foundTextureArray) {
                Debug.Log($"Found existing texture array '{foundTextureArray.name}'. Let's use it.");
                Utils.Profiler.EndSample();
                return foundTextureArray;
            }

            Debug.Log("Didn't find existing texture array. Let's create it.");

            var newTextureArray = CreateTextureArray(sourceTextures, isNormalMap);
            if (newTextureArray == null) {
                return null;
            }

            AssetDatabase.StartAssetEditing();
            var newTextureArrayPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(TextureArraysPath, isNormalMap ? "normal2DArray.asset" : "texture2DArray.asset"));
            AssetDatabase.CreateAsset(newTextureArray, newTextureArrayPath);
            AssetDatabase.SetLabels(newTextureArray, new[] {LabelFor(sourceTextures, isNormalMap)});
            Debug.Log($"Created texture array at {newTextureArrayPath}");

            AssetDatabase.StopAssetEditing();
            Utils.Profiler.EndSample();
            return newTextureArray;
        }

        private static Texture2DArray FindTexture2DArray(List<Texture2D> sourceTextures, bool isNormalMap)
        {
            Utils.Profiler.BeginSample("[Dig] TextureArrayManager.FindTexture2DArray>FindAssets");
            var label = LabelFor(sourceTextures, isNormalMap);
            var guids = AssetDatabase.FindAssets($"l:{label}", new[] {TextureArraysPath});
            Utils.Profiler.EndSample();
            if (guids == null || guids.Length == 0) {
                return null;
            }

            // we loop but there should be only one item in the list
            foreach (var guid in guids) {
                Utils.Profiler.BeginSample("[Dig] TextureArrayManager.FindTexture2DArray>LoadAssetAtPath");
                var textureArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>(AssetDatabase.GUIDToAssetPath(guid));
                Utils.Profiler.EndSample();
                if (textureArray && textureArray.depth == sourceTextures.Count) {
                    return textureArray;
                }
            }

            return null;
        }

        private static string LabelFor(List<Texture2D> sourceTextures, bool isNormalMap)
        {
            var label = isNormalMap ? NormalAssetLabel : TextureAssetLabel;
            foreach (var texture in sourceTextures) {
                label += $"_{texture.GetInstanceID()}";
            }

            return label;
        }

        private static void CreateDirs()
        {
            Utils.Profiler.BeginSample("[Dig] TextureArrayManager.CreateDirs");
            if (!Directory.Exists(ParentPath)) {
                AssetDatabase.CreateFolder("Assets", ParentFolder);
            }

            if (!Directory.Exists(TextureArraysPath)) {
                AssetDatabase.CreateFolder(ParentPath, TextureArraysFolder);
            }

            //if (!Directory.Exists(CachedTexturesPath)) {
            //    AssetDatabase.CreateFolder(TextureArraysPath, CachedTexturesFolder);
            //}
            Utils.Profiler.EndSample();
        }

        /// <summary>
        /// Turn source textures into a 2D texture array
        /// </summary>
        /// <param name="sourceTextures">Textures</param>
        /// <param name="isNormalMap">Is it normal map(s)?</param>
        /// <returns>TextureArray if successful, null otherwise</returns>
        private static Texture2DArray CreateTextureArray(List<Texture2D> sourceTextures, bool isNormalMap)
        {
            if (sourceTextures == null || sourceTextures.Count == 0) {
                var fakeTextureArray = new Texture2DArray(128, 128, 1, TextureFormat.ARGB32, true, isNormalMap);
                fakeTextureArray.SetPixels(NewFakeTexture(128, isNormalMap ? new Color(0.5f, 0.5f, 1f) : Color.white), 0);
                fakeTextureArray.Apply(true);
                return fakeTextureArray;
            }

            //Check they are all the same size
            var readableTextures = GetReadableTextures(sourceTextures);
            if (readableTextures == null || readableTextures.Count != sourceTextures.Count) {
                Debug.LogError("Could not get readable textures");
                return null;
            }

            var sourceTexture = readableTextures[0];

            var textureArray = new Texture2DArray(sourceTexture.width, sourceTexture.height, readableTextures.Count, TextureFormat.ARGB32, true, isNormalMap)
            {
                filterMode = sourceTexture.filterMode,
                wrapMode = sourceTexture.wrapMode,
                anisoLevel = sourceTexture.anisoLevel,
                mipMapBias = sourceTexture.mipMapBias
            };

            for (var i = 0; i < readableTextures.Count; i++) {
                var tex = readableTextures[i];
                tex.Apply(false);
                for (var mip = 0; mip < tex.mipmapCount; mip++) {
                    textureArray.SetPixels32(tex.GetPixels32(mip), i, mip);
                }
            }
            
            textureArray.Apply(false);
            
            return textureArray;
        }

        private static List<Texture2D> GetReadableTextures(List<Texture2D> sourceTextures)
        {
            var readableTextures = new List<Texture2D>();
            if (sourceTextures.Count == 0)
                return readableTextures;

            // Set maxTextureSize to the smallest texture size
            var maxTextureSize = int.MaxValue;
            foreach (var tex in sourceTextures) {
                var texPath = AssetDatabase.GetAssetPath(tex);
                var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (importer != null && importer.maxTextureSize < maxTextureSize)
                    maxTextureSize = importer.maxTextureSize;
                if (tex.width < maxTextureSize)
                    maxTextureSize = tex.width;
                if (tex.width != tex.height) {
                    Debug.LogError($"Terrain textures must have the same width and height, but '{tex.name}' has a width of {tex.width}px and a height of {tex.height}px.\n\n" +
                                   "Please fix this and click on 'Sync & Refresh' again.");
                    EditorUtility.DisplayDialog("Bad Terrain Texture(s)",
                                                $"Terrain textures must have the same width and height, but '{tex.name}' has a width of {tex.width}px and a height of {tex.height}px.\n\n" +
                                                "Please fix this and click on 'Sync & Refresh' again.",
                                                "Ok");
                    return readableTextures;
                }
            }

            foreach (var tex in sourceTextures) {
                var readableTexture = GetExistingReadableTexture(tex, maxTextureSize);
                if (!readableTexture) {
                    readableTextures.Clear();
                    Debug.Log("No readable texture found. Let's create them...");
                    break;
                }

                readableTextures.Add(readableTexture);
            }

            if (readableTextures.Count > 0) {
                Debug.Log("Readable textures already exist. No need to create them.");
                return readableTextures;
            }

            AssetDatabase.StartAssetEditing();
            foreach (var tex in sourceTextures) {
                CreateReadableTextureCopy(tex);
            }

            AssetDatabase.StopAssetEditing();

            AssetDatabase.StartAssetEditing();
            foreach (var tex in sourceTextures) {
                var readableTexture = GetReadableTexture(tex, maxTextureSize);
                if (!readableTexture) {
                    Debug.LogError($"Could not get readable texture out of {tex.name}");
                    return null;
                }

                readableTextures.Add(readableTexture);
            }

            AssetDatabase.StopAssetEditing();
            return readableTextures;
        }

        private static Texture2D GetReadableTexture(Texture2D texture, int maxTextureSize)
        {
            if (texture == null) {
                return null;
            }

            var readablePath = GetReadableTexturePath(texture);
            var readableTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(readablePath);
            if (readableTexture) {
                var importer = AssetImporter.GetAtPath(readablePath) as TextureImporter;
                if (importer == null) {
                    Debug.LogError("Could not get importer of readable texture");
                    return null;
                }

                if (importer.isReadable &&
                    importer.textureCompression == TextureImporterCompression.Uncompressed &&
                    importer.maxTextureSize == maxTextureSize)
                    return readableTexture;

                importer.isReadable = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = maxTextureSize;
                importer.SaveAndReimport();
                return readableTexture;
            }

            Debug.LogError($"Asset not found at {readablePath}. Did you forget to call CreateReadableTextureCopy?");
            return null;
        }

        private static void CreateReadableTextureCopy(Texture2D texture)
        {
            if (texture == null) {
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(texture);

            var readablePath = GetReadableTexturePath(texture);
            var readableTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(readablePath);
            if (!readableTexture) {
                AssetDatabase.CopyAsset(assetPath, readablePath);
            }
        }

        private static Texture2D GetExistingReadableTexture(Texture2D texture, int maxTextureSize)
        {
            if (texture == null) {
                return null;
            }

            var readablePath = GetReadableTexturePath(texture);
            var importer = AssetImporter.GetAtPath(readablePath) as TextureImporter;
            if (importer == null ||
                !importer.isReadable ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.maxTextureSize != maxTextureSize) {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(readablePath);
        }

        private static string GetReadableTexturePath(Texture2D sourceTexture)
        {
            var assetPath = AssetDatabase.GetAssetPath(sourceTexture);
            assetPath = assetPath.Replace("Assets", CachedTexturesPath);

            var dir = Path.GetDirectoryName(assetPath);
            if (dir != null)
                Directory.CreateDirectory(dir);

            return Path.ChangeExtension(assetPath, $"readable{Path.GetExtension(assetPath)}");
        }

        private static Color[] NewFakeTexture(int size, Color color)
        {
            var pixels = new Color[size * size];
            for (var i = 0; i < pixels.Length; ++i) {
                pixels[i] = color;
            }

            return pixels;
        }
    }
}