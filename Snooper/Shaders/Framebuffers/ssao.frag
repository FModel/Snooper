in vec2 vTexCoords;

uniform sampler2D gPosition;
uniform sampler2D gNormal;

uniform int uDirectionCount;
uniform int uStepsPerDirection;
uniform int uFrameCount;

uniform float radius;
uniform mat4 uProjectionMatrix;

out float FragColor;

const float PI = 3.14159265359;

// Interleaved gradient noise
float InterleavedGradientNoise(vec2 position, int frameCount)
{
    position += float(frameCount) * 5.588238;
    return fract(52.9829189 * fract(dot(position, vec2(0.06711056, 0.00583715))));
}

void main()
{
    vec3 viewPos = texture(gPosition, vTexCoords).xyz;
    vec3 viewNormal = texture(gNormal, vTexCoords).xyz;
    
    // Early exit for skybox/background
    if (length(viewNormal) < 0.1 || viewPos.z >= -0.001)
    {
        FragColor = 1.0;
        return;
    }
    
    vec2 texelSize = 1.0 / vec2(textureSize(gPosition, 0));
    float depth = -viewPos.z;
    
    // Adaptive radius
    float adaptiveRadius = radius * clamp(depth * 0.1, 0.5, 2.5);
    
    // Screen-space radius in pixels
    vec4 projRadius = uProjectionMatrix * vec4(adaptiveRadius, 0.0, viewPos.z, 1.0);
    float radiusPixels = abs(projRadius.x / projRadius.w) * float(textureSize(gPosition, 0).x) * 0.5;
    radiusPixels = clamp(radiusPixels, 10.0, 80.0);
    
    // Temporal rotation
    float temporalRotation = InterleavedGradientNoise(gl_FragCoord.xy, uFrameCount) * 2.0 * PI;
    
    float occlusion = 0.0;
    int validSamples = 0;
    
    // Sample in circular directions
    for (int i = 0; i < uDirectionCount; i++)
    {
        float angle = (float(i) / float(uDirectionCount)) * PI + temporalRotation;
        vec2 direction = vec2(cos(angle), sin(angle));
        
        // March along direction
        for (int j = 1; j <= uStepsPerDirection; j++)
        {
            float stepRatio = float(j) / float(uStepsPerDirection);
            vec2 sampleUV = vTexCoords + direction * stepRatio * radiusPixels * texelSize;
            
            // Bounds check
            if (sampleUV.x < 0.0 || sampleUV.x > 1.0 || sampleUV.y < 0.0 || sampleUV.y > 1.0)
                continue;
            
            vec3 samplePos = texture(gPosition, sampleUV).xyz;
            
            // Skip invalid samples
            if (samplePos.z >= -0.001)
                continue;
            
            vec3 diff = samplePos - viewPos;
            float dist = length(diff);
            
            // Skip very close samples and distant samples
            if (dist < 0.05 || dist > adaptiveRadius)
                continue;
            
            vec3 diffDir = diff / dist;
            
            // Check if sample is above the surface
            float normalDot = max(0.0, dot(diffDir, viewNormal));
            
            // Distance-based falloff
            float distFactor = dist / adaptiveRadius;
            float falloff = 1.0 - distFactor * distFactor;
            falloff = max(0.0, falloff);
            
            occlusion += normalDot * falloff;
            validSamples++;
        }
    }
    
    // Average and normalize
    if (validSamples > 0)
    {
        occlusion = occlusion / float(validSamples);
    }
    
    // Invert: high occlusion = dark (0), low occlusion = bright (1)
    occlusion = 1.0 - clamp(occlusion * 2.0, 0.0, 1.0);
    
    FragColor = occlusion;
}