#ifndef COMMON_GLSL
#define COMMON_GLSL

struct Ray{
    vec3 origin, direction, inverseDirection;
};

struct Result{
    uint nodeIndex;
    vec3 normal;
    vec2 uv;
    float t;
    uint64_t tree;
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

bool intersectBoundingBox(Ray ray, vec3 minBounds, vec3 maxBounds, out float tMin) {
    vec3 invDir = ray.inverseDirection;
    vec3 t0s = (minBounds - ray.origin) * invDir;
    vec3 t1s = (maxBounds - ray.origin) * invDir;

    vec3 tSmaller = min(t0s, t1s);
    vec3 tLarger = max(t0s, t1s);

    tMin = max(max(tSmaller.x, tSmaller.y), tSmaller.z);
    float tMax = min(min(tLarger.x, tLarger.y), tLarger.z);

    return tMin <= tMax && tMax > 0.0;
}

bool isInBoundingBox(vec3 point, vec3 minBounds, vec3 maxBounds){
    return point.x >= minBounds.x && point.x <= maxBounds.x &&
           point.y >= minBounds.y && point.y <= maxBounds.y &&
           point.z >= minBounds.z && point.z <= maxBounds.z;
}

Result resultEmpty(){
    return Result(0, vec3(0, 0, 0), vec2(0, 0), FloatMax, -1, false, vec3(0, 0, 1));
}

bool resultHit(Result result){
    return !floatEquals(result.t, FloatMax);
}

layout(buffer_reference, std430, buffer_reference_align=4) buffer UniformTree
{
    uint treeType;
    //deconstruct the inverse transform mat4 into 16 floats
    float inverseTransformMatrix[16];
    float transformMatrix[16];
    float transposedNormalMatrix[9];
    uint data[];
};

mat4 getTreeInverseTransform(uint64_t tree){
    UniformTree treeRef = UniformTree(tree);
    
    return mat4(
    treeRef.inverseTransformMatrix[0], treeRef.inverseTransformMatrix[1],  treeRef.inverseTransformMatrix[2],  treeRef.inverseTransformMatrix[3],
    treeRef.inverseTransformMatrix[4], treeRef.inverseTransformMatrix[5],  treeRef.inverseTransformMatrix[6],  treeRef.inverseTransformMatrix[7],
    treeRef.inverseTransformMatrix[8], treeRef.inverseTransformMatrix[9],  treeRef.inverseTransformMatrix[10], treeRef.inverseTransformMatrix[11],
    treeRef.inverseTransformMatrix[12],treeRef.inverseTransformMatrix[13], treeRef.inverseTransformMatrix[14], treeRef.inverseTransformMatrix[15]
    );
}

mat4 getTreeTransform(uint64_t tree){
    UniformTree treeRef = UniformTree(tree);
    
    return mat4(
    treeRef.transformMatrix[0],  treeRef.transformMatrix[1],  treeRef.transformMatrix[2],  treeRef.transformMatrix[3],
    treeRef.transformMatrix[4],  treeRef.transformMatrix[5],  treeRef.transformMatrix[6],  treeRef.transformMatrix[7],
    treeRef.transformMatrix[8],  treeRef.transformMatrix[9],  treeRef.transformMatrix[10], treeRef.transformMatrix[11],
    treeRef.transformMatrix[12], treeRef.transformMatrix[13], treeRef.transformMatrix[14], treeRef.transformMatrix[15]
    );
}

mat3 getTreeTransposedNormalMatrix(uint64_t tree){
    UniformTree treeRef = UniformTree(tree);
    
    return mat3(
    treeRef.transposedNormalMatrix[0], treeRef.transposedNormalMatrix[1], treeRef.transposedNormalMatrix[2],
    treeRef.transposedNormalMatrix[3], treeRef.transposedNormalMatrix[4], treeRef.transposedNormalMatrix[5],
    treeRef.transposedNormalMatrix[6], treeRef.transposedNormalMatrix[7], treeRef.transposedNormalMatrix[8]
    );
}

vec3 normalToWorldSpace(vec3 normal, uint64_t tree){
    return normalize(getTreeTransposedNormalMatrix(tree) * normal);
}

float tToWorldSpace(float t, vec3 rayDirection, uint64_t tree){
    return t * length(getTreeTransform(tree) * vec4(rayDirection, 0));
}

#endif // COMMON_GLSL
