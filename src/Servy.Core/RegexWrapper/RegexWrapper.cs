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
        /// Returns the text of every match of the regular expression in the input string.
        /// </summary>
        /// <param name="input">The string to search for a match.</param>
        /// <returns>The matched substrings, in the order they occur.</returns>
        /// <exception cref="RegexMatchTimeoutException">Thrown while the returned sequence is enumerated, if the execution time exceeds the regex timeout interval. Enumerate inside the try block that should observe the timeout.</exception>
        /// <exception cref="ArgumentNullException">Thrown while the returned sequence is enumerated, when <paramref name="input"/> is null.</exception>
        public IEnumerable<string> MatchValues(string input)
        {
            foreach (Match match in _regex.Matches(input))
            {
                yield return match.Value;
            }
        }
    }
}
