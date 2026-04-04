#pragma warning disable CA1819
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace RadiantConnect.SocketServices.XMPP.XMPPManagement
{
	/// <summary>
	/// Specifies the action to take when intercepting data or an operation.
	/// </summary>
	/// <remarks>Use this enumeration to indicate whether an intercepted operation should be allowed to proceed or
	/// be blocked. The meaning of each value depends on the context in which interception occurs.</remarks>
	public enum InterceptAction
	{
		/// <summary>
		/// Gets or sets a value indicating whether the operation should proceed in the forward direction.
		/// </summary>
		Forward,
		/// <summary>
		/// Specifies that the data is blocked and not forwarded.
		/// </summary>
		Block
	}

	/// <summary>
	/// Provides context and data for intercepting and manipulating content, including byte and string representations,
	/// within an interception workflow.
	/// </summary>
	public class InterceptContext
	{
		/// <summary>
		/// Gets or sets the raw byte data associated with this instance.
		/// </summary>
		public byte[] Bytes { get; set; }

		/// <summary>
		/// Gets or sets the number of bytes represented by the current instance.
		/// </summary>
		public int ByteCount { get; set; }

		/// <summary>
		/// Gets or sets the textual content associated with this instance.
		/// </summary>
		public string Content { get; set; }

		/// <summary>
		/// Gets or sets the action to take when an interception occurs.
		/// </summary>
		/// <remarks>Use this property to specify how intercepted operations should be handled. The default value is
		/// InterceptAction.Forward, which forwards the operation without modification.</remarks>
		public InterceptAction Action { get; set; } = InterceptAction.Forward;

		/// <summary>
		/// Replaces the current content with the specified string, updating all related data to reflect the new value.
		/// </summary>
		/// <param name="newContent">The new content to set. Cannot be null.</param>
		public void ReplaceContent(string newContent)
		{
			byte[] newBytes = Encoding.UTF8.GetBytes(newContent);
			Bytes = newBytes;
			ByteCount = newBytes.Length;
			Content = newContent;
		}

		/// <summary>
		/// Searches the content for all occurrences that match the specified regular expression pattern.
		/// </summary>
		/// <remarks>Use this method to retrieve all matches of a regular expression within the content. The method
		/// does not throw an exception if no matches are found; instead, it returns an empty collection.</remarks>
		/// <param name="pattern">The regular expression pattern to match against the content. Cannot be null.</param>
		/// <param name="options">A bitwise combination of enumeration values that modify the regular expression. The default is RegexOptions.None.</param>
		/// <returns>A MatchCollection containing all successful matches found in the content. The collection is empty if no matches
		/// are found.</returns>
		public MatchCollection RegexFind([StringSyntax(StringSyntaxAttribute.Regex)] string pattern, RegexOptions options = RegexOptions.None) => Matches(Content, pattern, options);

		/// <summary>
		/// Searches the content for all matches of the specified regular expression.
		/// </summary>
		/// <param name="regex">The regular expression to use for matching within the content. Cannot be null.</param>
		/// <returns>A collection of all matches found in the content. The collection is empty if no matches are found.</returns>
		public MatchCollection RegexFind(Regex regex)
		{
			ArgumentNullException.ThrowIfNull(regex);
			return regex.Matches(Content);
		}

		/// <summary>
		/// Determines whether the content matches the specified regular expression pattern.
		/// </summary>
		/// <param name="pattern">The regular expression pattern to match against the content. The pattern must be a valid regular expression.</param>
		/// <param name="options">A bitwise combination of enumeration values that modify the regular expression matching behavior. The default is
		/// RegexOptions.None.</param>
		/// <returns>true if the content matches the specified pattern; otherwise, false.</returns>
		public bool RegexContains([StringSyntax(StringSyntaxAttribute.Regex)] string pattern, RegexOptions options = RegexOptions.None) => IsMatch(Content, pattern, options);

		/// <summary>
		/// Determines whether the content matches the specified regular expression pattern.
		/// </summary>
		/// <param name="regex">The regular expression to match against the content. Cannot be null.</param>
		/// <returns>true if the content matches the regular expression pattern; otherwise, false.</returns>
		public bool RegexContains(Regex regex)
		{
			ArgumentNullException.ThrowIfNull(regex);
			return regex.IsMatch(Content);
		}

		/// <summary>
		/// Replaces all substrings in the content that match the specified regular expression pattern with the specified
		/// replacement string.
		/// </summary>
		/// <remarks>Use this method to perform complex text replacements using regular expressions. The method
		/// applies the specified pattern to the entire content and replaces all matches. If no matches are found, the content
		/// remains unchanged.</remarks>
		/// <param name="pattern">The regular expression pattern to match within the content. Must be a valid regular expression.</param>
		/// <param name="replacement">The replacement string to use for each match found by the regular expression.</param>
		/// <param name="options">A bitwise combination of enumeration values that modify the regular expression matching behavior. The default is
		/// RegexOptions.None.</param>
		public void RegexReplace([StringSyntax(StringSyntaxAttribute.Regex)] string pattern, string replacement, RegexOptions options = RegexOptions.None)
		{
			string newContent = Replace(Content, pattern, replacement, options);
			ReplaceContent(newContent);
		}

		/// <summary>
		/// Replaces all substrings in the content that match the specified regular expression with the specified replacement
		/// string.
		/// </summary>
		/// <param name="regex">The regular expression used to identify substrings to replace. Cannot be null.</param>
		/// <param name="replacement">The string to replace each matched substring with. Can be an empty string to remove matches.</param>
		public void RegexReplace(Regex regex, string replacement)
		{
			ArgumentNullException.ThrowIfNull(regex);
			string newContent = regex.Replace(Content, replacement);
			ReplaceContent(newContent);
		}

		/// <summary>
		/// Replaces all substrings in the content that match the specified regular expression pattern using a custom match
		/// evaluator and options.
		/// </summary>
		/// <remarks>Use this method to perform complex replacements where the replacement value depends on the
		/// matched content. The match evaluator is called for each match found in the content.</remarks>
		/// <param name="pattern">The regular expression pattern to match within the content. The pattern must be a valid regular expression.</param>
		/// <param name="evaluator">A custom method that processes each match and returns the replacement string for that match.</param>
		/// <param name="options">A bitwise combination of enumeration values that modify the regular expression matching behavior. The default is
		/// RegexOptions.None.</param>
		public void RegexReplace([StringSyntax(StringSyntaxAttribute.Regex)] string pattern, MatchEvaluator evaluator, RegexOptions options = RegexOptions.None)
		{
			string newContent = Replace(Content, pattern, evaluator, options);
			ReplaceContent(newContent);
		}

		/// <summary>
		/// Replaces all substrings in the content that match the specified regular expression with the results of a custom
		/// match evaluator.
		/// </summary>
		/// <remarks>This method updates the content in place by applying the specified regular expression and using
		/// the provided evaluator to determine each replacement. The operation processes all matches found in the current
		/// content.</remarks>
		/// <param name="regex">The regular expression used to identify matches within the content. Cannot be null.</param>
		/// <param name="evaluator">A delegate that defines the logic to generate replacement strings for each match found by the regular expression.</param>
		public void RegexReplace(Regex regex, MatchEvaluator evaluator)
		{
			ArgumentNullException.ThrowIfNull(regex);
			string newContent = regex.Replace(Content, evaluator);
			ReplaceContent(newContent);
		}

		/// <summary>
		/// Blocks the current content if it matches the specified regular expression pattern.
		/// </summary>
		/// <remarks>Use this method to prevent further processing of content that matches certain patterns. The
		/// action is only taken if a match is found.</remarks>
		/// <param name="pattern">The regular expression pattern to match against the content. Must be a valid regular expression.</param>
		/// <param name="options">A bitwise combination of enumeration values that modify the regular expression matching behavior. The default is
		/// RegexOptions.None.</param>
		/// <returns>true if the content matches the specified pattern and is blocked; otherwise, false.</returns>
		public bool RegexBlock([StringSyntax(StringSyntaxAttribute.Regex)] string pattern, RegexOptions options = RegexOptions.None)
		{
			if (!IsMatch(Content, pattern, options)) return false;
			Action = InterceptAction.Block;
			return true;
		}

		/// <summary>
		/// Blocks the current content if it matches the specified regular expression pattern.
		/// </summary>
		/// <remarks>If the content matches the provided regular expression, the action is set to block. The method
		/// does not modify the content itself.</remarks>
		/// <param name="regex">The regular expression to evaluate against the content. Cannot be null.</param>
		/// <returns>true if the content matches the regular expression and is blocked; otherwise, false.</returns>
		public bool RegexBlock(Regex regex)
		{
			ArgumentNullException.ThrowIfNull(regex);
			if (!regex.IsMatch(Content)) return false;
			Action = InterceptAction.Block;
			return true;
		}
	}
}
