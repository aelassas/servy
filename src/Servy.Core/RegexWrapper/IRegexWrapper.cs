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
        /// Searches the specified input string for all occurrences of the regular expression.
        /// </summary>
        /// <param name="input">The string to search for a match.</param>
        /// <returns>A collection of successful matches found in the input string.</returns>
        /// <exception cref="RegexMatchTimeoutException">Thrown if the execution time exceeds the regex timeout interval.</exception>
        MatchCollection Matches(string input);
    }
}