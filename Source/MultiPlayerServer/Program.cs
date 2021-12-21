using System;

namespace Orts.MultiPlayerServer
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.Title = "OpenRails MultiPlayer Server";
            try
            {
                int port = 30000;
                if (args.Length > 0 && !int.TryParse(args[0], out port))
                    port = 30000;
                Host server = new Host(port);
                _ = server.Run().ConfigureAwait(false);
                Console.ReadLine();
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadLine();
            }
        }
    }
}
