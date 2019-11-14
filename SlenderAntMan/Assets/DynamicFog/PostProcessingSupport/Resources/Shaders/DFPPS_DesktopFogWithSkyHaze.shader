Shader "DynamicFog/Image Effect PPS/Desktop Fog With Sky Haze"
{
	Properties{
		_NoiseTex("Noise (RGB)", 2D) = "white" {}
		_Noise2Tex("Noise 2 (RGB)", 2D) = "white" {}
	}
    HLSLINCLUDE

        #pragma multi_compile __ FOG_OF_WAR_ON
        #pragma multi_compile __ DITHER_ON
        #include "DFPPS_Common.hlsl"


    TEXTURE2D_SAMPLER2D(_NoiseTex, sampler_NoiseTex);
    TEXTURE2D_SAMPLER2D(_Noise2Tex, sampler_Noise2Tex);

    float4 _FogDistance; // x = min distance, y = min distance falloff, x = max distance, y = max distance fall off
    float4 _FogHeightData;
    float4 _FogNoiseData; // x = noise, y = turbulence, z = depth attenuation
    float4 _FogSkyData; // x = haze, y = speed, z = noise, w = alpha
    float3 _FogSpeed;
    half4 _FogColor, _FogColor2;

    inline half4 computeSkyColor(half4 color, float3 worldPos) {
        float wpy = abs(worldPos.y) + 2.0;
        float2 np = worldPos.xz/wpy;
        float skyNoise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, np * 0.01 +_Time.x * _FogSkyData.y).g;
        half4 skyFogColor = lerp(_FogColor, _FogColor2, saturate(wpy / _FogHeightData.x));
        return lerp(color, skyFogColor, _FogSkyData.w * saturate((_FogSkyData.x / wpy)*(1-skyNoise*_FogSkyData.z)));
    }
    
    inline half4 computeGroundColor(half4 color, float3 worldPos, float depth) {
    
        #if FOG_OF_WAR_ON
        half voidAlpha = 1.0;
        float2 fogTexCoord = worldPos.xz / _FogOfWarSize.xz - _FogOfWarCenterAdjusted.xz;
        voidAlpha = SAMPLE_TEXTURE2D(_FogOfWar, sampler_FogOfWar, fogTexCoord).a;
        if (voidAlpha <=0) return color;
        #endif
    
        // Compute noise
        float2 xzr = worldPos.xz * (_FogNoiseData.w * 0.1) + _Time.yy * _FogSpeed.xz;
        float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, xzr).g;
        float noise2 = SAMPLE_TEXTURE2D(_Noise2Tex, sampler_Noise2Tex, xzr).g;
        noise = noise*noise2;
        float nt = noise * _FogNoiseData.y; //FogTurbulence;
        noise /= (depth*_FogNoiseData.z); // far clip correction with depth atten

        // Compute ground fog color     
        worldPos.y -= nt;
        float d = (depth - _FogDistance.x) / _FogDistance.y;
        float dmax = (_FogDistance.z - depth) / _FogDistance.w;
        d = min(d, dmax);
        float fogHeight = _FogHeightData.x + nt;
        float h = (fogHeight - worldPos.y) / (fogHeight*_FogHeightData.w);
        float groundColor = saturate(min(d,h))*saturate(_FogAlpha*(1-noise*_FogNoiseData.x));   
    
        #if FOG_OF_WAR_ON
        groundColor *= voidAlpha;
        #endif
        
        half4 fogColor = lerp(_FogColor, _FogColor2, saturate(worldPos.y / fogHeight));
        return lerp(color, fogColor, groundColor);
    }

    // Fragment Shader
     half4 Frag (VaryingsDynamicFog i) : SV_Target {
        half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
        float depth = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, i.depthUV));

        if (depth>0.999) {
            float3 worldPos = (i.cameraToFarPlane * depth) + _WorldSpaceCameraPos;
            color = computeSkyColor(color, worldPos);
        } else if (depth<_FogDistance.z) {
            float3 worldPos = (i.cameraToFarPlane * depth) + _WorldSpaceCameraPos;
            worldPos.y -= _FogHeightData.y + 0.00001;
            if (worldPos.y>_FogHeightData.z && worldPos.y<_FogHeightData.x+_FogNoiseData.y) color = computeGroundColor(color, worldPos, depth);
        }

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
    

