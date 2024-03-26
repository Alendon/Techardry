using System.Numerics;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace Techardry.UI;

public partial class MainMenu : Panel
{
    private void BuildUI()
		{
			var label1 = new Label();
			label1.Text = "Main Menu";
			label1.TextAlign = FontStashSharp.RichText.TextHorizontalAlignment.Center;
			label1.Margin = new Thickness(0, 30, 0, 0);
			label1.Border = new SolidBrush("#575757FF");
			label1.BorderThickness = new Thickness(5);
			label1.Padding = new Thickness(2);
			label1.HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment.Center;
			label1.Scale = new Vector2();

			var label2 = new Label();
			label2.Text = "Singleplayer";
			label2.TextAlign = FontStashSharp.RichText.TextHorizontalAlignment.Center;
			label2.HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment.Center;
			label2.VerticalAlignment = Myra.Graphics2D.UI.VerticalAlignment.Stretch;

			singleplayer = new Button();
			singleplayer.Width = 150;
			singleplayer.Border = new SolidBrush("#8C8C8CFF");
			singleplayer.BorderThickness = new Thickness(5);
			singleplayer.HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment.Center;
			singleplayer.VerticalAlignment = Myra.Graphics2D.UI.VerticalAlignment.Stretch;
			singleplayer.Id = "singleplayer";
			singleplayer.Content = label2;

			var label3 = new Label();
			label3.Text = "Multiplayer";
			label3.HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment.Center;
			label3.VerticalAlignment = Myra.Graphics2D.UI.VerticalAlignment.Stretch;

			multiplayer = new Button();
			multiplayer.Width = 150;
			multiplayer.Border = new SolidBrush("#8C8C8CFF");
			multiplayer.BorderThickness = new Thickness(5);
			multiplayer.HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment.Center;
			multiplayer.VerticalAlignment = Myra.Graphics2D.UI.VerticalAlignment.Stretch;
			multiplayer.Id = "multiplayer";
			multiplayer.Content = label3;

			player_name = new TextBox();
			player_name.HintText = "Player Name";
			player_name.Wrap = true;
			player_name.Width = 150;
			player_name.Id = "player_name";

			player_id = new TextBox();
			player_id.HintText = "Player ID";
			player_id.Wrap = true;
			player_id.Width = 150;
			player_id.Id = "player_id";

			var horizontalStackPanel1 = new HorizontalStackPanel();
			horizontalStackPanel1.Spacing = 75;
			horizontalStackPanel1.HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment.Center;
			horizontalStackPanel1.VerticalAlignment = Myra.Graphics2D.UI.VerticalAlignment.Top;
			horizontalStackPanel1.Widgets.Add(player_name);
			horizontalStackPanel1.Widgets.Add(player_id);

			target_ip = new TextBox();
			target_ip.HintText = "Target IP";
			target_ip.Wrap = true;
			target_ip.Width = 150;
			target_ip.Id = "target_ip";

			target_port = new TextBox();
			target_port.HintText = "Target Port";
			target_port.Wrap = true;
			target_port.Width = 150;
			target_port.Id = "target_port";

			var horizontalStackPanel2 = new HorizontalStackPanel();
			horizontalStackPanel2.Spacing = 75;
			horizontalStackPanel2.HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment.Center;
			horizontalStackPanel2.VerticalAlignment = Myra.Graphics2D.UI.VerticalAlignment.Top;
			horizontalStackPanel2.Widgets.Add(target_ip);
			horizontalStackPanel2.Widgets.Add(target_port);

			var label4 = new Label();
			label4.Text = "Quit";
			label4.HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment.Center;
			label4.VerticalAlignment = Myra.Graphics2D.UI.VerticalAlignment.Stretch;

			quit = new Button();
			quit.Width = 150;
			quit.Border = new SolidBrush("#8C8C8CFF");
			quit.BorderThickness = new Thickness(5);
			quit.HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment.Center;
			quit.VerticalAlignment = Myra.Graphics2D.UI.VerticalAlignment.Stretch;
			quit.Id = "quit";
			quit.Content = label4;

			var verticalStackPanel1 = new VerticalStackPanel();
			verticalStackPanel1.Spacing = 20;
			verticalStackPanel1.HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment.Center;
			verticalStackPanel1.VerticalAlignment = Myra.Graphics2D.UI.VerticalAlignment.Center;
			verticalStackPanel1.Widgets.Add(singleplayer);
			verticalStackPanel1.Widgets.Add(multiplayer);
			verticalStackPanel1.Widgets.Add(horizontalStackPanel1);
			verticalStackPanel1.Widgets.Add(horizontalStackPanel2);
			verticalStackPanel1.Widgets.Add(quit);

			
			Background = new SolidBrush("#B1BF93FF");
			Widgets.Add(label1);
			Widgets.Add(verticalStackPanel1);
		}

		
		public Button singleplayer;
		public Button multiplayer;
		public TextBox player_name;
		public TextBox player_id;
		public TextBox target_ip;
		public TextBox target_port;
		public Button quit;
}