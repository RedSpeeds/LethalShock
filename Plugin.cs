using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using LethalShock.Patches;
using PiShockApi;
using PiShockApi.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Random = System.Random;

namespace LethalShock
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class LethalShock : BaseUnityPlugin
    {
        internal enum ShockModes
        {
            FIRST, LAST, RANDOM, ROUND_ROBIN, RANDOM_ALL, ALL
        }
        private readonly Harmony harmony = new("LethalShock");
        private readonly PiShockApiClient pishock = new PiShockApiClient();
        internal readonly string pishockLogId = "LethalShock (Lethal company)";
        private readonly Random rnd = new();

        internal static LethalShock instance;

        internal ManualLogSource mls;
        internal ConfigEntry<string> pishockUsername;
        internal ConfigEntry<string> pishockApiKey;
        internal ConfigEntry<List<string>> pishockCodes;

        internal ConfigEntry<bool> shockOnDeath;
        internal ConfigEntry<bool> shockOnDamage;
        internal ConfigEntry<bool> vibrateOnly;
        internal ConfigEntry<bool> warningVibration;
        internal ConfigEntry<bool> shockBasedOnHealth;

        internal ConfigEntry<int> duration;
        internal ConfigEntry<int> maxIntensity;
        internal ConfigEntry<int> minIntensity;
        internal ConfigEntry<ShockModes> shockMode;

        internal ConfigEntry<int> durationDeath;
        internal ConfigEntry<int> intensityDeath;
        internal ConfigEntry<ShockModes> modeDeath;
        

        private void Awake()
        {
            // Plugin startup logic
            mls = BepInEx.Logging.Logger.CreateLogSource("LethalShock");
            mls.LogInfo("LethalShock initiated");
            pishockUsername = this.Config.Bind<string>("LethalShock", "username", "");
            pishockApiKey = this.Config.Bind<string>("LethalShock", "apikey", "");
            pishockCodes = this.Config.Bind<List<string>>("LethalShock", "codes", ["code1", "code2"]);
            shockOnDeath = this.Config.Bind<bool>("LethalShock", "shockOnDeath", true, "Enable to get shocked when you die");
            shockOnDamage = this.Config.Bind<bool>("LethalShock", "shockOnDamage", true, "Enable to get shocked when you take damage (Based on the damage value)");
            vibrateOnly = this.Config.Bind<bool>("LethalShock", "vibrateOnly", false, "Enable to only use vibrations");
            warningVibration = this.Config.Bind<bool>("LethalShock", "warnning", true, "Enable to get a warning vibration before a shock");
            shockBasedOnHealth = this.Config.Bind<bool>("LethalShock", "shockByHealth", false, "Enable to calculate shock intensity based on your remaining health instead of the damage taken (shockOnDeath must be enabled)");
            duration = this.Config.Bind<int>("LethalShock", "duration", 1, "Duration of the shock/vibration");
            maxIntensity = this.Config.Bind<int>("LethalShock", "max", 80, new ConfigDescription("Maximum intensity of the shock/vibration", new AcceptableValueRange<int>(1, 100)));
            minIntensity = this.Config.Bind<int>("LethalShock", "min", 1, new ConfigDescription("Minimum intensity of the shock/vibration", new AcceptableValueRange<int>(1, 100)));
            shockMode = this.Config.Bind<ShockModes>("LethalShock", "mode", ShockModes.RANDOM, "What to do when you have multiple shockers?");
            durationDeath = this.Config.Bind<int>("LethalShock", "durationDeath", 5, "Duration of the shock/vibration when you die");
            intensityDeath = this.Config.Bind<int>("LethalShock", "intensityDeath", 100, "The intensity of the shock/vibration when you die");
            modeDeath = this.Config.Bind<ShockModes>("LethalShock", "modeDeath", ShockModes.ALL, "What to do when you have multiple shockers when you die");
            harmony.PatchAll(typeof(LethalShock));
            harmony.PatchAll(typeof(PlayerControllerB_DamagePlayer));
            harmony.PatchAll(typeof(PlayerControllerB_KillPlayer));
            if (instance == null)
            {
                instance = this;
            }
        }

        internal void doDamage(int dmg, int health)
        {
            int maxDmgShock = Mathf.Clamp(dmg, minIntensity.Value, maxIntensity.Value);
            int shockHealth = 100 - health;
            int maxHealthShock = Mathf.Clamp(shockHealth, minIntensity.Value, maxIntensity.Value);
            if (shockBasedOnHealth.Value)
            {
                mls.LogDebug("Shocking based on health for " + maxHealthShock);
                DoOperation(maxHealthShock, duration.Value, shockMode.Value);
            }
            else
            {
                mls.LogDebug("Shocking based on health for " + maxDmgShock);
                DoOperation(maxDmgShock, duration.Value, shockMode.Value);
            }
        }
        internal void doDeath()
        {
            mls.LogDebug("Death shock");
            DoOperation(intensityDeath.Value, durationDeath.Value, modeDeath.Value);
        }

        private void DoOperation(int intensity, int duration, ShockModes mode)
        {
            string[] codes = pishockCodes.Value.ToArray();
            bool[] picked = pickShockers(mode, codes.Length);
            for (int i = 0; i < codes.Length; i++)
            {
                mls.LogDebug("Running DoOperation for shocker coded " + codes[i]);
                if (!picked[i]) continue;
                PiShockUser user = new()
                {
                    Username = pishockUsername.Value,
                    ApiKey = pishockApiKey.Value,
                    Code = codes[i]
                };

                if(vibrateOnly.Value || warningVibration.Value)
                {
                    pishock.SendVibrationAsync(user, intensity, duration);
                    if (!vibrateOnly.Value)
                    {
                        Task.Delay(duration*1000).ContinueWith(t => { pishock.SendShockAsync(user, intensity, duration); }).Start();
                    }
                }
                else
                {
                    pishock.SendShockAsync(user,intensity,duration);
                }

            }
        }

        public bool NextBoolean(Random random)
        {
            return random.Next() > (Int32.MaxValue / 2);
        }

        private int roundRobin = 0;

        private bool[] pickShockers(ShockModes mode, int length)
        {
            bool[] shocks = new bool[length];
            int ranindex = rnd.Next(0, length);
            if (roundRobin >= length) roundRobin = 0;
           
            for (int i = 0; i < length; i++)
            {
                switch (mode)
                {
                    case ShockModes.ALL:
                        shocks[i] = true;
                        break;
                    case ShockModes.RANDOM_ALL:
                        shocks[i] = NextBoolean(rnd);
                        break;
                    case ShockModes.RANDOM:
                        shocks[i] = i == ranindex;
                        break;
                    case ShockModes.FIRST:
                        shocks[i] = i == 0;
                        break;
                    case ShockModes.LAST:
                        shocks[i] = i == length - 1;
                        break;
                    case ShockModes.ROUND_ROBIN:
                        shocks[i] = i == roundRobin;
                        break;
                }
            }
            roundRobin++;
            if(mode == ShockModes.RANDOM_ALL)
            {
                bool hasShock = false;
                foreach (bool item in shocks)
                {
                    if (item)
                    {
                        hasShock = true;
                        break;
                    }
                }
                if(!hasShock) shocks[ranindex] = true; 
            }
            return shocks;
        }
    }
}
namespace LethalShock.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DamagePlayer))]
    internal class PlayerControllerB_DamagePlayer
    {
        private static void PostFix(int ___health, int damageNumber)
        {
            LethalShock.instance.doDamage(damageNumber, ___health);
        }
    }
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.KillPlayer))]
    internal class PlayerControllerB_KillPlayer
    {
        private static void Postfix()
        {
            LethalShock.instance.doDeath();
        }
    }

}

