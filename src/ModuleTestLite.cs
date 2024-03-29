using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Text;

namespace TestLite
{
	public class ModuleTestLite : PartModule, IPartCostModifier
	{
		public const string groupName = "ModuleTestLite";
		public const string groupDisplayName = "TestLite";
		[KSPField(isPersistant = true)]
		public double in_du = -1;
		[KSPField()]
		public double in_du_vab = -1;
		[KSPField(isPersistant = true)]
		public double failure_du = 0;
		[KSPField(isPersistant = false, guiFormat = "F1", guiUnits = "du", guiName = "Collected data", groupName = groupName, groupDisplayName = groupDisplayName)]
		public double out_du = 0;
		[KSPField(isPersistant = true, guiFormat = "F1", guiUnits = "du", guiName = "Data at launch", groupName = groupName, groupDisplayName = groupDisplayName)]
		public double roll_du = -1;
		[KSPField(guiFormat = "F1", guiUnits = "du", guiName = "Data (incl. transfer)", groupName = groupName, groupDisplayName = groupDisplayName)]
		public double roll_du_vab = -1;
		[KSPField(isPersistant = true)]
		public double start_du = 0; // du at latest ignition

		[KSPField(isPersistant = true)]
		public double clampTime = 0;
		[KSPField(isPersistant = true, guiFormat = "F1", guiUnits = "s", guiName = "Accumulated burn time", groupName = groupName, groupDisplayName = groupDisplayName)]
		public double runTime = 0;
		[KSPField(isPersistant = true)]
		public double startTime = 0; // runTime at latest ignition

		[KSPField(isPersistant = true)]
		public bool telemetry = false;
		public void setTelemetry(bool v)
		{
			telemetry = v;
			Events["EventTelemetry"].guiName = telemetry ? "Disable Extra Telemetry" : "Enable Extra Telemetry";
		}
		[KSPEvent(guiName = "Enable Extra Telemetry", groupName = groupName, groupDisplayName = groupDisplayName)]
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
		[KSPEvent(guiName = "Enable Extra Preflight", groupName = groupName, groupDisplayName = groupDisplayName)]
		public void EventPreflight()
		{
			bool nv = !preflight;
			setPreflight(nv);
			GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}

		private bool initialised = false;

		[KSPField(guiFormat = "P2", guiName = "Full-burn failure rate", groupName = groupName, groupDisplayName = groupDisplayName)]
		public double failureRate = 0;
		[KSPField(guiFormat = "P2", guiName = "Ignition failure rate", groupName = groupName, groupDisplayName = groupDisplayName)]
		public double ignitionRate = 0; // P(ignition failure)
		[KSPField(guiFormat = "P2", guiName = "Q penalty", groupName = groupName, groupDisplayName = groupDisplayName)]
		public double qPenaltyGui = 0;

		[KSPField()]
		public double fstar = 0; /* 1/MTBF */
		[KSPField(guiName = "MTBF", groupName = groupName, groupDisplayName = groupDisplayName)]
		public string MTBF; /* 1/MTBF */
		[KSPField()]
		public bool running = false;
		private float currentThrottle = 1.0f;

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

		[KSPField(guiUnits = "s", guiName = "Rated total burntime", groupName = groupName, groupDisplayName = groupDisplayName)]
		public double ratedBurnTime;
		[KSPField(guiUnits = "s", guiName = "Rated continuous burntime", groupName = groupName, groupDisplayName = groupDisplayName)]
		public double ratedContinuousBurnTime;
		[KSPField(guiUnits = "s", guiName = "Tested overburn time", groupName = groupName, groupDisplayName = groupDisplayName)]
		public double overBurnTime;
		[KSPField()]
		public FloatCurve thrustModifier;
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
		public FloatCurve customQPenaltyCurve;
		[KSPField()]
		public bool haveCustomQCurve = false;
		[KSPField()]
		public double ignitionDynPresFailMultiplier = 0.0;
		[KSPField()]
		public string techTransfer;
		private Dictionary<string, double> techTransferData;
		[KSPField()]
		public float techTransferMax = 1000;
		[KSPField()]
		public double techTransferGenerationPenalty = 0.05;
		[KSPField()]
		public bool isSolid = false;

		#region IPartCostModifier implementation

		public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
		{
			if (!engine)
				return 0f;
			if (disableTestLite || determinismMode)
				return 0f;
			float multiplier = (telemetry ? 1f : 0f) + (preflight ? 1f : 0f);
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

		public double add_du {
			get {
				double goodTime = Math.Min(currentRunTime, remainingBurnTime + 5.0);
				return Math.Max(goodTime - clampTime, 0.0) * dataRate;
			}
		}
		public double local_du {
			get {
				float dataMultiplier = telemetry ? 2f : 1f;
				out_du = Math.Min(in_du + (add_du + start_du + failure_du) * dataMultiplier, maxData);
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

		public double remainingBurnTime {
			get {
				return Math.Max(Math.Min(ratedContinuousBurnTime, ratedBurnTime - startTime), 0d);
			}
		}
		[KSPField(guiFormat = "F1", guiUnits = "s", guiName = "Current burn time", groupName = groupName, groupDisplayName = groupDisplayName)]
		public double _currentRunTime = 0;
		public double currentRunTime {
			get {
				return runTime - startTime;
			}
		}

		private RealFuels.ModuleEnginesRF engine;
		private RealFuels.ModuleEngineConfigs mec;

		public override string GetInfo()
		{
			try {
				double start = reliabilityCurve.Evaluate(0f) * ratedBurnTime;
				double end = reliabilityCurve.Evaluate((float)maxData) * ratedBurnTime;
				double istart = ignitionCurve.Evaluate(0f);
				double iend = ignitionCurve.Evaluate((float)maxData);
				return String.Format("{0}: rated {1}s\ncycle {2:P1} -> {3:P1}\nignition {4:P1} -> {5:P1}", configuration, ratedBurnTime, 1d - start, 1d - end, istart, iend);
			} catch (Exception exc) {
				Logging.LogException(exc);
				return "Erk.";
			}
		}

		private FloatCurve qPenaltyCurve {
			get {
				if (haveCustomQCurve)
					return customQPenaltyCurve;
				if (Core.Instance != null)
					return Core.Instance.qPenaltyCurve;
				return null;
			}
		}

		private void updateFailureRate()
		{
			float pfMult = preflight ? 0.75f : 1f;
			failureRate = reliabilityCurve.Evaluate((float)roll_du_any) * ratedBurnTime * pfMult;
			double qScaled = 0d;
			if (ignitionDynPresFailMultiplier > 0d && vessel != null)
				qScaled = vessel.dynamicPressurekPa * 1000d / ignitionDynPresFailMultiplier;
			/* 1.0f for no penalty, 0.0f always fails */
			float qPenalty = 1.0f;
			if (qPenaltyCurve != null)
				qPenalty = Mathf.Clamp(qPenaltyCurve.Evaluate((float)qScaled), 0.0f, 1.0f);
			qPenaltyGui = 1.0f - qPenalty;
			ignitionRate = (1d - ignitionCurve.Evaluate((float)roll_du_any) * qPenalty) * pfMult;
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
			if (remainingBurnTime < ratedContinuousBurnTime)
				flatP *= Math.Exp(remainingBurnTime / ratedContinuousBurnTime);
			if (zoner < flatP) {
				// T = 5 - ln(U)/κ
				// κ = -ln(1 - flatP) / B
				double kappa = -Math.Log(1d - flatP) / ratedContinuousBurnTime;
				return 5 - Math.Log(1d - zoner) / kappa;
			}
			zoner -= flatP;
			zoner /= (1d - flatP);
			// tested overburn is median, double it
			return 5 + remainingBurnTime + overBurnTime * 2.0 * zoner;
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
			double kappa = -Math.Log(1d - flatP) / ratedContinuousBurnTime;
			return 5 - Math.Log(1d - zoner) / kappa;
		}

		private double rollOne(int i)
		{
			if (bathtubType[i])
				return rollBathtub(infantPFactor[i] * failureRate,
						   flatPFactor[i] * failureRate);
			else
				return rollPorch(infantPFactor[i] * failureRate,
						 flatPFactor[i] * failureRate);
		}

		public void Roll()
		{
			if (engine != null)
				Logging.LogFormat("Rolling at {0} => {1:R}, rbt {2}", roll_du, failureRate, remainingBurnTime);
			if (determinismMode) {
				for (int i = 0; i < (int)failureTypes.IGNITION; i++)
					failureTime[i] = Double.MaxValue;
				failureTime[(int)failureTypes.PERMANENT] = remainingBurnTime + 5d;
				return;
			}
			for (int i = 0; i < (int)failureTypes.IGNITION; i++) {
				failureTime[i] = rollOne(i);
				if (engine != null)
					Logging.LogFormat("Rolled {0} at {1}", ((failureTypes)i).ToString(), failureTime[i]);
			}
		}

		public void ReRoll()
		{
			if (engine == null) return;
			for (int i = 0; i < (int)failureTypes.IGNITION; i++) {
				double time = rollOne(i) + currentRunTime;
				if (time < failureTime[i]) {
					failureTime[i] = time;
					Logging.LogFormat("Re-rolled {0} at {1} (fraternal damage)", ((failureTypes)i).ToString(), failureTime[i]);
				}
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
			if (currentRunTime > 0d && currentRunTime < 5d)
				return -Math.Log(1d - infantP) / 5d; // lambda
			if (!bathtub || currentRunTime - 5d < remainingBurnTime)
				return -Math.Log(1d - flatP) / ratedContinuousBurnTime; // kappa
			return 1d / Math.Max(overBurnTime * 2.0 + remainingBurnTime + 5d - currentRunTime, 1d);
		}

		public void Update()
		{
			bool hadEngine = getEngine();
			updateFieldsGui(hadEngine, engine != null);
			if (!initialised && Core.Instance != null)
				Initialise();
			_currentRunTime = currentRunTime;
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
			double mtbf = 0.5d / Math.Max(fstar * thrustModifier.Evaluate(currentThrottle), 1e-12);
			if (mtbf < 1200)
				MTBF = String.Format("{0:0.#}s", mtbf);
			else
				MTBF = String.Format("{0:0.##}m", mtbf / 60.0);
		}

		private void fraternalDamage()
		{
			/* Other engines running at the same time (so, putatively, in the same stage) get extra chances to fail */
			foreach (Part p in vessel.parts) {
				foreach (ModuleTestLite tl in p.FindModulesImplementing<ModuleTestLite>()) {
					if (tl.initialised && tl.running)
						tl.ReRoll();
				}
			}
		}

		private void triggerFailure(int type)
		{
			if (isSolid) { /* Map non-solid-appropriate failureTypes to more suitable ones */
				if (type == (int)failureTypes.TRANSIENT) {
					type = (int)failureTypes.PERMANENT;
				} else if (type == (int)failureTypes.THRUSTLOSS) {
					type = (int)failureTypes.PERFLOSS;
				}
			}
			double fdScale = Math.Max(0.0, Math.Min(1.0, (remainingBurnTime + overBurnTime + 5.0 - currentRunTime) / overBurnTime));
			double award_du = failureData[type] * Math.Pow(fdScale, 2.0);
			failure_du += award_du;
			failureTypes ft = (failureTypes)type;
			updateCore(); /* Make sure we save our failureData, just in case we explode the part */
			Logging.LogFormat("Failing engine {0}: {1}; awarding {2} du", configuration, ft.ToString(), award_du);
			ScreenMessages.PostScreenMessage(String.Format("[TestLite] {0} on {1}", failureDescription[type], configuration), 5f);
			FlightLogger.eventLog.Add(String.Format("[{0}] {1} on {2}", KSPUtil.PrintTimeCompact((int)Math.Floor(this.vessel.missionTime), false), failureDescription[type], configuration));
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
				// temporarily override allowShutdown so that e.g. a solid can ignition-fail
				bool allow = engine.allowShutdown;
				engine.allowShutdown = true;
				engine.Shutdown();
				engine.allowShutdown = allow;
				break;
			case failureTypes.PERMANENT:
				engine.Shutdown();
				/* It's permanently dead.  It might even explode. */
				engine.ignitions = 0;
				if (isSolid || Core.Instance.rand.Next(3) == 0) { /* Solids always explode */
					part.explode();
					if (!determinismMode)
						fraternalDamage();
				}
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
			if (engine != null && engine.finalThrust > 0f) {
				if (!running) {
					start_du += add_du;
					startTime = runTime;
					clampTime = 0;
					if (!determinismMode) {
						double r = Core.Instance.rand.NextDouble();
						Logging.LogFormat("Igniting, r={0:F4} ignitionRate={1:F4} (with qPenalty={2:F4})", r, ignitionRate, 1.0f - qPenaltyGui);
						if (r < ignitionRate && (preLaunchFailures || vessel.situation != Vessel.Situations.PRELAUNCH))
							triggerFailure((int)failureTypes.IGNITION);
						Roll();
					}
				}
				double oldRunTime = currentRunTime;
				// calculation copied from TF, to ensure we match its semantics for the config curve
				// except that atmCurveIsp seems to evaluate to 0, use atmosphereCurve instead since that's what everyone in RealFuels-land seems to use
				currentThrottle = engine.finalThrust / engine.maxThrust * engine.atmosphereCurve.Evaluate(0f) / engine.realIsp / engine.multIsp / engine.flowMultiplier;
				runTime += TimeWarp.fixedDeltaTime * thrustModifier.Evaluate(currentThrottle);
				if (vessel.situation == Vessel.Situations.PRELAUNCH)
					clampTime = currentRunTime;
				for (int i = 0; i < (int)failureTypes.IGNITION; i++)
					if (oldRunTime < failureTime[i] && failureTime[i] <= currentRunTime)
						triggerFailure(i);
				running = true;
				updateCore();
			} else {
				// Just display MTBFs for full throttle
				currentThrottle = 1.0f;
				running = false;
			}
			updateMTBF();
		}

		public bool getEngine()
		{
			bool hadEngine = (engine != null);
			if (disableTestLite) {
				engine = null;
				mec = null;
				return hadEngine;
			}
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

		private void clearFlightState()
		{
			/* In case we were KCT-recovered, clear any Flight state on Editor initialisation. */
			major_failure = minor_failure = transient_failure = false;
			runTime = clampTime = startTime = 0;
			in_du = roll_du = -1;
			failure_du = start_du = 0;
			for (int i = 0; i < (int)failureTypes.IGNITION; i++)
				failureTime[i] = -1;
			running = false;
		}

		public void Initialise()
		{
			bool editor = !HighLogic.LoadedSceneIsFlight;
			bool prelaunch = vessel == null || vessel.situation == Vessel.Situations.PRELAUNCH;

			if (Core.Instance == null)
				return;
			if (editor)
				clearFlightState();
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
				}
			}
			updateFailureRate();
			updateMTBF();
			bool hadEngine = getEngine();
			updateFieldsGui(hadEngine, engine != null);
			setTelemetry(telemetry);
			setPreflight(preflight);
			initialised = true;
		}

		private void updateFieldsGui(bool had, bool have)
		{
			if (had == have)
				return;
			Fields["ratedBurnTime"].guiActive = Fields["ratedBurnTime"].guiActiveEditor = have;
			Fields["ratedContinuousBurnTime"].guiActive = Fields["ratedContinuousBurnTime"].guiActiveEditor = have && (ratedContinuousBurnTime < ratedBurnTime);
			Fields["overBurnTime"].guiActive = Fields["overBurnTime"].guiActiveEditor = have;
			Fields["runTime"].guiActive = have;
			Fields["_currentRunTime"].guiActive = have && (ratedContinuousBurnTime < ratedBurnTime);
			Fields["roll_du"].guiActive = Fields["roll_du_vab"].guiActiveEditor = have && !determinismMode;
			Fields["out_du"].guiActive = have && !determinismMode;
			Fields["ignitionRate"].guiActive = Fields["ignitionRate"].guiActiveEditor = have && !determinismMode;
			Fields["qPenaltyGui"].guiActive = have && !determinismMode;
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

		public override void OnStartFinished(StartState state)
		{
			base.OnStartFinished(state);
			if (HighLogic.LoadedSceneIsFlight)
				GameEvents.OnGameSettingsApplied.Add(Initialise);
		}

		public void OnDestroy()
		{
			GameEvents.OnGameSettingsApplied.Remove(Initialise);
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
			if (node.HasNode("thrustModifier")) {
				thrustModifier = new FloatCurve();
				thrustModifier.Load(node.GetNode("thrustModifier"));
			}
			/* Usually we just refer to the default
			 * pressureCurve, but allow cfgs to override it
			 */
			if (node.HasNode("pressureCurve")) {
				customQPenaltyCurve = new FloatCurve();
				customQPenaltyCurve.Load(node.GetNode("pressureCurve"));
				haveCustomQCurve = true;
			}
			OnAwake();
		}
	}
}
