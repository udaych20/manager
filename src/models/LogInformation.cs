using SQLite;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class LogInformation
{
    [PrimaryKey]
    [AutoIncrement]
    public int Id { get; set; }

    public string Topic { get; set; }

    public string podName { get; set; }

    public string InternetConnected { get; set; }

    public string Message { get; set; }

    public string Exception { get; set; }

    public DateTime Timestamp { get; set; }
    public string ClientId { get; internal set; }
}
