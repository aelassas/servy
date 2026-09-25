using System.Text.RegularExpressions;

namespace Servy.Core.RegexWrapper
{
    /// <summary>
    /// Defines a wrapper interface for regular expression operations to facilitate unit testing
    /// of components that otherwise rely on static or non-virtual <see cref="Regex"/> members.
    /// </summary>
    public interface IRegexWrapper
    {
        /// <summary>
        /// Returns the text of every match of the regular expression in the input string.
        /// </summary>
        /// <param name="input">The string to search for a match.</param>
        /// <returns>The matched substrings, in the order they occur.</returns>
        /// <exception cref="RegexMatchTimeoutException">Thrown while the returned sequence is enumerated, if the execution time exceeds the regex timeout interval. Enumerate inside the try block that should observe the timeout.</exception>
        /// <exception cref="ArgumentNullException">Thrown while the returned sequence is enumerated, when <paramref name="input"/> is null.</exception>
        IEnumerable<string> MatchValues(string input);
    }
}
