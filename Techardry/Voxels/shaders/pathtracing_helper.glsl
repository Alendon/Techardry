#ifndef PATHTRACING_HELPER_GLSL
#define PATHTRACING_HELPER_GLSL

#include "master_bvh.glsl"

#ifndef M_PI
#define M_PI 3.1415926535897932384626433832795
#endif // M_PI

vec3 sunDirection = normalize(vec3(0.5, 1, 0.2));

bool sunVisible(vec3 position){
    Ray ray;
    ray.origin = position + 0.001 * -sunDirection;
    ray.direction = -sunDirection;
    ray.inverseDirection = 1 / ray.direction;
    
    Result result = resultEmpty();
    raycast(ray, result);
    
    bool hit = resultHit(result);
    return !hit;
}

vec2 get_random_numbers(inout uvec2 seed) {
    // This is PCG2D: https://jcgt.org/published/0009/03/02/
    seed = 1664525u * seed + 1013904223u;
    seed.x += 1664525u * seed.y;
    seed.y += 1664525u * seed.x;
    seed ^= (seed >> 16u);
    seed.x += 1664525u * seed.y;
    seed.y += 1664525u * seed.x;
    seed ^= (seed >> 16u);
    // Convert to float. The constant here is 2^-32.
    return vec2(seed) * 2.32830643654e-10;
}

// Given uniform random numbers u_0, u_1 in [0,1)^2, this function returns a
// uniformly distributed point on the unit sphere (i.e. a random direction)
// (omega)
vec3 sample_sphere(vec2 random_numbers) {
    float z = 2.0 * random_numbers[1] - 1.0;
    float phi = 2.0 * M_PI * random_numbers[0];
    float x = cos(phi) * sqrt(1.0 - z * z);
    float y = sin(phi) * sqrt(1.0 - z * z);
    return vec3(x, y, z);
}

vec3 sample_hemisphere(vec2 random_numbers, vec3 normal) {
    vec3 direction = sample_sphere(random_numbers);
    if (dot(normal, direction) < 0.0)
    direction -= 2.0 * dot(normal, direction) * normal;
    return direction;
}



#endif // PATHTRACING_HELPER_GLSL
