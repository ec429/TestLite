using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP.UI.Screens;

namespace TestLite
{
	[KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
	public class Core : MonoBehaviour
	{
		public static Core Instance { get; protected set; }
		private ApplicationLauncherButton button = null;
		public System.Random rand;
		public Dictionary<string, double> du;
		/* TODO add a GUI when we have stuff to put in it
		private UI.MasterWindow masterWindow;
		private UI.ConfigWindow configWindow;
		*/

		public void Start()
		{
			if (Instance != null) {
				Destroy(this);
				return;
			}

			Instance = this;
			du = new Dictionary<string, double>();
			rand = new System.Random();
			if (ScenarioTestLite.Instance != null)
				Load(ScenarioTestLite.Instance.node);
			Logging.Log("Core loaded successfully");
		}

		protected void Awake()
		{
			try {
				GameEvents.onGUIApplicationLauncherReady.Add(this.OnGuiAppLauncherReady);
			} catch (Exception ex) {
				Logging.LogException(ex);
			}
		}

		public void OnGUI()
		{
			GUI.depth = 0;

			Action windows = delegate { };
			foreach (var window in UI.AbstractWindow.Windows.Values)
				windows += window.Draw;
			windows.Invoke();
		}

		private void OnGuiAppLauncherReady()
		{
			try {
				/*
				button = ApplicationLauncher.Instance.AddModApplication(
					masterWindow.Show,
					HideGUI,
					null,
					null,
					null,
					null,
					ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH,
					GameDatabase.Instance.GetTexture("TestLite/Textures/toolbar_icon", false));
				GameEvents.onGameSceneLoadRequested.Add(this.OnSceneChange);
				*/
			} catch (Exception ex) {
				Logging.LogException(ex);
			}
		}

		private void HideGUI()
		{
			foreach (var window in UI.AbstractWindow.Windows.Values)
				window.Hide();
		}

		private void OnSceneChange(GameScenes s)
		{
			HideGUI();
		}

		public void OnDestroy()
		{
			Instance = null;
			try {
				GameEvents.onGUIApplicationLauncherReady.Remove(this.OnGuiAppLauncherReady);
				if (button != null)
					ApplicationLauncher.Instance.RemoveModApplication(button);
			} catch (Exception ex) {
				Logging.LogException(ex);
			}
		}

		public void Save(ConfigNode node)
		{
			ConfigNode dn = node.AddNode("du");
			foreach (KeyValuePair<string, double> kvp in du)
				dn.AddValue(kvp.Key, kvp.Value.ToString());
		}

		public void Load(ConfigNode node)
		{
			ConfigNode dn = node.GetNode("du");
			if (dn == null)
				return;
			foreach (ConfigNode.Value v in dn.values)
				du[v.name] = Double.Parse(v.value);
		}
	}

	[KSPScenario(ScenarioCreationOptions.AddToAllGames, GameScenes.FLIGHT, GameScenes.EDITOR, GameScenes.SPACECENTER, GameScenes.TRACKSTATION)]
	public class ScenarioTestLite : ScenarioModule
	{
		public static ScenarioTestLite Instance {get; protected set; }
		public ConfigNode node;

		public override void OnAwake()
		{
			Instance = this;
			base.OnAwake();
		}

		public override void OnSave(ConfigNode node)
		{
			Core.Instance.Save(node);
		}

		public override void OnLoad(ConfigNode node)
		{
			this.node = node;
			if (Core.Instance != null)
				Core.Instance.Load(node);
		}
	}

	public class TestLiteGameSettings : GameParameters.CustomParameterNode {
		public override string Title { get { return "TestLite Options"; } }
		public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
		public override string Section { get { return "TestLite"; } }
		public override string DisplaySection { get { return Section; } }
		public override int SectionOrder { get { return 1; } }
		public override bool HasPresets { get { return true; } }

		[GameParameters.CustomParameterUI("Deterministic mode", toolTip = "No ignition failures, engines always run for rated burn time then die.  du is irrelevant.")]
		public bool determinismMode = false;
		[GameParameters.CustomParameterUI("Pre-Launch Ignition Failures Enabled?", toolTip = "Set to enable ignition failures on the Launch Pad.")]
		public bool preLaunchFailures = true;

		public override void SetDifficultyPreset(GameParameters.Preset preset)
		{
			Logging.Log("Setting difficulty preset");
			switch (preset) {
			case GameParameters.Preset.Easy:
				determinismMode = true;
				preLaunchFailures = false;
				break;
			case GameParameters.Preset.Normal:
				determinismMode = false;
				preLaunchFailures = false;
				break;
			case GameParameters.Preset.Moderate:
			case GameParameters.Preset.Hard:
			case GameParameters.Preset.Custom:
			default:
				determinismMode = false;
				preLaunchFailures = true;
				break;
			}
		}
	}
}
