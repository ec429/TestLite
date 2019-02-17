using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Text;

namespace TestLite
{
	public class ModuleTestLite : PartModule
	{
		[KSPField()]
		public double in_du = -1;
		[KSPField()]
		public double failure_du = 0;
		[KSPField(isPersistant = false)]
		public double out_du = 0;

		[KSPField()]
		public double runTime = 0;

		[KSPField()]
		public double failureRate = 0;

		[KSPField()]
		public double fstar = 0; /* 1/MTBF */
		[KSPField()]
		public string MTBF; /* 1/MTBF */

		/* For now we have just the one failure, later this will need more stuff */
		[KSPField()]
		public double failureTime = -1;

		[KSPField()]
		public double ratedBurnTime;
		[KSPField()]
		public double dataRate = 1.0;

		[KSPField()]
		public string configuration;
		public string oldConfiguration;

		public bool initialised = false;

		[KSPField]
		public double maxData;
		[KSPField]
		public FloatCurve reliabilityCurve;
		private Dictionary<string, double> techTransfer;
		[KSPField]
		public float techTransferMax = 1000;
		[KSPField()]
		public double techTransferGenerationPenalty = 0.05;

		public double local_du {
			get {
				out_du = Math.Min(in_du + runTime * dataRate + failure_du, maxData);
				return out_du;
			}
		}
		public double total_du {
			get {
				double transferred = 0d;
				var enumerator = techTransfer.GetEnumerator();
				while (enumerator.MoveNext()) {
					KeyValuePair<string, double> kvp = enumerator.Current;
					if (Core.Instance.du.ContainsKey(kvp.Key))
						transferred += Core.Instance.du[kvp.Key] * kvp.Value;
				}
				transferred = Math.Min(transferred, techTransferMax);
				return Math.Min(local_du + transferred, maxData);
			}
		}

		private RealFuels.ModuleEnginesRF engine;
		private RealFuels.ModuleEngineConfigs mec;

		public override string GetInfo()
		{
			return "TODO put something here.";
		}

		private double rollBathtub(double infantP, double flatP)
		{
			double zoner = Core.Instance.rand.NextDouble();
			if (zoner < infantP) {
				// T = -ln(U)/λ
				// where λ = -⅕ln(1 - infantP)
				double lambda = -Math.Log(1d - infantP) / 5d;
				return -Math.Log(1d - zoner) / lambda;
			}
			zoner -= infantP;
			if (zoner < flatP) {
				// T = 5 - ln(U)/κ
				// κ = -ln(1 - flatP) / B
				double kappa = -Math.Log(1d - flatP) / ratedBurnTime;
				return 5 - Math.Log(1d - zoner) / kappa;
			}
			zoner -= flatP;
			double restP = 1d - infantP - flatP;
			return 5 + ratedBurnTime * (1d + zoner / restP);
		}

		private void updateFailureRate()
		{
			failureRate = reliabilityCurve.Evaluate((float)total_du) * ratedBurnTime;
		}

		public void Roll()
		{
			if (failureTime >= 0d)
				return;
			Logging.LogFormat("Rolling at {0} => {1:R}", total_du, failureRate);
			failureTime = rollBathtub(0.03 * failureRate, 0.15 * failureRate);
		}

		private void setColour()
		{
			if (failureTime <= runTime)
				part.stackIcon.SetIconColor(XKCDColors.Red);
			else
				part.stackIcon.SetIconColor(XKCDColors.White);
		}

		private double fStar(double infantP, double flatP, bool bathtub)
		{
			if (runTime > 0d && runTime < 5d)
				return -Math.Log(1d - infantP) / 5d; // lambda
			if (!bathtub || runTime - 5d < ratedBurnTime)
				return -Math.Log(1d - flatP) / ratedBurnTime; // kappa
			return 1d / Math.Max(2d * ratedBurnTime + 5d - runTime, 1d);
		}

		public override void OnUpdate()
		{
			getEngine();
			if (HighLogic.LoadedSceneIsFlight)
				Initialise(); /* will do nothing if we're already flying */
			base.OnUpdate();
		}

		private void updateCore()
		{
			if (Core.Instance == null)
				return;
			if (!Core.Instance.du.ContainsKey(configuration)) /* should never happen */
				Core.Instance.du[configuration] = 0d;
			Core.Instance.du[configuration] = Math.Max(local_du, Core.Instance.du[configuration]);
		}

		private void updateMTBF()
		{
			fstar = fStar(0.15 * failureRate, 0.85 * failureRate, true);
			double mtbf = 1d / Math.Max(fstar, 1e-12);
			if (mtbf < 1200)
				MTBF = String.Format("{0:0.#}s", mtbf);
			else
				MTBF = String.Format("{0:0.##}m", mtbf / 60.0);
		}

		public override void OnFixedUpdate()
		{
			updateMTBF();
			if (engine != null && engine.finalThrust > 0f) {
				double oldRunTime = runTime;
				runTime += TimeWarp.fixedDeltaTime;
				if (oldRunTime < failureTime && failureTime <= runTime) {
					Logging.LogFormat("Failing engine {0}: Permanent Shutdown", configuration);
					engine.Shutdown();
					failure_du += 1000d;
					/* It's permanently dead.  It might even explode. */
					engine.ignitions = 0;
					if (Core.Instance.rand.Next(3) == 0)
						part.explode();
					setColour();
				}
			}
			updateCore();
			base.OnFixedUpdate();
		}

		public void getEngine()
		{
			if (oldConfiguration != null && oldConfiguration.Equals(configuration))
				return;
			oldConfiguration = configuration;
			Logging.LogFormat("Looking for {0} ({1} core)", configuration, Core.Instance == null ? "no" : "have");
			engine = null;
			mec = null;
			List<RealFuels.ModuleEngineConfigs> mecs = part.FindModulesImplementing<RealFuels.ModuleEngineConfigs>();
			int i, l = mecs.Count;
			for (i = 0; i < l; i++) {
				if (mecs[i].configuration.Equals(configuration)) {
					mec = mecs[i];
					break;
				}
			}
			if (mec == null)
				return;
			Logging.LogFormat("Found MEC using configuration {0} (engineID {1})", configuration, mec.engineID);
			List<RealFuels.ModuleEnginesRF> merfs = part.FindModulesImplementing<RealFuels.ModuleEnginesRF>();
			l = merfs.Count;
			for (i = 0; i < l; i++) {
				if (mec.engineID.Equals(String.Empty) || merfs[i].engineID.Equals(mec.engineID)) {
					engine = merfs[i];
					break;
				}
			}
			if (engine != null)
				Logging.Log("Found MERF too");
		}

		public void Initialise()
		{
			/* This is a mess, caused by stuff maybe getting recorded and persisted from the editor */
			/* TODO figure out what actually needs to happen here */
			if (in_du < 0d || !HighLogic.LoadedSceneIsFlight) {
				if (Core.Instance == null) {
					Logging.LogWarningFormat("No core, can't lookup {0}.", configuration);
					return;
				}
				if (!Core.Instance.du.TryGetValue(configuration, out in_du)) {
					Logging.LogWarningFormat("Lookup {0} not found, setting 0.", configuration);
					in_du = 0;
					Core.Instance.du[configuration] = in_du;
				} else {
					Logging.LogFormat("Looked up {0}, found {1}", configuration, in_du);
				}
			}
			if (initialised)
				return;
			updateFailureRate();
			if (HighLogic.LoadedSceneIsFlight)
				Roll();
			Logging.LogFormat("Initialise {0} (in_du = {1})", engine != null, in_du);
			updateMTBF();
			if (engine) {
				Fields["in_du"].guiActive = Fields["in_du"].guiActiveEditor = true;
				Fields["out_du"].guiActive = true;
				Fields["runTime"].guiActive = true;
				Fields["failureRate"].guiActive = true;
				Fields["fstar"].guiActive = true;
				Fields["MTBF"].guiActive = Fields["MTBF"].guiActiveEditor = true;
				Fields["failureTime"].guiActive = true;
			}
			initialised = true;
		}

		public override void OnAwake()
		{
			getEngine();
		}

		public void LoadTechTransfer(string text)
		{
			if (string.IsNullOrEmpty(text))
				return;
			foreach (string branch in text.Split('&')) {
				string[] modifiers = branch.Split(':');
				if (modifiers.Length != 2) {
					Logging.LogWarningFormat("Skipping bad techTransfer component '{0}' to {1}",
								 branch, configuration);
					continue;
				}
				string[] parts = modifiers[0].Split(',');
				double branchModifier = double.Parse(modifiers[1]) / 100d;
				int i, l = parts.Length;
				for (i = 0; i < l; i++) {
					double transfer = Math.Max(branchModifier - techTransferGenerationPenalty * i, 0d);
					if (transfer <= 0d) {
						Logging.LogWarningFormat("Truncating techTransfer from {0} to {1}",
									 parts[i], configuration);
						break;
					}
					if (techTransfer.ContainsKey(parts[i])) {
						Logging.LogWarningFormat("Skipping duplicate techTransfer from {0} to {1}",
									 parts[i], configuration);
						continue;
					}
					techTransfer[parts[i]] = transfer;
				}
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			techTransfer = new Dictionary<string, double>();
			if (node.HasValue("techTransfer")) {
				string techTransferText = node.GetValue("techTransfer");
				LoadTechTransfer(techTransferText);
			}
			if (node.HasNode("reliabilityCurve")) {
				reliabilityCurve = new FloatCurve();
				reliabilityCurve.Load(node.GetNode("reliabilityCurve"));
			}
			Initialise();
		}
	}
}
