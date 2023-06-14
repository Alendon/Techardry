#version 460

layout (location = 0) in vec4 in_Color;

layout (location = 0) out vec4 outFragColor;

void main(){
    outFragColor = in_Color;
}