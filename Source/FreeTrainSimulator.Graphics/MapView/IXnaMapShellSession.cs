namespace FreeTrainSimulator.Graphics.MapView
{
    public interface IXnaMapShellSession : IMapShellSession
    {
        new IXnaMapShellHost ShellHost { get; }
    }
}
