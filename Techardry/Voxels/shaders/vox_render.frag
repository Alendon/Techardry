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
    uint color;
    int not_empty;
    uint texture_start_x;
    uint texture_start_y;
    uint array_index;
    uint texture_size_x;
    uint texture_size_y;
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

layout(std430, set = 3, binding = 0) readonly buffer OctreeNodes
{
    Node nodes[];
} nodes[];

layout(std430, set = 3, binding = 1) readonly buffer OctreeData
{
    Voxel voxels[];
} data[];

struct Result{
    Node node;
    vec3 normal;
    vec2 uv;
    float t;
};


bool raycast(vec3 position, Ray ray, out Result result);

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
    
    vec3 forward = normalize(-camPos);
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

    Result result;

    if(!raycast(octreePosition, ray, result)){
        out_color = oldColor;
        return;
    }

    float lDepth = linearDepth(depth);

    float hitDepth = result.t - camera.data.Near;

    if(hitDepth > lDepth){
         out_color = oldColor;
        return;
    }

    float newDepth = delinearizeDepth(hitDepth);
    gl_FragDepth = newDepth;

    #define TextureSize 128.

    Voxel voxel = data[0].voxels[result.node.dataIndex];

    vec3 texStart = vec3(voxel.texture_start_x / TextureSize, voxel.texture_start_y / TextureSize, voxel.array_index);
    
    vec2 texSize = vec2(voxel.texture_size_x / TextureSize, voxel.texture_size_y / TextureSize);
    out_color = texture(tex, texStart + vec3(result.uv * texSize, 0)).rgb;


    if(result.normal.x != 0)
    {
        out_color *= 0.5;
    }
    if(result.normal.y != 0)
    {
        out_color *= 0.75;
    }
    if(result.normal.z != 0)
    {
        out_color *= 1;
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

bool approxEqual(float a, float b){
    return abs(a - b) < 0.00001;
}

bool raycast(vec3 position, Ray ray, out Result result){

    int childIndexModifier = 0;
    vec3 originalRayDir = ray.direction;
    vec3 originalOrigin = ray.origin;

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

        Node node = nodes[0].nodes[currentEntry.nodeIndex];

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
                result.node = node;
                
                if(t0.x > t0.y && t0.x > t0.z){
                   
                    result.t = t0.x;

                    vec3 hitPos = originalOrigin + originalRayDir * result.t;
                    result.uv = mod(vec2(hitPos.y - position.y, hitPos.z - position.z), 1);

                    if(originalRayDir.x > 0 ){
                        result.normal = vec3(-1, 0, 0);

                        float temp = result.uv.r;
                        result.uv.r = result.uv.g;
                        result.uv.g = temp;

                        result.uv.r = abs(1 - result.uv.r);
                    }
                    else{
                        result.normal = vec3(1, 0, 0);

                        float temp = result.uv.r;
                        result.uv.r = result.uv.g;
                        result.uv.g = temp;
                    }
                }
                else if (t0.y > t0.x && t0.y > t0.z){
                    

                    result.t = t0.y;

                    vec3 hitPos = originalOrigin + originalRayDir * result.t;
                    result.uv = mod(vec2(hitPos.x - position.x, hitPos.z - position.z), 1);

                    if(originalRayDir.y > 0 ){
                        result.normal = vec3(0, -1, 0);
                    }
                    else{
                        result.normal = vec3(0, 1, 0);
                    }
                }
                else if (t0.z > t0.x && t0.z > t0.y){
                    

                    result.t = t0.z;

                    vec3 hitPos = originalOrigin + originalRayDir * result.t;
                    result.uv = mod(vec2(hitPos.x - position.x, hitPos.y - position.y), 1);

                    if(originalRayDir.z > 0 ){
                        result.normal = vec3(0, 0, -1);
                    }
                    else{
                        result.normal = vec3(0, 0, 1);

                        result.uv.r = abs(result.uv.r - 1);
                    }
                }

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