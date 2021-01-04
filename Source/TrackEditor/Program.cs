using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

using Orts.Common.Native;

namespace Orts.TrackEditor
{
    public static class Program
    {
        [STAThread]
        private static void Main()
        {
            NativeMethods.SetProcessDpiAwareness(NativeMethods.PROCESS_DPI_AWARENESS.Process_Per_Monitor_DPI_Aware);
            Application.SetCompatibleTextRenderingDefault(false);

            using (GameWindow game = new GameWindow())
            {
                if (Debugger.IsAttached)
                {
                    game.Run();
                }
                else
                {
                    try
                    {
                        game.Run();
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                    {

                    }
                }
            }
        }
    }
}
