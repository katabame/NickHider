using Dalamud.Configuration;
using System;

namespace NickHider;

[Serializable]
public class Configuration : IPluginConfiguration
{
	public int Version { get; set; } = 1;
	public string ReplacementName { get; set; } = "Anonymous";

	public void Save()
	{
		Plugin.PluginInterface.SavePluginConfig(this);
	}
}
