// See https://aka.ms/new-console-template for more information

using System.Text.Json;
using Techardry.Utils;


var indices = JsonSerializer.Deserialize<Int3[]>(File.ReadAllText("indices.json"));



for (int i = 0; i < indices.Length; i++)
{
    if (!File.Exists($"renderdoc_export_{i}.bin")) continue;

    var fromCode = File.ReadAllBytes($"code_export_{indices[i].X}_{indices[i].Y}_{indices[i].Z}.bin");
    var fromRenderDoc = File.ReadAllBytes($"renderdoc_export_{i}.bin");

    var codeSpan = fromCode.AsSpan(0, fromRenderDoc.Length);
    var renderSpan = fromRenderDoc.AsSpan(0);
    bool equals = codeSpan.SequenceEqual(renderSpan);
    Console.WriteLine( $"{indices[i]} + {i}: {equals}");

    if(equals) continue;

    int misMatches = 0;
    
    
    for (int j = 0; j < codeSpan.Length; j++)
    {
        if (codeSpan[j] != renderSpan[j])
        {
            misMatches++;
        }
    }
    
    Console.WriteLine($"{codeSpan.Length} - {misMatches} : {(double)misMatches/ codeSpan.Length}");
}