Shader "Custom/MultipleGaussians"
{
    Properties
    {
        _Amplitude ("Amplitude", Float) = 1.0
        _Sigma ("Sigma", Float) = 0.1
        _NumGaussians ("Number of Gaussians", Int) = 3 // Number of Gaussians
        _OrbitColor ("Orange Color", Color) = (1.0, 0.5, 0.0, 1.0) // Orange color
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

            float _Amplitude;
            float _Sigma;
            float4 _Centers[10];  // Define a 10-element array for Gaussian centers
            int _NumGaussians;
            float4 _OrbitColor;

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
                o.uv = v.uv;
                return o;
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
                float value = 0.0;
                for (int j = 0; j < _NumGaussians; j++)
                {
                    float2 center = _Centers[j].xy;
                    value += gaussian(i.uv, center, _Amplitude, _Sigma);
                }
                value = saturate(value)*0.5;  // Ensure output stays within 0-1 range
                
                // Output the Gaussian value with an orange color
                return float4(_OrbitColor.rgb * value, value);  // Apply the orange color and alpha
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}