#version 460

struct rect {
    float x;
    float y;
    float w;
    float h;
};

layout ( push_constant) uniform PushConstants {
    vec4 location;
    vec4 color;
} pushConstants;

layout (location = 0) out vec4 out_Color;

vec2 getVertex();

void main()
{
    out_Color = pushConstants.color;
    gl_Position = vec4(getVertex(), 0.f, 1.f);
}

vec2 getVertex() {
    rect r = rect(pushConstants.location.x, pushConstants.location.y, pushConstants.location.z, pushConstants.location.w);

    vec2 vertex;
    
    switch (gl_VertexIndex) {
        case 0:
            vertex = vec2(r.x, r.y);
            break;
        case 1:
            vertex = vec2(r.x, r.y + r.h);
            break;
        case 2:
            vertex = vec2(r.x + r.w, r.y + r.h);
            break;
        case 3:
            vertex = vec2(r.x, r.y );
            break;
        case 4:
            vertex = vec2(r.x + r.w, r.y + r.h);
            break;
        case 5:
            vertex = vec2(r.x + r.w, r.y );
            break;
        default:
            return vec2(0.f, 0.f);
    }
    
    //for conveniance the rectangle is in the range [0, 1] so we need to convert it to [-1, 1]
    return vertex * 2.f - 1.f;
}