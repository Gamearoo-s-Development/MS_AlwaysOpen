using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace MS_AlwaysOpen;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class AlwaysOpenPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.gamearoo.megastore.alwaysopen";
    public const string PluginName = "MS Always Open";
    public const string PluginVersion = "1.0.0";

    private static ConfigEntry<bool> alwaysOpen;
    private static ConfigEntry<bool> useCustomHours;
    private static ConfigEntry<int> openHour;
    private static ConfigEntry<int> closeHour;

    private Harmony harmony;
    private float overnightAccumulator;

    private const float QuarterTickSeconds = 0.066f;
    private const int FullDayMinutesFromSixAm = 1440;

    private void Awake()
    {
        alwaysOpen = Config.Bind("General", "AlwaysOpen24x7", true, "If true, the market is treated as always open and customer spawn is not time-limited.");
        useCustomHours = Config.Bind("General", "UseCustomHours", false, "If true and AlwaysOpen24x7 is false, use OpeningHour and ClosingHour for spawn windows.");
        openHour = Config.Bind("General", "OpeningHour", 6, new ConfigDescription("Opening hour in 24h format (0-23).", new AcceptableValueRange<int>(0, 23)));
        closeHour = Config.Bind("General", "ClosingHour", 22, new ConfigDescription("Closing hour in 24h format (0-23). Supports overnight windows (e.g., 22 to 6).", new AcceptableValueRange<int>(0, 23)));

        harmony = new Harmony(PluginGuid);
        harmony.PatchAll();

        Logger.LogInfo($"{PluginName} loaded. AlwaysOpen24x7={alwaysOpen.Value}, UseCustomHours={useCustomHours.Value}, OpeningHour={openHour.Value}, ClosingHour={closeHour.Value}");
    }

    private void OnDestroy()
    {
        harmony?.UnpatchSelf();
    }

    private void Update()
    {
        if (!ShouldSuppressEndDayTooltip())
        {
            return;
        }

        var timeManager = SingletonBehaviour<TimeManager>.Instance;
        if (timeManager == null)
        {
            return;
        }

        if (timeManager.CurrentMin < 960)
        {
            return;
        }

        overnightAccumulator += Time.unscaledDeltaTime;

        while (overnightAccumulator >= QuarterTickSeconds)
        {
            overnightAccumulator -= QuarterTickSeconds;

            var currentQuarters = (int)AccessTools.Field(typeof(TimeManager), "quarters").GetValue(timeManager);
            currentQuarters++;
            AccessTools.Field(typeof(TimeManager), "quarters").SetValue(timeManager, currentQuarters);
            AccessTools.Field(typeof(TimeManager), "mins").SetValue(timeManager, (float)currentQuarters / 10f);

            AccessTools.Method(typeof(TimeManager), "RepaintSun").Invoke(timeManager, null);

            if (currentQuarters % 10 == 0)
            {
                GenericDataSerializer.SaveInt("CurrentHour", timeManager.CurrentMin);
                AccessTools.Method(typeof(TimeManager), "RepaintHourText").Invoke(timeManager, null);
                TimeManager.OnMinPassed?.Invoke();

                if (timeManager.CurrentMin >= FullDayMinutesFromSixAm)
                {
                    overnightAccumulator = 0f;
                    timeManager.StartTheNewDay();
                    break;
                }
            }
        }
    }

    internal static bool ShouldForceAlwaysOpen()
    {
        return alwaysOpen != null && alwaysOpen.Value;
    }

    internal static bool IsAllowedForCurrentTime(TimeManager timeManager)
    {
        if (ShouldForceAlwaysOpen())
        {
            return true;
        }

        if (useCustomHours == null || !useCustomHours.Value)
        {
            return true;
        }

        var minutesOfDay = (360 + timeManager.CurrentMin) % 1440;
        if (minutesOfDay < 0)
        {
            minutesOfDay += 1440;
        }
        var currentHour = minutesOfDay / 60;

        var start = openHour.Value;
        var end = closeHour.Value;

        if (start == end)
        {
            return true;
        }

        if (start < end)
        {
            return currentHour >= start && currentHour < end;
        }

        return currentHour >= start || currentHour < end;
    }

    internal static bool ShouldSuppressEndDayTooltip()
    {
        return (alwaysOpen != null && alwaysOpen.Value) || (useCustomHours != null && useCustomHours.Value);
    }
}

[HarmonyPatch(typeof(OpenCloseLabel), "Start")]
internal static class OpenCloseLabelStartPatch
{
    private static void Postfix(OpenCloseLabel __instance)
    {
        if (!AlwaysOpenPlugin.ShouldForceAlwaysOpen())
        {
            return;
        }

        if (!__instance.IsOpen)
        {
            __instance.OnLabelClicked();
        }
    }
}

[HarmonyPatch(typeof(OpenCloseLabel), nameof(OpenCloseLabel.OnLabelClicked))]
internal static class OpenCloseLabelTogglePatch
{
    private static bool Prefix(OpenCloseLabel __instance)
    {
        if (!AlwaysOpenPlugin.ShouldForceAlwaysOpen())
        {
            return true;
        }

        if (__instance.IsOpen)
        {
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(TimeManager), nameof(TimeManager.CanSpawnCustomer))]
internal static class TimeManagerCanSpawnCustomerPatch
{
    private static bool Prefix(TimeManager __instance, ref bool __result)
    {
        __result = AlwaysOpenPlugin.IsAllowedForCurrentTime(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(TimeManager), nameof(TimeManager.CanSpawnWanderer))]
internal static class TimeManagerCanSpawnWandererPatch
{
    private static bool Prefix(TimeManager __instance, ref bool __result)
    {
        __result = AlwaysOpenPlugin.IsAllowedForCurrentTime(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(TooltipUI), nameof(TooltipUI.ShowTooltip))]
internal static class TooltipUiShowTooltipPatch
{
    private static bool Prefix(string key)
    {
        if (AlwaysOpenPlugin.ShouldSuppressEndDayTooltip() && key == "end_day_tooltip")
        {
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(TimeManager), nameof(TimeManager.ShowNextDayUI))]
internal static class TimeManagerShowNextDayUiPatch
{
    private static bool Prefix()
    {
        if (!AlwaysOpenPlugin.ShouldSuppressEndDayTooltip())
        {
            return true;
        }

        return false;
    }
}

[HarmonyPatch(typeof(TimeManager), nameof(TimeManager.GetHourText))]
internal static class TimeManagerGetHourTextPatch
{
    private static bool Prefix(int mins, ref string __result)
    {
        if (!AlwaysOpenPlugin.ShouldSuppressEndDayTooltip())
        {
            return true;
        }

        var minutesOfDay = (360 + mins) % 1440;
        if (minutesOfDay < 0)
        {
            minutesOfDay += 1440;
        }

        var hour = minutesOfDay / 60;
        var minute = minutesOfDay % 60;
        __result = string.Format("{0:D2}:{1:D2}", hour, minute);
        return false;
    }
}
