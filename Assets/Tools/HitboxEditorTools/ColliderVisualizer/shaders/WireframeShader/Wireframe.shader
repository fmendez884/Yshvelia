Shader "Custom/Wireframe"
{
    Properties
    {
        _Color ("Wireframe Color", Color) = (1,1,1,1)
        _WireThickness ("Wireframe Thickness", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            ZWrite Off
            ZTest LEqual
            Cull Off

            // Wireframe rendering
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma exclude_renderers gles xbox360 ps3
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : POSITION;
            };

            float _WireThickness;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // Simple wireframe color
                return half4(1, 1, 1, 1);
            }
            ENDCG
        }

        Pass
        {
            Name "WireframePass"
            Tags { "LightMode" = "Always" }
            ZWrite Off
            ZTest LEqual
            Cull Off
            ColorMask RGB
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma exclude_renderers gles xbox360 ps3
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : POSITION;
            };

            float _WireThickness;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                return half4(0, 0, 0, 0);
            }
            ENDCG
        }
    }
}
