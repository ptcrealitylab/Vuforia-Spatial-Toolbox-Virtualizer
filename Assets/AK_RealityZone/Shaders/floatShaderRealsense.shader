Shader "Custom/floatShaderRealsense" {
	Properties{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo (RGB)", 2D) = "white" {}
		_Glossiness("Smoothness", Range(0,1)) = 0.5
		_Metallic("Metallic", Range(0,1)) = 0.0
		_Distance("Distance", Range(0,10)) = .1


	}
		SubShader{
			Tags { "RenderType" = "Opaque" }
			LOD 200

			CGPROGRAM
			// Physically based Standard lighting model, and enable shadows on all light types
			#pragma surface surf Standard fullforwardshadows

			// Use shader model 3.0 target, to get nicer looking lighting
			#pragma target 3.0

			sampler2D _MainTex;

			struct Input {
				float2 uv_MainTex;
			};

			half _Glossiness;
			half _Metallic;
			half _Distance;
			fixed4 _Color;

			// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
			// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
			// #pragma instancing_options assumeuniformscaling
			UNITY_INSTANCING_BUFFER_START(Props)
				// put more per-instance properties here
			UNITY_INSTANCING_BUFFER_END(Props)

			void surf(Input IN, inout SurfaceOutputStandard o) {
				// Albedo comes from a texture tinted by color
				//fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
				fixed4 c = tex2D(_MainTex, IN.uv_MainTex);


				//float dist = sqrt(c.r*c.r + c.g*c.g + c.b*c.b);
				//float val = -(dist - _Distance) / _Distance;
				//float val = c.r / _Distance;
				
				
				float val = c.r / _Distance;
				
				//for testing XYMap:
				/*
				float val = c.g;
				if (val < _Distance) {
					c.r = 1.0;
					c.g = 1.0;
					c.b = 1.0;
				}
				else {
					c.r = 0.0;
					c.g = 0.0;
					c.b = 0.0;
				}
				*/

				c.r = val;
				c.g = val;
				c.b = val;



				o.Albedo = c.rgb;
				//o.Albedo = c.rgb / _Divider;
				// Metallic and smoothness come from slider variables
				o.Metallic = _Metallic;
				o.Smoothness = _Glossiness;
				o.Alpha = c.a;
			}
			ENDCG
		}
			FallBack "Diffuse"
}
