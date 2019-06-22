using System.Reflection;

namespace HSNXT.Greed
{
    internal static class Shared
    {
        /// <summary>
        /// Gets the version of the executing assembly with omitting the trailing zeros.
        /// </summary>
        public static string PrettyVersion
        {
            get
            {
                var ver = Assembly.GetExecutingAssembly().GetName().Version;
                var fieldCount = 0;

                // Increment the required fields until there is a value (this omits the trailing zeros)
                if (ver.Major != 0)
                    fieldCount = 1;
                if (ver.Minor != 0)
                    fieldCount = 2;
                if (ver.Build != 0)
                    fieldCount = 3;
                if (ver.Revision != 0)
                    fieldCount = 4;

                return "v" + ver.ToString(fieldCount);
            }
        }
    }
}