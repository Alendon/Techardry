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
#define BEAM_CALCULATION


layout (location = 0) in vec3 in_position;
layout (location = 0) out float out_depth;


#define CAMERA_DATA_SET 0
#include "camera.glsl"

#define RENDER_DATA_SET 1
#define RENDER_DATA_SET_WORLD_GRID_HEADER_BINDING 0
#define RENDER_DATA_SET_WORLD_GRID_BINDING 1

#include "master_bvh.glsl"
#include "pathtracing_helper.glsl"


float sample_depth(Ray ray) {
    vec3 color = vec3(0);
    vec3 attenuation = vec3(1);


    Result result = resultEmpty();
    raycast(ray, result);

    return result.t;
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

    out_depth = sample_depth(ray);
}
