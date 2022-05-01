using JetBrains.Annotations;
using MintyCore.Modding;
using MintyCore.Utils;

namespace Techardry;

[UsedImplicitly]
public partial class TechardryMod : IMod
{
    public ushort ModId { get; set; }
    public string StringIdentifier => "techardry";
    public string ModDescription => "Techardry mod";
    public string ModName => "Techardry";
    public ModVersion ModVersion => new(0,0,1);
    public ModDependency[] ModDependencies => Array.Empty<ModDependency>();
    public GameType ExecutionSide => GameType.Local;
    
    public void Dispose()
    {
        Logger.WriteLog("Disposing TechardryMod", LogImportance.Info, "Techardry");
    }

    public void PreLoad()
    {
        Instance = this;
    }

    public void Load()
    {
        Logger.WriteLog("Loading TechardryMod", LogImportance.Info, "Techardry");
        InternalRegister();
    }

    public void PostLoad()
    {
    }

    public void Unload()
    {
        InternalUnregister();
    }

    public static TechardryMod? Instance { get; private set; }
    
}