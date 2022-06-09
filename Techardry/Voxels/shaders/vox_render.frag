#version 460
#extension GL_EXT_debug_printf : enable

#define Dimensions 16
#define ChildCount 8
#define MaxDepth 10
#define printf debugPrintfEXT

layout (location = 0) in vec3 in_position;
//layout (location = 1) in vec3 normal;

layout (location = 0) out vec3 out_color;


//layout(set = 0, binding = 0) uniform CameraBuffer
//{
//    mat4 viewProjection;
//} cameraBuffer;

struct Voxel
{
    float color_r;
    float color_g;
    float color_b;
    float color_a;
};

struct Node
{
    int children[8];
    int dataIndex;
    int Index;
    int ParentIndex;
    int Data;
};

bool raycast(vec3 position, vec3 origin, vec3 direction, out Voxel voxel, out vec3 normal);
bool aabbCheck(vec3 origin, vec3 direction, vec3 minBox, vec3 maxBox, out float T, out vec3 normal);

int ParentChildIndex(Node node)
{
    return node.Data & 0x000000FF;
}

bool IsLeaf(Node node)
{
    return (node.Data & 0x00FF0000) != 0;
}

bool SharesDataWithParent(Node node)
{
    return (node.Data & 0xFF000000) != 0;
}

int DepthOfNode(Node node)
{
    return (node.Data & 0x0000FF00) >> 8;
}


layout(std430, set = 0, binding = 0) readonly buffer OctreeNodes
{
    Node nodes[];
} nodes;

layout(std430, set = 0, binding = 1) readonly buffer OctreeData
{
    Voxel voxels[];
} data;


void main()
{
    vec3 octreePosition = vec3(0,0,0);

    vec2 screenPos = vec2(in_position.xy);
    vec3 cameraDirection = vec3(0,0,1);
    vec3 cameraPlaneU = vec3(1.0, 0.0, 0.0);
	vec3 cameraPlaneV = vec3(0.0, 1.0, 0.0) * 540 / 960;
	vec3 rayDir = cameraDirection + screenPos.x * cameraPlaneU + screenPos.y * cameraPlaneV;
	vec3 rayPos = vec3(0.0, 0, -32.0);

    Voxel voxel;
    vec3 normal;

    vec3 halfSize = vec3(Dimensions) / 2;

    vec3 boxMin = octreePosition - halfSize;
    vec3 boxMax = octreePosition + halfSize;

    vec3 rayPos2 = in_position * halfSize;
    rayPos2 += vec3(0,0,24);
    vec3 rayDir2 = vec3(0,0,-1);

    float T;
    /*if(aabbCheck(rayPos, rayDir, boxMin, boxMax, T, normal)){
        out_color = abs(normal);
    }
    else{
        out_color = vec3(1,1,1);
    }*/

    //print the complete content of nodes.nodes[0]
    printf("Children: %d %d %d %d %d %d %d %d, DataIndex: %d, Index: %d, ParentIndex: %d, Data: %d, %b\n",
        nodes.nodes[0].children[0],
        nodes.nodes[0].children[1],
        nodes.nodes[0].children[2],
        nodes.nodes[0].children[3],
        nodes.nodes[0].children[4],
        nodes.nodes[0].children[5],
        nodes.nodes[0].children[6],
        nodes.nodes[0].children[7],
        nodes.nodes[0].dataIndex,
        nodes.nodes[0].Index,
        nodes.nodes[0].ParentIndex,
        nodes.nodes[0].Data,
        IsLeaf(nodes.nodes[0]));

    raycast(octreePosition, rayPos, rayDir, voxel, normal);
    out_color = vec3(voxel.color_r, voxel.color_g, voxel.color_b);

    
}

vec3 GetChildOffset(int index){
    switch(index){
        case 0: return vec3(-1, +1, -1);
        case 1: return vec3(-1, +1, +1);
        case 2: return vec3(-1, -1, -1);
        case 3: return vec3(-1, -1, +1);
        case 4: return vec3(+1, +1, -1);
        case 5: return vec3(+1, +1, +1);
        case 6: return vec3(+1, -1, -1);
        case 7: return vec3(+1, -1, +1);
    }
    return vec3(0,0,0);
}

bool raycast(vec3 position, vec3 origin, vec3 direction, out Voxel voxel, out vec3 normal){
    Node rootNode = nodes.nodes[0];
    normal = vec3(0,0,0);

    vec3 halfScale = vec3(Dimensions) / 2;
    vec3 center = position + halfScale;
    vec3 minBox = center - halfScale;
    vec3 maxBox = center + halfScale;

    struct StackEntry{
        vec3 center;
        vec3 halfScale;
        int nodeIndex;
        int remainingChildrenToCheck;
        int childSortOrder[ChildCount];
        float childT[ChildCount];
        float T;
    } stack[MaxDepth + 1];

    int stackPos = 0;

    float ignored;
    if(!aabbCheck(origin, direction, minBox, maxBox, ignored, normal))
    {
        voxel.color_r = 0;
        voxel.color_g = 0;
        voxel.color_b = 1;
        voxel.color_a = 0;
        return false;
    }

    //Fill the first stack entry with the center and halfscale
    stack[stackPos].center = center;
    stack[stackPos].halfScale = halfScale;
    stack[stackPos].remainingChildrenToCheck = -1;
    stack[stackPos].nodeIndex = 0;

    struct Children{
        int childIndex;
        float T;
    } children[ChildCount];

    int iterations = 0;
    while(stackPos >= 0){

        iterations++;
        if(iterations > 100){
            voxel.color_r = 1;
            voxel.color_g = 0;
            voxel.color_b = 0;
            return false;
        }

        //Pop the current stack entry
        int nodeIndex = stack[stackPos].nodeIndex;
        center = stack[stackPos].center;
        halfScale = stack[stackPos].halfScale;
        int remainingChildrenToCheck = stack[stackPos].remainingChildrenToCheck;
        int childrenToCheck = -1;

        vec3 childHalfScale = halfScale / 2;
        Node currentNode = nodes.nodes[nodeIndex];

        //Check if the node is a leaf or the max depth is reached
        if(IsLeaf(currentNode) || DepthOfNode(currentNode) + 1 > MaxDepth)
        {
            Voxel current = data.voxels[currentNode.dataIndex];

            //a = 0 is full transparency
            if(current.color_a != 0)
            {
                float T;
                aabbCheck(origin, direction, center - halfScale, center + halfScale, T, normal);
                voxel = current;
                voxel.color_r = 0;
                voxel.color_g = 1;
                voxel.color_b = 0;
                voxel.color_a = 0;
                return true;
            }

            stackPos--;
            continue;
        }

        if(remainingChildrenToCheck == 0)
        {
            stackPos--;
            continue;
        }

        if(remainingChildrenToCheck > 0){
            remainingChildrenToCheck = remainingChildrenToCheck -1;
            childrenToCheck = stack[stackPos].childSortOrder[remainingChildrenToCheck];
        }

        if(remainingChildrenToCheck == -1){
            remainingChildrenToCheck = 0;

            for(int i = 0; i < ChildCount; i++){
                children[i].childIndex = 0;
                children[i].T = 0;
            }

            for(int childIndex = 0; childIndex < ChildCount; childIndex++){

                Node childNode = nodes.nodes[currentNode.children[childIndex]];

                Voxel childVoxel = data.voxels[childNode.dataIndex];

                if(IsLeaf(childNode) && childVoxel.color_a == 0){
                    continue;
                }

                vec3 childCenter = center + (childHalfScale * GetChildOffset(childIndex));

                vec3 childMinBox = childCenter - childHalfScale;
                vec3 childMaxBox = childCenter + childHalfScale;

                float childT;
                vec3 childNormal;
                if(!aabbCheck(origin, direction, childMinBox, childMaxBox, childT, childNormal)){
                    continue;
                }

                children[remainingChildrenToCheck].childIndex = childIndex;
                children[remainingChildrenToCheck].T = childT;
                remainingChildrenToCheck++;
            }

            //Sort the children by the highest T
            for(int i = 0; i < remainingChildrenToCheck; i++){
                for(int j = i + 1; j < remainingChildrenToCheck; j++){
                    if(children[i].T < children[j].T){
                        Children temp = children[i];
                        children[i] = children[j];
                        children[j] = temp;
                    }
                }
            }

            //Fill the stack entry with the children to check
            for(int i = 0; i < remainingChildrenToCheck; i++){
                stack[stackPos].childSortOrder[i] = children[i].childIndex;
                //stack[stackPos].childT[i] = children[i].T;
            }

            childrenToCheck = stack[stackPos].childSortOrder[--remainingChildrenToCheck];
        }

        if(childrenToCheck < 0 || childrenToCheck >= ChildCount){
            stackPos--;
            continue;
        }

        stack[stackPos].remainingChildrenToCheck = remainingChildrenToCheck;

        stackPos++;
        stack[stackPos].nodeIndex = currentNode.children[childrenToCheck];
        stack[stackPos].center = center + childHalfScale * GetChildOffset(childrenToCheck);
        stack[stackPos].halfScale = childHalfScale;
        stack[stackPos].remainingChildrenToCheck = -1;

    }

    voxel.color_r = 0;
    voxel.color_g = 0;
    voxel.color_b = 1;
    voxel.color_a = 0;
    return false;
}

bool aabbCheck(vec3 origin, vec3 direction, vec3 minBox, vec3 maxBox, out float T, out vec3 normal){
    vec3 halfExtent = (maxBox - minBox) / 2;
    vec3 position = minBox + halfExtent;
    vec3 offset = origin - position;

    vec3 offsetToTScale = vec3( direction.x < 0 ? 1: -1, direction.y < 0 ? 1: -1, direction.z < 0 ? 1: -1);
    offsetToTScale = offsetToTScale / max(vec3(1e-15f), abs(direction));

    vec3 negativeT = (offset - halfExtent) * offsetToTScale;
    vec3 positiveT = (offset + halfExtent) * offsetToTScale;
    vec3 entryT = min(negativeT, positiveT);
    vec3 exitT = max(negativeT, positiveT);

    float earliestExit = min(exitT.x, min(exitT.y, exitT.z));
    if(earliestExit < 0)
    {
        return false;
    }

    float latestEntry;
    if (entryT.x > entryT.y)
    {
        if (entryT.x > entryT.z)
        {
           latestEntry = entryT.x;
            normal = vec3(1,0,0);
        }
        else
        {
            latestEntry = entryT.z;
            normal = vec3(0,0,1);
        }
    }
    else
    {
        if (entryT.y > entryT.z)
        {
            latestEntry = entryT.y;
            normal = vec3(0,1,0);
        }
        else
        {
            latestEntry = entryT.z;
            normal = vec3(0,0,1);
        }
    }

    if (earliestExit < latestEntry){
        return false;
    }
    T = max(0, latestEntry);

    if(dot(normal, offset) < 0){
        normal = -normal;
    }
    return true;
}