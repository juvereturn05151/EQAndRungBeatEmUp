Shader "BeatEmUp/Interactable Pixel Outline"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite",2D)="white"{}
        [PerRendererData] _OutlineColor("Highlight",Color)=(.35,1,.9,.3)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_TexelSize; fixed4 _OutlineColor;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata v) { v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o; }
            fixed4 frag(v2f i):SV_Target
            {
                float a=tex2D(_MainTex,i.uv).a;float border=0;
                for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                    border=max(border,tex2D(_MainTex,i.uv+float2(x,y)*_MainTex_TexelSize.xy).a);
                fixed4 color=_OutlineColor;color.a*=saturate(border-a);return color;
            }
            ENDCG
        }
    }
}
