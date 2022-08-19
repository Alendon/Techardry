#version 460

layout(location = 0) out vec3 out_position;

vec2 mesh[] = {
    vec2(-1,1), vec2(-1,-1), vec2(1,-1),
    vec2(-1,1), vec2(1,-1), vec2(1,1)
};




void main(){
    out_position = vec3(mesh[gl_VertexIndex], 0.0);
    gl_Position = vec4( mesh[gl_VertexIndex], 0.0, 1.0);
}