// See https://aka.ms/new-console-template for more information

using System.Text.Json;
using Techardry.Utils;

var ServerRenderDistance = 1;

var currentChunk = new Int3(2, 2, 2);
var lastChunk = new Int3(1, 1, 1);


foreach (var chunk in DiffRangeVector(lastChunk, currentChunk, ServerRenderDistance))
{
    Console.WriteLine(chunk);
}

static IEnumerable<Int3> DiffRangeVector(Int3 from, Int3 to, int renderDistance)
{
    if (from == to) yield break;

    var xRange = DiffRange(from.X, to.X, renderDistance);
    var yRange = DiffRange(from.Y, to.Y, renderDistance);
    var zRange = DiffRange(from.Z, to.Z, renderDistance);

    var xHasValues = from.X != to.X;
    var yHasValues = from.Y != to.Y;
    var zHasValues = from.Z != to.Z;

    if (xHasValues && yHasValues && zHasValues)
    {
        foreach (var x in xRange)
        {
            foreach (var y in yRange)

            {
                foreach (var z in zRange)
                {
                    yield return new Int3(x, y, z);
                }
            }
        }

        yield break;
    }

    if (xHasValues && yHasValues)
    {
        foreach (var x in xRange)
        {
            foreach (var y in yRange)
            {
                yield return new Int3(x, y, from.Z);
            }
        }

        yield break;
    }

    if (xHasValues && zHasValues)
    {
        foreach (var x in xRange)
        {
            foreach (var z in zRange)
            {
                yield return new Int3(x, from.Y, z);
            }
        }

        yield break;
    }

    if (yHasValues && zHasValues)
    {
        foreach (var y in yRange)
        {
            foreach (var z in zRange)
            {
                yield return new Int3(from.X, y, z);
            }
        }

        yield break;
    }

    if (xHasValues)
    {
        foreach (var x in xRange)
        {
            yield return new Int3(x, from.Y, from.Z);
        }

        yield break;
    }

    if (yHasValues)
    {
        foreach (var y in yRange)
        {
            yield return new Int3(from.X, y, from.Z);
        }

        yield break;
    }

    if (zHasValues)
    {
        foreach (var z in zRange)
        {
            yield return new Int3(from.X, from.Y, z);
        }

        yield break;
    }
}

static IEnumerable<int> DiffRange(int from, int to, int renderDistance)
{
    if (from == to) yield break;

    var direction = from < to ? 1 : -1;

    var start = from - direction * renderDistance;
    var max = from + direction * (renderDistance + 1);
    var end = start + to - from;

    while (start != end && start != max)
    {
        yield return start;
        start += direction;
    }
}