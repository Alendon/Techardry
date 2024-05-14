#ifndef VOXEL_OCTREE_GLSL
#define VOXEL_OCTREE_GLSL

#include "common.glsl"

layout(buffer_reference, std430, buffer_reference_align = 4) readonly buffer ChunkOctree
{
    uint treeType;
    //deconstruct the inverse transform mat4 into 16 floats
    float inverseTransformMatrix[16];
    float transformMatrix[16];
    float transposedNormalMatrix[9];
    uint nodeCount;
    uint data[];
};

int octree_GetFirstNode(vec3 t0, vec3 tm);
int octree_GetNextNode(vec3 tm, ivec3 c);
int octree_GetNextChildIndex(int currentChildIndex, vec3 t0, vec3 t1, vec3 tm);
void octree_GetChildT(int childIndex, vec3 t0, vec3 t1, vec3 tm, out vec3 childT0, out vec3 childT1);

uint voxelNode_GetChildren(uint64_t tree, uint nodeIndex, uint childIndex);
uint voxelNode_GetDataIndex(uint64_t tree, uint nodeIndex);
bool voxelNode_IsLeaf(uint64_t tree, uint nodeIndex);
bool voxelNode_IsEmpty(uint64_t tree, uint nodeIndex);
uint voxelData_GetColor(uint64_t tree, uint voxelIndex);
float voxelData_GetTextureStartX(uint64_t tree, uint voxelIndex);
float voxelData_GetTextureStartY(uint64_t tree, uint voxelIndex);
float voxelData_GetTextureSizeX(uint64_t tree, uint voxelIndex);
float voxelData_GetTextureSizeY(uint64_t tree, uint voxelIndex);

void raycastChunk(in Ray ray, uint64_t tree, inout Result result){

    Ray originalRay = ray;
    
    vec3 treeMin = vec3(0);
    vec3 treeMax = treeMin + Dimensions;

    int childIndexModifier = 0;

    //This algorithm only works with positive direction values. Those adjustements fixes negative directions
    if (ray.direction.x < 0){
        ray.origin.x =  Dimensions - ray.origin.x;
        ray.direction.x = -ray.direction.x;
        childIndexModifier |= 4;
    }
    if (ray.direction.y < 0){
        ray.origin.y = Dimensions - ray.origin.y;
        ray.direction.y = -ray.direction.y;
        childIndexModifier |= 2;
    }
    if (ray.direction.z < 0){
        ray.origin.z = Dimensions - ray.origin.z;
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
        if (counter > 1000){
            result.tree = tree;
            result.fail = true;
            result.failColor = vec3(0, 1, 0);
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
                
                float localT = max(max(t0.x, t0.y), t0.z);
                float globalT = tToWorldSpace(localT, ray.direction, tree);
                if (globalT > result.t){
                    return;
                }
                
                result.tree = tree;
                result.nodeIndex = node;
                result.t = globalT;
                
                if (localT == t0.x) {
                    result.normal = originalRay.direction.x > 0.0 ? vec3(-1, 0, 0) : vec3(1, 0, 0);
                } else if (localT == t0.y) {
                    result.normal = originalRay.direction.y > 0.0 ? vec3(0, -1, 0) : vec3(0, 1, 0);
                } else {
                    result.normal = originalRay.direction.z > 0.0 ? vec3(0, 0, -1) : vec3(0, 0, 1);
                }
                result.normal = normalToWorldSpace(result.normal, tree);

                // UV-Koordinaten berechnen
                vec3 hitPos = originalRay.origin + originalRay.direction * localT;
                vec2 uv;
                if (result.normal.x != 0.0) {
                    result.uv = hitPos.yz;
                } else if (result.normal.y != 0.0) {
                    result.uv = hitPos.xz;
                } else {
                    result.uv = hitPos.yx;
                }
                // UV-Koordinaten auf [0, 1] skalieren
                result.uv = fract(result.uv);
                
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

ChunkOctree getChunkOctree(uint64_t tree){
    return ChunkOctree(tree);
}

#define NodeSize 1

#define Node_Children_Offset 0
uint voxelNode_GetChildren(uint64_t tree, uint nodeIndex, uint childIndex){
    ChunkOctree chunkOctree = getChunkOctree(tree);
    uint data = chunkOctree.data[nodeIndex];
    data = data & 0x7FFFFFFFu;
    data = data + childIndex;
    return data;
    
    //return (getChunkOctree(tree).data[nodeIndex] & 0x7FFFFFFFu) + childIndex;
}

uint voxelNode_GetDataIndex(uint64_t tree, uint nodeIndex){
    return getChunkOctree(tree).data[nodeIndex];
}

bool voxelNode_IsLeaf(uint64_t tree, uint nodeIndex){
    return getChunkOctree(tree).data[nodeIndex] < 0x80000000u;
}

bool voxelNode_IsEmpty(uint64_t tree, uint nodeIndex){
    return getChunkOctree(tree).data[nodeIndex] == 0;
}
#undef Node_Children_Offset

#define VoxelSize 5

#define Voxel_Color_Offset 0
uint voxelData_GetColor(uint64_t tree, uint voxelIndex){
    uint nodeCount = getChunkOctree(tree).nodeCount;
    return getChunkOctree(tree).data[nodeCount * NodeSize + voxelIndex * VoxelSize + Voxel_Color_Offset];
}
#undef Voxel_Color_Offset

#define Voxel_TextureStartX_Offset 1
float voxelData_GetTextureStartX(uint64_t tree, uint voxelIndex){
    uint nodeCount = getChunkOctree(tree).nodeCount;
    return uintBitsToFloat(getChunkOctree(tree).data[nodeCount * NodeSize + voxelIndex * VoxelSize + Voxel_TextureStartX_Offset]);
}
#undef Voxel_TextureStartX_Offset

#define Voxel_TextureStartY_Offset 2
float voxelData_GetTextureStartY(uint64_t tree, uint voxelIndex){
    uint nodeCount = getChunkOctree(tree).nodeCount;
    return uintBitsToFloat(getChunkOctree(tree).data[nodeCount * NodeSize + voxelIndex * VoxelSize + Voxel_TextureStartY_Offset]);
}
#undef Voxel_TextureStartY_Offset

#define Voxel_TextureSizeX_Offset 3
float voxelData_GetTextureSizeX(uint64_t tree, uint voxelIndex){
    uint nodeCount = getChunkOctree(tree).nodeCount;
    return uintBitsToFloat(getChunkOctree(tree).data[nodeCount * NodeSize + voxelIndex * VoxelSize + Voxel_TextureSizeX_Offset]);
}
#undef Voxel_TextureSizeX_Offset

#define Voxel_TextureSizeY_Offset 4
float voxelData_GetTextureSizeY(uint64_t tree, uint voxelIndex){
    uint nodeCount = getChunkOctree(tree).nodeCount;
    return uintBitsToFloat(getChunkOctree(tree).data[nodeCount * NodeSize + voxelIndex * VoxelSize + Voxel_TextureSizeY_Offset]);
}
#undef Voxel_TextureSizeY_Offset
#undef VoxelSize
#undef NodeSize

#endif// VOXEL_OCTREE_GLSL
