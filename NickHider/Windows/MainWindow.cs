using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

namespace NickHider.Windows;

public class MainWindow : Window, IDisposable
{
	private readonly Plugin plugin;
	private readonly Configuration config;

	public MainWindow(Plugin plugin) : base("NickHider###NickHider_MainWindow")
	{
		this.plugin = plugin;
		config = plugin.Configuration;
		Size = new Vector2(320, 400);
		SizeCondition = ImGuiCond.FirstUseEver;
	}

	public void Dispose() { }

	public override void Draw()
	{
		using var child = ImRaii.Child("ChildWithAScrollbar", Vector2.Zero, true);

		if (child.Success)
		{
			var name = config.ReplacementName;
			if (ImGui.InputText("表示したいニックネーム", ref name, 64))
			{
				config.ReplacementName = name;
				config.Save();
			}
		}
	}
}
