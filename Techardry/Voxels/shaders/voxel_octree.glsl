#ifndef VOXEL_OCTREE_GLSL
#define VOXEL_OCTREE_GLSL

#include "common.glsl"

layout(std430, set = RENDER_DATA_SET, binding = RENDER_DATA_SET_OCTREE_BINDING) readonly buffer Octree
{
    uint nodeCount;
    int minX;
    int minY;
    int minZ;
    uint data[];
} trees[];

int octree_GetFirstNode(vec3 t0, vec3 tm);
int octree_GetNextNode(vec3 tm, ivec3 c);
int octree_GetNextChildIndex(int currentChildIndex, vec3 t0, vec3 t1, vec3 tm);
void octree_GetChildT(int childIndex, vec3 t0, vec3 t1, vec3 tm, out vec3 childT0, out vec3 childT1);

uint voxelNode_GetChildren(uint tree, uint nodeIndex, uint childIndex);
uint voxelNode_GetDataIndex(uint tree, uint nodeIndex);
uint voxelNode_GetNodeIndex(uint tree, uint nodeIndex);
uint voxelNode_GetParentIndex(uint tree, uint nodeIndex);
uint voxelNode_GetParentChildIndex(uint tree, uint nodeIndex);
bool voxelNode_IsLeaf(uint tree, uint nodeIndex);
uint voxelNode_GetDepth(uint tree, uint nodeIndex);
bool voxelNode_IsEmpty(uint tree, uint nodeIndex);
uint voxelData_GetColor(uint tree, uint voxelIndex);
uint voxelData_GetTextureStartX(uint tree, uint voxelIndex);
uint voxelData_GetTextureStartY(uint tree, uint voxelIndex);
uint voxelData_GetTextureArrayIndex(uint tree, uint voxelIndex);
uint voxelData_GetTextureSizeX(uint tree, uint voxelIndex);
uint voxelData_GetTextureSizeY(uint tree, uint voxelIndex);

void raycastChunk(in Ray ray, int tree, inout Result result){

    vec3 treeMin = vec3(trees[nonuniformEXT(tree)].minX, trees[nonuniformEXT(tree)].minY, trees[nonuniformEXT(tree)].minZ) * Dimensions;
    vec3 treeMax = treeMin + Dimensions;

    int childIndexModifier = 0;
    ray.direction = normalize(ray.direction);
    Ray originalRay = ray;

    //This algorithm only works with positive direction values. Those adjustements fixes negative directions
    if (ray.direction.x < 0){
        ray.origin.x =  treeMin.x * 2 + Dimensions - ray.origin.x;
        ray.direction.x = -ray.direction.x;
        childIndexModifier |= 4;
    }
    if (ray.direction.y < 0){
        ray.origin.y = treeMin.y * 2 + Dimensions - ray.origin.y;
        ray.direction.y = -ray.direction.y;
        childIndexModifier |= 2;
    }
    if (ray.direction.z < 0){
        ray.origin.z = treeMin.z * 2 + Dimensions - ray.origin.z;
        ray.direction.z = -ray.direction.z;
        childIndexModifier |= 1;
    }

    vec3 dirInverse = 1 / ray.direction;

    vec3 t0 = (treeMin - ray.origin) * dirInverse;
    vec3 t1 = (treeMax - ray.origin) * dirInverse;

    //Early exit if the tree isnt hit
    if (max(max(t0.x, t0.y), t0.z) > min(min(t1.x, t1.y), t1.z)){
        return;
    }

    //Normally at this point a recursion is used to traverse the tree.
    //But since this is glsl we can't use recursion.
    //So we use a stack to store the nodes to traverse.

    struct StackEntry{
        uint nodeIndex;
        int lastChildIndex;
        vec3 t0;
        vec3 t1;
    } stack[MaxDepth + 1];

    int stackIndex = 0;
    stack[0] = StackEntry(0, -1, t0, t1);

    int counter = 0;

    while (stackIndex >= 0){

        counter++;
        if(counter > 1000){
            result.tree = tree;
            result.fail = true;
            result.failColor = vec3(0,1,0);
            return;
        }

        //Split definition and declaration to avoid a false error from the glsl plugin
        StackEntry currentEntry;
        currentEntry = stack[stackIndex];

        stackIndex--;

        t0 = currentEntry.t0;
        t1 = currentEntry.t1;

        uint node = currentEntry.nodeIndex;

        if (t1.x < 0 || t1.y < 0 || t1.z < 0 || max(max(t0.x, t0.y), t0.z) > min(min(t1.x, t1.y), t1.z)){
            continue;
        }

        if (voxelNode_IsLeaf(tree, node)){
            //We found a leaf.
            if (voxelNode_IsEmpty(tree, node)){
                continue;
            }
            else {
                if (t0.x > t0.y && t0.x > t0.z && t0.x < result.t){

                    result.t = t0.x;
                    result.nodeIndex = node;
                    result.tree = tree;

                    vec3 hitPos = originalRay.origin + originalRay.direction * result.t;
                    result.uv = mod(vec2(hitPos.y, hitPos.z), 1.);

                    if (ray.direction.x > 0){
                        result.normal = vec3(-1, 0, 0);
                    }
                    else {
                        result.normal = vec3(1, 0, 0);
                    }
                }
                else if (t0.y > t0.x && t0.y > t0.z && t0.y < result.t){

                    result.t = t0.y;
                    result.nodeIndex = node;
                    result.tree = tree;

                    vec3 hitPos = originalRay.origin + originalRay.direction * result.t;
                    result.uv = mod(vec2(hitPos.x, hitPos.z), 1.);

                    if (ray.direction.y > 0){
                        result.normal = vec3(0, -1, 0);
                    }
                    else {
                        result.normal = vec3(0, 1, 0);
                    }
                }
                else if (t0.z > t0.x && t0.z > t0.y && t0.z < result.t){

                    result.t = t0.z;
                    result.nodeIndex = node;
                    result.tree = tree;

                    vec3 hitPos = originalRay.origin + originalRay.direction * result.t;
                    result.uv = mod(vec2(hitPos.x, hitPos.y), 1.);

                    if (ray.direction.z > 0){
                        result.normal = vec3(0, 0, -1);
                    }
                    else {
                        result.normal = vec3(0, 0, 1);

                        result.uv.r = abs(result.uv.r - 1);
                    }
                }

                return;
            }
        }

        vec3 tm = (t0 + t1) * 0.5;

        int lastChildIndex = currentEntry.lastChildIndex;
        int nextChildIndex = octree_GetNextChildIndex(lastChildIndex, t0, t1, tm);

        if (nextChildIndex >= 8){
            //The end is reached
            continue;
        }

        //Get the parameters for the next child
        vec3 childT0;
        vec3 childT1;
        octree_GetChildT(nextChildIndex, t0, t1, tm, childT0, childT1);

        stackIndex++;
        currentEntry.lastChildIndex = nextChildIndex;
        stack[stackIndex] = currentEntry;

        stackIndex++;

        uint nodeChildren = voxelNode_GetChildren(tree, node, nextChildIndex ^ childIndexModifier);
        stack[stackIndex] = StackEntry(nodeChildren, -1, childT0, childT1);
    }
}

int octree_GetFirstNode(vec3 t0, vec3 tm){
    int result = 0;

    if (t0.x > t0.y){
        if (t0.x > t0.z)// PLANE YZ
        {
            if (tm.y < t0.x) result|=2;// set bit at position 1
            if (tm.z < t0.x) result|=1;// set bit at position 0 			
            return result;
        }
    }
    else
    {
        if (t0.y > t0.z)// PLANE XZ
        {
            if (tm.x < t0.y) result|=4;// set bit at position 2
            if (tm.z < t0.y) result|=1;// set bit at position 0
            return result;
        }
    }

    // PLANE XY
    if (tm.x < t0.z) result|=4;// set bit at position 2
    if (tm.y < t0.z) result|=2;// set bit at position 1
    return result;
}

int octree_GetNextNode(vec3 tm, ivec3 c){
    if (tm.x < tm.y){
        if (tm.x < tm.z){
            return c.x;
        }
    }
    else {
        if (tm.y < tm.z){
            return c.y;
        }
    }
    return c.z;

}

int octree_GetNextChildIndex(int currentChildIndex, vec3 t0, vec3 t1, vec3 tm){
    switch (currentChildIndex){
        case -1:{
                    return octree_GetFirstNode(t0, tm);
                }
        case 0:{
                    return octree_GetNextNode(vec3(tm.x, tm.y, tm.z), ivec3(4, 2, 1));
                }
        case 1:{
                    return octree_GetNextNode(vec3(tm.x, tm.y, t1.z), ivec3(5, 3, 8));
                }
        case 2:{
                    return octree_GetNextNode(vec3(tm.x, t1.y, tm.z), ivec3(6, 8, 3));
                }
        case 3:{
                    return octree_GetNextNode(vec3(tm.x, t1.y, t1.z), ivec3(7, 8, 8));
                }
        case 4:{
                    return octree_GetNextNode(vec3(t1.x, tm.y, tm.z), ivec3(8, 6, 5));
                }
        case 5:{
                    return octree_GetNextNode(vec3(t1.x, tm.y, t1.z), ivec3(8, 7, 8));
                }
        case 6:{
                    return octree_GetNextNode(vec3(t1.x, t1.y, tm.z), ivec3(8, 8, 7));
                }
        default :{
                    return 8;
                }
    }
}

void octree_GetChildT(int childIndex, vec3 t0, vec3 t1, vec3 tm, out vec3 childT0, out vec3 childT1){
    switch (childIndex){
        case 0:{
                   childT0 = vec3(t0.x, t0.y, t0.z);
                   childT1 = vec3(tm.x, tm.y, tm.z);
                   break;
               }
        case 1:{
                   childT0 = vec3(t0.x, t0.y, tm.z);
                   childT1 = vec3(tm.x, tm.y, t1.z);
                   break;
               }
        case 2:{
                   childT0 = vec3(t0.x, tm.y, t0.z);
                   childT1 = vec3(tm.x, t1.y, tm.z);
                   break;
               }
        case 3:{
                   childT0 = vec3(t0.x, tm.y, tm.z);
                   childT1 = vec3(tm.x, t1.y, t1.z);
                   break;
               }
        case 4:{
                   childT0 = vec3(tm.x, t0.y, t0.z);
                   childT1 = vec3(t1.x, tm.y, tm.z);
                   break;
               }
        case 5:{
                   childT0 = vec3(tm.x, t0.y, tm.z);
                   childT1 = vec3(t1.x, tm.y, t1.z);
                   break;
               }
        case 6:{
                   childT0 = vec3(tm.x, tm.y, t0.z);
                   childT1 = vec3(t1.x, t1.y, tm.z);
                   break;
               }
        case 7:{
                   childT0 = vec3(tm.x, tm.y, tm.z);
                   childT1 = vec3(t1.x, t1.y, t1.z);
                   break;
               }
    }
}

#define NodeSize 6

#define Node_Children_Offset 0
uint voxelNode_GetChildren(uint tree, uint nodeIndex, uint childIndex){
    return trees[nonuniformEXT(tree)].data[nodeIndex * NodeSize + Node_Children_Offset] + childIndex;
}
#undef Node_Children_Offset

    #define Node_DataIndex_Offset 1
uint voxelNode_GetDataIndex(uint tree, uint nodeIndex){
    return trees[nonuniformEXT(tree)].data[nodeIndex * NodeSize + Node_DataIndex_Offset];
}
#undef Node_DataIndex_Offset

    #define Node_Index_Offset 2
uint voxelNode_GetNodeIndex(uint tree, uint nodeIndex){
    return trees[nonuniformEXT(tree)].data[nodeIndex * NodeSize + Node_Index_Offset];
}
#undef Node_Index_Offset

    #define Node_ParentIndex_Offset 3
uint voxelNode_GetParentIndex(uint tree, uint nodeIndex){
    return trees[nonuniformEXT(tree)].data[nodeIndex * NodeSize + Node_ParentIndex_Offset];
}
#undef Node_ParentIndex_Offset

    #define Node_AdditionalData_Offset 4

uint voxelNode_GetParentChildIndex(uint tree, uint nodeIndex){
    return trees[nonuniformEXT(tree)].data[nodeIndex * NodeSize + Node_AdditionalData_Offset] & 0x7;
}


bool voxelNode_IsLeaf(uint tree, uint nodeIndex){
    return voxelNode_GetChildren(tree, nodeIndex, 0) == 0xFFFFFFFF;
}

uint voxelNode_GetDepth(uint tree, uint nodeIndex){
    return (trees[nonuniformEXT(tree)].data[nodeIndex * NodeSize + Node_AdditionalData_Offset] & 0xF0 ) >> 4;
}

bool voxelNode_IsEmpty(uint tree, uint nodeIndex){
    return voxelNode_GetDataIndex(tree, nodeIndex) == 0xFFFFFFFF;
}
#undef Node_AdditionalData_Offset


    #define VoxelSize 6

    #define Voxel_Color_Offset 0
uint voxelData_GetColor(uint tree, uint voxelIndex){
    uint nodeCount = trees[nonuniformEXT(tree)].nodeCount;
    return trees[nonuniformEXT(tree)].data[nodeCount * NodeSize + voxelIndex * VoxelSize + Voxel_Color_Offset];
}
#undef Voxel_Color_Offset

    #define Voxel_TextureStartX_Offset 1
uint voxelData_GetTextureStartX(uint tree, uint voxelIndex){
    uint nodeCount = trees[nonuniformEXT(tree)].nodeCount;
    return trees[nonuniformEXT(tree)].data[nodeCount * NodeSize + voxelIndex * VoxelSize + Voxel_TextureStartX_Offset];
}
#undef Voxel_TextureStartX_Offset

    #define Voxel_TextureStartY_Offset 2
uint voxelData_GetTextureStartY(uint tree, uint voxelIndex){
    uint nodeCount = trees[nonuniformEXT(tree)].nodeCount;
    return trees[nonuniformEXT(tree)].data[nodeCount * NodeSize + voxelIndex * VoxelSize + Voxel_TextureStartY_Offset];
}
#undef Voxel_TextureStartY_Offset

    #define Voxel_ArrayIndex_Offset 3
uint voxelData_GetTextureArrayIndex(uint tree, uint voxelIndex){
    uint nodeCount = trees[nonuniformEXT(tree)].nodeCount;
    return trees[nonuniformEXT(tree)].data[nodeCount * NodeSize + voxelIndex * VoxelSize + Voxel_ArrayIndex_Offset];
}
#undef Voxel_ArrayIndex_Offset

    #define Voxel_TextureSizeX_Offset 4
uint voxelData_GetTextureSizeX(uint tree, uint voxelIndex){
    uint nodeCount = trees[nonuniformEXT(tree)].nodeCount;
    return trees[nonuniformEXT(tree)].data[nodeCount * NodeSize + voxelIndex * VoxelSize + Voxel_TextureSizeX_Offset];
}
#undef Voxel_TextureSizeX_Offset

    #define Voxel_TextureSizeY_Offset 5
uint voxelData_GetTextureSizeY(uint tree, uint voxelIndex){
    uint nodeCount = trees[nonuniformEXT(tree)].nodeCount;
    return trees[nonuniformEXT(tree)].data[nodeCount * NodeSize + voxelIndex * VoxelSize + Voxel_TextureSizeY_Offset];
}
#undef Voxel_TextureSizeY_Offset
#undef VoxelSize
#undef NodeSize

#endif // VOXEL_OCTREE_GLSL
