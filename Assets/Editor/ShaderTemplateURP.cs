public static class ViewDependenceNetworkShaderURP {
    public const string Template = @"Shader ""MobileNeRF/ViewDependenceNetworkShaderURP/OBJECT_NAME"" {
       Properties {
        tDiffuse0x (""Diffuse Texture 0"", 2D) = ""white"" {}
        tDiffuse1x (""Diffuse Texture 1"", 2D) = ""white"" {}
        weightsZero (""Weights Zero"", 2D) = ""white"" {}
        weightsOne (""Weights One"", 2D) = ""white"" {}
        weightsTwo (""Weights Two"", 2D) = ""white"" {}
    }
    SubShader {
        Cull Off
        ZTest LEqual

        Tags{""RenderType"" = ""Opaque"" ""RenderPipeline"" = ""UniversalRenderPipeline"" ""IgnoreProjector"" = ""True""}

        Pass {

            Tags{""LightMode"" = ""UniversalForward""}

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl""

            struct Attributes {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 rayDirection : TEXCOORD1;
            };

            Varyings vert (Attributes v) {
                Varyings o;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);

                o.vertex = vertexInput.positionCS;
                o.uv = v.uv;
                o.rayDirection = -GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);

                AXIS_SWIZZLE

                return o;
            }

            sampler2D tDiffuse0x;
            sampler2D tDiffuse1x;
            sampler2D tDiffuse2x;

            TEXTURE2D(weightsZero);
            TEXTURE2D(weightsOne);
            TEXTURE2D(weightsTwo);

            half3 evaluateNetwork(half4 f0, half4 f1, half4 viewdir) {
                half intermediate_one[NUM_CHANNELS_ONE] = { BIAS_LIST_ZERO };
                int i = 0;
                int j = 0;

                for (j = 0; j < NUM_CHANNELS_ZERO; ++j) {
                    half input_value = 0.0;
                    if (j < 4) {
                    input_value =
                        (j == 0) ? f0.r : (
                        (j == 1) ? f0.g : (
                        (j == 2) ? f0.b : f0.a));
                    } else if (j < 8) {
                    input_value =
                        (j == 4) ? f1.r : (
                        (j == 5) ? f1.g : (
                        (j == 6) ? f1.b : f1.a));
                    } else {
                    input_value =
                        (j == 8) ? viewdir.r : (
                        (j == 9) ? viewdir.g : viewdir.b);
                    }
                    for (i = 0; i < NUM_CHANNELS_ONE; ++i) {
                    intermediate_one[i] += input_value * weightsZero.Load(int3(j, i, 0)).x;
                    }
                }

                half intermediate_two[NUM_CHANNELS_TWO] = { BIAS_LIST_ONE };

                for (j = 0; j < NUM_CHANNELS_ONE; ++j) {
                    if (intermediate_one[j] <= 0.0) {
                        continue;
                    }
                    for (i = 0; i < NUM_CHANNELS_TWO; ++i) {
                        intermediate_two[i] += intermediate_one[j] * weightsOne.Load(int3(j, i, 0)).x;
                    }
                }

                half result[NUM_CHANNELS_THREE] = { BIAS_LIST_TWO };

                for (j = 0; j < NUM_CHANNELS_TWO; ++j) {
                    if (intermediate_two[j] <= 0.0) {
                        continue;
                    }
                    for (i = 0; i < NUM_CHANNELS_THREE; ++i) {
                        result[i] += intermediate_two[j] * weightsTwo.Load(int3(j, i, 0)).x;
                    }
                }
                for (i = 0; i < NUM_CHANNELS_THREE; ++i) {
                    result[i] = 1.0 / (1.0 + exp(-result[i]));
                }
                return half3(result[0]*viewdir.a+(1.0-viewdir.a),
                            result[1]*viewdir.a+(1.0-viewdir.a),
                            result[2]*viewdir.a+(1.0-viewdir.a));
            }

            half4 frag (Varyings i) : SV_Target {
                half4 diffuse0 = tex2D( tDiffuse0x, i.uv );
                if (diffuse0.r == 0.0) discard;
                half4 diffuse1 = tex2D( tDiffuse1x, i.uv );
                half4 rayDir = half4(i.rayDirection, 1.0);


                //deal with iphone
                diffuse0.a = diffuse0.a*2.0-1.0;
                diffuse1.a = diffuse1.a*2.0-1.0;
                rayDir.a = rayDir.a*2.0-1.0;

                half4 fragColor;
                fragColor.rgb = evaluateNetwork(diffuse0,diffuse1,rayDir);
                fragColor.a = 1.0;

                return fragColor;
            }
            ENDHLSL
        }

        Pass {
            Name ""ShadowCaster""
            Tags { ""LightMode""=""ShadowCaster"" }
 
            ZWrite On
            ZTest LEqual
 
            HLSLPROGRAM

            #pragma vertex CustomShadowPassVertex
    	    #pragma fragment CustomShadowPassFragment

	        // Material Keywords
	        #pragma shader_feature _ALPHATEST_ON
	        #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

	        // GPU Instancing
	        #pragma multi_compile_instancing
	        // (Note, this doesn't support instancing for properties though. Same as URP/Lit)
	        // #pragma multi_compile _ DOTS_INSTANCING_ON
	        // (This was handled by LitInput.hlsl. I don't use DOTS so haven't bothered to support it)

            float3 _LightDirection;
            float3 _LightPosition;

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
            #include ""Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl""
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl""
            

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float2 uv : TEXCOORD0;
                float4 positionCS   : SV_POSITION;
            };

            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return positionCS;
            }

            Varyings CustomShadowPassVertex (Attributes v) {
                Varyings o;

                UNITY_SETUP_INSTANCE_ID(v);

                o.positionCS = GetShadowPositionHClip(v);
                o.uv = v.uv;

                return o;
            }

            sampler2D tDiffuse0x;
            
            half4 CustomShadowPassFragment(Varyings input) : SV_TARGET
            {
                half4 diffuse0 = tex2D( tDiffuse0x, input.uv );
                if (diffuse0.r == 0.0) discard;

                return 0;
            }

            ENDHLSL
        }
    }
}";
}
