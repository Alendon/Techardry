#version 460

layout(location = 0) in vec3 in_Position;
layout(location = 1) in vec3 in_Color;
layout(location = 2) in vec3 in_Normal;
layout(location = 3) in vec2 in_UV;

layout(location = 0) out vec3 out_position;


void main(){
    out_position = in_Position;
    gl_Position = vec4(in_Position, 1.0);
}