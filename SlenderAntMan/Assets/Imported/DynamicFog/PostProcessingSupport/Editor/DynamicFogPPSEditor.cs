using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEditor;
using UnityEditor.Rendering.PostProcessing;
using System;
using System.Collections;

namespace DynamicFogAndMist {
    [PostProcessEditor(typeof(DynamicFogPPS))]
    public class DynamicFogPPSEditor : PostProcessEffectEditor<DynamicFogPPS> {

        static GUIStyle titleLabelStyle, sectionHeaderStyle;
        static Color titleColor;
        static bool[] expandSection = new bool[5];
        const string SECTION_PREFS = "DynamicFogExpandSection";
        static string[] sectionNames = new string[] {
                                                "Fog Properties",
                                                "Sky Properties",
                                                "Fog of War"
                                };
        const int FOG_PROPERTIES = 0;
        const int SKY_PROPERTIES = 1;
        const int FOG_OF_WAR = 2;
        SerializedParameterOverride effectType, enableDithering, ditherStrength, useSinglePassStereoRenderingMatrix;
        SerializedParameterOverride alpha, noiseStrength, noiseScale, distance, distanceFallOff, maxDistance, maxDistanceFallOff, height, maxHeight;
        SerializedParameterOverride heightFallOff, baselineHeight, clipUnderBaseline, turbulence, speed, windDirection, color, color2;
        SerializedParameterOverride skyHaze, skySpeed, skyNoiseStrength, skyAlpha, scattering, scatteringColor;
        SerializedParameterOverride fogOfWarEnabled, fogOfWarCenter, fogOfWarSize, fogOfWarTextureSize;
        SerializedParameterOverride useDirectionalLightAsSun;

        public override void OnEnable() {
            titleColor = EditorGUIUtility.isProSkin ? new Color(0.52f, 0.66f, 0.9f) : new Color(0.12f, 0.16f, 0.4f);
            for (int k = 0; k < expandSection.Length; k++) {
                expandSection[k] = EditorPrefs.GetBool(SECTION_PREFS + k, false);
            }
            effectType = FindParameterOverride(x => x.effectType);
            enableDithering = FindParameterOverride(x => x.enableDithering);
            ditherStrength = FindParameterOverride(x => x.ditherStrength);
            alpha = FindParameterOverride(x => x.alpha);
            noiseStrength = FindParameterOverride(x => x.noiseStrength);
            noiseScale = FindParameterOverride(x => x.noiseScale);
            distance = FindParameterOverride(x => x.distance);
            distanceFallOff = FindParameterOverride(x => x.distanceFallOff);
            maxDistance = FindParameterOverride(x => x.maxDistance);
            maxDistanceFallOff = FindParameterOverride(x => x.maxDistanceFallOff);
            maxHeight = FindParameterOverride(x => x.maxHeight);
            height = FindParameterOverride(x => x.height);
            heightFallOff = FindParameterOverride(x => x.heightFallOff);
            baselineHeight = FindParameterOverride(x => x.baselineheight);
            clipUnderBaseline = FindParameterOverride(x => x.clipUnderBaseline);
            turbulence = FindParameterOverride(x => x.turbulence);
            speed = FindParameterOverride(x => x.speed);
            windDirection = FindParameterOverride(x => x.windDirection);

            color = FindParameterOverride(x => x.color);
            color2 = FindParameterOverride(x => x.color2);
            skyHaze = FindParameterOverride(x => x.skyHaze);
            skySpeed = FindParameterOverride(x => x.skySpeed);
            skyNoiseStrength = FindParameterOverride(x => x.skyNoiseStrength);
            skyAlpha = FindParameterOverride(x => x.skyAlpha);
            useDirectionalLightAsSun = FindParameterOverride(x => x.useDirectionalLightAsSun);
            scattering = FindParameterOverride(x => x.scattering);
            scatteringColor = FindParameterOverride(x => x.scatteringColor);

            fogOfWarEnabled = FindParameterOverride(x => x.fogOfWarEnabled);
            fogOfWarCenter = FindParameterOverride(x => x.fogOfWarCenter);
            fogOfWarSize = FindParameterOverride(x => x.fogOfWarSize);
            fogOfWarTextureSize = FindParameterOverride(x => x.fogOfWarTextureSize);
        }

        void OnDestroy() {
            // Save folding sections state
            for (int k = 0; k < expandSection.Length; k++) {
                EditorPrefs.SetBool(SECTION_PREFS + k, expandSection[k]);
            }
        }

        public override void OnInspectorGUI() {

            if (sectionHeaderStyle == null) {
                sectionHeaderStyle = new GUIStyle(EditorStyles.foldout);
            }
            sectionHeaderStyle.SetFoldoutColor();

            if (titleLabelStyle == null) {
                titleLabelStyle = new GUIStyle(EditorStyles.label);
            }
            titleLabelStyle.normal.textColor = titleColor;
            titleLabelStyle.fontStyle = FontStyle.Bold;


            EditorGUILayout.Separator();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("General Settings", titleLabelStyle);
            if (GUILayout.Button("Help", GUILayout.Width(40))) {
                if (!EditorUtility.DisplayDialog("Dynamic Fog & Mist", "To learn more about a property in this inspector move the mouse over the label for a quick description (tooltip).\n\nPlease check README file in the root of the asset for details and contact support.\n\nIf you like Dynamic Fog & Mist, please rate it on the Asset Store. For feedback and suggestions visit our support forum on kronnect.com.", "Close", "Visit Support Forum")) {
                    Application.OpenURL("http://kronnect.com/taptapgo");
                }
            }
            EditorGUILayout.EndHorizontal();

            //EditorGUILayout.IntPopup(effectType.value, FOG_TYPE_OPTIONS, FOG_TYPE_VALUES, new GUIContent("Effect Type", "Choose a shader variant. Each variant provides different capabilities. Read documentation for explanation."));
            PropertyField(effectType, new GUIContent("Effect Type", "Choose a shader variant. Each variant provides different capabilities. Read documentation for explanation."));
            switch (effectType.value.intValue) {
                case (int)FOG_TYPE.DesktopFogPlusWithSkyHaze:
                    EditorGUILayout.HelpBox("BEST IMMERSION. 5 step raymarching based fog effect which does not require geometry. Uses a more complex algorithm to simulate 3D noise in world space. Also adds sky haze at the background.", MessageType.Info);
                    break;
                case (int)FOG_TYPE.DesktopFogPlusOrthogonal:
                    EditorGUILayout.HelpBox("Variant of Desktop Plus With Sky Haze which treats distance fog and height fog separately. This variant does not add sky haze at the background.", MessageType.Info);
                    break;
                case (int)FOG_TYPE.DesktopFogWithSkyHaze:
                    EditorGUILayout.HelpBox("Depth based fog effect that lays out fog over existing geometry. Uses two noise textures and adds haze effect at the background to complete fog composition (geometry fog + sky fog).", MessageType.Info);
                    break;
                case (int)FOG_TYPE.MobileFogWithSkyHaze:
                    EditorGUILayout.HelpBox("Depth based fog effect over existing geometry. Similar to Desktop Fog with Sky, but uses one noise texture instead of two, and also adds haze effect at the background to complete fog composition (geometry fog + sky fog).", MessageType.Info);
                    break;
                case (int)FOG_TYPE.MobileFogOnlyGround:
                    EditorGUILayout.HelpBox("FASTEST, Depth based fog effect over existing geometry. Similar to Mobile Fog with Sky, but only affects geometry (no sky haze).", MessageType.Info);
                    break;
                case (int)FOG_TYPE.MobileFogSimple:
                    EditorGUILayout.HelpBox("GREAT PERFORMANCE/QUALITY. Similar to Desktop Fog Plus with Sky Haze but uses 3 steps instead of 5 and does not add sky haze at the background.", MessageType.Info);
                    break;
                case (int)FOG_TYPE.MobileFogOrthogonal:
                    EditorGUILayout.HelpBox("Variant of Mobile Fog (Simplified) which treats distance fog and height fog separately. Does not use noise textures. Does not adds sky haze at the background.", MessageType.Info);
                    break;
                case (int)FOG_TYPE.MobileFogBasic:
                    EditorGUILayout.HelpBox("FASTEST, Ray-marching variant. Uses only 1 step. Does not use noise textures. Does not adds sky haze at the background.", MessageType.Info);
                    break;
            }

            int effect = effectType.value.intValue;

            PropertyField(useDirectionalLightAsSun, new GUIContent("Use Directional Light", "Takes a directional light as the Sun light to make the fog color sync automatically with the Sun orientation and light intensity."));
            if (effect == (int)FOG_TYPE.DesktopFogPlusWithSkyHaze) {
                if (!useDirectionalLightAsSun.value.boolValue) {
                    EditorGUILayout.HelpBox("Light scattering requires a Sun reference.", MessageType.Info);
                    GUI.enabled = false;
                }
                PropertyField(scattering, new GUIContent("Light Scattering", "Amount of Sun light diffusion when it crosses the fog towards viewer."));
                PropertyField(scatteringColor, new GUIContent("   Scattering Color", "Tint color for the light scattering effect."));
                GUI.enabled = true;
            }

            PropertyField(enableDithering, new GUIContent("Enable Dithering", "Reduces banding artifacts."));
            if (enableDithering.value.boolValue) {
                PropertyField(ditherStrength, new GUIContent("   Dither Strength", "Intensity of dither blending."));
            }

            EditorGUILayout.Separator();
            expandSection[FOG_PROPERTIES] = EditorGUILayout.Foldout(expandSection[FOG_PROPERTIES], sectionNames[FOG_PROPERTIES], sectionHeaderStyle);

            if (expandSection[FOG_PROPERTIES]) {
                PropertyField(alpha, new GUIContent("Alpha", "Global fog transparency. You can also change the transparency at color level."));
                if (effect != 4 && effect != 5 && effect != 6) {
                    PropertyField(noiseStrength, new GUIContent("Noise Strength", "Set this value to zero to use solid colors."));
                    PropertyField(noiseScale, new GUIContent("Noise Scale", "Scale factor for sampling noise."));
                }
                PropertyField(distance, new GUIContent("Distance", "The starting distance of the fog measure in linear 0-1 values (0=camera near clip, 1=camera far clip)."));
                PropertyField(distanceFallOff, new GUIContent("Distance Fall Off", "Makes the fog appear smoothly on the near distance."));
                if (effect < 4)
                    PropertyField(maxDistance, new GUIContent("Max Distance", "The end distance of the fog measure in linear 0-1 values (0=camera near clip, 1=camera far clip)."));
                if (effect < 3) {
                    PropertyField(maxDistanceFallOff, new GUIContent("Distance Fall Off", "Makes the fog disappear smoothly on the far distance."));
                }

                if (effect == (int)FOG_TYPE.MobileFogOrthogonal || effect == (int)FOG_TYPE.DesktopFogPlusOrthogonal) {
                    PropertyField(maxHeight, new GUIContent("Max Height", "Max. height of the fog in meters."));
                }
                PropertyField(height, new GUIContent("Height", "Height of the fog in meters."));
                PropertyField(heightFallOff, new GUIContent("Height Fall Off", "Increase to make the fog change gradually its density based on height."));
                PropertyField(baselineHeight, new GUIContent("Baseline Height", "Vertical position of the fog in meters. Height is counted above this baseline height."));

                if (effect < 3) {
                    PropertyField(clipUnderBaseline, new GUIContent("Clip Under Baseline", "Enable this property to only render fog above baseline height."));
                    PropertyField(turbulence, new GUIContent("Turbulence", "Amount of fog turbulence."));
                }

                if (effect < 4 || effect == (int)FOG_TYPE.DesktopFogPlusOrthogonal) {
                    PropertyField(speed, new GUIContent("Speed", "Speed of fog animation if noise strength or turbulence > 0 (turbulence not available in Desktop Fog Plus mode)."));
                    PropertyField(windDirection, new GUIContent("Wind Direction", "Direction of the wind to take into account for the fog animation."));
                }

                PropertyField(color);
                if (effect != 4 && effect != 5)
                    PropertyField(color2);
            }

            if (effect != 2 && effect != 4 && effect != 5 && effect != (int)FOG_TYPE.DesktopFogPlusOrthogonal) {
                EditorGUILayout.Separator();
                expandSection[SKY_PROPERTIES] = EditorGUILayout.Foldout(expandSection[SKY_PROPERTIES], sectionNames[SKY_PROPERTIES], sectionHeaderStyle);

                if (expandSection[SKY_PROPERTIES]) {
                    PropertyField(skyHaze, new GUIContent("Haze", "Vertical range for the sky haze."));
                    PropertyField(skySpeed, new GUIContent("Speed", "Speed of sky haze animation."));
                    PropertyField(skyNoiseStrength, new GUIContent("Noise Strength", "Amount of noise for the sky haze effect."));
                    PropertyField(skyAlpha, new GUIContent("Alpha", "Transparency of sky haze."));
                }
            }

            EditorGUILayout.Separator();
            expandSection[FOG_OF_WAR] = EditorGUILayout.Foldout(expandSection[FOG_OF_WAR], sectionNames[FOG_OF_WAR], sectionHeaderStyle);

            if (expandSection[FOG_OF_WAR]) {
                PropertyField(fogOfWarEnabled, new GUIContent("Enabled", "Enables fog of war feature. This requires that you assign a fog of war mask texture at runtime. Read documentation or demo scene for details."));
                PropertyField(fogOfWarCenter, new GUIContent("Center", "World space position of the center of the fog of war mask texture."));
                PropertyField(fogOfWarSize, new GUIContent("Area Size", "Size of the fog of war area in world space units."));
                PropertyField(fogOfWarTextureSize, new GUIContent("Texture Size", "Size of the fog of war mask texture."));
            }
            EditorGUILayout.Separator();
        }

    }

}
