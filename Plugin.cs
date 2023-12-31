﻿using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using LethalShock.Patches;
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
        internal ConfigEntry<bool> shockOnFired;

        internal ConfigEntry<int> duration;
        internal ConfigEntry<int> maxIntensity;
        internal ConfigEntry<int> minIntensity;
        internal ConfigEntry<ShockModes> shockMode;

        internal ConfigEntry<int> durationDeath;
        internal ConfigEntry<int> intensityDeath;
        internal ConfigEntry<ShockModes> modeDeath;

        internal ConfigEntry<int> durationFired;
        internal ConfigEntry<int> intensityFired;
        internal ConfigEntry<ShockModes> modeFired;

        internal ConfigEntry<bool> enableInterval;
        internal ConfigEntry<int> interval;

        internal DateTime lastShock;


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
            durationFired = Config.Bind("LethalShock", "durationFired", 10, "Duration of the the shock/vibration when you get fired (gameover)");
            intensityFired = Config.Bind("LethalShock", "intensityFired", 100, "Intensity of the shock/vibration when you get fired (gameover)");
            modeFired = Config.Bind("LethalShock", "modeFired", ShockModes.ALL, "What to do when you have multiple shockers when you get fired (gameover)");
            shockOnFired = Config.Bind("LethalShock", "shockOnFired", true, "Should you get shocked when fired?");
            enableInterval = Config.Bind("LethalShock", "enableInterval", true, "Should there be a interval between damage shocks? (This makes bees and snear fleas bearable)");
            interval = Config.Bind("LethalShock", "interval", 10, "Whats the interval between damage shocks?");
            lastShock = DateTime.Now;
            harmony.PatchAll(typeof(LethalShock));
            harmony.PatchAll(typeof(PlayerControllerBPatch));
            harmony.PatchAll(typeof(StartOfRoundPatch));
            if (instance == null)
            {
                instance = this;
            }
        }
        internal void DoDamage(int dmg, int health)
        {
            TimeSpan calculatedTime = DateTime.Now - lastShock;
            if (enableInterval.Value && calculatedTime < TimeSpan.FromSeconds(interval.Value))
            {
                Logger.LogDebug("Didn't shock due to interval. LastShock: " + lastShock.ToLongTimeString());
                return;
            }
            int maxDmgShock = Mathf.Clamp(dmg, minIntensity.Value, maxIntensity.Value);
            int shockHealth = 100 - health;
            int maxHealthShock = Mathf.Clamp(shockHealth, minIntensity.Value, maxIntensity.Value);
            if (shockBasedOnHealth.Value)
            {
                mls.LogInfo("Shocking based on health for " + maxHealthShock);
                DoOperation(maxHealthShock, duration.Value, shockMode.Value);
            }
            else if(shockOnDamage.Value)
            {
                mls.LogInfo("Shocking based on damage for " + maxDmgShock);
                DoOperation(maxDmgShock, duration.Value, shockMode.Value);
            }
            lastShock = DateTime.Now;
        }
        private bool DidDeath = false;
        internal void DoDeath()
        {
            if (DidDeath || !shockOnDeath.Value) return;
            Logger.LogInfo("Death shock");
            DoOperation(intensityDeath.Value, durationDeath.Value, modeDeath.Value);
            DidDeath = true;
            Task.Run(async () =>
            {
                await Task.Delay(20000);
                DidDeath = false;
            });
        }
        private bool DidFired = false;
        internal void DoFired()
        {
            if (DidFired) return;
            mls.LogInfo("Fired shock");
            DoOperation(intensityFired.Value, durationFired.Value, modeFired.Value);
            DidFired = true;
            Task.Run(async () =>
            {
                await Task.Delay(durationFired.Value * 1000);
                DidFired = false;
            });
        }

        private async void DoOperation(int intensity, int duration, ShockModes mode)
        {
            string[] codes = pishockCodes.Value.Split(',');
            bool[] picked = PickShockers(mode, codes.Length);
            for (int i = 0; i < codes.Length; i++)
            {
                mls.LogDebug("Running DoOperation for shocker coded " + codes[i]);
                if (!picked[i]) continue;
                PiShockApi user = new()
                {
                    username = pishockUsername.Value,
                    apiKey = pishockApiKey.Value,
                    code = codes[i],
                    senderName = pishockLogId
                };

                if(vibrateOnly.Value || warningVibration.Value)
                {
                    await user.Vibrate(intensity, duration);
                    if (!vibrateOnly.Value)
                    {
                        mls.LogDebug("Vibrating with delay");
                        await Task.Delay(duration+1 * 1000);
                        mls.LogDebug("Shocking after delay");
                        await user.Shock(intensity, duration);
                    }
                }
                else
                {
                    await user.Shock(intensity, duration);
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
        private static void DeathPatch(PlayerControllerB __instance)
        {
            if (__instance.IsOwner)
            {
                LethalShock.instance.DoDeath();
            }
        }
        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DamagePlayer))]
        [HarmonyPostfix]
        private static void DamagePatch(int ___health, int damageNumber)
        {
            LethalShock.instance.DoDamage(damageNumber, ___health);
        }
    }
    internal class StartOfRoundPatch
    {
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.FirePlayersAfterDeadlineClientRpc))]
        [HarmonyPostfix]
        private static void FirePlayersAfterDeadlinePatch()
        {
            LethalShock.instance.DoFired();
        }
    }

}

