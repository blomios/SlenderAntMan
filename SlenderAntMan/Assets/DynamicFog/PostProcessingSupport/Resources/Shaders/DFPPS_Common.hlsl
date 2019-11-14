    #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"

    TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
	TEXTURE2D_SAMPLER2D(_CameraDepthTexture, sampler_CameraDepthTexture);
	float4 _MainTex_ST;
	half _FogAlpha;

    #if FOG_OF_WAR_ON 
    TEXTURE2D_SAMPLER2D(_FogOfWar, sampler_FogOfWar);
    float3 _FogOfWarCenter;
    float3 _FogOfWarSize;
    float3 _FogOfWarCenterAdjusted;
    #endif

    float4x4 _ClipToWorld;

	struct VaryingsDynamicFog  {
        float4 vertex : SV_POSITION;
        float2 uv : TEXCOORD0;
    	float2 depthUV : TEXCOORD1;
    	float3 cameraToFarPlane : TEXCOORD2;
	};

    VaryingsDynamicFog VertDynamicFog(AttributesDefault v) {
        VaryingsDynamicFog o;
        o.vertex = float4(v.vertex.xy, 0.0, 1.0);
        o.uv = TransformTriangleVertexToUV(v.vertex.xy);
        #if UNITY_UV_STARTS_AT_TOP
            o.uv = o.uv * float2(1.0, -1.0) + float2(0.0, 1.0);
        #endif
        o.uv = TransformStereoScreenSpaceTex(o.uv, 1.0);
        o.depthUV = o.uv;
   	      
    	// Clip space X and Y coords
    	float2 clipXY = o.vertex.xy / o.vertex.w;
               
    	// Position of the far plane in clip space
    	float4 farPlaneClip = float4(clipXY, 1, 1);
               
    	// Homogeneous world position on the far plane
    	farPlaneClip *= float4(1,_ProjectionParams.x,1,1);    	

   		#if UNITY_SINGLE_PASS_STEREO
		_ClipToWorld = mul(_ClipToWorld, unity_StereoCameraInvProjection[unity_StereoEyeIndex]);
    	//_ClipToWorld = mul(_ClipToWorld, unity_CameraInvProjection);
    	#endif
    	float4 farPlaneWorld4 = mul(_ClipToWorld, farPlaneClip);
               
    	// World position on the far plane
    	float3 farPlaneWorld = farPlaneWorld4.xyz / farPlaneWorld4.w;
               
    	// Vector from the camera to the far plane
    	o.cameraToFarPlane = farPlaneWorld - _WorldSpaceCameraPos;
    	return o;
	}

	
    float4 _MainTex_TexelSize;
    half  _FogDither;

    void ApplyColor(float2 uv, inout half4 color) {
        float dither = dot(float2(2.4084507, 3.2535211), uv * _MainTex_TexelSize.zw);
        dither = frac(dither) - 0.4;
        color *= 1.0 + dither * _FogDither;
    }
