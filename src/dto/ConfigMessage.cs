using System;
using System.Collections.Generic;

public class ConfigMessage
{
    public long DeviceId { get; set; }
    public long ServiceId { get; set; }
    public string ServiceName { get; set; }
    public List<Dictionary<string, string>> Config { get; set; }
}
