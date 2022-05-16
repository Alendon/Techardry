﻿// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities;
using MintyCore.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Techardry.World;
using MathHelper = Techardry.Utils.MathHelper;

Console.WriteLine("Hello, World!");

Box box = new Box(1, 1, 1);
box.RayTest(new RigidPose(new Vector3(0.5f, 0.5f, 0.5f), Quaternion.Identity), new Vector3(0.5f, 5, 0.5f),
    new Vector3(0, -1, 0), out var t, out var n);

MathHelper.BoxIntersect((Vector3.Zero, Vector3.One), (new Vector3(0.5f, 5, 0.5f), new Vector3(0,-1,0)), out _);



var tree = new VoxelOctree(VoxelOctree.MaximumLevelCount);
int seed = 1;
Random rnd = new Random(seed);

for (int i = 0; i < VoxelOctree.Dimensions * VoxelOctree.Dimensions * 2; i++)
{
    tree.Insert(new Voxel(rnd.Next(2, 8)), new Vector3(rnd.Next(16), rnd.Next(16), rnd.Next(16)),
        VoxelOctree.SizeOneDepth);
}

//for (int x = 0; x < VoxelOctree.Dimensions; x++)
//{
//    for (int y = 0; y < VoxelOctree.Dimensions; y++)
//    {
//        tree.Insert(new Voxel(rnd.Next(2, 8)), new Vector3(x, y, 8), VoxelOctree.SizeOneDepth);
//    }
//}


tree.Insert(new Voxel(5), new Vector3(5, 6, 9), VoxelOctree.SizeOneDepth);

    
Image<Rgba32> image = new Image<Rgba32>(1000, 1000);

var rotation = Matrix4x4.CreateFromYawPitchRoll(45, 75, 0);

Stopwatch sw = Stopwatch.StartNew();

/*Parallel.For(0, image.Height, y =>
{
    for (int x = 0; x < image.Width; x++)
    {
        var yAdjusted = (y - image.Height / 2) * 0.001f;
        var xAdjusted = (x - image.Width / 2) * 0.001f;

        var cameraDir = Vector3.UnitZ;
        var cameraPlaneU = Vector3.UnitX;
        var cameraPlaneV = Vector3.UnitY;
        var rayDir = cameraDir + xAdjusted * cameraPlaneU + yAdjusted * cameraPlaneV;
        var rayPos = new Vector3(0, 0, -32);

        //rotate the ray dir and pos by the rotation matrix
        rayDir = Vector3.Transform(rayDir, rotation);
        rayPos = Vector3.Transform(rayPos, rotation);

        rayPos += new Vector3(8, 8, 0);

        tree.Raycast(rayPos, rayDir, out var voxel, out var normal);
        Rgba32 color = voxel.Id switch
        {
            7 => Color.Green,
            6 => Color.Blue,
            5 => Color.Red,
            4 => Color.Yellow,
            3 => Color.Purple,
            2 => Color.Orange,
            _ => Color.White
        };
        
        if(normal.X < 0 || normal.Y < 0 || normal.Z < 0)
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

});*/

for (int i = 0; i < 80; i++)
for (int y = 0; y < image.Height; y++)
{
    for (int x = 0; x < image.Width; x++)
    {
        var yAdjusted = (y - image.Height / 2) * 0.001f;
        var xAdjusted = (x - image.Width / 2) * 0.001f;

        var cameraDir = Vector3.UnitZ;
        var cameraPlaneU = Vector3.UnitX;
        var cameraPlaneV = Vector3.UnitY;
        var rayDir = cameraDir + xAdjusted * cameraPlaneU + yAdjusted * cameraPlaneV;
        var rayPos = new Vector3(0, 0, -32);

        //rotate the ray dir and pos by the rotation matrix
        rayDir = Vector3.Transform(rayDir, rotation);
        rayPos = Vector3.Transform(rayPos, rotation);

        rayPos += new Vector3(8, 8, 0);

        tree.Raycast(rayPos, rayDir, out var voxel, out var normal);
        Rgba32 color = voxel.Id switch
        {
            7 => Color.Green,
            6 => Color.Blue,
            5 => Color.Red,
            4 => Color.Yellow,
            3 => Color.Purple,
            2 => Color.Orange,
            _ => Color.White
        };
        
        if(normal.X < 0 || normal.Y < 0 || normal.Z < 0)
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

Console.WriteLine($"Rendering took {sw.Elapsed.TotalMilliseconds / 80}ms per frame");
Console.WriteLine("Bye, World!");