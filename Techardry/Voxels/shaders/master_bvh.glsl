#ifndef MASTER_BVH_GLSL
#define MASTER_BVH_GLSL

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

bool bvhNode_IsLeaf(in BvhNode node);
AABB bvhNode_GetAABB(in BvhNode node);

void raycast(in Ray ray, inout Result result){

    int nodeIndex = 0;
    int stack[BvhStackSize];
    int stackIndex = 0;

    while(true)
    {
        if(bvhNode_IsLeaf(masterBvh.nodes[nodeIndex])){
            for(int i = 0; i < masterBvh.nodes[nodeIndex].count; i++){
                int tree = masterBvhIndices.indices[masterBvh.nodes[nodeIndex].leftFirst + i];
                raycastChunk(ray, tree, result);
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
    vec3 texStart = vec3(voxelData_GetTextureStartX(result.tree, voxel) / TextureSize, voxelData_GetTextureStartY(result.tree, voxel) / TextureSize, voxelData_GetTextureArrayIndex(result.tree, voxel));
    vec2 texSize = vec2(voxelData_GetTextureSizeX(result.tree, voxel) / TextureSize, voxelData_GetTextureSizeY(result.tree, voxel) / TextureSize);
    return texture(tex, texStart + vec3(result.uv * texSize, 0)).rgb;
}

#endif // MASTER_BVH_GLSL
