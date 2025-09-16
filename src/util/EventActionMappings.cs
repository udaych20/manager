public static class EventActionMappings
{
    public static readonly Dictionary<string, string> NotificationActions = new Dictionary<string, string>
    {
        ["SCALE_OUT"] = "readyToMoveToCloud",
        ["SCALE_BACK"] = "readyToMoveBackDevice",
        ["SCALE_UP"] = "readyToScaleUp",
        ["SCALE_DOWN"] = "readyToScaleDown"
    };

    public static readonly Dictionary<string, string> DefaultActions = new Dictionary<string, string>
    {
        ["SCALE_OUT"] = "SCALEOUT",
        ["SCALE_BACK"] = "SCALEBACK",
        ["SCALE_UP"] = "SCALEUP",
        ["SCALE_DOWN"] = "SCALEDOWN"
    };
}
