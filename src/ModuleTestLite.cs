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
		[KSPField(isPersistant = false)]
		public double roll_du = 0;

		[KSPField()]
		public double clampTime = 0;
		[KSPField()]
		public double runTime = 0;

		[KSPField()]
		public double failureRate = 0;
		[KSPField()]
		public double ignitionRate = 0; // P(ignition failure)

		[KSPField()]
		public double fstar = 0; /* 1/MTBF */
		[KSPField()]
		public string MTBF; /* 1/MTBF */
		[KSPField()]
		public bool running = false;

		public enum failureTypes {
			TRANSIENT,
			PERMANENT,
			PERFLOSS,
			THRUSTLOSS,
			IGNITION
		};
		public static readonly double[] infantPFactor = {0.05, 0.03, 0.04, 0.03};
		public static readonly double[] flatPFactor = {0.25, 0.15, 0.25, 0.2};
		public static readonly bool[] bathtubType = {false, true, false, false};
		public static readonly double[] failureData = {200d, 1000d, 500d, 500d, 500d};
		public static readonly int[] severityType = {0, 2, 1, 1, 0};
		public static readonly string[] failureDescription = {"Transient shutdown", "Permanent shutdown", "Performance loss", "Thrust loss", "Ignition failure"};
		public double[] failureTime = {-1, -1, -1, -1};

		public bool transient_failure = false, minor_failure = false, major_failure = false;

		[KSPField()]
		public double ratedBurnTime;
		[KSPField()]
		public double dataRate = 1.0;

		[KSPField()]
		public string configuration;
		public string oldConfiguration;

		private bool preLaunchFailures = true, determinismMode = false;

		[KSPField()]
		public double maxData;
		[KSPField()]
		public FloatCurve reliabilityCurve;
		[KSPField()]
		public FloatCurve ignitionCurve;
		[KSPField()]
		public string techTransfer;
		private Dictionary<string, double> techTransferData;
		[KSPField()]
		public float techTransferMax = 1000;
		[KSPField()]
		public double techTransferGenerationPenalty = 0.05;

		public double local_du {
			get {
				out_du = Math.Min(in_du + (runTime - clampTime) * dataRate + failure_du, maxData);
				return out_du;
			}
		}
		public double total_du {
			get {
				double transferred = 0d;
				var enumerator = techTransferData.GetEnumerator();
				while (enumerator.MoveNext()) {
					KeyValuePair<string, double> kvp = enumerator.Current;
					if (Core.Instance.du.ContainsKey(kvp.Key))
						transferred += Core.Instance.du[kvp.Key] * kvp.Value;
				}
				transferred = Math.Min(transferred, techTransferMax);
				return Math.Min(in_du + transferred, maxData);
			}
		}

		private RealFuels.ModuleEnginesRF engine;
		private RealFuels.ModuleEngineConfigs mec;

		public override string GetInfo()
		{
			return "TODO put something here.";
		}

		private void updateFailureRate()
		{
			failureRate = reliabilityCurve.Evaluate((float)roll_du) * ratedBurnTime;
			ignitionRate = 1d - ignitionCurve.Evaluate((float)roll_du);
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
			zoner /= (1d - infantP);
			if (zoner < flatP) {
				// T = 5 - ln(U)/κ
				// κ = -ln(1 - flatP) / B
				double kappa = -Math.Log(1d - flatP) / ratedBurnTime;
				return 5 - Math.Log(1d - zoner) / kappa;
			}
			zoner -= flatP;
			zoner /= (1d - flatP);
			return 5 + ratedBurnTime * (1d + zoner);
		}

		private double rollPorch(double infantP, double flatP)
		{
			double zoner = Core.Instance.rand.NextDouble();
			if (zoner < infantP) {
				// T = -ln(U)/λ
				// where λ = -⅕ln(1 - infantP)
				double lambda = -Math.Log(1d - infantP) / 5d;
				return -Math.Log(1d - zoner) / lambda;
			}
			zoner -= infantP;
			zoner /= (1d - infantP);
			// T = 5 - ln(U)/κ
			// κ = -ln(1 - flatP) / B
			double kappa = -Math.Log(1d - flatP) / ratedBurnTime;
			return 5 - Math.Log(1d - zoner) / kappa;
		}

		public void Roll()
		{
			if (failureTime[0] >= 0d)
				return;
			if (engine != null)
				Logging.LogFormat("Rolling at {0} => {1:R}", roll_du, failureRate);
			if (determinismMode) {
				for (int i = 0; i < (int)failureTypes.IGNITION; i++)
					failureTime[i] = Double.MaxValue;
				failureTime[(int)failureTypes.PERMANENT] = ratedBurnTime + 5d;
				return;
			}
			for (int i = 0; i < (int)failureTypes.IGNITION; i++) {
				if (bathtubType[i])
					failureTime[i] = rollBathtub(infantPFactor[i] * failureRate,
								     flatPFactor[i] * failureRate);
				else
					failureTime[i] = rollPorch(infantPFactor[i] * failureRate,
								   flatPFactor[i] * failureRate);
				if (engine != null)
					Logging.LogFormat("Rolled {0} at {1}", ((failureTypes)i).ToString(), failureTime[i]);
			}
		}

		private void setColour()
		{
			if (major_failure)
				part.stackIcon.SetIconColor(XKCDColors.Red);
			else if (minor_failure)
				part.stackIcon.SetIconColor(XKCDColors.KSPNotSoGoodOrange);
			else if (transient_failure)
				part.stackIcon.SetIconColor(XKCDColors.Yellow);
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

		public void Update()
		{
			bool hadEngine = getEngine();
			updateFieldsGui(hadEngine, engine != null);
			if (HighLogic.LoadedSceneIsFlight)
				Initialise(); /* will do nothing if we're already flying */
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
			fstar = 0d;
			for (int i = 0; i < (int)failureTypes.IGNITION; i++)
				fstar += fStar(infantPFactor[i] * failureRate,
					       flatPFactor[i] * failureRate,
					       bathtubType[i]);
			double mtbf = 1d / Math.Max(fstar, 1e-12);
			if (mtbf < 1200)
				MTBF = String.Format("{0:0.#}s", mtbf);
			else
				MTBF = String.Format("{0:0.##}m", mtbf / 60.0);
		}

		private void triggerFailure(int type)
		{
			failure_du += failureData[type];
			failureTypes ft = (failureTypes)type;
			updateCore(); /* Make sure we save our failureData, just in case we explode the part */
			Logging.LogFormat("Failing engine {0}: {1}", configuration, ft.ToString());
			ScreenMessages.PostScreenMessage(String.Format("[TestLite] {0} on {1}", failureDescription[type], configuration), 5f);
			switch (severityType[type]) {
			case 0:
				transient_failure = true;
				break;
			case 1:
				minor_failure = true;
				break;
			case 2:
				major_failure = true;
				break;
			}
			switch (ft) {
			case failureTypes.TRANSIENT:
			case failureTypes.IGNITION:
				engine.Shutdown();
				break;
			case failureTypes.PERMANENT:
				engine.Shutdown();
				/* It's permanently dead.  It might even explode. */
				engine.ignitions = 0;
				if (Core.Instance.rand.Next(3) == 0)
					part.explode();
				break;
			case failureTypes.PERFLOSS:
				engine.ispMult *= 0.4d + Core.Instance.rand.NextDouble() * 0.2d;
				break;
			case failureTypes.THRUSTLOSS:
				engine.flowMult *= 0.4d + Core.Instance.rand.NextDouble() * 0.2d;
				break;
			}
			setColour();
		}

		public void FixedUpdate()
		{
			updateMTBF();
			if (engine != null && engine.finalThrust > 0f) {
				double oldRunTime = runTime;
				runTime += TimeWarp.fixedDeltaTime;
				if (vessel.situation == Vessel.Situations.PRELAUNCH)
					clampTime = runTime;
				for (int i = 0; i < (int)failureTypes.IGNITION; i++)
					if (oldRunTime < failureTime[i] && failureTime[i] <= runTime)
						triggerFailure(i);
				if (!running && !determinismMode) {
					double r = Core.Instance.rand.NextDouble();
					Logging.LogFormat("Igniting, r={0} ignitionRate={1}", r, ignitionRate);
					if (r < ignitionRate && (preLaunchFailures || vessel.situation != Vessel.Situations.PRELAUNCH))
						triggerFailure((int)failureTypes.IGNITION);
				}
				running = true;
				updateCore();
			} else {
				running = false;
			}
		}

		public bool getEngine()
		{
			bool hadEngine = (engine != null);
			if (mec != null && mec.configuration.Equals(configuration))
				/* No change, nothing to do */
				return hadEngine;
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
				return hadEngine;
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
			return hadEngine;
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
			roll_du = total_du;
			TestLiteGameSettings settings = HighLogic.CurrentGame.Parameters.CustomParams<TestLiteGameSettings>();
			preLaunchFailures = settings.preLaunchFailures;
			determinismMode = settings.determinismMode;
			updateFailureRate();
			if (HighLogic.LoadedSceneIsFlight)
				Roll();
			updateMTBF();
			updateFieldsGui(false, engine != null);
		}

		private void updateFieldsGui(bool had, bool have)
		{
			if (had == have)
				return;
			Fields["ratedBurnTime"].guiActive = Fields["ratedBurnTime"].guiActiveEditor = have;
			Fields["in_du"].guiActive = Fields["in_du"].guiActiveEditor = have && !determinismMode;
			Fields["roll_du"].guiActive = Fields["roll_du"].guiActiveEditor = have && !determinismMode;
			Fields["out_du"].guiActive = have && !determinismMode;
			Fields["runTime"].guiActive = have;
			Fields["failureRate"].guiActive = have && !determinismMode;
			Fields["ignitionRate"].guiActive = Fields["ignitionRate"].guiActiveEditor = have && !determinismMode;
			Fields["fstar"].guiActive = have && !determinismMode;
			Fields["MTBF"].guiActive = Fields["MTBF"].guiActiveEditor = have && !determinismMode;
		}

		public override void OnAwake()
		{
			bool hadEngine = getEngine();
			updateFieldsGui(hadEngine, engine != null);
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
					if (techTransferData.ContainsKey(parts[i])) {
						Logging.LogWarningFormat("Skipping duplicate techTransfer from {0} to {1}",
									 parts[i], configuration);
						continue;
					}
					techTransferData.Add(parts[i], transfer);
				}
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			techTransferData = new Dictionary<string, double>();
			LoadTechTransfer(techTransfer);
			if (node.HasNode("reliabilityCurve")) {
				reliabilityCurve = new FloatCurve();
				reliabilityCurve.Load(node.GetNode("reliabilityCurve"));
			}
			if (node.HasNode("ignitionCurve")) {
				ignitionCurve = new FloatCurve();
				ignitionCurve.Load(node.GetNode("ignitionCurve"));
			}
			this.OnAwake();
			Initialise();
		}
	}
}
