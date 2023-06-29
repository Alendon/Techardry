#ifndef CAMERA_GLSL
#define CAMERA_GLSL

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

layout(set = CAMERA_DATA_SET, binding = 0) readonly uniform CameraData
{
    CameraDataStruct data;
} camera;

float linearizeDepth(float depth)
{
    float z = depth * 2.0f - 1.0f;
    return (2.0f * camera.data.Near * camera.data.Far) / (camera.data.Far + camera.data.Near - z * (camera.data.Far - camera.data.Near));
}

//delinearize depth to get the depth value in the range [0, 1]
//(the inverse of linearizeDepth)
float delinearizeDepth(float linearDepth)
{
    return ((- (((2 * camera.data.Near * camera.data.Far) / linearDepth) - camera.data.Far - camera.data.Near) / (camera.data.Far - camera.data.Near)) + 1.0f) / 2.0f;
}

#endif //CAMERA_GLSL
