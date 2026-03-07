using System;
using System.Collections.Generic;
using System.Text;
using Waves.Core.Models.Enums;

namespace Waves.Core.Contracts;

public interface ILocalGameRole
{
    public GameType Type { get; set; }
}
