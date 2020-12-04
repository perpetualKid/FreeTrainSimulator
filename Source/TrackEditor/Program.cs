using System;

using Orts.Common.Native;

namespace Orts.TrackEditor
{
    public static class Program
    {
        [STAThread]
        private static void Main()
        {
            NativeMethods.SetProcessDpiAwareness(NativeMethods.PROCESS_DPI_AWARENESS.Process_Per_Monitor_DPI_Aware);

            using (GameWindow game = new GameWindow())
                game.Run();
        }
    }
}
