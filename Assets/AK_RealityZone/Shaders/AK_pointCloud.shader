// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

//======= Copyright (c) Stereolabs Corporation, All rights reserved. ===============
//Displays point cloud though geometry
Shader "Custom/AK_pointCloud"
{
	Properties
	{
		_MainTex("Texture",2D) = "white"{}
		_ColorTex("Texture",2D) = "white"{}
		_DepthTex("Texture",2D) = "white"{}
		_DistortionMapTex("Texture",2D) = "white"{}
		_Size("Size", Range(0,2)) = 0.1
		_Play("Play", Range(-2,2)) = 0

		_Delta("Delta", Range(0,50)) = 0
		_Beta("Beta", Range(-0.5,0.5)) = 0
		_Thresh("Thresh", Range(0,10)) = 0.01
		
		_MinX("MinX", Range(-20,20)) = -2
		_MaxX("MaxX", Range(-20,20)) = 2
		_MinY("MinY", Range(-20,20)) = -2
		_MaxY("MaxY", Range(-20,20)) = 2
		_MinZ("MinZ", Range(-20,20)) = -2
		_MaxZ("MaxZ", Range(-20,20)) = 2
		_Xoffset("Xoffset", Range(-20,20)) = 0
		_Yoffset("Yoffset", Range(-20,20)) = 0
		_Zoffset("Zoffset", Range(-20,20)) = 0

	}
		SubShader
		{


			Pass
			{
				Tags{"RenderType"="Opaque"
				"Queue"="1000"}
				ZWrite On
				//ZTest Always

				Cull Off
				CGPROGRAM
				#pragma target 5.0
				#pragma vertex vert
				#pragma geometry geom
				#pragma fragment frag


				#include "UnityCG.cginc"


				struct PS_INPUT
				{
					float4 position : SV_POSITION;
					float2 color_uv : TEXCOORD0;
					float3 normal : NORMAL;
					float depth : PDEPTH;
					//float size : PSIZE;

				};


				struct GEOM_OUTPUT
				{
					float4 position : SV_POSITION;
					float2 color_uv : TEXCOORD0;
					float3 normal : NORMAL;
					//float size : PSIZE;
				};


				//the three textures we need to do the calculations of point position and color:
				sampler2D _MainTex;
				float4 _MainTex_TexelSize;

				sampler2D _ColorTex;
				float4 _ColorTex_TexelSize;

				sampler2D _DepthTex;
				float4 _DepthTex_TexelSize;

				sampler2D _DistortionMapTex;
				float4 _DistortionMapTex_TexelSize;

				//the size of the output point in the pointlcoud (these are little quads basically)
				float _Size;

				//matrix describing the position of the object this is attached to. So the pointcloud moves with the object.
				float4x4 _Position;

				float _Play;


				//information about the color camera:
				float4x4 _color_extrinsics;
				float _color_cx;
				float _color_cy;
				float _color_fx;
				float _color_fy;
				float _color_k1;
				float _color_k2;
				float _color_k3;
				float _color_k4;
				float _color_k5;
				float _color_k6;
				float _color_codx;
				float _color_cody;
				float _color_p1;
				float _color_p2;
				float _color_metric_radius;





				/*
				uniform float4x4 modelMatrix;
				uniform float4x4 colorExtrinsicMatrix;

				float _depth_cx;
				float _depth_cy;
				float _depth_fx;
				float _depth_fy;

				float _color_cx;
				float _color_cy;
				float _color_fx;
				float _color_fy;






				sampler2D _MainTex;
				float4 _MainTex_ST;
				float _Delta;
				float _Beta;
				float _Thresh;
				float _MinX;
				float _MaxX;
				float _MinY;
				float _MaxY;
				float _MinZ;
				float _MaxZ;
				float _Xoffset;
				float _Yoffset;
				float _Zoffset;



				sampler2D _XYZTex;
				sampler2D _ColorTex;
				float4 _XYZTex_TexelSize;
				float4x4 _Position;

				float _Size;
				*/

				PS_INPUT vert(appdata_full v, uint vertex_id : SV_VertexID, uint instance_id : SV_InstanceID)
				{
					PS_INPUT o;
					o.normal = v.normal;

					//this converts the vertex_id into a uv position
					//stolen from the ZED shader
					// this is notably very brittle and should somehow incorporate the actual position of
					// the vert instead of just assuming that there's 1 vert per depth pixel
					uint id = vertex_id;
					float _u = fmod(id, _DepthTex_TexelSize.z) * _DepthTex_TexelSize.x;
					float _v = (id - fmod(id, _DepthTex_TexelSize.z)) * _DepthTex_TexelSize.x * _DepthTex_TexelSize.y;
					float2 uv = float2(_u, _v);
						//clamp(u, _DepthTex_TexelSize.x, 1.0 - _DepthTex_TexelSize.x),
						//clamp(((id - fmod(id, _DepthTex_TexelSize.z) * _DepthTex_TexelSize.x) / _DepthTex_TexelSize.z) * _DepthTex_TexelSize.y, _DepthTex_TexelSize.y, 1.0 - _DepthTex_TexelSize.y)
						//);
					float4 uv4 = float4(uv.x, uv.y, 0.0, 0.0);

					//get XYZ position:
					float4 XYZpos = float4(0.0, 0.0, 0.0, 1.0);
					float depth = tex2Dlod(_DepthTex, uv4)*65.536f;
					if (isnan(depth)) {
						depth = 0.0;
					}
					float4 distortionCorrection = tex2Dlod(_DistortionMapTex, uv4);

					XYZpos.x = depth * distortionCorrection.x ;
					XYZpos.y = depth * distortionCorrection.y ;
					XYZpos.z = depth;
					o.depth = depth;

					//get color info:
					
					//multiply point by color camera extrnsic matrix:
					float4 colorPos = float4(0.0, 0.0, 0.0, 1.0);
					colorPos = mul(_color_extrinsics, XYZpos);
					colorPos = colorPos / colorPos.w;
					colorPos.y = colorPos.y;
					XYZpos.y = -XYZpos.y;

					//project into color camera pixel space, without correcting for distortion
					float4 color_uv = float4(0.0f, 0.0f, 0.0f, 0.0f);
					color_uv.x = colorPos.x / colorPos.z*_color_fx + _color_cx;
					color_uv.y = colorPos.y / colorPos.z*_color_fy + _color_cy;

					//perform radial distortion of camera so we grab the right pixel:
					float x = colorPos.x / colorPos.z;
					float y = colorPos.y / colorPos.z;
					float r2 = pow(x, 2) + pow(y, 2);
					float r4 = pow(r2, 2);
					float r6 = pow(r2, 3);
					float r8 = pow(r2, 4);
					float r10 = pow(r2, 5);
					float r12 = pow(r2, 6);

					//float dx = x * (1 + _color_k1 * r2 + _color_k2 * r4 + _color_k3 * r6);
					//float dx = x * (1 + _color_k1 * r2 + _color_k2 * r4 + _color_k3 * r6 + _color_k4 * r8 + _color_k5 * r10 + _color_k6 * r12);
					float dx = x;
					//dx = dx + x*(_color_k1 * r2 + _color_k2 * r4 + _color_k3 * r6 + _color_k4 * r8 + _color_k5 * r10 + _color_k6 * r12);
					dx = x * (1 + _color_k1 * r2 + _color_k2 * r4 + _color_k3 * r6)/(1 + _color_k4 * r2 + _color_k5 * r4 + _color_k6 * r6);
					dx = dx + 2*_color_p1*x*y + _color_p2*(r2 + 2*pow(x, 2));
					dx = dx * _color_fx + _color_cx;

					//float dy = y * (1 + _color_k1 * r2 + _color_k2 * r4 + _color_k3 * r6);
					//float dy = y * (1 + _color_k1 * r2 + _color_k2 * r4 + _color_k3 * r6 + _color_k4 * r8 + _color_k5 * r10 + _color_k6 * r12);
					float dy = y;
					//dy = dy + y*(_color_k1 * r2 + _color_k2 * r4 + _color_k3 * r6 + _color_k4 * r8 + _color_k5 * r10 + _color_k6 * r12);
					dy = y * (1 + _color_k1 * r2 + _color_k2 * r4 + _color_k3 * r6) / (1 + _color_k4 * r2 + _color_k5 * r4 + _color_k6 * r6);
					dy = dy + 2*_color_p2*x*y + _color_p1*(r2 + 2*pow(y, 2));
					dy = dy * _color_fy + _color_cy;
					
					//divide by color texture width and heigh to get proper uv's between 0 and 1.
					//color_uv.x = color_uv.x * _ColorTex_TexelSize.x;
					//color_uv.y = _Play - (color_uv.y * _ColorTex_TexelSize.y);
					//color_uv.y = _Play;
					color_uv.x = dx * _ColorTex_TexelSize.x;
					color_uv.y = dy * _ColorTex_TexelSize.y;



					//float4 color_uv = float4(uv.x, uv.y, 0.0f, 0.0f);
					//o.color = float4(tex2Dlod(_ColorTex, color_uv).rgb, 0.5f);
					//o.color = float4(1,1,1,1);
					o.color_uv = float2(color_uv.x, color_uv.y);

					/*
					if (color_uv.x < 0 || color_uv.x >1.0 || color_uv.y < 0.0 || color_uv.y>1.0) {
					//if ((colorPos.x / colorPos.z*_color_fx + _color_cx)* _ColorTex_TexelSize.x < _Play) {
					//if ((colorPos.y / colorPos.z*_color_fy + _color_cy)* _ColorTex_TexelSize.y < _Play) {
					//if(color_uv.y < _Play){
						o.color.r = 0.0;
						o.color.g = 0.0;
						o.color.b = 0.0;
						//o.color.a = 0.0;
					}
					*/
					

					
					/*
					if (_color_cx < _Play) {
						o.color.r = 1.0;
						o.color.g = 0.0;
						o.color.b = 0.0;

					}
					else {
						o.color.r = 0.0;
						o.color.g = 1.0;
						o.color.b = 1.0;
					}
					*/

					/*
					if (depth < _Play) {
						o.color.r = 1.0;
						o.color.g = 0.0;
						o.color.b = 0.0;
					}
					*/
					
					/*
					o.color.r = 0.0;
					o.color.g = 1.0;
					o.color.b = 1.0;
					*/

					//project point onto the current rendering unity camera
					o.position = mul(mul(UNITY_MATRIX_VP, _Position), XYZpos);

					return o;


				}


				[maxvertexcount(4)]
				void geom(point PS_INPUT i[1], inout TriangleStream<GEOM_OUTPUT> triStream)
				{
				
					GEOM_OUTPUT o;
					o.position = i[0].position;
					o.color_uv = i[0].color_uv;
					o.normal = i[0].normal;

					float size = _Size * i[0].depth / 2;
					float aspect = 1.9;
					float colorSize = 0.5;
					float4 offset1 = float4(-0.1, 0.1 * aspect, 0, 0)*size;
					o.position = o.position + offset1;
					o.color_uv = i[0].color_uv + colorSize * float2(-_DepthTex_TexelSize.x, _DepthTex_TexelSize.y);
					triStream.Append(o);
					o.position = o.position - offset1;

					float4 offset2 = float4(-0.1, -0.1 * aspect, 0, 0)*size;
					o.position = o.position + offset2;
					o.color_uv = i[0].color_uv + colorSize * float2(-_DepthTex_TexelSize.x, -_DepthTex_TexelSize.y);
					triStream.Append(o);
					o.position = o.position - offset2;

					float4 offset3 = float4(0.1, 0.1 * aspect, 0, 0)*size;
					o.position = o.position + offset3;
					o.color_uv = i[0].color_uv + colorSize * float2(_DepthTex_TexelSize.x, _DepthTex_TexelSize.y);
					triStream.Append(o);
					o.position = o.position - offset3;

					float4 offset4 = float4(0.1, -0.1 * aspect, 0, 0)*size;
					o.position = o.position + offset4;
					o.color_uv = i[0].color_uv + colorSize * float2(_DepthTex_TexelSize.x, -_DepthTex_TexelSize.y);
					triStream.Append(o);
					o.position = o.position - offset4;
					// */
					/*
					GEOM_OUTPUT o;
					o.position = i[0].position;
					o.color = i[0].color;
					o.normal = i[0].normal;
					triStream.Append(o);
					
					o.position = i[1].position;
					o.color = i[1].color;
					o.normal = i[1].normal;
					triStream.Append(o);
					
					o.position = i[2].position;
					o.color = i[2].color;
					o.normal = i[2].normal;
					triStream.Append(o);
					// */

					//float4 v = i[0].position;
					//triStream.Append(o);
					//triStream.Append(o);
					//triStream.Append(o);


				}

				struct gs_out {
					float4 position : SV_POSITION;
					float4 color : COLOR;
				};



				fixed4 frag(PS_INPUT i) : SV_Target
				{
					float4 color_uv = float4(i.color_uv.x, i.color_uv.y, 0, 0);
					float3 rgb = tex2Dlod(_ColorTex, color_uv).rgb;
					// float gray = (rgb.r + rgb.b + rgb.g) / 3;
					// return float4(gray, gray, gray, 0.1f);
					return float4(rgb, 0.99f);
				}
				ENDCG
			}
		}
}
