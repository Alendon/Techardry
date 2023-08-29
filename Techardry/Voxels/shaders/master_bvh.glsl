#ifndef MASTER_BVH_GLSL
#define MASTER_BVH_GLSL

#define INVALID_TREE_TYPE 0
#define VOXEL_OCTREE_TYPE 1

#include "common.glsl"
#include "voxel_octree.glsl"

layout(std430, set = RENDER_DATA_SET, binding = RENDER_DATA_SET_MASTER_BVH_BINDING) readonly buffer MasterBvh
{
    BvhNode nodes[];
} masterBvh;

layout(std430, set = RENDER_DATA_SET, binding = RENDER_DATA_SET_MASTER_BVH_INDICES_BINDING) readonly buffer MasterBvhIndices
{
    int indices[];
} masterBvhIndices;

layout(std430, set = RENDER_DATA_SET, binding = RENDER_DATA_SET_OCTREE_BINDING) readonly buffer UniformTree
{
    uint treeType;
    //deconstruct the inverse transform mat4 into 16 floats
    float inverseTransformMatrix[16];
    uint data[];
} trees[];

bool bvhNode_IsLeaf(in BvhNode node);
AABB bvhNode_GetAABB(in BvhNode node);
mat4 getTreeInverseTransform(int tree);

void raycast_tree(in Ray ray, in int tree, inout Result result){
    //transform the ray into the tree's local space
    mat4 inverseTransform = getTreeInverseTransform(tree);
    
    Ray originalRay = ray;
    
    vec4 rayOrigin = inverseTransform * vec4(ray.origin, 1);
    vec4 rayDirection = inverseTransform * vec4(ray.direction, 0);
    
    ray.origin = rayOrigin.xyz;
    ray.direction = normalize(rayDirection.xyz);
    ray.inverseDirection = 1.0 / ray.direction;
    
    
    switch(trees[tree].treeType){
        case VOXEL_OCTREE_TYPE:
            raycastChunk(ray, originalRay, tree, result);
            break;
        default:
        case INVALID_TREE_TYPE:
            result.fail = true;
            break;
    }
}

void raycast(in Ray ray, inout Result result){

    int nodeIndex = 0;
    int stack[BvhStackSize];
    int stackIndex = 0;

    while(true)
    {
        if(bvhNode_IsLeaf(masterBvh.nodes[nodeIndex])){
            for(int i = 0; i < masterBvh.nodes[nodeIndex].count; i++){
                int tree = masterBvhIndices.indices[masterBvh.nodes[nodeIndex].leftFirst + i];
                raycast_tree(ray, tree, result);
                if(result.fail) 
                    return;
            }

            if(stackIndex == 0) break;
            nodeIndex = stack[--stackIndex];
            continue;
        }

        int child1 = masterBvh.nodes[nodeIndex].leftFirst;
        int child2 = child1 + 1;

        float dist1 = intersectBoundingBox(ray, bvhNode_GetAABB(masterBvh.nodes[child1]), result.t);
        float dist2 = intersectBoundingBox(ray, bvhNode_GetAABB(masterBvh.nodes[child2]), result.t);

        if(dist1 > dist2){
            int tempChild = child1;
            child1 = child2;
            child2 = tempChild;

            float tempDist = dist1;
            dist1 = dist2;
            dist2 = tempDist;
        }

        if( floatEquals(dist1, FloatMax)){
            if(stackIndex == 0) break;
            nodeIndex = stack[--stackIndex];
        }
        else{
            nodeIndex = child1;
            if(!floatEquals(dist2, FloatMax)){
                stack[stackIndex++] = child2;
            }
        }
    }
}

bool bvhNode_IsLeaf(in BvhNode node){
    return node.count > 0;
}

AABB bvhNode_GetAABB(in BvhNode node){
    return AABB(vec3(node.minX, node.minY, node.minZ), vec3(node.maxX, node.maxY, node.maxZ));
}

vec3 resultGetColor(in Result result){
    uint voxel = voxelNode_GetDataIndex(result.tree, result.nodeIndex);
    vec2 texStart = vec2(voxelData_GetTextureStartX(result.tree, voxel), voxelData_GetTextureStartY(result.tree, voxel));
    vec2 texSize = vec2(voxelData_GetTextureSizeX(result.tree, voxel), voxelData_GetTextureSizeY(result.tree, voxel));
    vec2 texEnd = texStart + texSize;
    return texture(tex, mix(texStart, texEnd, result.uv)).rgb;
}

mat4 getTreeInverseTransform(int tree){
    return mat4(
        trees[nonuniformEXT(tree)].inverseTransformMatrix[0], trees[nonuniformEXT(tree)].inverseTransformMatrix[1], trees[nonuniformEXT(tree)].inverseTransformMatrix[2], trees[nonuniformEXT(tree)].inverseTransformMatrix[3],
        trees[nonuniformEXT(tree)].inverseTransformMatrix[4], trees[nonuniformEXT(tree)].inverseTransformMatrix[5], trees[nonuniformEXT(tree)].inverseTransformMatrix[6], trees[nonuniformEXT(tree)].inverseTransformMatrix[7],
        trees[nonuniformEXT(tree)].inverseTransformMatrix[8], trees[nonuniformEXT(tree)].inverseTransformMatrix[9], trees[nonuniformEXT(tree)].inverseTransformMatrix[10], trees[nonuniformEXT(tree)].inverseTransformMatrix[11],
        trees[nonuniformEXT(tree)].inverseTransformMatrix[12], trees[nonuniformEXT(tree)].inverseTransformMatrix[13], trees[nonuniformEXT(tree)].inverseTransformMatrix[14], trees[nonuniformEXT(tree)].inverseTransformMatrix[15]
    );
}

#endif // MASTER_BVH_GLSL
