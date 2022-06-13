﻿#version 460
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
    int not_empty;
};

struct Node
{
    int children[8];
    int dataIndex;
    int Index;
    int ParentIndex;
    int ParentChildIndex;
    int Leaf;
    int ShareData;
    int Depth;
    int IsEmpty;
};

struct Ray{
    vec3 origin, direction, inverseDirection;
};

struct Hit{
    vec3 position;
    float t;
    float tMax;
    float tMin;
    vec3 normal;
};

bool raycast(vec3 position, vec3 origin, vec3 direction, out Voxel voxel, out vec3 normal);
bool aabbCheck(Ray ray, vec3 boxMin, vec3 boxMax, out Hit hit);
bool raycastNew(vec3 position, Ray ray, out Node result, out bool octreeHit, out bool center);

int ParentChildIndex(Node node)
{
    return node.ParentChildIndex;
}

bool IsLeaf(Node node)
{
    return node.Leaf != 0;
}

bool IsEmpty(Node node){
    return node.IsEmpty != 0;
}

bool SharesDataWithParent(Node node)
{
    return node.ShareData != 0;
}

int DepthOfNode(Node node)
{
    return node.Depth;
}


layout(std430, set = 0, binding = 0) readonly buffer OctreeNodes
{
    Node nodes[];
} nodes;

layout(std430, set = 1, binding = 0) readonly buffer OctreeData
{
    Voxel voxels[];
} data;

vec2 rotate2d(vec2 v, float a) {
	float sinA = sin(a);
	float cosA = cos(a);
	return vec2(v.x * cosA - v.y * sinA, v.y * cosA + v.x * sinA);	
}

mat4 rotationMatrix(vec3 axis, float angle) {
    axis = normalize(axis);
    float s = sin(angle);
    float c = cos(angle);
    float oc = 1.0 - c;
    
    return mat4(oc * axis.x * axis.x + c,           oc * axis.x * axis.y - axis.z * s,  oc * axis.z * axis.x + axis.y * s,  0.0,
                oc * axis.x * axis.y + axis.z * s,  oc * axis.y * axis.y + c,           oc * axis.y * axis.z - axis.x * s,  0.0,
                oc * axis.z * axis.x - axis.y * s,  oc * axis.y * axis.z + axis.x * s,  oc * axis.z * axis.z + c,           0.0,
                0.0,                                0.0,                                0.0,                                1.0);
}

vec3 rotate(vec3 v, vec3 axis, float angle) {
	mat4 m = rotationMatrix(axis, angle);
	return (m * vec4(v, 1.0)).xyz;
}

void main()
{
    vec3 octreePosition = vec3(-Dimensions * 2, -Dimensions * 1.5,0);

    vec2 screenPos = vec2(in_position.xy);
    screenPos.y = -screenPos.y;
    vec3 cameraDirection = normalize(vec3(0.00001, 0.00001, 1));
    vec3 cameraPlaneU = vec3(1.0, 0.0, 0.0);
	vec3 cameraPlaneV = vec3(0.0, 1.0, 0.0) *  540.0 / 960.0;
    vec3 rayPos = vec3(0, 0, -64) + Dimensions * 0;

	vec3 rayDir = normalize((cameraDirection + screenPos.x * cameraPlaneU + screenPos.y * cameraPlaneV));

    //rayPos = rotate(rayPos, vec3(0,1,0), 1 * 3.14159265359);
    //rayDir = rotate(rayDir, vec3(0,1,0), 1 * 3.14159265359);
	


    Voxel voxel;
    vec3 normal;

    vec3 boxMin = octreePosition;
    vec3 boxMax = octreePosition + Dimensions;

    Ray ray;
    ray.origin = rayPos;
    ray.direction = rayDir;
    ray.inverseDirection = 1 / ray.direction;

    Hit hit;

    out_color = vec3(1,1,1);

    float T;
    

    Node result;
    bool oHit;
    bool centerHit;
    if(raycastNew(octreePosition, ray, result, oHit, centerHit))
    {
        if(centerHit){
            out_color = vec3(1,0,1);
        }
        else{        
            Voxel voxel = data.voxels[result.dataIndex];
            out_color = vec3(voxel.color_r, voxel.color_g, voxel.color_b);
            //out_color = vec3(0,1,0);
        }
    }
    else
    {
        //out_color = vec3(1,1,1);
        if(oHit){
            out_color = vec3(1,0,0);
        }
    }
    if(!aabbCheck(ray, boxMin, boxMax, hit)){
        out_color += vec3(0.3,0.3,0.3);
    }
    else{
        //out_color = vec3(1,0,0);
    }
     

    //print the complete content of nodes.nodes[0]
    printf("%d", 5 ^ 3);    
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

int getFirstNode(vec3 t0, vec3 tm){
    int result = 0;

    if(t0.x > t0.y){
		if(t0.x > t0.z)// PLANE YZ
        { 
			if(tm.y < t0.x) result|=2;	// set bit at position 1
			if(tm.z < t0.x) result|=1;	// set bit at position 0 			
            return  result; 		           
        } 	            
    } 	        
    else 
    { 		
        if(t0.y > t0.z)// PLANE XZ
        { 
            if(tm.x < t0.y) result|=4;	// set bit at position 2
			if(tm.z < t0.y) result|=1;	// set bit at position 0
			return result;
		}
	}

	// PLANE XY
	if(tm.x < t0.z) result|=4;	// set bit at position 2
	if(tm.y < t0.z) result|=2;	// set bit at position 1
	return result;
}

int nextNode(vec3 tm, ivec3 c){
    if(tm.x < tm.y){
        if(tm.x < tm.z){
            return c.x;
        }
    }
    else{
        if(tm.y < tm.z){
            return c.y;
        }
    }
    return c.z;
    
}

bool raycastNew(vec3 position, Ray ray, out Node result, out bool octreeHit, out bool center){

    int childIndexModifier = 0;


/*
 *  Prepare some stuff
 */

    ray.direction = normalize(ray.direction);

    //This algorithm only works with positive direction values. Those adjustements fixes negative directions
    if(ray.direction.x < 0){
        ray.origin.x =  position.x * 2 + Dimensions - ray.origin.x;
        ray.direction.x = -ray.direction.x;
        childIndexModifier |= 4;
    }
    if(ray.direction.y < 0){
        ray.origin.y = position.y * 2 + Dimensions - ray.origin.y;
        ray.direction.y = -ray.direction.y;
        childIndexModifier |= 2;
    }
    if(ray.direction.z < 0){
        ray.origin.z = position.z * 2 + Dimensions - ray.origin.z;
        ray.direction.z = -ray.direction.z;
        childIndexModifier |= 1;
    }

    if(abs(ray.direction.z - 1) < 0.00005){
        center = true;
        return true;
    }

    vec3 treeMin = position;
    vec3 treeMax = position + Dimensions;


    vec3 dirInverse = 1 / ray.direction;

    vec3 t0 = (treeMin - ray.origin) * dirInverse;
    vec3 t1 = (treeMax - ray.origin) * dirInverse;


    //Early exit if the tree isnt hit
    if(max(max(t0.x, t0.y), t0.z) > min(min(t1.x, t1.y), t1.z)){
        octreeHit = false;

        return false;
    }
    octreeHit = true;
    //return true;

    //Normally at this point a recursion is used to traverse the tree.
    //But since this is glsl we can't use recursion.
    //So we use a stack to store the nodes to traverse.

    struct StackEntry{
        int nodeIndex;
        int lastChildIndex;
        vec3 t0;
        vec3 t1;
    } stack[MaxDepth + 1];

    int stackIndex = 0;
    stack[0] = StackEntry(0, -1, t0, t1);

    while(stackIndex >= 0){
        StackEntry currentEntry = stack[stackIndex];
        stackIndex--;

        t0 = currentEntry.t0;
        t1 = currentEntry.t1;

        Node node = nodes.nodes[currentEntry.nodeIndex];

        if(t1.x < 0 || t1.y < 0 || t1.z < 0){
            continue;
        }

        if(IsLeaf(node)){
            //We found a leaf.
            if(IsEmpty(node)){
                continue;
            }
            else{
                //TODO hit results
                result = node;
                return true;
            }
        }

        vec3 tm = (t0 + t1) * 0.5;

        int lastChildIndex = currentEntry.lastChildIndex;
        int nextChildIndex;
        //Get the next child index
        switch(lastChildIndex){
            case -1:{
                nextChildIndex = getFirstNode(t0, tm);
                break;
            }
            case 0:{
                nextChildIndex = nextNode(vec3(tm.x, tm.y, tm.z), ivec3(4, 2, 1));
                break;
            }
            case 1:{
                nextChildIndex = nextNode(vec3(tm.x, tm.y, t1.z), ivec3(5, 3, 8));
                break;
            }
            case 2:{
                nextChildIndex = nextNode(vec3(tm.x, t1.y, tm.z), ivec3(6, 8, 3));
                break;
            }
            case 3:{
                nextChildIndex = nextNode(vec3(tm.x, t1.y, t1.z), ivec3(7, 8, 8));
                break;
            }
            case 4:{
                nextChildIndex = nextNode(vec3(t1.x, tm.y, tm.z), ivec3(8, 6, 5));
                break;
            }
            case 5:{
                nextChildIndex = nextNode(vec3(t1.x, tm.y, t1.z), ivec3(8, 7, 8));
                break;
            }
            case 6:{
                nextChildIndex = nextNode(vec3(t1.x, t1.y, tm.z), ivec3(8, 8, 7));
                break;
            }
            case 7:{
                nextChildIndex = 8;
                break;
            }
        }

        if(nextChildIndex >= 8){
            //The end is reached
            continue;
        }

        //Get the parameters for the next child
        vec3 childT0;
        vec3 childT1;
        switch(nextChildIndex){
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

        stackIndex++;
        currentEntry.lastChildIndex = nextChildIndex;
        stack[stackIndex] = currentEntry;
        
        stackIndex++;
        stack[stackIndex] = StackEntry(node.children[nextChildIndex ^ childIndexModifier], -1, childT0, childT1);
    }



    return false;
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

    Ray ray;
    ray.origin = origin;
    ray.direction = direction;
    ray.inverseDirection = 1 / direction;

    Hit hit;

    float ignored;
    if(!aabbCheck(ray, minBox, maxBox, hit))
    {
        /*voxel.color_r = 0;
        voxel.color_g = 0;
        voxel.color_b = 1;
        voxel.color_a = 0;*/
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
        if(iterations > 10000){
            /*voxel.color_r = 1;
            voxel.color_g = 0;
            voxel.color_b = 0;*/
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
            if(currentNode.IsEmpty == 0)
            {
                Hit hit;
                Ray ray;
                ray.origin = origin;
                ray.direction = direction;
                ray.inverseDirection = 1 / direction;
                aabbCheck(ray, center - halfScale, center + halfScale, hit);
                normal = hit.normal;
                voxel = current;
                /*voxel.color_r = 0;
                voxel.color_g = 1;
                voxel.color_b = 0;
                voxel.color_a = 0;*/
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

                if(IsLeaf(childNode) && childNode.IsEmpty == 1){
                    continue;
                }

                vec3 childCenter = center + (childHalfScale * GetChildOffset(childIndex));

                vec3 childMinBox = childCenter - childHalfScale;
                vec3 childMaxBox = childCenter + childHalfScale;

                Ray ray;
                ray.origin = origin;
                ray.direction = direction;
                ray.inverseDirection = 1 / direction;

                Hit hit;

                if(!aabbCheck(ray, childMinBox, childMaxBox, hit)){
                    continue;
                }

                children[remainingChildrenToCheck].childIndex = childIndex;
                children[remainingChildrenToCheck].T = hit.t;
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
        stack[stackPos].remainingChildrenToCheck = -1;
        stack[stackPos].center = center + childHalfScale * GetChildOffset(childrenToCheck);
        stack[stackPos].halfScale = childHalfScale;

    }

    /*voxel.color_r = 0;
    voxel.color_g = 0;
    voxel.color_b = 1;
    voxel.color_a = 0;*/
    return false;
}

bool aabbCheck(Ray ray, vec3 boxMin, vec3 boxMax, out Hit hit)
{
    vec3 tbot = ray.inverseDirection * (boxMin - ray.origin);
	vec3 ttop = ray.inverseDirection * (boxMax - ray.origin);
	vec3 tmin = min(ttop, tbot);
	vec3 tmax = max(ttop, tbot);
	vec2 t = max(tmin.xx, tmin.yz);
	float t0 = max(t.x, t.y);
	t = min(tmax.xx, tmax.yz);
	float t1 = min(t.x, t.y);
	hit.tMin = t0;
	hit.tMax = t1;
    return t1 > max(t0, 0.0);
}