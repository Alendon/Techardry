#ifndef PATHTRACING_HELPER_GLSL
#define PATHTRACING_HELPER_GLSL

#include "master_bvh.glsl"

vec3 sunDirection = normalize(vec3(1, -1, 0.2));

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

float random(vec2 st) {
    return fract(sin(dot(st.xy, vec2(12.9898,78.233))) * 43758.5453123);
}

vec3 randomDirectionInHemisphere(vec3 normal, vec2 randomSeed) {
    vec3 randomPoint;
    float iteration = 0.0;
    do {
        // Generiere zufälligen Punkt in Einheitswürfel
        randomPoint = vec3(random(randomSeed + vec2(iteration)), random(randomSeed + vec2(1.0 + iteration, 0.0)), random(randomSeed + vec2(0.0, 1.0 + iteration))) * 2.0 - vec3(1.0);

        iteration += 1.0;

        // Wiederhole, bis Punkt innerhalb der Einheitskugel liegt
    } while (dot(randomPoint, randomPoint) > 1.0);

    // Wenn Punkt in der unteren Hemisphäre liegt, invertiere ihn
    if (dot(randomPoint, normal) < 0.0) {
        randomPoint = -randomPoint;
    }

    return normalize(randomPoint);
}




#endif // PATHTRACING_HELPER_GLSL
