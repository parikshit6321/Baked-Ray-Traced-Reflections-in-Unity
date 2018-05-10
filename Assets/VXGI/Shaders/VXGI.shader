// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Hidden/VXGI"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}

	CGINCLUDE

	#include "UnityCG.cginc"

	// Structure representing an individual voxel element
	struct Voxel
	{
		int data;
	};

	// Structured buffer containing the data for the specular voxel grid
	uniform StructuredBuffer<Voxel> _VoxelVolumeBufferSpecular;

	// Main texture for the composite pass
	uniform sampler2D				_MainTex;

	// Depth texture of the current camera
	uniform sampler2D				_CameraDepthTexture;

	// DepthNormals texture of the current camera
	uniform sampler2D				_CameraDepthNormalsTexture;

	// Indirect specular lighting texture for the composite pass
	uniform sampler2D				_IndirectSpecular;

	// Inverse projection Matrix of the current camera.
	uniform float4x4				InverseProjectionMatrix;

	// Inverse view matrix of the current camera.
	uniform float4x4				InverseViewMatrix;

	// Texel size used for calculating offset during blurring
	uniform float4					_MainTex_TexelSize;

	// World space position of the current camera.
	uniform float3					mainCameraPosition;

	// Variable denoting the dimensions of the specular voxel grid
	uniform int						_VoxelVolumeDimensionSpecular;

	// Variable denoting the boundary of the world volume which has been voxelized
	uniform int						_WorldVolumeBoundary;

	// Strength of the direct lighting
	uniform float					_DirectStrength;

	// Strength of the indirect specular lighting
	uniform float					_IndirectSpecularStrength;

	// Maximum number of iterations in indirect specular cone tracing pass
	uniform float					_MaximumIterations;

	// Step value for indirect specular cone tracing pass
	uniform float					_ConeStep;

	// Angle for the cone tracing step in indirect diffuse lighting
	uniform float					_ConeAngle;

	// Offset value for indirect specular cone tracing pass
	uniform float					_ConeOffset;

	// Step value used for blurring
	uniform float					_BlurStep;

	// Threshold for masking reflective surfaces
	uniform float					threshold;

	// Structure representing the input to the vertex shader
	struct appdata
	{
		float4 vertex : POSITION;
		float2 uv : TEXCOORD0;
	};

	// Structure representing the input to the composite pass fragment shaders
	struct v2f_composite
	{
		float2 uv : TEXCOORD0;
		float4 vertex : SV_POSITION;
	};

	// Structure representing the input to the indirect lighting fragment shaders
	struct v2f_render
	{
		float2 uv : TEXCOORD0;
		float4 vertex : SV_POSITION;
		float4 cameraRay : TEXCOORD1;
	};

	// Structure representing the input to the fragment shader of blur pass
	struct v2f_blur
	{
		float2 uv : TEXCOORD0;
		float4 vertex : SV_POSITION;
		float2 offset1 : TEXCOORD1;
		float2 offset2 : TEXCOORD2;
		float2 offset3 : TEXCOORD3;
		float2 offset4 : TEXCOORD4;
	};

	// Vertex shader for the horizontal blurring pass
	v2f_blur vert_horizontal_blur(appdata v)
	{
		half unitX = _MainTex_TexelSize.x * _BlurStep;

		v2f_blur o;

		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = v.uv;

		o.offset1 = half2(-2.0 * unitX, 0.0);
		o.offset2 = half2(-unitX, 0.0);
		o.offset3 = half2(unitX, 0.0);
		o.offset4 = half2(2.0 * unitX, 0.0);

		return o;
	}

	// Vertex shader for the vertical blurring pass
	v2f_blur vert_vertical_blur(appdata v)
	{
		half unitY = _MainTex_TexelSize.y * _BlurStep;

		v2f_blur o;

		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = v.uv;

		o.offset1 = half2(0.0, 2.0 * unitY);
		o.offset2 = half2(0.0, unitY);
		o.offset3 = half2(0.0, -unitY);
		o.offset4 = half2(0.0, -2.0 * unitY);

		return o;
	}

	// Fragment shader for the blur pass
	float4 frag_blur(v2f_blur i) : SV_Target
	{
		float4 col = tex2D(_MainTex, i.uv);
		col += tex2D(_MainTex, i.uv + i.offset1);
		col += tex2D(_MainTex, i.uv + i.offset2);
		col += tex2D(_MainTex, i.uv + i.offset3);
		col += tex2D(_MainTex, i.uv + i.offset4);

		col *= 0.2;

		return col;
	}

	// Vertex shader for the indirect diffuse lighting pass
	v2f_render vert_indirect(appdata v)
	{
		v2f_render o;
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = v.uv;

		//transform clip pos to view space
		float4 clipPos = half4( v.uv * 2.0 - 1.0, 1.0, 1.0);
		float4 cameraRay = mul(InverseProjectionMatrix, clipPos);
		o.cameraRay = cameraRay / cameraRay.w;

		return o;
	}

	// Function used to unpack the data of the current voxel
	inline float4 UnpackData(int packedData)
	{
		int occupied = packedData & 1;
		packedData = packedData >> 8;
		int colorB = packedData & 255;
		packedData = packedData >> 8;
		int colorG = packedData & 255;
		packedData = packedData >> 8;
		int colorR = packedData & 255;

		return float4(((float)colorR / 255.0), ((float)colorG / 255.0), ((float)colorB / 255.0), (float)occupied);
	}

	// Returns the information (xyz - directLightingColor; w - occupied flag ( 0 - Not Occupied ; 1 - Occupied)) of the voxel at the given world position from the specular voxel grid
	inline float4 GetVoxelInfoSpecular(float3 worldPosition)
	{
		// Default value
		float4 info = float4(0.0, 0.0, 0.0, 0.0);

		// Check if the given position is inside the voxelized volume
		if ((abs(worldPosition.x) < _WorldVolumeBoundary) && (abs(worldPosition.y) < _WorldVolumeBoundary) && (abs(worldPosition.z) < _WorldVolumeBoundary))
		{
			worldPosition += _WorldVolumeBoundary;
			worldPosition /= (2.0 * _WorldVolumeBoundary);

			float3 temp = worldPosition * _VoxelVolumeDimensionSpecular;

			int3 voxelPosition = (int3)(temp);

			int index = (voxelPosition.x * _VoxelVolumeDimensionSpecular * _VoxelVolumeDimensionSpecular) + (voxelPosition.y * _VoxelVolumeDimensionSpecular) + (voxelPosition.z);

			info = UnpackData(_VoxelVolumeBufferSpecular[index].data);
		}

		return info;
	}

	// Traces a ra starting from the current voxel in the reflected ray direction and accumulates color
	inline float4 RayTrace(float3 worldPosition, float3 reflectedRayDirection, float3 pixelColor)
	{
		// Color for storing all the samples
		float3 accumulatedColor = float3(0.0, 0.0, 0.0);

		float3 currentPosition = worldPosition + (_ConeOffset * reflectedRayDirection);
		float4 currentVoxelInfo = float4(0.0, 0.0, 0.0, 0.0);

		float currentWeight = 1.0;
		float totalWeight = 0.0;

		bool hitFound = false;

		// Loop for tracing the ray through the scene
		for (float i = 0.0; i < _MaximumIterations; i += 1.0)
		{
			// Traverse the ray in the reflected direction
			currentPosition += (reflectedRayDirection * _ConeStep);

			// Get the currently hit voxel's information
			currentVoxelInfo = GetVoxelInfoSpecular(currentPosition);

			// At the currently traced sample
			if (((int)currentVoxelInfo.w > 0.0) && (!hitFound))
			{
				accumulatedColor += (currentVoxelInfo.xyz * pixelColor);
				totalWeight += currentWeight;
				hitFound = true;
			}
		}

		// Average out the accumulated color
		accumulatedColor /= totalWeight;

		return float4(accumulatedColor, 1.0);
	}

	inline float3 DecodePosition(float3 inputPosition)
	{

		float3 decodedPosition = inputPosition * (2.0f * _WorldVolumeBoundary);
		decodedPosition.x -= _WorldVolumeBoundary;
		decodedPosition.y -= _WorldVolumeBoundary;
		decodedPosition.z -= _WorldVolumeBoundary;
		return decodedPosition;

	}

	// Fragment shader for the indirect specular lighting pass
	float4 frag_indirect_specular(v2f_render i) : SV_Target
	{
		// Color which will be accumulated during the cone tracing pass
		float4 accumulatedColor = float4(0.0, 0.0, 0.0, 1.0);

		// Extract the current pixel's color
		float3 pixelColor = tex2D(_MainTex, i.uv).rgb;

		// read low res depth and reconstruct world position
		float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
		
		//linearise depth		
		float lindepth = Linear01Depth (depth);
		
		//get view and then world positions		
		float4 viewPos = float4(i.cameraRay.xyz * lindepth,1);
		float3 worldPosition = mul(InverseViewMatrix, viewPos).xyz;
		
		// Compute the current pixel to camera unit vector
		float3 pixelToCameraUnitVector = normalize(mainCameraPosition - worldPosition);

		// Extract the information of the current pixel from the voxel grid
		half depthValue;
		half3 viewSpaceNormal;
		DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, i.uv), depthValue, viewSpaceNormal);
		viewSpaceNormal = normalize(viewSpaceNormal);
		half3 pixelNormal = mul((half3x3)InverseViewMatrix, viewSpaceNormal);

		// Compute the reflected ray direction
		float3 reflectedRayDirection = normalize(reflect(pixelToCameraUnitVector, pixelNormal));

		reflectedRayDirection *= -1.0;

		// Perform the cone tracing step
		accumulatedColor = RayTrace(worldPosition, reflectedRayDirection, pixelColor);

		// Return the final color accumulated after tracing rays
		return accumulatedColor;
	}

	// Vertex shader for the voxelization debug pass
	v2f_render vert_voxelization(appdata v)
	{
		v2f_render o;
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = v.uv;

		//transform clip pos to view space
		float4 clipPos = half4( v.uv * 2.0 - 1.0, 1.0, 1.0);
		float4 cameraRay = mul(InverseProjectionMatrix, clipPos);
		o.cameraRay = cameraRay / cameraRay.w;

		return o;
	}

	// Fragment shader for the voxelization debug pass
	float4 frag_voxelization(v2f_render i) : SV_Target
	{
		// read low res depth and reconstruct world position
		float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
		
		//linearise depth		
		float lindepth = Linear01Depth (depth);
		
		//get view and then world positions		
		float4 viewPos = float4(i.cameraRay.xyz * lindepth,1);
		float3 worldPosition = mul(InverseViewMatrix, viewPos).xyz;
		
		return float4(GetVoxelInfoSpecular(worldPosition).xyz, 1.0);
	}

	// Vertex shader for the composite pass
	v2f_composite vert_composite(appdata v)
	{
		v2f_composite o;
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = v.uv;
		return o;
	}

	// Fragment shader for the indirect specular composite pass
	float4 frag_composite_specular(v2f_composite i) : SV_Target
	{
		float4 directLighting = tex2D(_MainTex, i.uv) * _DirectStrength;
		float4 indirectSpecularLighting = tex2D(_IndirectSpecular, i.uv) * _IndirectSpecularStrength;

		float4 finalColor = directLighting + indirectSpecularLighting;

		return finalColor;
	}

	ENDCG

	SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

		// 0 : Indirect Specular Lighting Pass
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_indirect
			#pragma fragment frag_indirect_specular
			#pragma target 5.0
			ENDCG
		}

		// 1 : Composite Pass for indirect specular lighting
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_composite
			#pragma fragment frag_composite_specular
			#pragma target 5.0
			ENDCG
		}

		// 2 : Vertical Blurring
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_vertical_blur
			#pragma fragment frag_blur
			#pragma target 5.0
			ENDCG
		}

		// 3 : Horizontal Blurring
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_horizontal_blur
			#pragma fragment frag_blur
			#pragma target 5.0
			ENDCG
		}

		// 4 : Voxelization Debug Pass
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_voxelization
			#pragma fragment frag_voxelization
			#pragma target 5.0
			ENDCG
		}
	}
}