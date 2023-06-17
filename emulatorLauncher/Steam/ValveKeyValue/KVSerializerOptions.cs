using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ValveKeyValue
{
    /// <summary>
    /// Options to use when deserializing a KeyValues file.
    /// </summary>
    public sealed class KVSerializerOptions
    {
        /// <summary>
        /// Gets or sets a list of conditions to use to match conditional values.
        /// </summary>
        public IList<string> Conditions { get { return _conditions; } }
        private List<string> _conditions = new List<string>(GetDefaultConditions());

        /// <summary>
        /// Gets or sets a value indicating whether the parser should translate escape sequences (e.g. <c>\n</c>, <c>\t</c>).
        /// </summary>
        public bool HasEscapeSequences { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether invalid escape sequences should truncate strings rather than throwing a <see cref="InvalidDataException"/>.
        /// </summary>
        public bool EnableValveNullByteBugBehavior { get; set; }

        /// <summary>
        /// Gets or sets a way to open any file referenced with <c>#include</c> or <c>#base</c>.
        /// </summary>
        public IIncludedFileLoader FileLoader { get; set; }

        /// <summary>
        /// Gets the default options (used when none are specified).
        /// </summary>
        public static KVSerializerOptions DefaultOptions { get { return _defaultOptions; } }
        static KVSerializerOptions _defaultOptions = new KVSerializerOptions();

        static IEnumerable<string> GetDefaultConditions()
        {
            // TODO: In the future we will want to skip this for consoles and mobile devices?
            yield return "WIN32";
            yield return "WINDOWS";
        }
    }
}
