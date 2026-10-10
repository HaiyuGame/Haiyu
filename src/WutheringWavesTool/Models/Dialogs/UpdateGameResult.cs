using Waves.Core.Models.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.Models.Dialogs;

public class UpdateGameResult
{
    public GameResourceParameter? Parameter { get; init; }
    public string DiffSavePath { get; set; }
    public bool IsOk { get; internal set; }
}
