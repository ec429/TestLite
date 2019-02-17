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
}
