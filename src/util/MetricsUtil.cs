
public static class MetricsUtil
{
    public static double GetMemoryByUnits(double memoryInBytes, string unit)
    {
        double result = 0;
        switch (unit)
        {
            case "MB":
                result = memoryInBytes / (1024 * 1024);
                break;
            case "GB":
                result = memoryInBytes / (1024 * 1024 * 1024);
                break;
            case "TB":
                result = memoryInBytes / (1024L * 1024 * 1024 * 1024);
                break;
            default:
                throw new ArgumentException("Invalid memory unit provided");
        }
        return Math.Round(result, 2);
    }
}
