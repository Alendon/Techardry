#version 460

layout (location = 0) in vec3 in_Color;
layout (location = 1) in vec2 in_TexCoords;

layout (location = 0) out vec4 outFragColor;

layout(set = 0, binding = 0) uniform sampler2D Sampler;

void main(){
    outFragColor = vec4(in_Color,1) * texture(Sampler, in_TexCoords);
}