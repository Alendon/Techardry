#version 460

struct rect {
    float x;
    float y;
    float w;
    float h;
};

layout (location = 0) in vec4 in_Location;
layout (location = 1) in vec4 in_Color;

layout (location = 0) out vec4 out_Color;

vec2 getVertex();

void main()
{
    out_Color = in_Color;
    gl_Position = vec4(getVertex(), 0.f, 1.f);
}

vec2 getVertex() {
    rect r = rect(in_Location.x, in_Location.y, in_Location.z, in_Location.w);

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