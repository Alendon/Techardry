#version 460
#extension GL_EXT_nonuniform_qualifier : require

#define Dimensions 16
#define MaxDepth 10

layout (location = 0) in vec3 in_position;

layout (location = 0) out vec3 out_color;

struct Ray{
    vec3 origin, direction, inverseDirection;
};

struct CameraDataStruct{
    float HFov;
    float AspectRatio;
    float ForwardX;
    float ForwardY;
    float ForwardZ;
    float UpwardX;
    float UpwardY;
    float UpwardZ;
    float PositionX;
    float PositionY;
    float PositionZ;
    float Near;
    float Far;
};

layout(set = 0, binding = 0) readonly uniform CameraData
{
    CameraDataStruct data;
} camera;

layout(set = 1, binding = 0) uniform sampler2DArray tex;

layout (input_attachment_index = 0, set = 2, binding = 0) uniform subpassInput inDepth;
layout (input_attachment_index = 1, set = 2, binding = 1) uniform subpassInput inColor;

layout(std430, set = 3, binding = 0) readonly buffer MasterOctree
{
    int minX;
    int minY;
    int minZ;
    int dimension;
    int depth;
    int data[];
} masterOctree;

layout(std430, set = 4, binding = 0) readonly buffer Octree
{
    uint nodeCount;
    int minX;
    int minY;
    int minZ;
    uint data[];
} trees[];

#define NodeSize 6

#define Node_Children_Offset 0
uint NodeChildren(uint tree, uint nodeIndex, uint childIndex){
    return trees[nonuniformEXT(tree)].data[nodeIndex * NodeSize + Node_Children_Offset] + childIndex;
}
    #undef Node_Children_Offset

    #define Node_DataIndex_Offset 1
uint NodeDataIndex(uint tree, uint nodeIndex){
    return trees[nonuniformEXT(tree)].data[nodeIndex * NodeSize + Node_DataIndex_Offset];
}
    #undef Node_DataIndex_Offset

    #define Node_Index_Offset 2
uint NodeIndex(uint tree, uint nodeIndex){
    return trees[nonuniformEXT(tree)].data[nodeIndex * NodeSize + Node_Index_Offset];
}
    #undef Node_Index_Offset

    #define Node_ParentIndex_Offset 3
uint NodeParentIndex(uint tree, uint nodeIndex){
    return trees[nonuniformEXT(tree)].data[nodeIndex * NodeSize + Node_ParentIndex_Offset];
}
    #undef Node_ParentIndex_Offset

    #define Node_AdditionalData_Offset 4

uint NodeParentChildIndex(uint tree, uint nodeIndex){
    return trees[nonuniformEXT(tree)].data[nodeIndex * NodeSize + Node_AdditionalData_Offset] & 0x7;
}


bool NodeLeaf(uint tree, uint nodeIndex){
    return NodeChildren(tree, nodeIndex, 0) == 0xFFFFFFFF;
}

uint NodeDepth(uint tree, uint nodeIndex){
    return (trees[nonuniformEXT(tree)].data[nodeIndex * NodeSize + Node_AdditionalData_Offset] & 0xF0 ) >> 4;
}

bool NodeIsEmpty(uint tree, uint nodeIndex){
    return NodeDataIndex(tree, nodeIndex) == 0xFFFFFFFF;
}
    #undef Node_AdditionalData_Offset


    #define VoxelSize 6

    #define Voxel_Color_Offset 0
uint VoxelColor(uint tree, uint voxelIndex){
    uint nodeCount = trees[nonuniformEXT(tree)].nodeCount;
    return trees[nonuniformEXT(tree)].data[nodeCount * NodeSize + voxelIndex * VoxelSize + Voxel_Color_Offset];
}
    #undef Voxel_Color_Offset

    #define Voxel_TextureStartX_Offset 1
uint VoxelTextureStartX(uint tree, uint voxelIndex){
    uint nodeCount = trees[nonuniformEXT(tree)].nodeCount;
    return trees[nonuniformEXT(tree)].data[nodeCount * NodeSize + voxelIndex * VoxelSize + Voxel_TextureStartX_Offset];
}
    #undef Voxel_TextureStartX_Offset

    #define Voxel_TextureStartY_Offset 2
uint VoxelTextureStartY(uint tree, uint voxelIndex){
    uint nodeCount = trees[nonuniformEXT(tree)].nodeCount;
    return trees[nonuniformEXT(tree)].data[nodeCount * NodeSize + voxelIndex * VoxelSize + Voxel_TextureStartY_Offset];
}
    #undef Voxel_TextureStartY_Offset

    #define Voxel_ArrayIndex_Offset 3
uint VoxelArrayIndex(uint tree, uint voxelIndex){
    uint nodeCount = trees[nonuniformEXT(tree)].nodeCount;
    return trees[nonuniformEXT(tree)].data[nodeCount * NodeSize + voxelIndex * VoxelSize + Voxel_ArrayIndex_Offset];
}
    #undef Voxel_ArrayIndex_Offset

    #define Voxel_TextureSizeX_Offset 4
uint VoxelTextureSizeX(uint tree, uint voxelIndex){
    uint nodeCount = trees[nonuniformEXT(tree)].nodeCount;
    return trees[nonuniformEXT(tree)].data[nodeCount * NodeSize + voxelIndex * VoxelSize + Voxel_TextureSizeX_Offset];
}
    #undef Voxel_TextureSizeX_Offset

    #define Voxel_TextureSizeY_Offset 5
uint VoxelTextureSizeY(uint tree, uint voxelIndex){
    uint nodeCount = trees[nonuniformEXT(tree)].nodeCount;
    return trees[nonuniformEXT(tree)].data[nodeCount * NodeSize + voxelIndex * VoxelSize + Voxel_TextureSizeY_Offset];
}
    #undef Voxel_TextureSizeY_Offset

    #undef VoxelSize

    #undef NodeSize

struct Result{
    uint nodeIndex;
    vec3 normal;
    vec2 uv;
    float t;
    int tree;
    bool fail;
};


bool raycast(in Ray ray, inout Result result);
bool raycastChunk(in Ray ray, int tree, vec3 t0, vec3 t1, int childIndexModifier, inout Result result);


float linearDepth(float depth)
{
    float z = depth * 2.0f - 1.0f;
    return (2.0f * camera.data.Near * camera.data.Far) / (camera.data.Far + camera.data.Near - z * (camera.data.Far - camera.data.Near));
}

//delinearize depth to get the depth value in the range [0, 1]
//(the inverse of linearDepth)
float delinearizeDepth(float linearDepth)
{
    return ((- (((2 * camera.data.Near * camera.data.Far) / linearDepth) - camera.data.Far - camera.data.Near) / (camera.data.Far - camera.data.Near)) + 1.0f) / 2.0f;
}

void main()
{
    vec3 octreePosition = vec3(-Dimensions / 2, -Dimensions / 2 - 20, -Dimensions / 2);
    vec3 camPos = vec3(camera.data.PositionX, camera.data.PositionY, camera.data.PositionZ);

    vec2 screenPos = vec2(in_position.xy);

    vec3 forward = vec3(camera.data.ForwardX, camera.data.ForwardY, camera.data.ForwardZ);
    vec3 upward = vec3(camera.data.UpwardX, camera.data.UpwardY, camera.data.UpwardZ);
    vec3 right = cross(forward, upward);

    float fov = camera.data.HFov;
    float angle = tan(fov / 2);


    float ratio = camera.data.AspectRatio;
    float x = screenPos.x * ratio * angle;
    float y = screenPos.y * angle;

    vec3 direction = normalize(forward + x * right + y * upward);


    Ray ray;
    ray.origin = camPos;
    ray.direction = normalize(direction);
    ray.inverseDirection = 1 / ray.direction;
    float depth = subpassLoad(inDepth).r;
    vec3 oldColor = subpassLoad(inColor).rgb;

    Result result = Result(0, vec3(0, 0, 0), vec2(0, 0), 0, -1, false);

    bool hit = raycast(ray, result);

    if(result.fail){
        out_color = vec3(1,0,0);
        return;
    }

    if ( !hit){
        out_color = oldColor;
        return;
    }

    float lDepth = linearDepth(depth);

    float hitDepth = result.t - camera.data.Near;

    if (hitDepth > lDepth){
        out_color = oldColor;
        return;
    }
    

    float newDepth = delinearizeDepth(hitDepth);
    gl_FragDepth = newDepth;

    #define TextureSize 128.

    uint voxel = NodeDataIndex(result.tree, result.nodeIndex);



    vec3 texStart = vec3(VoxelTextureStartX(result.tree, voxel) / TextureSize, VoxelTextureStartY(result.tree, voxel) / TextureSize, VoxelArrayIndex(result.tree, voxel));

    vec2 texSize = vec2(VoxelTextureSizeX(result.tree, voxel) / TextureSize, VoxelTextureSizeY(result.tree, voxel) / TextureSize);
    out_color = texture(tex, texStart + vec3(result.uv * texSize, 0)).rgb;


    if (result.normal.x != 0)
    {
        out_color *= 0.5;
    }
    if (result.normal.y != 0)
    {
        out_color *= 0.75;
    }
    if (result.normal.z != 0)
    {
        out_color *= 1;
    }

}

int getFirstNode(vec3 t0, vec3 tm){
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

int nextNode(vec3 tm, ivec3 c){
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

int getNextChildIndex(int currentChildIndex, vec3 t0, vec3 t1, vec3 tm){
    switch (currentChildIndex){
        case -1:{
            return getFirstNode(t0, tm);
        }
        case 0:{
            return nextNode(vec3(tm.x, tm.y, tm.z), ivec3(4, 2, 1));
        }
        case 1:{
            return nextNode(vec3(tm.x, tm.y, t1.z), ivec3(5, 3, 8));
        }
        case 2:{
            return nextNode(vec3(tm.x, t1.y, tm.z), ivec3(6, 8, 3));
        }
        case 3:{
            return nextNode(vec3(tm.x, t1.y, t1.z), ivec3(7, 8, 8));
        }
        case 4:{
            return nextNode(vec3(t1.x, tm.y, tm.z), ivec3(8, 6, 5));
        }
        case 5:{
            return nextNode(vec3(t1.x, tm.y, t1.z), ivec3(8, 7, 8));
        }
        case 6:{
            return nextNode(vec3(t1.x, t1.y, tm.z), ivec3(8, 8, 7));
        }
        default :{
            return 8;
        }
    }
}

void getChildT(int childIndex, vec3 t0, vec3 t1, vec3 tm, out vec3 childT0, out vec3 childT1){
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


bool raycast(in Ray ray, inout Result result){
    vec3 treeMin = vec3(masterOctree.minX, masterOctree.minY, masterOctree.minZ);
    vec3 treeMax = treeMin + masterOctree.dimension;

    int childIndexModifier = 0;
    ray.direction = normalize(ray.direction);
    Ray originalRay = ray;
    
    //This algorithm only works with positive direction values. Those adjustements fixes negative directions
    if (ray.direction.x < 0){
        ray.origin.x =  treeMin.x * 2 + masterOctree.dimension - ray.origin.x;
        ray.direction.x = -ray.direction.x;
        childIndexModifier |= 4;
    }
    if (ray.direction.y < 0){
        ray.origin.y = treeMin.y * 2 + masterOctree.dimension - ray.origin.y;
        ray.direction.y = -ray.direction.y;
        childIndexModifier |= 2;
    }
    if (ray.direction.z < 0){
        ray.origin.z = treeMin.z * 2 + masterOctree.dimension - ray.origin.z;
        ray.direction.z = -ray.direction.z;
        childIndexModifier |= 1;
    }

    vec3 dirInverse = 1 / ray.direction;

    vec3 t0 = (treeMin - ray.origin) * dirInverse;
    vec3 t1 = (treeMax - ray.origin) * dirInverse;

    //Early exit if the tree isnt hit
    if (max(max(t0.x, t0.y), t0.z) > min(min(t1.x, t1.y), t1.z)){
        result.fail = false;
        return false;
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
            result.fail = true;
            return false;
        }

        int currentDepth = stackIndex;

        StackEntry currentEntry = stack[stackIndex];
        stackIndex--;

        t0 = currentEntry.t0;
        t1 = currentEntry.t1;

        uint node = currentEntry.nodeIndex;

        if (t1.x < 0 || t1.y < 0 || t1.z < 0){
            continue;
        }

        int masterOctreeTargetDepth = masterOctree.depth;

        if (currentDepth == masterOctreeTargetDepth){
            int tree = masterOctree.data[node];
            
            if(tree == -1) continue;

            /*tMax = max(max(t0.x, t0.y), t0.z);
            return true;*/
            

            //return raycastChunk(originalRay, tree, result);
            if(raycastChunk(originalRay, tree, t0, t1, childIndexModifier, result)){
                result.tree = tree;
                return true;
            }
            if(result.fail){
                return false;
            }

            continue;
        }

        vec3 tm = (t0 + t1) * 0.5;

        int lastChildIndex = currentEntry.lastChildIndex;
        int nextChildIndex = getNextChildIndex(lastChildIndex, t0, t1, tm);

        if (nextChildIndex >= 8){
            //The end is reached
            continue;
        }

        //Get the parameters for the next child
        vec3 childT0;
        vec3 childT1;
        getChildT(nextChildIndex, t0, t1, tm, childT0, childT1);

        stackIndex++;
        currentEntry.lastChildIndex = nextChildIndex;
        stack[stackIndex] = currentEntry;

        stackIndex++;

        uint nodeChildren= node * 8 + (nextChildIndex ^ childIndexModifier);
        stack[stackIndex] = StackEntry(nodeChildren, -1, childT0, childT1);
    }
    
    return false;
}

bool raycastChunk(in Ray ray, int tree, vec3 t0, vec3 t1, int childIndexModifier, inout Result result){
    //Early exit if the tree isnt hit
    if (max(max(t0.x, t0.y), t0.z) > min(min(t1.x, t1.y), t1.z)){
        return false;
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
            return false;
        }

        StackEntry currentEntry = stack[stackIndex];
        stackIndex--;

        t0 = currentEntry.t0;
        t1 = currentEntry.t1;

        uint node = currentEntry.nodeIndex;

        if (t1.x < 0 || t1.y < 0 || t1.z < 0 || max(max(t0.x, t0.y), t0.z) > min(min(t1.x, t1.y), t1.z)){
            continue;
        }

        if (NodeLeaf(tree, node)){
            //We found a leaf.
            if (NodeIsEmpty(tree, node)){
                continue;
            }
            else {
                //TODO hit results
                result.nodeIndex = node;
                result.tree = tree;

                if (t0.x > t0.y && t0.x > t0.z){

                    result.t = t0.x;

                    vec3 hitPos = ray.origin + ray.direction * result.t;
                    result.uv = mod(vec2(hitPos.y, hitPos.z), 1);

                    if (ray.direction.x > 0){
                        result.normal = vec3(-1, 0, 0);
                    }
                    else {
                        result.normal = vec3(1, 0, 0);
                    }
                }
                else if (t0.y > t0.x && t0.y > t0.z){


                    result.t = t0.y;

                    vec3 hitPos = ray.origin + ray.direction * result.t;
                    result.uv = mod(vec2(hitPos.x, hitPos.z), 1);

                    if (ray.direction.y > 0){
                        result.normal = vec3(0, -1, 0);
                    }
                    else {
                        result.normal = vec3(0, 1, 0);
                    }
                }
                else if (t0.z > t0.x && t0.z > t0.y){


                    result.t = t0.z;

                    vec3 hitPos = ray.origin + ray.direction * result.t;
                    result.uv = mod(vec2(hitPos.x, hitPos.y), 1);

                    if (ray.direction.z > 0){
                        result.normal = vec3(0, 0, -1);
                    }
                    else {
                        result.normal = vec3(0, 0, 1);

                        result.uv.r = abs(result.uv.r - 1);
                    }
                }

                return true;
            }
        }

        vec3 tm = (t0 + t1) * 0.5;

        int lastChildIndex = currentEntry.lastChildIndex;
        int nextChildIndex = getNextChildIndex(lastChildIndex, t0, t1, tm);

        if (nextChildIndex >= 8){
            //The end is reached
            continue;
        }

        //Get the parameters for the next child
        vec3 childT0;
        vec3 childT1;
        getChildT(nextChildIndex, t0, t1, tm, childT0, childT1);

        stackIndex++;
        currentEntry.lastChildIndex = nextChildIndex;
        stack[stackIndex] = currentEntry;

        stackIndex++;

        uint nodeChildren = NodeChildren(tree, node, nextChildIndex ^ childIndexModifier);
        stack[stackIndex] = StackEntry(nodeChildren, -1, childT0, childT1);
    }

    return false;
}