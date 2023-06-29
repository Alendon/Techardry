#version 460
#extension GL_EXT_nonuniform_qualifier: require

#define Dimensions 16
#define MaxDepth 10
#define FloatMax 1e+30
#define BvhStackSize 64

#define CAMERA_DATA_SET 0 
#include "camera.glsl" 

#define RENDER_DATA_SET 3
#define RENDER_DATA_SET_MASTER_BVH_BINDING 2
#define RENDER_DATA_SET_MASTER_BVH_INDICES_BINDING 2
#define RENDER_DATA_SET_OCTREE_BINDING 2

#include "master_bvh.glsl"

//shader in/output
layout (location = 0) in vec3 in_position;
layout (location = 0) out vec3 out_color;



layout (set = 1, binding = 0) uniform sampler2DArray tex;

layout (input_attachment_index = 0, set = 2, binding = 0) uniform subpassInput inDepth;
layout (input_attachment_index = 1, set = 2, binding = 1) uniform subpassInput inColor;

//actual shader code

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
    float depth = subpassLoad(inDepth).r;
    vec3 oldColor = subpassLoad(inColor).rgb;

    Result result = Result(0, vec3(0, 0, 0), vec2(0, 0), FloatMax, -1, false, vec3(0, 0, 1));

    raycast(ray, result);

    bool hit = !floatEquals(result.t, FloatMax);

    if (result.fail) {
        out_color = result.failColor;
        return;
    }

    if (!hit) {
        out_color = oldColor;
        return;
    }

    float lDepth = linearizeDepth(depth);

    float hitDepth = result.t - camera.data.Near;

    if (hitDepth > lDepth) {
        out_color = oldColor;
        return;
    }


    float newDepth = delinearizeDepth(hitDepth);
    gl_FragDepth = newDepth;

    #define TextureSize 128.

    uint voxel = voxelNode_GetDataIndex(result.tree, result.nodeIndex);

    vec3 texStart = vec3(voxelData_GetTextureStartX(result.tree, voxel) / TextureSize, voxelData_GetTextureStartY(result.tree, voxel) / TextureSize, voxelData_GetTextureArrayIndex(result.tree, voxel));

    vec2 texSize = vec2(voxelData_GetTextureSizeX(result.tree, voxel) / TextureSize, voxelData_GetTextureSizeY(result.tree, voxel) / TextureSize);
    out_color = texture(tex, texStart + vec3(result.uv * texSize, 0)).rgb;
}
