using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haiyu.ServiceHost.XBox
{
    public class XBoxButton : XBoxComponent<bool>
    {
        public XBoxButton(bool initialValue = false) : base(initialValue) { }
    }
}
