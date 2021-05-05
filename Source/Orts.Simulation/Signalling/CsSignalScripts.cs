using System.IO;

namespace Orts.Simulation.Signalling
{
    // TODO 20210505 this class may be integrated somewhere else 
    public static class CsSignalScripts
    {
        public static CsSignalScript TryGetScript(string scriptName)
        {
            string scriptPath = Path.Combine(Simulator.Instance.RoutePath, "Script", "Signal");
            return Simulator.Instance.ScriptManager.Load(scriptPath, scriptName) as CsSignalScript;
        }
    }
}
