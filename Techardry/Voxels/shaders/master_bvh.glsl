#ifndef MASTER_BVH_GLSL
#define MASTER_BVH_GLSL

#define INVALID_TREE_TYPE 0
#define VOXEL_OCTREE_TYPE 1

#include "common.glsl"
#include "voxel_octree.glsl"

layout(set = RENDER_DATA_SET, binding = RENDER_DATA_SET_WORLD_GRID_HEADER_BINDING) uniform WorldGridHeader
{
//minimum point of the grid. Stored as chunk coordinates (not world coordinates)
    int minX;
    int minY;
    int minZ;

//size of the grid in chunks
    int sizeX;
    int sizeY;
    int sizeZ;
} worldGridHeader;

layout(std430, set = RENDER_DATA_SET, binding = RENDER_DATA_SET_WORLD_GRID_BINDING) readonly buffer WorldGrid
{
    uint64_t chunkPointers[];
} worldGrid;

void raycast_tree(in Ray ray, in uint64_t tree, inout Result result){
    //transform the ray into the tree's local space
    mat4 inverseTransform = getTreeInverseTransform(tree);

    vec4 rayOrigin = inverseTransform * vec4(ray.origin, 1);
    vec4 rayDirection = inverseTransform * vec4(ray.direction, 0);

    ray.origin = rayOrigin.xyz;
    ray.direction = normalize(rayDirection.xyz);
    ray.inverseDirection = 1.0 / ray.direction;

    UniformTree treeRef = UniformTree(tree);
    switch (treeRef.treeType){
        case VOXEL_OCTREE_TYPE:
        raycastChunk(ray, tree, result);
        break;
        default :
        case INVALID_TREE_TYPE:
        result.fail = true;
        break;
    }
}

void raycast(in Ray ray, inout Result result){

    ivec3 gridMin = ivec3(worldGridHeader.minX, worldGridHeader.minY, worldGridHeader.minZ);
    ivec3 gridSize = ivec3(worldGridHeader.sizeX, worldGridHeader.sizeY, worldGridHeader.sizeZ);

    vec3 cellSize = vec3(Dimensions);

    //calculate the current position in the cell
    vec3 normalizedPosition = (ray.origin - vec3(gridMin) * cellSize) / cellSize;
    ivec3 currentCell = ivec3(floor(normalizedPosition));

    // Handle the case where the ray origin is outside the grid
    vec3 gridMinWorld = vec3(gridMin) * cellSize;
    vec3 gridMaxWorld = vec3(gridMin + gridSize) * cellSize;

    if (any(lessThan(ray.origin, gridMinWorld)) || any(greaterThan(ray.origin, gridMaxWorld))) {
        // Calculate t values for intersections with the grid boundaries
        vec3 tMin = (gridMinWorld - ray.origin) * ray.inverseDirection;
        vec3 tMax = (gridMaxWorld - ray.origin) * ray.inverseDirection;

        float tEntry = max(max(tMin.x, tMin.y), tMin.z);
        float tExit = min(min(tMax.x, tMax.y), tMax.z);

        if (tEntry > tExit || tExit < 0.0) {
            // Ray does not intersect the grid
            return;
        }

        vec3 entryPoint = ray.origin + tEntry * ray.direction;
        normalizedPosition = (entryPoint - vec3(gridMin) * cellSize) / cellSize;
        currentCell = ivec3(floor(normalizedPosition));
    }

    vec3 deltaDist = abs(vec3(length(ray.direction)) / ray.direction);
    ivec3 step = ivec3(sign(ray.direction));
    vec3 sideDist = (sign(ray.direction) * (vec3(currentCell) - normalizedPosition) + (sign(ray.direction) * 0.5) + 0.5) * deltaDist;

    while (currentCell.x >= 0 && currentCell.x < gridSize.x &&
    currentCell.y >= 0 && currentCell.y < gridSize.y &&
    currentCell.z >= 0 && currentCell.z < gridSize.z){

        int cellIndex = currentCell.x + currentCell.y * gridSize.x + currentCell.z * gridSize.x * gridSize.y;
        uint64_t tree = worldGrid.chunkPointers[cellIndex];

        if (tree != 0) {
            Result tempResult = resultEmpty();
            raycast_tree(ray, tree, tempResult);

            if (tempResult.t < result.t) {
                result = tempResult;
                return;
            }
        }

        if (sideDist.x < sideDist.y) {
            if (sideDist.x < sideDist.z) {
                sideDist.x += deltaDist.x;
                currentCell.x += step.x;
            }
            else {
                sideDist.z += deltaDist.z;
                currentCell.z += step.z;
            }
        }
        else {
            if (sideDist.y < sideDist.z) {
                sideDist.y += deltaDist.y;
                currentCell.y += step.y;
            }
            else {
                sideDist.z += deltaDist.z;
                currentCell.z += step.z;
            }
        }
    }
}

vec3 resultGetColor(in Result result){
    #ifndef BEAM_CALCULATION
    uint voxel = voxelNode_GetDataIndex(result.tree, result.nodeIndex);
    vec2 texStart = vec2(voxelData_GetTextureStartX(result.tree, voxel), voxelData_GetTextureStartY(result.tree, voxel));
    vec2 texSize = vec2(voxelData_GetTextureSizeX(result.tree, voxel), voxelData_GetTextureSizeY(result.tree, voxel));
    vec2 texEnd = texStart + texSize;
    return texture(tex, mix(texStart, texEnd, result.uv)).rgb;
    #else
    return vec3(1.0, 0.0, 0.0);
    #endif
}



#endif// MASTER_BVH_GLSL
