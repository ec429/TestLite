﻿using System;
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
		public System.Random rand;
		public Dictionary<string, double> du;

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

		public void OnDestroy()
		{
			Instance = null;
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

		[GameParameters.CustomParameterUI("Disable TestLite", toolTip = "Set to disable all engine failures and du collection.")]
		public bool disabled = false;
		[GameParameters.CustomParameterUI("Deterministic mode", toolTip = "No ignition failures, engines always run for rated burn time then die.  du is irrelevant.")]
		public bool determinismMode = false;
		[GameParameters.CustomParameterUI("Pre-Launch Ignition Failures Enabled?", toolTip = "Set to enable ignition failures on the Launch Pad.")]
		public bool preLaunchFailures = true;
		[GameParameters.CustomFloatParameterUI("Start with du", minValue = 0, maxValue = 10000, stepCount = 21, toolTip = "Set to have engines start at this du.")]
		public float startingDu = 0;

		public override void SetDifficultyPreset(GameParameters.Preset preset)
		{
			if (preset == GameParameters.Preset.Custom)
				return; /* Leave whatever was set before */
			disabled = false;
			startingDu = 0;
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
			default:
				determinismMode = false;
				preLaunchFailures = true;
				break;
			}
		}
	}
}
