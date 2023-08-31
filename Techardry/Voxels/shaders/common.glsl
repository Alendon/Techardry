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

Result resultEmpty(){
    return Result(0, vec3(0, 0, 0), vec2(0, 0), FloatMax, -1, false, vec3(0, 0, 1));
}

bool resultHit(Result result){
    return !floatEquals(result.t, FloatMax);
}

//This is the general layout / header of all trees which are contained in the master bvh
layout(std430, set = RENDER_DATA_SET, binding = RENDER_DATA_SET_OCTREE_BINDING) readonly buffer UniformTree
{
    uint treeType;
//deconstruct the inverse transform mat4 into 16 floats
    float inverseTransformMatrix[16];
    float transformMatrix[16];
    float transposedNormalMatrix[9];
    uint data[];
} trees[];

mat4 getTreeInverseTransform(int tree){
    return mat4(
    trees[nonuniformEXT(tree)].inverseTransformMatrix[0], trees[nonuniformEXT(tree)].inverseTransformMatrix[1], trees[nonuniformEXT(tree)].inverseTransformMatrix[2], trees[nonuniformEXT(tree)].inverseTransformMatrix[3],
    trees[nonuniformEXT(tree)].inverseTransformMatrix[4], trees[nonuniformEXT(tree)].inverseTransformMatrix[5], trees[nonuniformEXT(tree)].inverseTransformMatrix[6], trees[nonuniformEXT(tree)].inverseTransformMatrix[7],
    trees[nonuniformEXT(tree)].inverseTransformMatrix[8], trees[nonuniformEXT(tree)].inverseTransformMatrix[9], trees[nonuniformEXT(tree)].inverseTransformMatrix[10], trees[nonuniformEXT(tree)].inverseTransformMatrix[11],
    trees[nonuniformEXT(tree)].inverseTransformMatrix[12], trees[nonuniformEXT(tree)].inverseTransformMatrix[13], trees[nonuniformEXT(tree)].inverseTransformMatrix[14], trees[nonuniformEXT(tree)].inverseTransformMatrix[15]
    );
}

mat4 getTreeTransform(int tree){
    return mat4(
    trees[nonuniformEXT(tree)].transformMatrix[0], trees[nonuniformEXT(tree)].transformMatrix[1], trees[nonuniformEXT(tree)].transformMatrix[2], trees[nonuniformEXT(tree)].transformMatrix[3],
    trees[nonuniformEXT(tree)].transformMatrix[4], trees[nonuniformEXT(tree)].transformMatrix[5], trees[nonuniformEXT(tree)].transformMatrix[6], trees[nonuniformEXT(tree)].transformMatrix[7],
    trees[nonuniformEXT(tree)].transformMatrix[8], trees[nonuniformEXT(tree)].transformMatrix[9], trees[nonuniformEXT(tree)].transformMatrix[10], trees[nonuniformEXT(tree)].transformMatrix[11],
    trees[nonuniformEXT(tree)].transformMatrix[12], trees[nonuniformEXT(tree)].transformMatrix[13], trees[nonuniformEXT(tree)].transformMatrix[14], trees[nonuniformEXT(tree)].transformMatrix[15]
    );
}

mat3 getTreeTransposedNormalMatrix(int tree){
    return mat3(
    trees[nonuniformEXT(tree)].transposedNormalMatrix[0], trees[nonuniformEXT(tree)].transposedNormalMatrix[1], trees[nonuniformEXT(tree)].transposedNormalMatrix[2],
    trees[nonuniformEXT(tree)].transposedNormalMatrix[3], trees[nonuniformEXT(tree)].transposedNormalMatrix[4], trees[nonuniformEXT(tree)].transposedNormalMatrix[5],
    trees[nonuniformEXT(tree)].transposedNormalMatrix[6], trees[nonuniformEXT(tree)].transposedNormalMatrix[7], trees[nonuniformEXT(tree)].transposedNormalMatrix[8]
    );
}

vec3 normalToWorldSpace(vec3 normal, int tree){
    return normalize(getTreeTransposedNormalMatrix(tree) * normal);
}

float tToWorldSpace(float t, vec3 rayDirection, int tree){
    return t * length(getTreeTransform(tree) * vec4(rayDirection, 0));
}

#endif // COMMON_GLSL
