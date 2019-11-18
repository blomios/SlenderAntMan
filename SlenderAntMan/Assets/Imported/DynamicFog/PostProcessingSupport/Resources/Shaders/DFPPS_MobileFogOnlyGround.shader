Shader "DynamicFog/Image Effect PPS/Mobile Fog Only Ground"
{
	Properties{
		_NoiseTex("Noise (RGB)", 2D) = "white" {}
	}
    HLSLINCLUDE

        #pragma multi_compile __ FOG_OF_WAR_ON
        #pragma multi_compile __ DITHER_ON
        #include "DFPPS_Common.hlsl"

		TEXTURE2D_SAMPLER2D(_NoiseTex, sampler_NoiseTex);
    float4 _FogDistance; // x = min distance, y = min distance falloff, x = max distance, y = max distance fall off
    float4 _FogHeightData;
    float4 _FogNoiseData; // x = noise, y = turbulence, z = depth attenuation
    float3 _FogSpeed;
    half4 _FogColor, _FogColor2;

    //Fragment Shader
     half4 Frag (VaryingsDynamicFog i) : SV_Target {
        half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

        // Reconstruct the world position of the pixel
        float depth = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, i.depthUV));
        if (depth > _FogDistance.z) return color;
        
        float3 worldPos = (i.cameraToFarPlane * depth) + _WorldSpaceCameraPos;
        worldPos.y -= _FogHeightData.y;
        if (worldPos.y<_FogHeightData.z || worldPos.y>_FogHeightData.x+_FogNoiseData.y) return color;

        #if FOG_OF_WAR_ON
        half voidAlpha = 1.0;
        float2 fogTexCoord = worldPos.xz / _FogOfWarSize.xz - _FogOfWarCenterAdjusted.xz;
        voidAlpha = SAMPLE_TEXTURE2D(_FogOfWar, sampler_FogOfWar, fogTexCoord).a;
        if (voidAlpha <=0) return color;
        #endif
        
        // Compute noise
        float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, worldPos.xz * (_FogNoiseData.w * 0.1) + _Time.yy * _FogSpeed.xz).g;
        float nt = noise * _FogNoiseData.y;
        noise /= (depth*_FogNoiseData.z); // attenuate with distance

        // Compute ground fog color     
        worldPos.y -= nt;
        float d = (depth-_FogDistance.x)/_FogDistance.y;
        float dmax = (_FogDistance.z - depth) / _FogDistance.w;
        d = min(d, dmax);
        float fogHeight = _FogHeightData.x + nt;
        float h = (fogHeight - worldPos.y) / (fogHeight*_FogHeightData.w);
        float groundColor = saturate(min(d,h))*saturate(_FogAlpha*(1-noise*_FogNoiseData.x));
        
        #if FOG_OF_WAR_ON
        groundColor *= voidAlpha;
        #endif
        
        // Compute final blended fog color
        half4 fogColor = lerp(_FogColor, _FogColor2, saturate(worldPos.y / fogHeight));
        color = lerp(color, fogColor, groundColor);
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
    

