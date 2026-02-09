using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.Models
{
    public class PluginModel
    {
        public string Name { get; set; }

        public string Guid { get; set; }

        public TimeSpan RunTime { get; set; }

        public bool IsRuning { get; set; }

        
    }
}
