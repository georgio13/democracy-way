Shader "DemocracyWay/UI/Soft Backdrop" {
Properties { [PerRendererData] _MainTex ("Texture",2D)="white" {} _Color("Tint",Color)=(1,1,1,1) }
SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
Cull Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
sampler2D _MainTex; float4 _MainTex_TexelSize; fixed4 _Color;
v2f vert(appdata v) { v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o; }
fixed4 frag(v2f i):SV_Target {
float2 d=_MainTex_TexelSize.xy*6;
fixed4 c=tex2D(_MainTex,i.uv)*.25;
c+=(tex2D(_MainTex,i.uv+float2(d.x,0))+tex2D(_MainTex,i.uv-float2(d.x,0))+tex2D(_MainTex,i.uv+float2(0,d.y))+tex2D(_MainTex,i.uv-float2(0,d.y)))*.125;
c+=(tex2D(_MainTex,i.uv+d)+tex2D(_MainTex,i.uv-d)+tex2D(_MainTex,i.uv+float2(d.x,-d.y))+tex2D(_MainTex,i.uv+float2(-d.x,d.y)))*.0625;
return c*i.color;
} ENDCG }
} }
