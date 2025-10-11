using System;
using KSP.Localization;
using KERBALISM;
using SystemHeat;
using System.Collections.Generic;

namespace KerbalismSystemHeat
{
    // Heat-only extension of Kerbalism's Harvester (drills / pumps) that emits SystemHeat loop flux.
    // NOTE: Resource IO and rates remain Kerbalism's responsibility.
    public class HarvesterSystemHeat : Harvester, IConfigurable
    {
        // --- SystemHeat-facing fields (no resource IO here) ---
        [KSPField(isPersistant = false)] public string systemHeatModuleID = "";   // Must match ModuleSystemHeat.moduleID on the same part
        [KSPField(isPersistant = false)] public float shutdownTemperature = 1000f;      // K
        [KSPField(isPersistant = false)] public float systemOutletTemperature = 1000f;  // K
        [KSPField(isPersistant = false)] public float systemPower = 0f;               // kW at full load

        // Efficiency vs loop temperature (mirrors SystemHeat converter behavior)
        [KSPField(isPersistant = false)] public FloatCurve systemEfficiency = new FloatCurve();

        [KSPField(isPersistant = false)] public bool AutoShutdown = true;
        [KSPField(isPersistant = false)] public bool GeneratesHeat = false;

        // Cached SystemHeat module on this part
        private ModuleSystemHeat heatModule;  // ModuleSystemHeat

        private bool isConfigurable = false;

        public override string GetInfo()
        {
            // Add SH info beneath Harvester tooltip text, mirroring your ProcessController version
            string baseInfo = base.GetInfo();
            int pos = baseInfo.IndexOf("\n\n");
            string sh = Localizer.Format("#LOC_SystemHeat_ModuleSystemHeatConverter_PartInfoAdd",
                          Utils.ToSI(systemPower, "F0"),
                          systemOutletTemperature.ToString("F0"),
                          shutdownTemperature.ToString("F0"));
            return pos < 0 ? baseInfo + "\n\n" + sh : baseInfo.Substring(0, pos) + sh + baseInfo.Substring(pos);
        }

        public void Start()
        {
            // Find the SystemHeat loop we publish to
            heatModule = ModuleUtils.FindHeatModule(this.part, systemHeatModuleID);
        }

        public void Configure(bool enable, int multiplier)
        {
            if (!enable)
            {
                DisableModule();
                if (heatModule)
                    heatModule.AddFlux(resource, 0f, 0f, false);
            }
        }

        public void ModuleIsConfigured() => isConfigurable = true;

        public new void FixedUpdate()
        {
            base.FixedUpdate();

            if (heatModule != null)
            {
                if (HighLogic.LoadedSceneIsFlight)
                {
                    GenerateHeatFlight();
                    UpdateSystemHeatFlight();
                }
                if (HighLogic.LoadedSceneIsEditor)
                {
                    GenerateHeatEditor();
                }
            }
        }

        protected void GenerateHeatEditor()
        {
            if (heatModule != null)
            {
                if (ModuleIsActive())
                    heatModule.AddFlux(resource, systemOutletTemperature, systemPower, true);
                else
                    heatModule.AddFlux(resource, 0f, 0f, false);
            }
        }

        protected void GenerateHeatFlight()
        {
            if (ModuleIsActive())
            {
                heatModule.AddFlux(resource, systemOutletTemperature, systemPower, true);
            }
            else
            {
                heatModule.AddFlux(resource, 0f, 0f, false);
            }
        }

        private void UpdateSystemHeatFlight()
        {
            if (!ModuleIsActive()) return;

            if (AutoShutdown && heatModule.currentLoopTemperature > shutdownTemperature)
            {
                ScreenMessages.PostScreenMessage(
                    new ScreenMessage(
                        Localizer.Format("#LOC_SystemHeat_ModuleSystemHeatHarvester_Message_Shutdown", part.partInfo.title),
                        3.0f, ScreenMessageStyle.UPPER_CENTER));
                base.DisableModule();
            }
        }
    }
}
