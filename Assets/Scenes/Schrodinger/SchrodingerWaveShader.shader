Shader "Custom/HydrogenAtom"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TimeScale ("Time Scale", Float) = 1.0
        _WaveFunctionParams ("Wave Function Parameters", Vector) = (1.0, 0.0, 0.0, 1.0)
    }
    SubShader
    {
        // Tags { "RenderType"="Opaque" }
        Tags { "RenderType"="Transparent" }
        LOD 200
        
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha 
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _TimeScale;
            float4 _WaveFunctionParams;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float ComplexModulus(float2 z)
            {
                return sqrt(z.x * z.x + z.y * z.y);
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv * 2.0 - 1.0; // Convert to range -1 ~ 1
                
                // Time 
                float t = _Time.y * _TimeScale;
                float timeFactor = sin(t * 10.0) * cos(t * 3.0) + 1.0;
                // Parameters
                float n = _WaveFunctionParams.x; // Principal quantum number
                float l = _WaveFunctionParams.y; // Azimuthal quantum number
                float m = _WaveFunctionParams.z; // Magnetic quantum number
                float a0 = _WaveFunctionParams.w; // Bohr radius - scale
                // Radial component 
                float r = length(uv) * a0;
                float radialPart = exp(-r / n) * pow(r, l) * timeFactor;
                // Angular component 
                float theta = atan2(uv.y, uv.x);
                float angularPart = cos(m * theta + t) * pow(abs(sin(uv.y * timeFactor)), abs(m));
                // Time-dependent part
                float2 waveFunction = radialPart * angularPart * float2(cos(t * 2.0), sin(t * 2.0));
                // Probability density (modulus squared of wave function)
                float probabilityDensity = ComplexModulus(waveFunction);
                probabilityDensity *= probabilityDensity;
                // color 
                float4 baseColor = float4(1.0, 0.3, 0.1, 1.0*probabilityDensity);
                float4 color = probabilityDensity * baseColor;
                color.a = probabilityDensity > 0.0 ? probabilityDensity : 0.0;
                return color;
            }
            ENDCG
        }
    }
    // FallBack "Diffuse"
    FallBack "Transparent" 
}
