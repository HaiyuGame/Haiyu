using System;
using System.Collections.Generic;
using System.Text;

namespace ABI.Models
{
    public record MonitorRecord(CPUData cpu);

    public class CPUData
    {
        public string Load { get; set; }

        public string Temperature { get; set; }

        public string Power { get; set;  }
        public string Clock { get; set;  }
        public string Voltage { get; set;  }
    }
}
