#version 460
#extension GL_EXT_nonuniform_qualifier: require

#define Dimensions 16
#define MaxDepth 10
#define FloatMax 1e+30
#define BvhStackSize 64
#define SampleCount 4
#define MaxPathLength 4


layout (location = 0) in vec3 in_position;
layout (location = 0) out vec3 out_color;

layout (push_constant) uniform PushConstants {
    uint frame;
} pushConstants;

layout (set = 1, binding = 0) uniform sampler2D tex;

layout (input_attachment_index = 0, set = 2, binding = 0) uniform subpassInput inDepth;
layout (input_attachment_index = 1, set = 2, binding = 1) uniform subpassInput inColor;

#define CAMERA_DATA_SET 0
#include "camera.glsl"

#define RENDER_DATA_SET 3
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

    Ray ray;
    ray.origin = camPos;
    ray.direction = normalize(direction);
    ray.inverseDirection = 1 / ray.direction;

    uvec2 randomSeed = uvec2(gl_FragCoord.xy) * 4241 ^ uvec2(pushConstants.frame * 4637, pushConstants.frame * 4759);

    out_color = vec3(0);
    for (int i = 0; i < SampleCount; i++)
    {
        out_color += pathtrace(ray, randomSeed);
    }
    out_color /= float(SampleCount);
}
