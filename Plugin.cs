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

        internal static LethalShock instance = null;

        internal ManualLogSource mls;
        internal ConfigEntry<string> pishockUsername;
        internal ConfigEntry<string> pishockApiKey;
        internal ConfigEntry<string> pishockCodes;

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
            pishockUsername = Config.Bind("LethalShock", "username", "");
            pishockApiKey = Config.Bind("LethalShock", "apikey", "");
            pishockCodes = Config.Bind("LethalShock", "codes", "code1,code2");
            shockOnDeath = Config.Bind("LethalShock", "shockOnDeath", true, "Enable to get shocked when you die");
            shockOnDamage = Config.Bind("LethalShock", "shockOnDamage", true, "Enable to get shocked when you take damage (Based on the damage value)");
            vibrateOnly = Config.Bind("LethalShock", "vibrateOnly", false, "Enable to only use vibrations");
            warningVibration = Config.Bind("LethalShock", "warnning", true, "Enable to get a warning vibration before a shock");
            shockBasedOnHealth = Config.Bind("LethalShock", "shockByHealth", false, "Enable to calculate shock intensity based on your remaining health instead of the damage taken (shockOnDeath must be enabled)");
            duration = Config.Bind("LethalShock", "duration", 1, "Duration of the shock/vibration");
            maxIntensity = Config.Bind("LethalShock", "max", 80, new ConfigDescription("Maximum intensity of the shock/vibration", new AcceptableValueRange<int>(1, 100)));
            minIntensity = Config.Bind("LethalShock", "min", 1, new ConfigDescription("Minimum intensity of the shock/vibration", new AcceptableValueRange<int>(1, 100)));
            shockMode = Config.Bind("LethalShock", "mode", ShockModes.RANDOM, "What to do when you have multiple shockers?");
            durationDeath = Config.Bind("LethalShock", "durationDeath", 5, "Duration of the shock/vibration when you die");
            intensityDeath = Config.Bind("LethalShock", "intensityDeath", 100, "The intensity of the shock/vibration when you die");
            modeDeath = Config.Bind("LethalShock", "modeDeath", ShockModes.ALL, "What to do when you have multiple shockers when you die");
            harmony.PatchAll(typeof(LethalShock));
            harmony.PatchAll(typeof(PlayerControllerBPatch));
            if (instance == null)
            {
                instance = this;
            }
        }

        internal void DoDamage(int dmg, int health)
        {
            int maxDmgShock = Mathf.Clamp(dmg, minIntensity.Value, maxIntensity.Value);
            int shockHealth = 100 - health;
            int maxHealthShock = Mathf.Clamp(shockHealth, minIntensity.Value, maxIntensity.Value);
            if (shockBasedOnHealth.Value)
            {
                mls.LogInfo("Shocking based on health for " + maxHealthShock);
                DoOperation(maxHealthShock, duration.Value, shockMode.Value);
            }
            else
            {
                mls.LogInfo("Shocking based on health for " + maxDmgShock);
                DoOperation(maxDmgShock, duration.Value, shockMode.Value);
            }
        }
        internal void DoDeath()
        {
            Logger.LogInfo("Death shock");
            DoOperation(intensityDeath.Value, durationDeath.Value, modeDeath.Value);
        }

        private void DoOperation(int intensity, int duration, ShockModes mode)
        {
            string[] codes = pishockCodes.Value.Split(',');
            bool[] picked = PickShockers(mode, codes.Length);
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
                        Task.Run(async () =>
                        {
                            await Task.Delay(duration * 1000);
                            _ = pishock.SendShockAsync(user, intensity, duration);
                        });
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

        private bool[] PickShockers(ShockModes mode, int length)
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
    internal class PlayerControllerBPatch
    {
        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.KillPlayer))]
        [HarmonyPostfix]
        private static void DeathPatch()
        {
            LethalShock.instance.DoDeath();
        }
        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DamagePlayer))]
        [HarmonyPostfix]
        private static void DamagePatch(int ___health, int damageNumber)
        {
            LethalShock.instance.DoDamage(damageNumber, ___health);
        }
    }

}

