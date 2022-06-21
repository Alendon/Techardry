#version 460
#extension GL_EXT_debug_printf : enable

#define Dimensions 16
#define ChildCount 8
#define MaxDepth 10
#define printf debugPrintfEXT

layout (location = 0) in vec3 in_position;

layout (location = 0) out vec3 out_color;


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

struct CameraDataStruct{
    float HFov;
    float AspectRatio;
    float ForwardX;
    float ForwardY;
    float ForwardZ;
    float UpwardX;
    float UpwardY;
    float UpwardZ;
};

layout(set = 2, binding = 0) readonly uniform CameraData
{
    CameraDataStruct data;
} camera;



bool raycast(vec3 position, Ray ray, out Node result);

mat4 rotation3d(vec3 axis, float angle) {
  axis = normalize(axis);
  float s = sin(angle);
  float c = cos(angle);
  float oc = 1.0 - c;

  return mat4(
    oc * axis.x * axis.x + c,           oc * axis.x * axis.y - axis.z * s,  oc * axis.z * axis.x + axis.y * s,  0.0,
    oc * axis.x * axis.y + axis.z * s,  oc * axis.y * axis.y + c,           oc * axis.y * axis.z - axis.x * s,  0.0,
    oc * axis.z * axis.x - axis.y * s,  oc * axis.y * axis.z + axis.x * s,  oc * axis.z * axis.z + c,           0.0,
    0.0,                                0.0,                                0.0,                                1.0
  );
}

void main()
{
    vec3 octreePosition = vec3(0, 0, 64);

    vec2 screenPos = vec2(in_position.xy);
    screenPos.y = -screenPos.y;
    
    vec3 forward = vec3(camera.data.ForwardX, camera.data.ForwardY, camera.data.ForwardZ);
    //TODO Upward is currently not used. But is relevant for the future. If we want to rotate the camera
    vec3 upward = vec3(camera.data.UpwardX, camera.data.UpwardY, camera.data.UpwardZ);

    float horizontalFov = camera.data.HFov;
    float aspectRatio = camera.data.AspectRatio;
    float verticalFov = 2 * atan(tan(horizontalFov / 2) * aspectRatio);

    vec3 rayDir = normalize(forward);

    float horizontalAngle = screenPos.x * horizontalFov;
    float verticalAngle = screenPos.y * verticalFov;

    mat4 horizontalRot = rotation3d(vec3(0, 1, 0), horizontalAngle);
    mat4 verticalRot = rotation3d(vec3(1, 0, 0), verticalAngle);

    //rotate the ray direction with the horizontal and vertical rotation
    rayDir = ((horizontalRot * verticalRot) * vec4(rayDir,1)).xyz;



    Ray ray;
    ray.origin = vec3(0);
    ray.direction = normalize(rayDir);
    ray.inverseDirection = 1 / ray.direction;

    out_color = vec3(1,1,1);

    Node result;
    if(raycast(octreePosition, ray, result))
    {
      
        Voxel voxel = data.voxels[result.dataIndex];
        out_color = vec3(voxel.color_r, voxel.color_g, voxel.color_b);
    } 
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

bool raycast(vec3 position, Ray ray, out Node result){

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

    vec3 treeMin = position;
    vec3 treeMax = position + Dimensions;


    vec3 dirInverse = 1 / ray.direction;

    vec3 t0 = (treeMin - ray.origin) * dirInverse;
    vec3 t1 = (treeMax - ray.origin) * dirInverse;


    //Early exit if the tree isnt hit
    if(max(max(t0.x, t0.y), t0.z) > min(min(t1.x, t1.y), t1.z)){
        return false;
    }
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