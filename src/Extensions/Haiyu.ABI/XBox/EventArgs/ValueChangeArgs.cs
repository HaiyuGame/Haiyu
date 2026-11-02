using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haiyu.ABI.XBox.EventArgs
{
    public class ValueChangeArgs<T>
    {
        public T Value { get; }

        public ValueChangeArgs(T value)
        {
            Value = value;
        }
    }
}
