Shader "Custom/MultipleHydrogen"
{
    Properties
    {
        _NumQubits ("Number of Qubits", Int) = 2 // Number of Qubits
        _OrbitColor ("Orange Color", Color) = (1.0, 0.5, 0.0, 1.0) 
        _MainTex ("Texture", 2D) = "white" {}
        _TimeScale ("Time Scale", Float) = 1.0
        _WaveFunctionParams ("Wave Function Parameters", Vector) = (1.0, 0.0, 0.0, 1.0)

    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Lighting Off
            Fog { Mode Off }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Centers[5];  
            int _NumQubits;
            float4 _OrbitColor;
            float4 _WaveFunctionParams;
            float _TimeScale;
            sampler2D _MainTex;
            float4 _MainTex_ST;

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

            float gaussian(float2 uv, float2 center, float amplitude, float sigma)
            {
                float2 diff = uv - center;
                float distSq = dot(diff, diff);  // (x - x0)^2 + (y - y0)^2
                float sigmaSq = sigma * sigma;
                return amplitude * exp(-distSq / (2.0 * sigmaSq));
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 value = float4(0.0,0.0,0.0,0.0);
                for (int j = 0; j < _NumQubits; j++)
                {
                    float2 center = _Centers[j].xy;
                    // Time
                    float t = _Time.y * _TimeScale;
                    float timeFactor = sin(t * 10.0) * cos(t * 3.0) + 1.0;
                    // Parameters
                    float n = _WaveFunctionParams.x; // Principal quantum number
                    float l = _WaveFunctionParams.y; // Azimuthal quantum number
                    float m = _WaveFunctionParams.z; // Magnetic quantum number
                    float a0 = _WaveFunctionParams.w; // Bohr radius - scale
                    // Radial component
                    float2 position = i.uv + center;
                    // float2 base = float2(j*10, j);
                    // float2 position = i.uv + base;
                    float r = length(position) * a0;
                    float radialPart = exp(-r / n) * pow(r, l) * timeFactor;
                    // Angular component
                    float theta = atan2(position.y, position.x);
                    float angularPart = cos(m * theta + t) * pow(abs(sin(position.y * timeFactor)), abs(m));
                    // Time-dependent part
                    float2 waveFunction = radialPart * angularPart * float2(cos(t * 2.0), sin(t * 2.0));
                    // Probability density (modulus squared of wave function)
                    float probabilityDensity = ComplexModulus(waveFunction);
                    probabilityDensity *= probabilityDensity;
                    // color
                    // float4 baseColor = float4(0.2 * j, 0.3, 0.1, 1.0*probabilityDensity);
                    float4 baseColor = float4(0.3 * (j+1), 1- 0.3 * (j+1), 0.1 * 1-(j+2), 1.0*probabilityDensity);
                    float4 color = probabilityDensity * baseColor;
                    color.a = probabilityDensity > 0.0 ? probabilityDensity : 0.0;
                    value.r = value.r + color.r;
                    value.g = value.g + color.g;
                    value.b = value.b + color.b;
                    value.a = value.a + color.a;
                }
                // value = saturate(value)*0.5;  // Ensure output stays within 0-1 range

                // Output the Gaussian value with an orange color
                return value;  // Apply the orange color and alpha
            }
            ENDCG
        }
    }
    // FallBack "Diffuse"
    FallBack "Transparent"
}