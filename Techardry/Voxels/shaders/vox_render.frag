#version 460
#extension GL_EXT_nonuniform_qualifier: require
#extension GL_EXT_buffer_reference: require
#extension GL_EXT_buffer_reference2: require
#extension GL_EXT_shader_explicit_arithmetic_types_int64: require


#define Dimensions 16
#define MaxDepth 10
#define FloatMax 1e+30
#define BvhStackSize 64
#define SampleCount 1
#define MaxPathLength 1

#define CAMERA_DATA_SET 0
#define TEXTURE_ATLAS_SET 1
#define RENDER_DATA_SET 2
#define BEAM_SET 3


layout (location = 0) in vec3 in_position;
layout (location = 0) out vec3 out_color;

layout (push_constant) uniform PushConstants {
    uint frame;
} pushConstants;

layout (set = TEXTURE_ATLAS_SET, binding = 0) uniform sampler2D tex;
layout (set = BEAM_SET, binding = 0) uniform sampler2D beamTex;

#include "camera.glsl"

#define RENDER_DATA_SET_MASTER_BVH_BINDING 0
#define RENDER_DATA_SET_MASTER_BVH_INDICES_BINDING 1
#define RENDER_DATA_SET_OCTREE_BINDING 2

#include "master_bvh.glsl"
#include "pathtracing_helper.glsl"

//shader in/output

#define UseFakeLighting


vec3 pathtrace(Ray ray, inout uvec2 randomSeed) {
    vec3 color = vec3(0);
    vec3 attenuation = vec3(1);
    
    
    
    #define MAX_BOUNCES MaxPathLength
    for (int i = 0; i < MAX_BOUNCES; i++){
        Result result = resultEmpty();        
        raycast(ray, result);

        if (result.fail){
            return /* pink */ vec3(1, 0, 1);
        }

        if (!resultHit(result)) break;

        #ifdef UseFakeLighting

        float intensity = clamp(dot(result.normal, sunDirection), 0.0, 1.0);
        intensity = intensity * intensity * intensity;
        intensity = intensity * 0.5 + 0.5;
        color = resultGetColor(result) * intensity;
        
        #else

        vec3 hitPos = ray.origin + ray.direction * result.t;
        vec3 hitNormal = result.normal;

        vec3 albedo = resultGetColor(result);

        if (sunVisible(hitPos))
        {
            float NdotL = max(dot(hitNormal, sunDirection), 0);
            color += attenuation * albedo * NdotL;
        }

        attenuation *= albedo;

        ray.direction = sample_hemisphere(get_random_numbers(randomSeed), hitNormal);

        ray.origin = hitPos + ray.direction * 0.001;
        #endif
    }

    return color;
}

void main()
{
    vec3 camPos = vec3(camera.data.PositionX, camera.data.PositionY, camera.data.PositionZ);

    vec2 screenPos = vec2(in_position.xy);

    vec3 forward = vec3(camera.data.ForwardX, camera.data.ForwardY, camera.data.ForwardZ);
    vec3 upward = vec3(camera.data.UpwardX, camera.data.UpwardY, camera.data.UpwardZ);
    vec3 right = cross(forward, upward);

    float fov = camera.data.HFov;
    float angle = tan(fov / 2);


    float ratio = camera.data.AspectRatio;
    float x = screenPos.x * ratio * angle;
    float y = screenPos.y * angle;

    vec3 direction = normalize(forward + x * right + y * upward);
    
    vec2 uv = (in_position.xy + vec2(1)) / 2;
    vec2 texSize = textureSize(beamTex, 0);
    vec2 texelSize = 1 / texSize;
    
    float startT = FloatMax;
    
    for(int x = -1; x <= 1; x++) {
    for(int y = -1; y <= 1; y++) {
        startT = min(startT, texture(beamTex, uv + (texelSize * vec2(x,y)) ).r);
    }}

    Ray ray;
    ray.origin = camPos + startT * direction;
    ray.direction = direction;
    ray.inverseDirection = 1 / direction;

    uvec2 randomSeed = uvec2(gl_FragCoord.xy) * 4241 ^ uvec2(pushConstants.frame * 4637, pushConstants.frame * 4759);

    out_color = vec3(0);
    for (int i = 0; i < SampleCount; i++)
    {
        out_color += pathtrace(ray, randomSeed);
    }
    out_color /= float(SampleCount);
}
