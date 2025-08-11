using DSunTools.Common.Options;
using System;

namespace DSunTools.Common.Options
{
    public abstract class OptionsProcessor<O> where O : IOptionsWithHelp
    {
        private const int ERROR_EXIT_VALUE = unchecked((int)0xDEADBEEF);

        public abstract O ParseArgs(string[] args);

        public abstract bool IsUsageValid(O options);

        public abstract void PrintUsage();

        public O Process(string[] args)
        {
            O options;
            try
            {
                options = ParseArgs(args);
            }
            catch (Exception ex) // Equivalent to OptionException
            {
                Console.Error.WriteLine(ex.Message);
                if (ex.InnerException != null)
                {
                    Console.Error.WriteLine(ex.InnerException.Message);
                }

                Environment.Exit(ERROR_EXIT_VALUE);
                return default; // This line won't be reached, but satisfies compiler
            }

            if (options.IsHelpRequested)
            {
                PrintUsage();
                Environment.Exit(0);
            }

            if (!IsUsageValid(options))
            {
                PrintUsage();
                Environment.Exit(ERROR_EXIT_VALUE);
            }

            return options;
        }
    }
}