using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ValveKeyValue.Deserialization.KeyValues1
{
    class KV1TokenReader : KVTokenReader
    {
        const char QuotationMark = '"';
        const char ObjectStart = '{';
        const char ObjectEnd = '}';
        const char CommentBegin = '/'; // Although Valve uses the double-slash convention, the KV spec allows for single-slash comments.
        const char ConditionBegin = '[';
        const char ConditionEnd = ']';
        const char InclusionMark = '#';

        public KV1TokenReader(TextReader textReader, KVSerializerOptions options) : base(textReader)
        {
            Require.NotNull(options, "options");

            this.options = options;
        }

        readonly KVSerializerOptions options;
        readonly StringBuilder sb = new StringBuilder();

        public KVToken ReadNextToken()
        {
            Require.NotDisposed("KV1TokenReader", disposed);
            SwallowWhitespace();

            var nextChar = Peek();
            if (IsEndOfFile(nextChar))
            {
                return new KVToken(KVTokenType.EndOfFile);
            }

            switch (nextChar)
            {
                case ObjectStart: return ReadObjectStart();
                case ObjectEnd: return ReadObjectEnd();
                case CommentBegin: return ReadComment();
                case ConditionBegin: return ReadCondition();
                case InclusionMark: return ReadInclusion();
                default: return ReadString();
            };
        }

        KVToken ReadString()
        {
            var text = ReadStringRaw();
            return new KVToken(KVTokenType.String, text);
        }

        KVToken ReadObjectStart()
        {
            ReadChar(ObjectStart);
            return new KVToken(KVTokenType.ObjectStart);
        }

        KVToken ReadObjectEnd()
        {
            ReadChar(ObjectEnd);
            return new KVToken(KVTokenType.ObjectEnd);
        }

        KVToken ReadComment()
        {
            ReadChar(CommentBegin);

            // Some keyvalues implementations have a bug where only a single slash is needed for a comment
            // If the file ends with a single slash then we have an empty comment, bail out

            if (!TryGetNext(out var next))
            {
                return new KVToken(KVTokenType.Comment, string.Empty);
            }
            // If the next character is not a slash, then we have a comment that starts with a single slash
            // Otherwise pretend the comment is a double-slash and ignore this new second slash.
            if (next != CommentBegin)
            {
                sb.Append(next);
            }

            while (TryGetNext(out next))
            {
                if (next == '\n')
                {
                    break;
                }

                sb.Append(next);
            }

            if (sb.Length > 0 && sb[sb.Length - 1] == '\r')
            {
                sb.Remove(sb.Length - 1, 1);
            }

            var text = sb.ToString();
            sb.Clear();

            return new KVToken(KVTokenType.Comment, text);
        }

        KVToken ReadCondition()
        {
            ReadChar(ConditionBegin);
            var text = ReadUntil(ConditionEnd);
            ReadChar(ConditionEnd);

            return new KVToken(KVTokenType.Condition, text);
        }

        KVToken ReadInclusion()
        {
            ReadChar(InclusionMark);
            var term = ReadUntil(new[] { ' ', '\t' });
            var value = ReadStringRaw();

            if (string.Equals(term, "include", StringComparison.Ordinal))
            {
                return new KVToken(KVTokenType.IncludeAndAppend, value);
            }
            else if (string.Equals(term, "base", StringComparison.Ordinal))
            {
                return new KVToken(KVTokenType.IncludeAndMerge, value);
            }

            throw new InvalidDataException("Unrecognized term after '#' symbol.");
        }

        string ReadUntil(params char[] terminators)
        {
            var escapeNext = false;

            var integerTerminators = new HashSet<int>(terminators.Select(t => (int)t));
            while (!integerTerminators.Contains(Peek()) || escapeNext)
            {
                var next = Next();

                if (options.HasEscapeSequences)
                {
                    if (!escapeNext && next == '\\')
                    {
                        escapeNext = true;
                        continue;
                    }

                    if (escapeNext)
                    {
                        switch (next)
                        {
                            case 'n':
                                next = '\n';
                                break;
                            case 't':
                                next = '\t';
                                break;
                            case 'v':
                                next = '\v';
                                break;
                            case 'b':
                                next = '\b';
                                break;
                            case 'r':
                                next = '\r';
                                break;
                            case 'f':
                                next = '\f';
                                break;
                            case 'a':
                                next = '\a';
                                break;
                            case '?':
                                next = '?';
                                break;
                            case '\\':
                                next = '\\';
                                break;
                            case '\'':
                                next = '\'';
                                break;
                            case '"':
                                next = '"';
                                break;
                            default:
                                if (options.EnableValveNullByteBugBehavior)
                                    next = '\0';
                                else
                                    throw new InvalidDataException("Unknown escape sequence '\\" + next +"'.");

                                break;
                        }

                        escapeNext = false;
                    }
                }

                sb.Append(next);
            }

            var result = sb.ToString();
            sb.Clear();

            // Valve bug-for-bug compatibility with tier1 KeyValues/CUtlBuffer: an invalid escape sequence is a null byte which
            // causes the text to be trimmed to the point of that null byte.
            if (options.EnableValveNullByteBugBehavior && result.IndexOf('\0') is var nullByteIndex && nullByteIndex >= 0)
            {
                    result = result.Substring(0, nullByteIndex);
            }
            return result;
        }

        string ReadUntilWhitespaceOrQuote()
        {
            while (true)
            {
                var next = Peek();
                if (next == -1 || char.IsWhiteSpace((char)next) || next == '"')
                {
                    break;
                }

                sb.Append(Next());
            }

            var result = sb.ToString();
            sb.Clear();

            return result;
        }

        string ReadStringRaw()
        {
            SwallowWhitespace();
            if (Peek() == '"')
            {
                return ReadQuotedStringRaw();
            }
            else
            {
                return ReadUntilWhitespaceOrQuote();
            }
        }

        string ReadQuotedStringRaw()
        {
            ReadChar(QuotationMark);
            var text = ReadUntil(QuotationMark);
            ReadChar(QuotationMark);
            return text;
        }
    }
}
