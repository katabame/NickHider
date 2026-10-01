using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using NickHider.Windows;
using Serilog;
using System;
using System.Collections.Generic;

namespace NickHider;

public sealed unsafe class Plugin : IDalamudPlugin
{
	[PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
	[PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
	[PluginService] internal static IPluginLog Log { get; private set; } = null!;

	private const string CommandName = "/nickhider";

	public Configuration Configuration { get; init; }

	public readonly WindowSystem WindowSystem = new("NickHider");
	private MainWindow MainWindow { get; init; }

	//private const string ReplacementName = "katabame";

	private readonly INamePlateGui namePlateGui;
	private readonly IObjectTable objectTable;
	private readonly IAddonLifecycle addonLifecycle;

	public Plugin(INamePlateGui namePlateGui, IObjectTable objectTable, IAddonLifecycle addonLifecycle)
	{
		this.namePlateGui = namePlateGui;
		this.objectTable = objectTable;
		this.addonLifecycle = addonLifecycle;
		

		Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
		MainWindow = new MainWindow(this);
		WindowSystem.AddWindow(MainWindow);

		CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
		{
			HelpMessage = "NickHiderのメインウィンドウを表示/非表示します。"
		});

		PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
		PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
		namePlateGui.OnNamePlateUpdate += OnNamePlateUpdate;
		addonLifecycle.RegisterListener(AddonEvent.PreDraw, "_PartyList", OnPartyListPreDraw);
		addonLifecycle.RegisterListener(AddonEvent.PreDraw, "_TargetInfo", OnTargetInfoPreDraw);
		addonLifecycle.RegisterListener(AddonEvent.PreDraw, "_TargetInfoMainTarget", OnTargetInfoPreDraw);
		addonLifecycle.RegisterListener(AddonEvent.PreDraw, "PartyMemberList", OnPartyMemberListPreDraw);
		addonLifecycle.RegisterListener(AddonEvent.PreDraw, "Character", OnCharacterPreDraw);

		Log.Information($"{PluginInterface.Manifest.Name} loaded.");
	}

	public void Dispose()
	{
		PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
		PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
		namePlateGui.OnNamePlateUpdate -= OnNamePlateUpdate;
		addonLifecycle.UnregisterListener(AddonEvent.PreDraw, "_PartyList", OnPartyListPreDraw);
		addonLifecycle.UnregisterListener(AddonEvent.PreDraw, "_TargetInfo", OnTargetInfoPreDraw);
		addonLifecycle.UnregisterListener(AddonEvent.PreDraw, "_TargetInfoMainTarget", OnTargetInfoPreDraw);
		addonLifecycle.UnregisterListener(AddonEvent.PreDraw, "PartyMemberList", OnPartyMemberListPreDraw);
		addonLifecycle.UnregisterListener(AddonEvent.PreDraw, "Character", OnCharacterPreDraw);

		WindowSystem.RemoveAllWindows();
		((IDisposable)MainWindow).Dispose();

		CommandManager.RemoveHandler(CommandName);
	}

	private void OnCommand(string command, string args)
	{
		MainWindow.Toggle();
	}

	public void ToggleMainUi() => MainWindow.Toggle();

	private void OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
	{
		var local = objectTable.LocalPlayer;
		if (local is null) return;
		foreach (var handler in handlers)
		{
			if (handler.NamePlateKind != NamePlateKind.PlayerCharacter) continue;
			if (handler.GameObjectId != local.GameObjectId) continue;
			handler.Name = new SeString(new TextPayload(Configuration.ReplacementName));
		}
	}

	private string? cachedPartyListTemplate;
	private void OnPartyListPreDraw(AddonEvent type, AddonArgs args)
	{
		var local = objectTable.LocalPlayer;
		if (local is null) return;

		var realName = local.Name.TextValue;
		if (string.IsNullOrEmpty(realName)) return;

		var addon = (AddonPartyList*)args.Addon.Address;
		if (addon == null) return;

		if (addon->PartyMembers.Length == 0) return;

		var nameNode = addon->PartyMembers[0].Name;
		if (nameNode == null) return;

		var currentText = nameNode->NodeText.ToString();
		if (currentText.Contains(realName)) cachedPartyListTemplate = currentText;

		if (cachedPartyListTemplate is null) return;

		var replaced = cachedPartyListTemplate.Replace(realName, Configuration.ReplacementName);
		if (replaced != currentText) nameNode->SetText(replaced);
	}

	private void OnTargetInfoPreDraw(AddonEvent type, AddonArgs args)
	{
		var local = objectTable.LocalPlayer;
		if (local is null) return;

		var realName = local.Name.TextValue;
		if (string.IsNullOrEmpty(realName)) return;

		var addon = (AtkUnitBase*)args.Addon.Address;
		if (addon == null) return;

		for (var i = 0; i < addon->UldManager.NodeListCount; i++)
		{
			var node = addon->UldManager.NodeList[i];
			if (node == null || node->Type != NodeType.Text) continue;

			var textNode = (AtkTextNode*)node;
			var text = textNode->NodeText.ToString();

			if (!text.Contains(realName)) continue;

			textNode->SetText(text.Replace(realName, Configuration.ReplacementName));
		}
	}

	private void OnPartyMemberListPreDraw(AddonEvent type, AddonArgs args)
	{
		var local = objectTable.LocalPlayer;
		if (local is null) return;

		var realName = local.Name.TextValue;
		if (string.IsNullOrEmpty(realName)) return;

		var addon = (AtkUnitBase*)args.Addon.Address;
		if (addon == null) return;

		ReplaceInNodes(&addon->UldManager, realName, 0);
	}

	private void OnCharacterPreDraw(AddonEvent type, AddonArgs args)
	{
		var local = objectTable.LocalPlayer;
		if (local is null) return;

		var realName = local.Name.TextValue;
		if (string.IsNullOrEmpty(realName)) return;

		var addon = (AtkUnitBase*)args.Addon.Address;
		if (addon == null) return;

		ReplaceInNodes(&addon->UldManager, realName, 0);
	}

	private void ReplaceInNodes(AtkUldManager* uld, string realName, int depth)
	{
		if (uld == null || uld->NodeList == null || depth > 8) return;

		for (var i = 0; i < uld->NodeListCount; i++)
		{
			var node = uld->NodeList[i];
			if (node == null) continue;

			if (node->Type == NodeType.Text)
			{
				var textNode = (AtkTextNode*)node;
				var text = textNode->NodeText.ToString();

				if (!text.Contains(realName)) continue;

				var replaced = text.Replace(realName, Configuration.ReplacementName);
				textNode->SetText(replaced);
			}
			else if ((int)node->Type >= 1000)
			{
				var component = ((AtkComponentNode*)node)->Component;
				if (component != null) ReplaceInNodes(&component->UldManager, realName, depth + 1);
			}
		}
	}
}
