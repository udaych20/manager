using Nini.Config;

public class ConfigPropertiesReader
{
    public ConfigPropertiesReader() { }

    public string GetPropertyValue(string key)
    {
        // Directly fetch from environment variables
        return Environment.GetEnvironmentVariable(key);
    }
}