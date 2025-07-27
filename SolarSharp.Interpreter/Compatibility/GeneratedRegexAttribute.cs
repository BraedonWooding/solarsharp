#if NETSTANDARD

namespace System.Text.RegularExpressions
{
    internal class GeneratedRegexAttribute(string pattern, RegexOptions regexOptions) : Attribute
    {
        public string Pattern { get; } = pattern;
        public RegexOptions RegexOptions { get; } = regexOptions;
    }
}
#endif