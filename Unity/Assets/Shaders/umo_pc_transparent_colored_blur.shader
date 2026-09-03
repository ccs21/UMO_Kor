// PC equivalent of the GLES-only original. The Android material is unchanged.
Shader "UMO/PC/TransparentColoredBlur" {
    Properties {
        _MainTex ("Base (RGB), Alpha (A)", 2D) = "white" {}
        _Distance ("Distance", Float) = 0.002
        _Sampling ("Sampling", Range(1, 10)) = 2
    }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass {
            Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Distance, _Sampling;
            v2f vert(appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }
            fixed4 frag(v2f i):SV_Target {
                fixed4 sum = tex2D(_MainTex, i.uv);
                int count = (int)clamp(ceil(_Sampling), 1, 10);
                [loop] for(int n=1; n<=count; n++) {
                    float d = _Distance*n;
                    sum += tex2D(_MainTex, i.uv+float2(d,d));
                    sum += tex2D(_MainTex, i.uv+float2(d,0));
                    sum += tex2D(_MainTex, i.uv+float2(d,-d));
                    sum += tex2D(_MainTex, i.uv+float2(0,d));
                    sum += tex2D(_MainTex, i.uv+float2(0,-d));
                    sum += tex2D(_MainTex, i.uv+float2(-d,d));
                    sum += tex2D(_MainTex, i.uv+float2(-d,0));
                    sum += tex2D(_MainTex, i.uv+float2(-d,-d));
                }
                return sum*i.color/(count*8+1);
            }
            ENDCG
        }
    }
}
