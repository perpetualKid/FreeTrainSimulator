using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orts.Common.Input
{
    public interface IInputCapture
    {
        bool InputCaptured { get; }
    }
}
