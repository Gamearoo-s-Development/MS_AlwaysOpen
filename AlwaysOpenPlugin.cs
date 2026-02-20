using System;
using System.Collections;
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
    public const string PluginVersion = "1.0.1";

    private static ConfigEntry<bool> alwaysOpen;
    private static ConfigEntry<bool> useCustomHours;
    private static ConfigEntry<int> openHour;
    private static ConfigEntry<int> closeHour;

    private Harmony harmony;

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

    internal static bool ShouldUseExtendedDayCycle()
    {
        return ShouldSuppressEndDayTooltip();
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

[HarmonyPatch(typeof(TimeManager), "DayRoutine")]
internal static class TimeManagerDayRoutinePatch
{
    private const int FullDayQuartersFromSixAm = 14400;

    private static readonly System.Reflection.FieldInfo Point1MinWaiterField = AccessTools.Field(typeof(TimeManager), "point1MinWaiter");
    private static readonly System.Reflection.FieldInfo MinsField = AccessTools.Field(typeof(TimeManager), "mins");
    private static readonly System.Reflection.FieldInfo QuartersField = AccessTools.Field(typeof(TimeManager), "quarters");
    private static readonly System.Reflection.MethodInfo RepaintSunMethod = AccessTools.Method(typeof(TimeManager), "RepaintSun");
    private static readonly System.Reflection.MethodInfo RepaintHourTextMethod = AccessTools.Method(typeof(TimeManager), "RepaintHourText");
    private static readonly Type DataSerializerType = AccessTools.TypeByName("ToolBox.Serialization.DataSerializer") ?? AccessTools.TypeByName("DataSerializer");
    private static readonly System.Reflection.MethodInfo DataSerializerSaveFileMethod = DataSerializerType == null ? null : AccessTools.Method(DataSerializerType, "SaveFile");

    private static bool Prefix(TimeManager __instance, ref IEnumerator __result)
    {
        if (!AlwaysOpenPlugin.ShouldUseExtendedDayCycle())
        {
            return true;
        }

        __result = DayRoutineExtended(__instance);
        return false;
    }

    private static IEnumerator DayRoutineExtended(TimeManager timeManager)
    {
        var waiter = Point1MinWaiterField.GetValue(timeManager);

        while (true)
        {
            var mins = (float)MinsField.GetValue(timeManager);
            var openCloseLabel = SingletonBehaviour<OpenCloseLabel>.Instance;

            if (Mathf.Approximately(mins, 0f) && openCloseLabel != null && !openCloseLabel.IsOpen)
            {
                yield return waiter;
                continue;
            }

            if (mins < 1440f)
            {
                var quarters = (int)QuartersField.GetValue(timeManager);
                mins = (float)quarters / 10f;
                MinsField.SetValue(timeManager, mins);
                quarters++;
                QuartersField.SetValue(timeManager, quarters);

                GenericDataSerializer.SaveInt("CurrentHour", timeManager.CurrentMin);
                RepaintSunMethod.Invoke(timeManager, null);

                if (quarters % 10 == 0)
                {
                    if (Mathf.Approximately(mins, 360f))
                    {
                        EventManager.NotifyEvent(UIEvents.OPEN_INCOME_OFFER);
                    }

                    if (quarters == 7500)
                    {
                        SingletonBehaviour<AudioManager>.Instance.OnSunDowned();
                    }

                    if (quarters == 8400)
                    {
                        EventManager.NotifyEvent(GameEvents.SPAWN_TIME_OVER);
                    }

                    if (quarters == 4800)
                    {
                        EventManager.NotifyEvent(GameEvents.EARLY_SHIFT_OVER);
                        EventManager.NotifyEvent(GameEvents.LATE_SHIFT_STARTED);
                    }

                    RepaintHourTextMethod.Invoke(timeManager, null);
                    TimeManager.OnMinPassed?.Invoke();

                    if (quarters >= FullDayQuartersFromSixAm)
                    {
                        EventManager.NotifyEvent(GameEvents.DAY_ENDED);
                        DataSerializerSaveFileMethod?.Invoke(null, null);
                        timeManager.StartTheNewDay();
                    }
                }
            }

            yield return waiter;
        }
    }
}

[HarmonyPatch(typeof(Cashier), nameof(Cashier.DeactivateInstant))]
internal static class CashierDeactivateInstantPatch
{
    private static readonly System.Reflection.FieldInfo BusyAnimatingField = AccessTools.Field(typeof(Cashier), "busyAnimating");
    private static readonly System.Reflection.FieldInfo ServingField = AccessTools.Field(typeof(Cashier), "serving");
    private static readonly System.Reflection.FieldInfo QuitWhenReadyField = AccessTools.Field(typeof(Cashier), "quitWhenReady");
    private static readonly System.Reflection.FieldInfo SwitchWhenReadyField = AccessTools.Field(typeof(Cashier), "switchWhenReady");
    private static readonly System.Reflection.FieldInfo NewCheckoutDeskIdField = AccessTools.Field(typeof(Cashier), "newCheckoutDeskID");

    private static void Postfix(Cashier __instance)
    {
        BusyAnimatingField.SetValue(__instance, false);
        ServingField.SetValue(__instance, false);
        QuitWhenReadyField.SetValue(__instance, false);
        SwitchWhenReadyField.SetValue(__instance, false);
        NewCheckoutDeskIdField.SetValue(__instance, -1);
    }
}
