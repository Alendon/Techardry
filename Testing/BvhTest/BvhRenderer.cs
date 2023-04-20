using System.Diagnostics;
using System.Numerics;
using Silk.NET.Maths;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Testing.BvhTest;

public class BvhRenderer
{
    private Triangle[] _triangles;
    private BvhTree _bvhTree;

    public void Init()
    {
        _triangles = new Triangle[64];

        var random = new Random(21);
        for (var index = 0; index < _triangles.Length; index++)
        {
            ref var triangle = ref _triangles[index];
            var r0 = new Vector3(random.NextSingle(), random.NextSingle(), random.NextSingle());
            var r1 = new Vector3(random.NextSingle(), random.NextSingle(), random.NextSingle());
            var r2 = new Vector3(random.NextSingle(), random.NextSingle(), random.NextSingle());

            triangle.V0 = r0 * 9 - new Vector3(5);
            triangle.V1 = triangle.V0 + r1;
            triangle.V2 = triangle.V1 + r2;
        }

        _bvhTree = new BvhTree(_triangles);
    }

    const bool UseBvh = true;
    const int Frames = 20;

    public void Render()
    {
        var image = new Image<Rgba64>(640, 640, Color.Black);

        Vector3 camPos = new(0, 0, -18);
        Vector3 p0 = new(-1, 1, -15), p1 = new(1, 1, -15), p2 = new(-1, -1, -15);

        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 1; i <= Frames; i++)
        {
            for (int y = 0; y < 640; y++)
            for (int x = 0; x < 640; x++)
            {
                Vector3 pixelPos = p0 + (p1 - p0) * (x / 640f) + (p2 - p0) * (y / 640f);
                var ray = new Ray(camPos, Vector3.Normalize(pixelPos - camPos));

                if (UseBvh)
                    _bvhTree.Intersect(ref ray, 0);
                else
                {
                    for (var index = 0; index < _triangles.Length; index++)
                    {
                        ref var triangle = ref _triangles[index];
                        BvhTree.IntersectTriangle(ref ray, ref triangle);
                    }
                }


                if (ray.T < float.MaxValue)
                    image[x, y] = Color.White;
            }
            
            sw.Stop();
            Console.WriteLine($"Frame {i} took {sw.ElapsedMilliseconds}ms");
            sw.Restart();
        }

        using var fileStream = new FileStream("image.png", FileMode.Create);
        image.Save(fileStream, new PngEncoder());
    }
}