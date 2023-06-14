#version 460

layout(push_constant) uniform PushConstants
{
    float pos_left;
    float pos_right;
    float pos_top;
    float pos_bottom;

    float uv_left;
    float uv_right;
    float uv_top;
    float uv_bottom;

    float r;
    float g;
    float b;
    float a;

} pushConstants;

layout(location = 0) out vec4 out_Color;
layout(location = 1) out vec2 out_UV;

vec4 constructPosition();
vec2 constructUV();
vec4 constructColor();

void main()
{
    out_Color = constructColor();
    out_UV = constructUV();
    gl_Position = constructPosition();
}

vec4 constructPosition()
{
    switch (gl_VertexIndex) {
        case 0: return vec4(pushConstants.pos_left, pushConstants.pos_bottom, 0.0, 1.0);
        case 1: return vec4(pushConstants.pos_left, pushConstants.pos_top, 0.0, 1.0);
        case 2: return vec4(pushConstants.pos_right, pushConstants.pos_top, 0.0, 1.0);
        case 3: return vec4(pushConstants.pos_right, pushConstants.pos_top, 0.0, 1.0);
        case 4: return vec4(pushConstants.pos_right, pushConstants.pos_bottom, 0.0, 1.0);
        case 5: return vec4(pushConstants.pos_left, pushConstants.pos_bottom, 0.0, 1.0);
    }
}

vec2 constructUV()
{
    switch (gl_VertexIndex) {
        case 0: return vec2(pushConstants.uv_left, pushConstants.uv_bottom);
        case 1: return vec2(pushConstants.uv_left, pushConstants.uv_top);
        case 2: return vec2(pushConstants.uv_right, pushConstants.uv_top);
        case 3: return vec2(pushConstants.uv_right, pushConstants.uv_top);
        case 4: return vec2(pushConstants.uv_right, pushConstants.uv_bottom);
        case 5: return vec2(pushConstants.uv_left, pushConstants.uv_bottom);
    }
}

vec4 constructColor()
{
    return vec4(pushConstants.r, pushConstants.g, pushConstants.b, pushConstants.a);
}