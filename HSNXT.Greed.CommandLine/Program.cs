namespace HSNXT.Greed
{
    /// <summary>
    /// Contains basic methods for SharpGMad, like the entry point.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        private static int Main(string[] args)
        {
            // Clean up mess the previous executions might have left behind.
            ContentFile.DisposeExternals();

            // If there are parameters present, program starts as a CLI application.
            //
            // If there are no paremeters, the program is restarted in its own console
            // If there are no parameters and no console present, the GUI will start.
            // (This obviously means a small flickering of a console window (for the restart process)
            // but that's expendable for the fact that one compiled binary contains "both" faces.)

            if (args != null && args.Length > 0)
            {
                // This is needed because we support "drag and drop" GMA onto the executable
                // and if a D&D happens, the first parameter (args[0]) is a path.

                // There was a requirement for the console interface. Parse the parameters.
                switch (args[0])
                {
                    // Load the legacy (gmad.exe) interface
                    case "create":
                    case "extract":
                        return Legacy.EntryPoint(args);
                    // Load the realtime command-line
                    case "realtime":
                        return RealtimeCommandline.EntryPoint(args);
                }
            }
            else
            {
                return 1;
            }

            return 0;
        }

    }
}