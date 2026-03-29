using System.Collections.ObjectModel;
using Waves.Core.Models;

namespace Waves.Core.GameContext;


/// <summary>
/// 步骤整合器
/// </summary>
partial class KuroGameContextBaseV2
{
    public List<string> Setups { get; set; } = new();

    public int CurrentSetups { get; set; }

   
}
