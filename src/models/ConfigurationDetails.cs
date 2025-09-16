using SQLite;
using System.ComponentModel.DataAnnotations;

public class ConfigurationDetails
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    // Assuming 'key' is a unique identifier for each configuration setting
    [Required]
    public string Key { get; set; }

    // The value of the configuration setting
    public string Value { get; set; }

    // Timestamp for when the configuration was last updated
    public DateTime Timestamp { get; set; }

    // Source of the configuration setting (e.g., user, system)
    public string Source { get; set; }

    public string serviceName { get; set; }

    public string type { get; set; }
}
