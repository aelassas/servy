using System.Text.RegularExpressions;

namespace Servy.Core.RegexWrapper
{
    /// <summary>
    /// Provides a concrete implementation of <see cref="IRegexWrapper"/> that delegates 
    /// regex operations to an underlying <see cref="Regex"/> instance.
    /// </summary>
    public class RegexWrapper : IRegexWrapper
    {
        private readonly Regex _regex;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegexWrapper"/> class.
        /// </summary>
        /// <param name="regex">The <see cref="Regex"/> instance to wrap.</param>
        public RegexWrapper(Regex regex)
        {
            _regex = regex ?? throw new ArgumentNullException(nameof(regex));
        }

        /// <summary>
        /// Searches the specified input string for all occurrences of the regular expression.
        /// </summary>
        /// <param name="input">The string to search for a match.</param>
        /// <returns>A collection of successful matches found in the input string.</returns>
        /// <exception cref="RegexMatchTimeoutException">Thrown if the execution time exceeds the timeout interval defined in the underlying <see cref="Regex"/> instance.</exception>
        public MatchCollection Matches(string input)
        {
            return _regex.Matches(input);
        }
    }
}