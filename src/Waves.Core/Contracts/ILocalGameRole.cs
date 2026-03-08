using System;
using System.Collections.Generic;
using System.Text;
using Waves.Core.Models.Enums;

namespace Waves.Core.Contracts;

public interface ILocalGameRole
{
    public string ServerName { get; set; }
    public GameType Type { get; set; }
}


public interface ILocalGamerPlayer
{
    public string Id { get; set; }

    public string ServerName { get; set; }

    public GameType Type { get; set; }
}