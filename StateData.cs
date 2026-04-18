using System.Collections.Generic;

namespace AeternalEverwatcher;

public static class StateData
{
    public static readonly HashSet<string> undergroundStates =
    [
        "Dig In 1",
        "Dig In 2",
        "Away",
        "Hiding",
        "Emerge Pause",
        "Dig Pos",
        "Dig Out Antic",
        "Dig Out 1"
    ];
    public static readonly HashSet<string> iframeStates =
    [
        "Uppercut 1",
        "Uppercut 2",
        "Uppercut 3",
        "Dig Out Uppercut",
        "Slash Combo 9",
        "Slash Combo 10",
        "Slash Combo 11",
        "Slash Combo 12",
    ];
    public static readonly HashSet<string> parryableStates =
    [
        "Slash Combo 1",
        "Slash Combo 2",
        "Slash Combo 3",
        "Slash Combo 5",
        "Slash Combo 6",
        "Slash Combo 7",
        "Slash Combo 9",
        "Slash Combo 10",
        "F Slash 2",
        "F Slash 3",
        "F Slash 4",
        "Uppercut 1",
        "Uppercut 2",
        "Uppercut 3",
        "Dig Out Uppercut"
    ];
}