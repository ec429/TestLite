using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Text;

namespace TestLite
{
	public class ModuleTestLite : PartModule, IPartCostModifier
	{
		[KSPField(isPersistant = true)]
		public double in_du = -1;
		[KSPField()]
		public double in_du_vab = -1;
		[KSPField(isPersistant = true)]
		public double failure_du = 0;
		[KSPField(isPersistant = false, guiFormat = "F1", guiUnits = "du", guiName = "Collected data")]
		public double out_du = 0;
		[KSPField(isPersistant = true, guiFormat = "F1", guiUnits = "du", guiName = "Data at launch")]
		public double roll_du = -1;
		[KSPField(guiFormat = "F1", guiUnits = "du", guiName = "Data (incl. transfer)")]
		public double roll_du_vab = -1;

		[KSPField(isPersistant = true)]
		public double clampTime = 0;
		[KSPField(isPersistant = true)]
		public double runTime = 0;

		[KSPField(isPersistant = true)]
		public bool telemetry = false;
		public void setTelemetry(bool v)
		{
			telemetry = v;
			Events["EventTelemetry"].guiName = telemetry ? "Disable Extra Telemetry" : "Enable Extra Telemetry";
		}
		[KSPEvent(guiName = "Enable Extra Telemetry")]
		public void EventTelemetry()
		{
			setTelemetry(!telemetry);
            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}
		[KSPField(isPersistant = true)]
		public bool preflight = false;
		public void setPreflight(bool v)
		{
			preflight = v;
			Events["EventPreflight"].guiName = preflight ? "Disable Extra Preflight" : "Enable Extra Preflight";
			updateFailureRate();
        }
		[KSPEvent(guiName = "Enable Extra Preflight")]
		public void EventPreflight()
		{
			bool nv = !preflight;
			setPreflight(nv);
            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}

		private bool initialised = false;

		[KSPField(guiFormat = "P2", guiName = "Full-burn failure rate")]
		public double failureRate = 0;
		[KSPField(guiFormat = "P2", guiName = "Ignition failure rate")]
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

		[KSPField(guiUnits = "s", guiName = "Rated burntime")]
		public double ratedBurnTime;
		[KSPField()]
		public double dataRate = 1.0;

		[KSPField()]
		public string configuration;

		private bool preLaunchFailures = true, determinismMode = false, disableTestLite = false;

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

		#region IPartCostModifier implementation

		public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
		{
			if (!engine)
				return 0f;
			float multiplier = (telemetry ? 2f : 0f) + (preflight ? 1f : 0f);
			float baseCost = part.partInfo.cost + (mec ? mec.GetModuleCost(defaultCost, sit) : 0f);
			return multiplier * baseCost;
		}

		public ModifierChangeWhen GetModuleCostChangeWhen()
		{
			return ModifierChangeWhen.FIXED;
		}

		#endregion

		public double in_du_any {
			get {
				return Math.Max(in_du, in_du_vab);
			}
		}

		public double roll_du_any {
			get {
				return Math.Max(roll_du, roll_du_vab);
			}
		}

		public double local_du {
			get {
				float dataMultiplier = telemetry ? 2f : 1f;
				double add_du = (runTime - clampTime) * dataRate + failure_du;
				out_du = Math.Min(in_du + add_du * dataMultiplier, maxData);
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
				enumerator.Dispose();
				transferred = Math.Min(transferred, techTransferMax);
				return Math.Min(in_du_any + transferred, maxData);
			}
		}

		private RealFuels.ModuleEnginesRF engine;
		private RealFuels.ModuleEngineConfigs mec;

		public override string GetInfo()
		{
			try {
				double start = reliabilityCurve.Evaluate(0f) * ratedBurnTime;
				double end = reliabilityCurve.Evaluate((float)maxData) * ratedBurnTime;
				return String.Format("{0}: rated {1}s\n{2:P1} at 0du\n{3:P1} at {4}du", configuration, ratedBurnTime, 1d - start, 1d - end, maxData);
			} catch (Exception exc) {
				Logging.LogException(exc);
				return "Erk.";
			}
		}

		private void updateFailureRate()
		{
			float pfMult = preflight ? 0.75f : 1f;
			failureRate = reliabilityCurve.Evaluate((float)roll_du_any) * ratedBurnTime * pfMult;
			ignitionRate = (1d - ignitionCurve.Evaluate((float)roll_du_any)) * pfMult;
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
			if (HighLogic.CurrentGame != null)
			{
				TestLiteGameSettings settings = HighLogic.CurrentGame.Parameters.CustomParams<TestLiteGameSettings>();
				if (settings != null)
				{
					if (disableTestLite != settings.disabled)
					{
						Logging.LogWarningFormat("TestLite changed disabled state = {0}", disableTestLite);
						disableTestLite = settings.disabled;
					}
				}
			}
			bool hadEngine = getEngine();
			updateFieldsGui(hadEngine, engine != null);
			if (!initialised && Core.Instance != null)
				Initialise();
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
			if (in_du_any < 0d)
				updateDu();
			if (!HighLogic.LoadedSceneIsFlight && roll_du_vab < 0d)
				roll_du_vab = total_du;
			updateFailureRate();
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
			List<RealFuels.ModuleEnginesRF> merfs = part.FindModulesImplementing<RealFuels.ModuleEnginesRF>();
			l = merfs.Count;
			for (i = 0; i < l; i++) {
				if (mec.engineID.Equals(String.Empty) || merfs[i].engineID.Equals(mec.engineID)) {
					engine = merfs[i];
					break;
				}
			}
			return hadEngine;
		}

		private void updateDuVAB()
		{
			if (Core.Instance == null)
				return;
			if (!Core.Instance.du.TryGetValue(configuration, out in_du_vab)) {
				in_du_vab = 0;
				Core.Instance.du[configuration] = in_du_vab;
			}
		}
		private void updateDu()
		{
			if (Core.Instance == null)
				return;
			if (!HighLogic.LoadedSceneIsFlight) {
				updateDuVAB();
			} else if (!Core.Instance.du.TryGetValue(configuration, out in_du)) {
				in_du = 0;
				Core.Instance.du[configuration] = in_du;
			}
		}

		public void Initialise()
		{
			bool editor = !HighLogic.LoadedSceneIsFlight;
			bool prelaunch = vessel == null || vessel.situation == Vessel.Situations.PRELAUNCH;

			if (Core.Instance == null)
				return;
			if (in_du < 0d || editor || prelaunch)
				updateDu();
			if (editor)
				roll_du_vab = total_du;
			else if (prelaunch)
				roll_du = total_du;
			if (HighLogic.CurrentGame != null) {
				TestLiteGameSettings settings = HighLogic.CurrentGame.Parameters.CustomParams<TestLiteGameSettings>();
				if (settings != null) {
					preLaunchFailures = settings.preLaunchFailures;
					determinismMode = settings.determinismMode;
					disableTestLite = settings.disabled;
					Logging.LogWarningFormat("TestLite disabled state = {0}", disableTestLite);
				}
			}
			updateFailureRate();
			if (!editor)
				Roll();
			updateMTBF();
			updateFieldsGui(false, engine != null);
			setTelemetry(telemetry);
			setPreflight(preflight);
			initialised = true;
		}

		private void updateFieldsGui(bool had, bool have)
		{
			if (have && disableTestLite)
			{
				have = false;
				engine = null;
			}
			if (had == have)
				return;
			Fields["ratedBurnTime"].guiActive = Fields["ratedBurnTime"].guiActiveEditor = have;
			Fields["roll_du"].guiActive = Fields["roll_du_vab"].guiActiveEditor = have && !determinismMode;
			Fields["out_du"].guiActive = have && !determinismMode;
			Fields["ignitionRate"].guiActive = Fields["ignitionRate"].guiActiveEditor = have && !determinismMode;
			Fields["MTBF"].guiActive = Fields["MTBF"].guiActiveEditor = have && !determinismMode;
			Fields["failureRate"].guiActiveEditor = have && !determinismMode;
			Events["EventTelemetry"].guiActiveEditor = have && !determinismMode;
			Events["EventPreflight"].guiActiveEditor = have && !determinismMode;
		}

		public override void OnAwake()
		{
			bool hadEngine = getEngine();
			updateFieldsGui(hadEngine, engine != null);
			techTransferData = new Dictionary<string, double>();
			LoadTechTransfer(techTransfer);
			Initialise();
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

		public override void OnCopy(PartModule fromModule)
		{
			ModuleTestLite from = fromModule as ModuleTestLite;
			base.OnCopy(fromModule);
			setTelemetry(from.telemetry);
			setPreflight(from.preflight);
			OnAwake();
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			if (node.HasNode("reliabilityCurve")) {
				reliabilityCurve = new FloatCurve();
				reliabilityCurve.Load(node.GetNode("reliabilityCurve"));
			}
			if (node.HasNode("ignitionCurve")) {
				ignitionCurve = new FloatCurve();
				ignitionCurve.Load(node.GetNode("ignitionCurve"));
			}
			OnAwake();
		}
	}
}
