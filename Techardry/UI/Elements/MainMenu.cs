using System.Drawing;
using MintyCore;
using MintyCore.ECS;
using MintyCore.Modding;
using MintyCore.Render;
using MintyCore.Utils;
using Serilog;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using SixLabors.Fonts;
using Techardry.Identifications;
using Techardry.Registries;
using Techardry.Render;
using Techardry.UI.Interfaces;
using static Techardry.UI.UiHelper;

namespace Techardry.UI.Elements;

/// <summary>
///     Ui element representing the main menu
/// </summary>
[RegisterUiRoot("main_menu")]
public class MainMenu : ElementContainer, IRootElement
{
    private readonly Identification _background;
    private readonly TextField _playerId;
    private readonly TextField _playerName;

    private readonly TextField _targetAddress;
    private readonly TextField _targetPort;

    private string _lastId = string.Empty;
    private string _lastPort = string.Empty;

    private ulong _playerIdValue;
    private ushort _targetPortValue;

    private IInputHandler InputHandler { get;  }
    private IVulkanEngine VulkanEngine { get;  }

    /// <summary>
    ///     Constructor
    /// </summary>
    public MainMenu(IInputHandler inputHandler, IVulkanEngine vulkanEngine) : base(new RectangleF(new PointF(0, 0), new SizeF(1, 1)))
    {
        InputHandler = inputHandler;
        VulkanEngine = vulkanEngine;
        Engine.Window!.WindowInstance.FramebufferResize += OnResize;

        var title = new TextBox(new RectangleF(0.3f, 0, 0.4f, 0.2f), "Main Menu", borderActive: false);
        title.IsActive = true;
        AddElement(title);

        const ushort fontSize = 35;
        
        var absoluteBorderWidth = 0.01f;

        _targetAddress = new TextField(InputHandler, new RectangleF(0.2f, 0.7f, 0.25f, 0.1f), 
            horizontalAlignment: HorizontalAlignment.Left, hint: "Target address", desiredFontSize: fontSize);
        AddElement(_targetAddress);
        _targetAddress.IsActive = true;
        _targetAddress.BorderWidth = GetRelativeBorderWidth(absoluteBorderWidth, _targetAddress);

        _targetPort = new TextField(InputHandler, new RectangleF(0.2f, 0.8f, 0.25f, 0.1f), 
            horizontalAlignment: HorizontalAlignment.Left, hint: "Target port", desiredFontSize: fontSize);
        AddElement(_targetPort);
        _targetPort.IsActive = true;
        _targetPort.BorderWidth = GetRelativeBorderWidth(absoluteBorderWidth, _targetPort);

        _playerName = new TextField(InputHandler, new RectangleF(0.55f, 0.7f, 0.25f, 0.1f), 
            horizontalAlignment: HorizontalAlignment.Left, hint: "Player name", desiredFontSize: fontSize);
        AddElement(_playerName);
        _playerName.IsActive = true;
        _playerName.BorderWidth = GetRelativeBorderWidth(absoluteBorderWidth, _playerName);

        _playerId = new TextField(InputHandler, new RectangleF(0.55f, 0.8f, 0.25f, 0.1f), 
            horizontalAlignment: HorizontalAlignment.Left, hint: "Player id", desiredFontSize: fontSize);
        AddElement(_playerId);
        _playerId.IsActive = true;
        _playerId.BorderWidth = GetRelativeBorderWidth(absoluteBorderWidth, _playerId);

        var play = new Button(new RectangleF(0.35f, 0.3f, 0.3f, 0.1f), "Play Local", fontSize);
        AddElement(play);
        play.IsActive = true;
        play.OnLeftClickCb += OnPlayLocal;
        play.BorderWidth = GetRelativeBorderWidth(absoluteBorderWidth, play);

        var connectToServer = new Button(new RectangleF(0.35f, 0.45f, 0.3f, 0.1f),
            "Connect to Server", fontSize);
        AddElement(connectToServer);
        connectToServer.IsActive = true;
        connectToServer.OnLeftClickCb += OnConnectToServer;
        connectToServer.BorderWidth = GetRelativeBorderWidth(absoluteBorderWidth, connectToServer);

        var createServer = new Button(new RectangleF(0.35f, 0.55f, 0.3f, 0.1f), "Create Server", fontSize);
        AddElement(createServer);
        createServer.IsActive = true;
        createServer.OnLeftClickCb += OnCreateServer;
        createServer.BorderWidth = GetRelativeBorderWidth(absoluteBorderWidth, createServer);

        PixelSize = new Size((int)VulkanEngine.SwapchainExtent.Width, (int)VulkanEngine.SwapchainExtent.Height);
        _background = TextureIDs.MainMenuBackground;
    }

    public override void Draw(IUiRenderer renderer, Rect2D scissor,
        Viewport viewport)
    {

        renderer.DrawTexture(_background, AbsoluteLayout, new RectangleF(0, 0, 1, 1), scissor, viewport);

        base.Draw( renderer, scissor, viewport);
    }

    private void OnPlayLocal()
    {
        Log.Logger.Information("{Method} not implemented yet", nameof(OnPlayLocal));
        
        /*
        TechardryMod.MainUiRenderer?.SetUiContext(null);

        Engine.SetGameType(GameType.Local);

        PlayerHandler.LocalPlayerId = _playerIdValue != 0 ? _playerIdValue : 1;
        PlayerHandler.LocalPlayerName = _playerName.InputText.Length != 0 ? _playerName.InputText : "Local";

        Engine.LoadMods(ModManager.GetAvailableMods(true));

        WorldHandler.CreateWorlds(GameType.Server);

        Engine.CreateServer(_targetPortValue != 0 ? _targetPortValue : Constants.DefaultPort);
        Engine.ConnectToServer(_targetAddress.InputText.Length == 0 ? "localhost" : _targetAddress.InputText,
            _targetPortValue != 0 ? _targetPortValue : Constants.DefaultPort);

        TechardryMod.GameLoop();*/
    }

    private void OnConnectToServer()
    {
        Log.Logger.Information("{Method} not implemented yet", nameof(OnConnectToServer));
        /*TechardryMod.MainUiRenderer?.SetUiContext(null);

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

        TechardryMod.GameLoop();*/
    }

    private void OnCreateServer()
    {
        Log.Logger.Information("{Method} not implemented yet", nameof(OnCreateServer));
        /*
        TechardryMod.MainUiRenderer?.SetUiContext(null);

        Engine.SetGameType(GameType.Server);

        Engine.LoadMods(ModManager.GetAvailableMods(true));

        WorldHandler.CreateWorlds(GameType.Server);

        Engine.CreateServer(_targetPortValue != 0 ? _targetPortValue : Constants.DefaultPort);

        TechardryMod.GameLoop();*/
    }

    /// <inheritdoc />
    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        if (!ulong.TryParse(_playerId.InputText, out _playerIdValue) && _playerId.InputText.Length != 0)
            _playerId.InputText = _lastId;
        else
            _lastId = _playerId.InputText;

        if (!ushort.TryParse(_targetPort.InputText, out _targetPortValue) && _targetPort.InputText.Length != 0)
            _targetPort.InputText = _lastPort;
        else
            _lastPort = _targetPort.InputText;
    }

    private void OnResize(Vector2D<int> obj)
    {
        PixelSize = new Size(obj.X, obj.Y);
    }

    protected override void Dispose(bool disposing)
    {
        Engine.Window!.WindowInstance.FramebufferResize -= OnResize;
        base.Dispose(disposing);
    }


    public Size PixelSize { get; set; }
}