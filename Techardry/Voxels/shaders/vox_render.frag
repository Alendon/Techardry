#version 460
#extension GL_EXT_nonuniform_qualifier: require

#define Dimensions 16
#define MaxDepth 10
#define FloatMax 1e+30
#define BvhStackSize 64

#define CAMERA_DATA_SET 0
#include "camera.glsl"

#define RENDER_DATA_SET 3
#define RENDER_DATA_SET_MASTER_BVH_BINDING 0
#define RENDER_DATA_SET_MASTER_BVH_INDICES_BINDING 1
#define RENDER_DATA_SET_OCTREE_BINDING 2

#include "master_bvh.glsl"
#include "pathtracing_helper.glsl"
#define TextureSize 128.

//shader in/output
layout (location = 0) in vec3 in_position;
layout (location = 0) out vec3 out_color;



layout (set = 1, binding = 0) uniform sampler2DArray tex;

layout (input_attachment_index = 0, set = 2, binding = 0) uniform subpassInput inDepth;
layout (input_attachment_index = 1, set = 2, binding = 1) uniform subpassInput inColor;

vec3 pathtrace(Ray ray) {
    vec3 color = vec3(0);
    vec3 attenuation = vec3(1);
    
    //create random seed based on ray.origin and ray.direction
    vec2 randomSeed = vec2(dot(ray.origin, ray.direction), dot(ray.origin, ray.direction) + 1);
    randomSeed = vec2(dot(vec2(random(randomSeed)), vec2(random(randomSeed + 1))), dot(vec2(random(randomSeed + 2)), vec2(random(randomSeed + 3))));
    
    #define MAX_BOUNCES 4
    for(int i = 0; i < MAX_BOUNCES; i++){
        Result result = resultEmpty();
        raycast(ray, result);
        
        if(result.fail){
            return /* pink */ vec3(1, 0, 1);
        }
        
        if(!resultHit(result)) break;
        
        
        vec3 hitPos = ray.origin + ray.direction * result.t;
        vec3 hitNormal = result.normal;
        
        uint voxel = voxelNode_GetDataIndex(result.tree, result.nodeIndex);

        vec3 texStart = vec3(voxelData_GetTextureStartX(result.tree, voxel) / TextureSize, voxelData_GetTextureStartY(result.tree, voxel) / TextureSize, voxelData_GetTextureArrayIndex(result.tree, voxel));

        vec2 texSize = vec2(voxelData_GetTextureSizeX(result.tree, voxel) / TextureSize, voxelData_GetTextureSizeY(result.tree, voxel) / TextureSize);
        vec3 albedo = texture(tex, texStart + vec3(result.uv * texSize, 0)).rgb;

        if(sunVisible(hitPos))
        {
            float NdotL = max(dot(hitNormal, -sunDirection), 0);
            color += attenuation * albedo * NdotL;
        }
        
        attenuation *= albedo;
        
        ray.direction = randomDirectionInHemisphere(hitNormal, randomSeed);
        randomSeed = vec2(randomSeed.x + 1, randomSeed.y + 1);
        ray.origin = hitPos + ray.direction * 0.001;
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

    #define maxBounces 10

    Ray ray;
    ray.origin = camPos;
    ray.direction = normalize(direction);
    ray.inverseDirection = 1 / ray.direction;

    out_color = pathtrace(ray);
}
