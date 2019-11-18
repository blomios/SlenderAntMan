using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace DynamicFogAndMist {

    [Serializable]
    public sealed class FogTypeParameter : ParameterOverride<FOG_TYPE> { }

    [Serializable]
    public sealed class FogPresetParameter : ParameterOverride<FOG_PRESET> { }

    [Serializable]
    public sealed class GameObjectParameter : ParameterOverride<GameObject> { }


    [Serializable]
    [PostProcess(typeof(DynamicFogPPSRenderer), PostProcessEvent.BeforeStack, "Kronnect/Dynamic Fog And Mist")]
    public sealed class DynamicFogPPS : PostProcessEffectSettings {

        public override bool IsEnabledAndSupported(PostProcessRenderContext context) {
            return enabled.value && (alpha.value > 0f || skyAlpha.value > 0f);
        }

        public FogTypeParameter effectType = new FogTypeParameter { value = FOG_TYPE.MobileFogWithSkyHaze };

        public BoolParameter enableDithering = new BoolParameter { value = false };

        [Range(0, 0.2f)]
        public FloatParameter ditherStrength = new FloatParameter { value = 0.03f };

        [Range(0, 1)]
        public FloatParameter alpha = new FloatParameter { value = 1f };

        [Range(0, 1)]
        public FloatParameter noiseStrength = new FloatParameter { value = 0.1f };

        [Range(0.01f, 1)]
        public FloatParameter noiseScale = new FloatParameter { value = 0.1f };

        [Range(0, 0.999f)]
        public FloatParameter distance = new FloatParameter { value = 0f };

        [Range(0.0001f, 2f)]
        public FloatParameter distanceFallOff = new FloatParameter { value = 0.01f };

        [Range(0, 1.2f)]
        public FloatParameter maxDistance = new FloatParameter { value = 0.999f };

        [Range(0.0001f, 0.5f)]
        public FloatParameter maxDistanceFallOff = new FloatParameter { value = 0f };

        [Range(0, 500)]
        public FloatParameter height = new FloatParameter { value = 8f };

        [Range(0, 500)]
        public FloatParameter maxHeight = new FloatParameter { value = 100f };

        // used in orthogonal fog
        [Range(0.0001f, 1)]
        public FloatParameter heightFallOff = new FloatParameter { value = 0.1f };

        public FloatParameter baselineheight = new FloatParameter { value = 0f };

        public BoolParameter clipUnderBaseline = new BoolParameter { value = false };

        [Range(0, 15)]
        public FloatParameter turbulence = new FloatParameter { value = 0.1f };

        [Range(0, 5.0f)]
        public FloatParameter speed = new FloatParameter { value = 0.1f };

        public Vector3Parameter windDirection = new Vector3Parameter { value = new Vector3(1, 0, 1) };

        public ColorParameter color = new ColorParameter { value = Color.white };

        public ColorParameter color2 = new ColorParameter { value = Color.gray };

        [Range(0, 500)]
        public FloatParameter skyHaze = new FloatParameter { value = 50f };

        [Range(0, 1)]
        public FloatParameter skySpeed = new FloatParameter { value = 0.3f };

        [Range(0, 1)]
        public FloatParameter skyNoiseStrength = new FloatParameter { value = 0.1f };

        [Range(0, 1)]
        public FloatParameter skyAlpha = new FloatParameter { value = 1.0f };

        public BoolParameter useDirectionalLightAsSun = new BoolParameter { value = false };

        public BoolParameter fogOfWarEnabled = new BoolParameter { value = false };

        public Vector3Parameter fogOfWarCenter = new Vector3Parameter();

        public Vector3Parameter fogOfWarSize = new Vector3Parameter { value = new Vector3(1024, 0, 1024) };

        public IntParameter fogOfWarTextureSize = new IntParameter { value = 256 };

        public BoolParameter useXZdistance = new BoolParameter { value = false };

        [Range(0, 1)]
        public FloatParameter scattering = new FloatParameter { value = 0.7f };

        public ColorParameter scatteringColor = new ColorParameter { value = new Color(1, 1, 0.8f) };

        #region Fog of War stuff

        public static Texture2D fogOfWarTexture;
        public static Color32[] fogOfWarColorBuffer;

		public void UpdateFogOfWarTexture() {
            if (!fogOfWarEnabled.value)
                return;
            int size = GetScaledSize(fogOfWarTextureSize.value, 1.0f);
            fogOfWarTexture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            fogOfWarTexture.hideFlags = HideFlags.DontSave;
            fogOfWarTexture.filterMode = FilterMode.Bilinear;
            fogOfWarTexture.wrapMode = TextureWrapMode.Clamp;
            ResetFogOfWar();
        }

        /// <summary>
        /// Changes the alpha value of the fog of war at world position. It takes into account FogOfWarCenter and FogOfWarSize.
        /// Note that only x and z coordinates are used. Y (vertical) coordinate is ignored.
        /// </summary>
        /// <param name="worldPosition">in world space coordinates.</param>
        /// <param name="radius">radius of application in world units.</param>
        public void SetFogOfWarAlpha(Vector3 worldPosition, float radius, float fogNewAlpha) {
            if (fogOfWarTexture == null)
                return;

            float tx = (worldPosition.x - fogOfWarCenter.value.x) / fogOfWarSize.value.x + 0.5f;
            if (tx < 0 || tx > 1f)
                return;
            float tz = (worldPosition.z - fogOfWarCenter.value.z) / fogOfWarSize.value.z + 0.5f;
            if (tz < 0 || tz > 1f)
                return;

            int tw = fogOfWarTexture.width;
            int th = fogOfWarTexture.height;
            int px = (int)(tx * tw);
            int pz = (int)(tz * th);
            int colorBufferPos = pz * tw + px;
            byte newAlpha8 = (byte)(fogNewAlpha * 255);
            Color32 existingColor = fogOfWarColorBuffer[colorBufferPos];
            if (newAlpha8 != existingColor.a) { // just to avoid over setting the texture in an Update() loop
                float tr = radius / fogOfWarSize.value.z;
                int delta = Mathf.FloorToInt(th * tr);
                for (int r = pz - delta; r <= pz + delta; r++) {
                    if (r > 0 && r < th - 1) {
                        for (int c = px - delta; c <= px + delta; c++) {
                            if (c > 0 && c < tw - 1) {
                                int distance = Mathf.FloorToInt(Mathf.Sqrt((pz - r) * (pz - r) + (px - c) * (px - c)));
                                if (distance <= delta) {
                                    colorBufferPos = r * tw + c;
                                    Color32 colorBuffer = fogOfWarColorBuffer[colorBufferPos];
                                    colorBuffer.a = (byte)Mathf.Lerp(newAlpha8, colorBuffer.a, (float)distance / delta);
                                    fogOfWarColorBuffer[colorBufferPos] = colorBuffer;
                                    fogOfWarTexture.SetPixel(c, r, colorBuffer);
                                }
                            }
                        }
                    }
                }
                fogOfWarTexture.Apply();
            }
        }

        public void ResetFogOfWarAlpha(Vector3 worldPosition, float radius) {
            if (fogOfWarTexture == null)
                return;

            float tx = (worldPosition.x - fogOfWarCenter.value.x) / fogOfWarSize.value.x + 0.5f;
            if (tx < 0 || tx > 1f)
                return;
            float tz = (worldPosition.z - fogOfWarCenter.value.z) / fogOfWarSize.value.z + 0.5f;
            if (tz < 0 || tz > 1f)
                return;

            int tw = fogOfWarTexture.width;
            int th = fogOfWarTexture.height;
            int px = (int)(tx * tw);
            int pz = (int)(tz * th);
            int colorBufferPos = pz * tw + px;
            float tr = radius / fogOfWarSize.value.z;
            int delta = Mathf.FloorToInt(th * tr);
            for (int r = pz - delta; r <= pz + delta; r++) {
                if (r > 0 && r < th - 1) {
                    for (int c = px - delta; c <= px + delta; c++) {
                        if (c > 0 && c < tw - 1) {
                            int distance = Mathf.FloorToInt(Mathf.Sqrt((pz - r) * (pz - r) + (px - c) * (px - c)));
                            if (distance <= delta) {
                                colorBufferPos = r * tw + c;
                                Color32 colorBuffer = fogOfWarColorBuffer[colorBufferPos];
                                colorBuffer.a = 255;
                                fogOfWarColorBuffer[colorBufferPos] = colorBuffer;
                                fogOfWarTexture.SetPixel(c, r, colorBuffer);
                            }
                        }
                    }
                }
                fogOfWarTexture.Apply();
            }
        }

        public void ResetFogOfWar() {
            if (fogOfWarTexture == null)
                return;
            int h = fogOfWarTexture.height;
            int w = fogOfWarTexture.width;
            int newLength = h * w;
            if (fogOfWarColorBuffer == null || fogOfWarColorBuffer.Length != newLength) {
                fogOfWarColorBuffer = new Color32[newLength];
            }
            Color32 opaque = new Color32(255, 255, 255, 255);
            for (int k = 0; k < newLength; k++)
                fogOfWarColorBuffer[k] = opaque;
            fogOfWarTexture.SetPixels32(fogOfWarColorBuffer);
            fogOfWarTexture.Apply();
        }

        int GetScaledSize(int size, float factor) {
            size = (int)(size / factor);
            size /= 4;
            if (size < 1)
                size = 1;
            return size * 4;
        }

        #endregion

        static DynamicFogPPS _instance;

        public static DynamicFogPPS instance {
            get {
                if (_instance==null) {
                    PostProcessVolume vol = FindObjectOfType<PostProcessVolume>();
                    if (vol!=null) {
                        vol.sharedProfile.TryGetSettings<DynamicFogPPS>(out _instance);
                    }
                }
                return _instance;
            }
        }
    }

    public sealed class DynamicFogPPSRenderer : PostProcessEffectRenderer<DynamicFogPPS> {

		public override void Init() {
            settings.UpdateFogOfWarTexture();
		}

		public override DepthTextureMode GetCameraFlags() {
            return DepthTextureMode.Depth;
        }

        public Light sunLight;
        Vector3 sunDirection = Vector3.zero;
        Color sunColor = Color.white;
        float sunIntensity = 1f;
        Texture2D noise0, noise1, noise3D;

        Texture2D GetTextureNoise0()
        {
            if (noise0==null)
            {
                noise0 = Resources.Load<Texture2D>("Textures/Noise0");
            }
            return noise0;
        }

        Texture2D GetTextureNoise1()
        {
            if (noise1 == null)
            {
                noise1 = Resources.Load<Texture2D>("Textures/Noise1");
            }
            return noise1;
        }


        Texture2D GetTextureNoise3D()
        {
            if (noise3D == null)
            {
                noise3D = Resources.Load<Texture2D>("Textures/Noise3D");
            }
            return noise3D;
        }


        public override void Render(PostProcessRenderContext context) {
            PropertySheet sheet = null;

            switch (settings.effectType.value) {
                case FOG_TYPE.DesktopFogPlusWithSkyHaze:
                    sheet = context.propertySheets.Get(Shader.Find("DynamicFog/Image Effect PPS/Desktop Fog Plus With Sky Haze"));
                    sheet.properties.SetTexture("_NoiseTex", GetTextureNoise0());
                    sheet.properties.SetTexture("_Noise3DTex", GetTextureNoise3D());
                    break;
                case FOG_TYPE.DesktopFogPlusOrthogonal:
                    sheet = context.propertySheets.Get(Shader.Find("DynamicFog/Image Effect PPS/Desktop Fog Plus Orthogonal"));
                    sheet.properties.SetTexture("_Noise3DTex", GetTextureNoise3D());
                    break;
                case FOG_TYPE.DesktopFogWithSkyHaze:
                    sheet = context.propertySheets.Get(Shader.Find("DynamicFog/Image Effect PPS/Desktop Fog With Sky Haze"));
                    sheet.properties.SetTexture("_NoiseTex", GetTextureNoise1());
                    sheet.properties.SetTexture("_Noise2Tex", GetTextureNoise0());
                    break;
                case FOG_TYPE.MobileFogWithSkyHaze:
                    sheet = context.propertySheets.Get(Shader.Find("DynamicFog/Image Effect PPS/Mobile Fog With Sky Haze"));
                    sheet.properties.SetTexture("_NoiseTex", GetTextureNoise1());
                    break;
                case FOG_TYPE.MobileFogBasic:
                    sheet = context.propertySheets.Get(Shader.Find("DynamicFog/Image Effect PPS/Mobile Fog Basic"));
                    break;
                case FOG_TYPE.MobileFogOnlyGround:
                    sheet = context.propertySheets.Get(Shader.Find("DynamicFog/Image Effect PPS/Mobile Fog Only Ground"));
                    sheet.properties.SetTexture("_NoiseTex", GetTextureNoise1());
                    break;
                case FOG_TYPE.MobileFogOrthogonal:
                    sheet = context.propertySheets.Get(Shader.Find("DynamicFog/Image Effect PPS/Mobile Fog Orthogonal"));
                    break;
                case FOG_TYPE.MobileFogSimple:
                    sheet = context.propertySheets.Get(Shader.Find("DynamicFog/Image Effect PPS/Mobile Fog Simple"));
                    break;
                default:
                    Debug.LogError("Effect type not implemented.");
                    break;
            }

            sheet.properties.SetVector("_ClipDir", context.camera.transform.forward);
            if (UnityEngine.XR.XRSettings.enabled && UnityEngine.XR.XRSettings.eyeTextureDesc.vrUsage == VRTextureUsage.TwoEyes) {
                sheet.properties.SetMatrix("_ClipToWorld", context.camera.cameraToWorldMatrix);
            } else {
                sheet.properties.SetMatrix("_ClipToWorld", context.camera.cameraToWorldMatrix * context.camera.projectionMatrix.inverse);
            }

            // x = haze, y = speed.value = z = noise, w = alpha
            Vector4 skyData = new Vector4(settings.skyHaze, settings.skySpeed, settings.skyNoiseStrength, settings.skyAlpha);
            sheet.properties.SetVector("_FogSkyData", skyData);

            if (settings.useDirectionalLightAsSun) {
                if (sunLight == null) sunLight = FindSun();
                if (sunLight != null) {
                    sunDirection = sunLight.transform.forward;
                    if (sunLight != null) {
                        sunColor = sunLight.color;
                        sunIntensity = sunLight.intensity;
                    }
                }
            }

            float fogIntensity = sunIntensity * Mathf.Clamp01(1.0f - sunDirection.y);
            sheet.properties.SetColor("_FogColor", fogIntensity * settings.color.value * sunColor);
            sheet.properties.SetColor("_FogColor2", fogIntensity * settings.color2.value * sunColor);
            Color sColor = fogIntensity * settings.scatteringColor.value;
            sheet.properties.SetColor("_SunColor", new Vector4(sColor.r, sColor.g, sColor.b, settings.scattering));
            sheet.properties.SetVector("_SunDir", -sunDirection);


            float sp = settings.effectType.value == FOG_TYPE.DesktopFogPlusWithSkyHaze ? settings.speed * 5f : settings.speed;
            sheet.properties.SetVector("_FogSpeed", settings.windDirection.value.normalized * sp);

            Vector4 noiseData = new Vector4(settings.noiseStrength, settings.turbulence, context.camera.farClipPlane * 15.0f / 1000f, settings.noiseScale);
            sheet.properties.SetVector("_FogNoiseData", noiseData);

            Vector4 heightData = new Vector4(settings.height + 0.001f, settings.baselineheight, settings.clipUnderBaseline ? -0.01f : -10000, settings.heightFallOff);
            if (settings.effectType == FOG_TYPE.MobileFogOrthogonal || settings.effectType == FOG_TYPE.DesktopFogPlusOrthogonal) {
                heightData.z = settings.maxHeight;
            }
            sheet.properties.SetVector("_FogHeightData", heightData);

            sheet.properties.SetFloat("_FogAlpha", settings.alpha);

            Vector4 distance = new Vector4(settings.distance, settings.distanceFallOff, settings.maxDistance, settings.maxDistanceFallOff);
            if (settings.effectType.value.isPlus()) {
                distance.x = context.camera.farClipPlane * settings.distance;
                distance.y = settings.distanceFallOff.value * distance.x + 0.0001f;
                distance.z *= context.camera.farClipPlane;
            }
            sheet.properties.SetVector("_FogDistance", distance);

            sheet.ClearKeywords();

            if (settings.fogOfWarEnabled) {
                if (DynamicFogPPS.fogOfWarTexture == null) {
                    settings.UpdateFogOfWarTexture();
                }
                Vector3 fogOfWarCenter = settings.fogOfWarCenter;
                Vector3 fogOfWarSize = settings.fogOfWarSize;
                sheet.properties.SetTexture("_FogOfWar", DynamicFogPPS.fogOfWarTexture);
                sheet.properties.SetVector("_FogOfWarCenter", fogOfWarCenter);
                sheet.properties.SetVector("_FogOfWarSize", fogOfWarSize);
                Vector3 ca = fogOfWarCenter - 0.5f * fogOfWarSize;
                sheet.properties.SetVector("_FogOfWarCenterAdjusted", new Vector3(ca.x / fogOfWarSize.x, 1f, ca.z / fogOfWarSize.z));
                sheet.EnableKeyword("FOG_OF_WAR_ON");
            }
            if (settings.enableDithering) {
                sheet.properties.SetFloat("_FogDither", settings.ditherStrength);
                sheet.EnableKeyword("DITHER_ON");
            }
            context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);
        }

        Light FindSun() {
            Light[] lights = GameObject.FindObjectsOfType<Light>();
            for (int k = 0; k < lights.Length; k++) {
                if (lights[k].type == LightType.Directional) return lights[k];
            }
            return null;
        }

    }
}