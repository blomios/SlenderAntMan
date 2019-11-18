Shader "DynamicFog/Image Effect PPS/Desktop Fog Plus Orthogonal"
{
	Properties{
	_Noise3DTex("Noise 3D (RGB)", 2D) = "white" {}
	}
    HLSLINCLUDE

        #pragma multi_compile __ FOG_OF_WAR_ON
        #pragma multi_compile __ DITHER_ON
        #include "DFPPS_Common.hlsl"

   #define DYNAMIC_FOG_STEPS 5

    TEXTURE2D_SAMPLER2D(_Noise3DTex, sampler_Noise3DTex);
    float4 _FogDistance; // x = min distance, y = min distance falloff, x = max distance, y = max distance fall off
    float4 _FogHeightData;
    float4 _FogNoiseData; // x = noise, y = turbulence, z = depth attenuation
    float4 _FogSkyData; // x = haze, y = speed, z = noise, w = alpha
    float3 _FogSpeed;
    half4 _FogColor, _FogColor2;
    float3 wsCameraPos;
    
    inline float noise3D(float3 x ) {
        float3 f = frac(x);
        float3 p = x - f;
        f = f*f*(3.0-2.0*f);
        float2 xy = p.xy + float2(37.0,17.0)*p.z + f.xy;
        xy = (xy + 0.5) / 256.0;
        float2 zz = SAMPLE_TEXTURE2D_LOD(_Noise3DTex, sampler_Noise3DTex, xy, 0).yx;
        return lerp( zz.x, zz.y, f.z );
    }
        
    inline float3 getWorldPos(VaryingsDynamicFog i, float depth01) {
        // Reconstruct the world position of the pixel
        wsCameraPos = float3(_WorldSpaceCameraPos.x, _WorldSpaceCameraPos.y - _FogHeightData.y, _WorldSpaceCameraPos.z);
        float3 worldPos = (i.cameraToFarPlane * depth01) + wsCameraPos;
        worldPos.y += 0.00001; // fixes artifacts when worldPos.y = _WorldSpaceCameraPos.y which is really rare but occurs at y = 0
        return worldPos;
    }

    inline half4 getFogColor(float2 uv, float3 worldPos, float depth, half4 color) {
        
        // early exit if fog is not crossed
        if (wsCameraPos.y>_FogHeightData.z && worldPos.y>_FogHeightData.z) {
            return color;       
        }

        half voidAlpha = _FogAlpha;

        // Determine "fog length" and initial ray position between object and camera, cutting by fog distance params
        float3 adir = worldPos - wsCameraPos;
        
        // ceiling cut
        float delta = length(adir.xz);
        float dist = delta;
        float2 ndirxz = adir.xz / delta;
        delta /= adir.y;
        
        float h = min(wsCameraPos.y, _FogHeightData.z);
        float xh = delta * (wsCameraPos.y - h);
        float2 xz = wsCameraPos.xz - ndirxz * xh;
        float3 fogCeilingCut = float3(xz.x, h, xz.y);
        
        // does fog stars after pixel? If it does, exit now
        float distanceToFog = distance(fogCeilingCut.xz, wsCameraPos.xz);
        if (distanceToFog>=dist) return color;

        // floor cut
        float hf = 0;
        // edge cases
        if (delta>0 && worldPos.y > -0.5) {
            hf = _FogHeightData.z;
        } else if (delta<0 && worldPos.y < 0.5) {
            hf = worldPos.y;
        }
        float xf = delta * ( hf - wsCameraPos.y ); 
        float2 xzb = wsCameraPos.xz - ndirxz * xf;
        float3 fogFloorCut = float3(xzb.x, hf, xzb.y);

        // fog length is...
        float fogLength = distance(fogCeilingCut.xz, fogFloorCut.xz);
        fogLength = min(fogLength, dist - distanceToFog);
        if (fogLength<=0) return color;
        fogFloorCut = fogCeilingCut + (adir/dist) * fogLength;
        
        #if FOG_OF_WAR_ON
        if (depth<0.999) {
            float2 fogTexCoord = fogFloorCut.xz / _FogOfWarSize.xz - _FogOfWarCenterAdjusted.xz;
            voidAlpha *= SAMPLE_TEXTURE2D(_FogOfWar, sampler_FogOfWar, fogTexCoord).a;
            if (voidAlpha <=0) return color;
        }
        #endif
        
        float3 st = (fogFloorCut - fogCeilingCut) / DYNAMIC_FOG_STEPS;
        float3 pos = fogCeilingCut;
        half4 fogColor = half4(0,0,0,0);
        float incDist = fogLength / DYNAMIC_FOG_STEPS;
        UNITY_UNROLL
        for (int k=DYNAMIC_FOG_STEPS;k>=0;k--, pos += st, distanceToFog += incDist) {
            float fh = (_FogHeightData.x - pos.y) / (_FogHeightData.x * _FogHeightData.w) - 0.1;
            float fl = (distanceToFog - _FogDistance.x) / _FogDistance.y;
            fh = max(fh, fl);
            float noise = noise3D(pos * _FogNoiseData.w + _Time.www * _FogSpeed);
            half4 col = lerp(_FogColor, _FogColor2, saturate( pos.y / _FogHeightData.x) );
            col.a *= saturate ( fh * (1.0 - noise * _FogNoiseData.x ));
            col.rgb *= col.a;
            fogColor += col * (1.0 - fogColor.a);
        }
        fogColor *= voidAlpha;
        return color * (1.0 - fogColor.a) + fogColor;
    }

    // Fragment Shader
    half4 Frag (VaryingsDynamicFog i) : SV_Target {
        half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
        float depth = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, i.depthUV));
        float3 worldPos = getWorldPos(i, depth);
        color = getFogColor(i.uv, worldPos, depth, color);
        #if DITHER_ON
        ApplyColor(i.uv, color);
        #endif
        return color;
    }

    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM

                #pragma vertex VertDynamicFog
                #pragma fragment Frag

            ENDHLSL
        }
    }
}
    

