Shader "Custom/UIBorder"
{
    Properties
    {
        _Color ("Border Color", Color) = (1,1,1,1)
        _BorderWidth ("Border Width", Range(0, 0.5)) = 0.05
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _Color;
            float _BorderWidth;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float left   = step(uv.x, _BorderWidth);
                float right  = step(1.0 - _BorderWidth, uv.x);
                float bottom = step(uv.y, _BorderWidth);
                float top    = step(1.0 - _BorderWidth, uv.y);
                float border = saturate(left + right + bottom + top);

                fixed4 col = _Color * i.color;
                col.a *= border;
                return col;
            }
            ENDCG
        }
    }
}