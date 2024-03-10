namespace Techardry.UI;

public partial class MainMenu
{
    public MainMenu()
    {
        BuildUI();
        
        singleplayer.Click += (_, _) => PlayLocal = true;
        multiplayer.Click += (_, _) => ConnectToServer = true;
        quit.Click += (_, _) => Quit = true;
    }

    public bool Quit { get; private set; }
    public bool PlayLocal { get; private set; }
    public bool ConnectToServer { get; private set; }
}