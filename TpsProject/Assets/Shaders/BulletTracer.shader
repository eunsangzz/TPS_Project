Shader "TPS/BulletTracer"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                return input.color;
            }
            ENDCG
        }
    }
}
