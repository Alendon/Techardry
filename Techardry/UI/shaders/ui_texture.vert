#version 460

layout(location = 0) in vec3 in_Position;
layout(location = 1) in vec3 in_Color;
layout(location = 2) in vec3 in_Normal;
layout(location = 3) in vec2 in_UV;

layout(location = 0) out vec3 out_Color;
layout(location = 1) out vec2 out_UV;

layout(std140, set = 0, binding = 1) readonly buffer TransformBuffer{
    mat4 data[];
} transformBuffer;

void main()
{
    mat4 transformMatrix = transformBuffer.data[gl_BaseInstance];
    gl_Position = transformMatrix * vec4(in_Position, 1.f);
    out_Color = vec3(in_UV, 1.f);
    out_UV = in_UV;
}