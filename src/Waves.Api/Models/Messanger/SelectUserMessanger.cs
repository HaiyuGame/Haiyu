namespace Waves.Api.Models.Messanger;

public class SelectUserMessanger
{
    public SelectUserMessanger(bool refresh)
    {
        Refresh = refresh;
    }

    public bool Refresh { get; }
}
