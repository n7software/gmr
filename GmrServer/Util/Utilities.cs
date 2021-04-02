using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace GmrServer.Util
{
    public class Utilities
    {
        static Dictionary<string, string> Tags = new Dictionary<string, string>
        {
            { "[b]", "<strong>" },
            { "[/b]", "</strong>" },
            { "[i]", "<i>" },
            { "[/i]", "</i>" }
        };

        public static string ParseCommentCodes(string input, bool throwException = true)
        {

            string output = "<p>" + input + "</p>";

            output = output.Replace("\r\n\r\n", "</p><p>");
            output = output.Replace("\n\n", "</p><p>");
            output = output.Replace("\r\r", "</p><p>");
            output = output.Replace("<p></p>", "");

            string linkStartTag = "[link]";
            string linkEndTag = "[/link]";

            int index = output.IndexOf(linkStartTag);
            while (index != -1)
            {
                int end = output.IndexOf(linkEndTag, index);
                if (end != -1)
                {
                    string link = output.Substring(index + linkStartTag.Length, end - index - linkStartTag.Length);
                    output = InsertLink(output, index, end, link, link);
                }
                else if (throwException)
                {
                    throw new InvalidCommentException("Tag mismatch error");
                }
                else
                {
                    return input;
                }

                index = output.IndexOf(linkStartTag, index + 1);
            }

            string specialLinkStart = "[link=";

            index = output.IndexOf(specialLinkStart);
            while (index != -1)
            {
                int endingBracket = output.IndexOf("]", index);
                if (endingBracket != -1)
                {
                    string link = output.Substring(index + specialLinkStart.Length, endingBracket - index - specialLinkStart.Length);
                    int end = output.IndexOf(linkEndTag, endingBracket);
                    if (end != -1)
                    {
                        string text = output.Substring(endingBracket + 1, end - endingBracket - 1);
                        output = InsertLink(output, index, end, link, text);
                    }
                    else if (throwException)
                    {
                        throw new InvalidCommentException("Tag mismatch error");
                    }
                    else
                    {
                        return input;
                    }
                }
                else if (throwException)
                {
                    throw new InvalidCommentException("Malformed link");
                }
                else
                {
                    return input;
                }

                index = output.IndexOf(specialLinkStart, index + 1);
            }

            foreach (var kvp in Tags)
            {
                output = output.Replace(kvp.Key, kvp.Value);
            }

            try
            {
                XDocument.Parse("<text>" + output + "</text>");
            }
            catch
            {
                if (throwException)
                {
                    throw new InvalidCommentException("XMl parse error");
                }
                else
                {
                    return input;
                }
            }

            return output;
        }

        private static string InsertLink(string str, int start, int end, string link, string text)
        {
            return str.Substring(0, start) + "<a href=\"" + link + "\" target=\"_blank\">" +
                text + "</a>" + str.Substring(end + 7);
        }

        public static bool IsTextMarkupValid(string text)
        {
            try
            {
                ParseCommentCodes(text);
            }
            catch (InvalidCommentException)
            {
                return false;
            }

            return true;
        }
    }

    public class InvalidCommentException : Exception
    {
        public InvalidCommentException()
            : base()
        {
        }

        public InvalidCommentException(string text)
            : base(text)
        {

        }
    }
}