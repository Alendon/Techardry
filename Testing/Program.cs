// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using System.Numerics;
using MintyCore.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Techardry.Lib.FastNoseLite;
using Techardry.World;
using MathHelper = Techardry.Utils.MathHelper;

Console.WriteLine("Hello, World!");

MathHelper.BoxIntersect((Vector3.Zero, Vector3.One), (new Vector3(0.5f, 5, 0.5f), new Vector3(0, -1, 0)), out _);



var tree = new VoxelOctree(VoxelOctree.MaximumLevelCount);
int seed = 4;
Random rnd = new Random(seed);

var noise = new FastNoiseLite(seed);
noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
noise.SetFrequency(0.02f);

for (int x = 0; x < VoxelOctree.Dimensions; x++)
{
    for (int z = 0; z < VoxelOctree.Dimensions; z++)
    {
        for (int y = 0; y < 6; y++)
        {
            tree.Insert(new VoxelData(8), new Vector3(x,y,z), VoxelOctree.SizeOneDepth);
        }
        
        var noiseValue = noise.GetNoise(x, z);
        noiseValue += 0.5f;
        noiseValue /= 0.5f;
        noiseValue *= 6;
        
        for (int y = 6; y < 7 + noiseValue; y++)
        {
            tree.Insert(new VoxelData(7), new Vector3(x,y,z), VoxelOctree.SizeOneDepth);
        }
    }
}


//for (int i = 0; i < VoxelOctree.Dimensions * VoxelOctree.Dimensions * 4; i++)
//{
//    tree.Insert(new VoxelData(rnd.Next(2, 8)), new Vector3(rnd.Next(16), rnd.Next(16), rnd.Next(16)),
//        VoxelOctree.SizeOneDepth);
//}

//for (int x = 0; x < VoxelOctree.Dimensions; x++)
//{
//    for (int y = 0; y < VoxelOctree.Dimensions; y++)
//    {
//        tree.Insert(new Voxel(rnd.Next(2, 8)), new Vector3(x, y, 8), VoxelOctree.SizeOneDepth);
//    }
//}

Image<Rgba32> image = new Image<Rgba32>(1000, 1000);

var rotation = Matrix4x4.CreateFromYawPitchRoll(1, 0.9f, 0);

Stopwatch sw = Stopwatch.StartNew();

int iterations = 1;

for (int i = 0; i < iterations; i++)
for (int y = 0; y < image.Height; y++)
{
    for (int x = 0; x < image.Width; x++)
    {
        var yAdjusted = (y - image.Height / 2) * 0.001f;
        var xAdjusted = (x - image.Width / 2) * 0.001f;

        var cameraDir = -Vector3.UnitZ;
        var cameraPlaneU = Vector3.UnitX;
        var cameraPlaneV = Vector3.UnitY;
        var rayDir = cameraDir + xAdjusted * cameraPlaneU + yAdjusted * cameraPlaneV;
        var rayPos = new Vector3(0, 0, 64);

        //rotate the ray dir and pos by the rotation matrix
       rayDir = Vector3.Transform(rayDir, rotation);
       rayPos = Vector3.Transform(rayPos, rotation);

        rayPos += new Vector3(8, 8, 0);

        Rgba32 color = Color.White;
        
        bool useConeTracing = true;
        Vector3 normal;
        if(useConeTracing)
        {
            if (tree.ConeTrace(rayPos, rayDir, 0.001f, out var node, out normal))
            {
                var voxel = tree.GetVoxelRenderDataRef(ref node);
                var voxelColor = voxel.Color;
                color = voxelColor;
            }
        }
        else
        {
            if (tree.Raycast(rayPos, rayDir, out var node, out normal))
            {
                var voxel = tree.GetVoxelRenderDataRef(ref node);
                var voxelColor = voxel.Color;
                color = voxelColor;
            }
        }

        if (normal.X < 0 || normal.Y < 0 || normal.Z < 0)
        {
            normal = Vector3.Negate(normal);
        }

        if (normal.Equals(Vector3.UnitX))
        {
            color.R = (byte) (color.R * 0.8);
            color.G = (byte) (color.G * 0.8);
            color.B = (byte) (color.B * 0.8);
        }

        if (normal.Equals(Vector3.UnitY))
        {
            color.R = (byte) (color.R * 0.9);
            color.G = (byte) (color.G * 0.9);
            color.B = (byte) (color.B * 0.9);
        }

        if (normal.Equals(Vector3.UnitZ))
        {
            color.R = (byte) (color.R * 1);
            color.G = (byte) (color.G * 1);
            color.B = (byte) (color.B * 1);
        }
        
        image[x, y] = color;
    }
}

sw.Stop();

var fileStream = new FileStream("test.png", FileMode.Create);
image.SaveAsPng(fileStream);
fileStream.Dispose();

Console.WriteLine($"Rendering took {sw.Elapsed.TotalMilliseconds / iterations}ms per frame");
Console.WriteLine("Bye, World!");