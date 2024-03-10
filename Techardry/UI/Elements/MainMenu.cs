using MintyCore;
using MintyCore.Utils;
using Serilog;

namespace Techardry.UI.Elements;

public class MainMenu
{
  /*
    private void OnPlayLocal()
    {
        Log.Logger.Information("{Method} not implemented yet", nameof(OnPlayLocal));
        
        TechardryMod.MainUiRenderer?.SetUiContext(null);

        Engine.SetGameType(GameType.Local);

        PlayerHandler.LocalPlayerId = _playerIdValue != 0 ? _playerIdValue : 1;
        PlayerHandler.LocalPlayerName = _playerName.InputText.Length != 0 ? _playerName.InputText : "Local";

        Engine.LoadMods(ModManager.GetAvailableMods(true));

        WorldHandler.CreateWorlds(GameType.Server);

        Engine.CreateServer(_targetPortValue != 0 ? _targetPortValue : Constants.DefaultPort);
        Engine.ConnectToServer(_targetAddress.InputText.Length == 0 ? "localhost" : _targetAddress.InputText,
            _targetPortValue != 0 ? _targetPortValue : Constants.DefaultPort);

        TechardryMod.GameLoop();
    }

    private void OnConnectToServer()
    {
        Log.Logger.Information("{Method} not implemented yet", nameof(OnConnectToServer));
        TechardryMod.MainUiRenderer?.SetUiContext(null);

        if (_playerIdValue == 0)
        {
            Logger.WriteLog("Player id cannot be 0", LogImportance.Error, "MintyCore");
            return;
        }

        if (_playerName.InputText.Length == 0)
        {
            Logger.WriteLog("Player name cannot be empty", LogImportance.Error, "MintyCore");
            return;
        }

        if (_targetAddress.InputText.Length == 0)
        {
            Logger.WriteLog("Target server cannot be empty", LogImportance.Error, "MintyCore");
            return;
        }

        Engine.SetGameType(GameType.Client);


        PlayerHandler.LocalPlayerId = _playerIdValue;
        PlayerHandler.LocalPlayerName = _playerName.InputText;

        Engine.ConnectToServer(_targetAddress.InputText,
            _targetPortValue != 0 ? _targetPortValue : Constants.DefaultPort);

        TechardryMod.GameLoop();
    }

    private void OnCreateServer()
    {
        Log.Logger.Information("{Method} not implemented yet", nameof(OnCreateServer));
        
        TechardryMod.MainUiRenderer?.SetUiContext(null);

        Engine.SetGameType(GameType.Server);

        Engine.LoadMods(ModManager.GetAvailableMods(true));

        WorldHandler.CreateWorlds(GameType.Server);

        Engine.CreateServer(_targetPortValue != 0 ? _targetPortValue : Constants.DefaultPort);

        TechardryMod.GameLoop();
    }*/
}