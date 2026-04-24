using BepInEx.Configuration;

namespace AeternalEverwatcher;

public static class Settings
{
    public static bool ON_DAMAGE_FREEZE;
    public static bool DISABLE_WIND_EFFECTS;
    public static float SAND_EFFECTS_BRIGHTNESS = 1;
    public static bool BOSS_AND_PLAYER_ABOVE_SAND;
    public static float PARRY_TIME_FREEZE = -1;
    public static int PHASE_2_QUOTA = 25;
    public static int PHASE_3_QUOTA = 75;
    public static int END_FIGHT_QUOTA = 160;
    public static int DESPERATION_PHASE_LENGTH = 50;
    public static void SetupSettings(ConfigFile Config)
    {
        SAND_EFFECTS_BRIGHTNESS = Config.Bind(
            "Visual Effects",
            "Sand Effects Brightness",
            1f,
            "By default i raise the brightness of the sand effects, and then multiply that by this value. Increase or decrease it to change the sand effects brightness, id advise to keep it above 0 to avoid shenanigans."
        ).Value;
        BOSS_AND_PLAYER_ABOVE_SAND = Config.Bind(
            "Visual Effects",
            "Boss And Player Above Sand",
            false,
            "I already put the boss and the player above most of the sand effects, turn this on if you want to put them above everything."
        ).Value;
        DISABLE_WIND_EFFECTS = Config.Bind(
            "Visual Effects",
            "Disable Wind Effects",
            false,
            "Disables the wind effects in the background and foreground."
        ).Value;
        PARRY_TIME_FREEZE = Config.Bind(
            "Accessibility",
            "Parry Time Freeze",
            -1f,
            "Use this to change the parry time freeze, set it to 0 to disable it entirely, or leave it at -1 to use the default time freeze. Must be greater than or equal 0 and less than or equal 0.261."
        ).Value;
        ON_DAMAGE_FREEZE = Config.Bind(
            "Accessibility",
            "On Damage Freeze",
            false,
            "This setting should help you keep your rhythm even when taking damage."
        ).Value;
        PHASE_2_QUOTA = Config.Bind(
            "Bossfight Settings",
            "Phase 2 Quota",
            25,
            "Quota of parries to trigger phase 2."
        ).Value;
        PHASE_3_QUOTA = Config.Bind(
            "Bossfight Settings",
            "Phase 3 Quota",
            75,
            "Quota of parries to trigger phase 3."
        ).Value;
        END_FIGHT_QUOTA = Config.Bind(
            "Bossfight Settings",
            "Fight End Quota",
            170,
            "Quota of parries to trigger the fight end."
        ).Value;
        DESPERATION_PHASE_LENGTH = Config.Bind(
            "Bossfight Settings",
            "Desperation Phase Length",
            50,
            "For each unit here, 2 coral spears spawn every 0.15 seconds."
        ).Value;
    }
}