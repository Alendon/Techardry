#ifndef COMMON_GLSL
#define COMMON_GLSL

struct Ray{
    vec3 origin, direction, inverseDirection;
};

struct AABB{
    vec3 min;
    vec3 max;
};

struct Result{
    uint nodeIndex;
    vec3 normal;
    vec2 uv;
    float t;
    int tree;
    bool fail;
    vec3 failColor;
};

struct BvhNode{
    float minX;
    float minY;
    float minZ;
    float padding1;

    float maxX;
    float maxY;
    float maxZ;
    float padding2;

    int leftFirst;
    int count;
};

bool floatEquals(float a, float b){
    float dynamicEpsilon = 0.001 * abs(a);

    return abs(a - b) < dynamicEpsilon;
}

float intersectBoundingBox(in Ray ray, in AABB aabb, float currentT){
    float tx1 = (aabb.min.x - ray.origin.x) * ray.inverseDirection.x;
    float tx2 = (aabb.max.x - ray.origin.x) * ray.inverseDirection.x;
    float tmin = min(tx1, tx2);
    float tmax = max(tx1, tx2);

    float ty1 = (aabb.min.y - ray.origin.y) * ray.inverseDirection.y;
    float ty2 = (aabb.max.y - ray.origin.y) * ray.inverseDirection.y;
    tmin = max(tmin, min(ty1, ty2));
    tmax = min(tmax, max(ty1, ty2));

    float tz1 = (aabb.min.z - ray.origin.z) * ray.inverseDirection.z;
    float tz2 = (aabb.max.z - ray.origin.z) * ray.inverseDirection.z;
    tmin = max(tmin, min(tz1, tz2));
    tmax = min(tmax, max(tz1, tz2));

    if(tmax >= tmin && tmin < currentT && tmax > 0){
        return tmin;
    }
    return FloatMax;
}

#endif // COMMON_GLSL
