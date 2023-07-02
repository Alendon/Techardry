using JetBrains.Annotations;

namespace Techardry.Render;

public enum TreeType : uint
{
    [UsedImplicitly] Invalid = 0,
    Octree = 1,
}