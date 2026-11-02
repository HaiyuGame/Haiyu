using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haiyu.ABI.XBox
{
    public class XBoxButton : XBoxComponent<bool>
    {
        public XBoxButton(bool initialValue = false) : base(initialValue) { }
    }
}
