using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceUsageMonitor
{
    public class UnacknowledgedMessageDto
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Topic { get; set; }
        public string ClientId { get; set; }
        public string Message { get; set; }
    }
}
