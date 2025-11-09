/*
** Regex library.
** Copyright (C) Copyright (c) 2024-2025, Braedon Wooding.  See copyright notice in LICENSE.md
**
** Major portions ported to C# or adapted from LuaJIT.
** Copyright (C) 2005-2025 Mike Pall. See Copyright Notice in luajit.h
**
** Major portions taken verbatim or adapted from the Lua interpreter.
** Copyright (C) 1994-2008 Lua.org, PUC-Rio. See Copyright Notice in lua.h
*/

using SolarSharp.Interpreter.DataTypes;
using System;
using System.Text;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;

// Lua has a very custom regex implementation, which is not compatible with standard regex libraries.
// The main issues being the way it handles balanced patterns & empty capture groups capturing positions.
// So this class implements the regex exactly how lua does, it's primarily based on Lua Jit's implementation.
namespace SolarSharp.Interpreter.Regex
{
    public static class LuaRegex
    {
#if NET8_0_OR_GREATER
        private static readonly System.Buffers.SearchValues<char> possibleChars = System.Buffers.SearchValues.Create("^$*+?.([%-");
#else
        private static readonly char[] possibleChars = "^$*+?.([%-".ToCharArray();        
#endif

        public static bool StringHasPattern(string str)
        {
#if NET8_0_OR_GREATER
            return str.AsSpan().IndexOfAny(possibleChars) >= 0;
#else
            return str.IndexOfAny(possibleChars) >=0;     
#endif
        }
        
        public static LuaValue FindPlainMatch(string str, string pattern, int init)
        {
            // Remapping from 1-based index -> 0-based.
            switch (init)
            {
                case < 0:
                    init = Math.Max(0, str.Length + init);
                    break;
                case > 0:
                    init--;
                    break;
            }

            if (init >= str.Length || (str.IndexOf(pattern, init, StringComparison.Ordinal) is var index && index < 0))
            {
                // no match
                return LuaValue.Nil;
            }

            // match found, return the start + end index
            return LuaValue.NewTuple(LuaValue.NewNumber(index + 1), LuaValue.NewNumber(index + pattern.Length));
        }

        public const int MaxDepth = 200;
        public const int MaxCaptures = 32;
        
        // Special values for captures
        private const int CaptureUnfinished = -1;
        private const int CapturePosition = -2;
        
        private static int Match(string str, string pattern, int index, int patternIndex, MatchState state)
        {
            state.Depth++;
            if (state.Depth > MaxDepth)
            {
                throw ScriptRuntimeException.PatternTooComplex();
            }
            
            init:
            if (patternIndex >= pattern.Length)
            {
                // End of string
                state.Depth--;
                return index;
            }

            switch (pattern[patternIndex])
            {
                case '(': /* start capture */
                    if (patternIndex + 1 < pattern.Length && pattern[patternIndex + 1] == ')')
                    {
                        // Position capture
                        index = StartCapture(str, pattern, index, patternIndex + 2, CapturePosition, state);
                    }
                    else
                    {
                        // Start capture
                        index = StartCapture(str, pattern, index, patternIndex + 1, CaptureUnfinished, state);
                    }
                    break;
                case ')': /* end capture */
                    index = EndCapture(str, pattern, index, patternIndex + 1, state);
                    break;
                case '%': /* escape */
                    if (patternIndex + 1 >= pattern.Length)
                    {
                        // https://github.com/LuaJIT/LuaJIT/blob/68354f444728ef99bb51bb4d86e8f1b40853a898/src/lib_string.c#L194
                        throw ScriptRuntimeException.MalformedPattern("ends with '%'");
                    }
                    
                    switch (pattern[patternIndex + 1])
                    {
                        case 'b': /* balanced string */
                        {
                            if (MatchBalance(str, pattern, index, patternIndex + 2) is var res and >= 0)
                            {
                                index = res;
                                patternIndex += 4;
                                goto init;
                            }

                            index = -1;
                            break;
                        }
                        case 'f': /* frontier */
                            patternIndex += 2;
                            if (patternIndex >= pattern.Length || pattern[patternIndex] != '[')
                            {
                                throw ScriptRuntimeException.MissingEndClassInPattern();
                            }
                            
                            var endIndex = FindClassEnd(pattern, patternIndex);
                            var previous = index == 0 ? '\0' : str[index - 1];
                            if (MatchBracketedClass(previous, pattern, patternIndex, endIndex - 1)
                                || !MatchBracketedClass(index < str.Length ? str[index] : '\0', pattern, patternIndex, endIndex - 1))
                            {
                                index = -1;
                                break;
                            }

                            patternIndex = endIndex;
                            goto init;
                        default: /* capture results? */
                        {
                            if (pattern[patternIndex + 1] >= '1' && pattern[patternIndex + 1] <= '9')
                            {
                                if (MatchCapture(str, index, pattern[patternIndex + 1] - '1',
                                        state) is var res and >= 0)
                                {
                                    index = res;
                                    patternIndex += 2;
                                    goto init;
                                }
                                
                                index = -1;
                                break;
                            }

                            goto dflt;
                        }
                    }
                    break;
                case '$': /* last char in pattern */
                    // If we aren't the last char in the pattern
                    if (patternIndex + 1 != pattern.Length)
                    {
                        goto dflt;
                    } 
                    
                    // Now check if we are at end of string
                    if (index != str.Length)
                    {
                        return -1;
                    }
                    break;
                default:
                    dflt:
                {
                    int endIndex = FindClassEnd(pattern, patternIndex);
                    bool isMatch = index < str.Length && SingleMatch(pattern, patternIndex, endIndex, str[index]);
                    switch (endIndex >= pattern.Length ? '\0' : pattern[endIndex])
                    {
                        case '?': /* optional */
                            if (isMatch
                                && Match(str, pattern, index + 1, patternIndex + 1, state) is var res and >= 0)
                            {
                                index = res;
                                break;
                            }
                            patternIndex = endIndex + 1;
                            goto init;
                        case '*': /* 0 or more (greedy) */
                            index = MaxExpand(str, pattern, index, patternIndex, endIndex, state);
                            break;
                        case '+': /* 1 or more */
                            index = isMatch ? MaxExpand(str, pattern, index + 1, patternIndex, endIndex, state) : -1;
                            break;
                        case '-': /* 0 or more (minimum) */
                            index = MinExpand(str, pattern, index, patternIndex, endIndex, state);
                            break;
                        default:
                            if (isMatch)
                            {
                                index++;
                                patternIndex = endIndex;
                                goto init;
                            }

                            index = -1;
                            break;
                    }
                    
                    break;
                }
            }

            state.Depth--;
            return index;
        }

        private static int StartCapture(string str, string pattern, int index, int patternIndex, int captureType, MatchState state)
        {
            if (state.Level >= MaxCaptures)
            {
                throw ScriptRuntimeException.TooManyCaptures();
            }
            
            state.Captures[state.Level].StartIndex = index;
            state.Captures[state.Level].Length = captureType;
            state.Level++;
            
            // Attempt match
            if (Match(str, pattern, index, patternIndex, state) is var res and >= 0)
            {
                return res;
            }
            
            // Undo capture
            state.Level--;
            return -1;
        }

        private static int EndCapture(string str, string pattern, int index, int patternIndex, MatchState state)
        {
            int level = state.Level - 1;
            for (; level >= 0; level--)
            {
                if (state.Captures[level].Length == CaptureUnfinished)
                {
                    // Finish capture
                    state.Captures[level].Length = index - state.Captures[level].StartIndex;
                    // Attempt match
                    if (Match(str, pattern, index, patternIndex, state) is var res and >= 0)
                    {
                        return res;
                    }
                    // Undo capture
                    state.Captures[level].Length = CaptureUnfinished;
                    return -1;
                }
            }

            throw ScriptRuntimeException.InvalidPatternCapture();
        }

        private static int MatchCapture(string str, int index, int captureIndex, MatchState state)
        {
            if (captureIndex >= state.Captures.Length || captureIndex < 0 || captureIndex >= state.Level ||
                state.Captures[captureIndex] is { Length: CaptureUnfinished })
            {
                throw ScriptRuntimeException.InvalidCaptureIndex(captureIndex, state.Level);
            }
            
            var capture = state.Captures[captureIndex];
            if (index + capture.Length > str.Length)
            {
                return -1;
            }
            
            if (str.AsSpan(capture.StartIndex, capture.Length).Equals(str.AsSpan(index, capture.Length), StringComparison.InvariantCulture))
            {
                return index + capture.Length;
            }

            return -1;
        }
        
        private static int MatchBalance(string str, string pattern, int index, int patternIndex)
        {
            if (patternIndex + 1 >= pattern.Length)
            {
                throw ScriptRuntimeException.UnbalancedPattern();
            }
            
            if (index >= str.Length || str[index] != pattern[patternIndex])
            {
                return -1;
            }

            int balanced = pattern[patternIndex];
            int unbalanced = pattern[patternIndex + 1];
            int count = 1;
            while (++index < str.Length)
            {
                if (str[index] == unbalanced)
                {
                    if (--count == 0)
                    {
                        return index + 1;
                    }
                }
                else if (str[index] == balanced)
                {
                    count++;
                }
            }

            // string ends with unbalanced still left
            return -1;
        }

        private static int MaxExpand(string str, string pattern, int index, int patternIndex, int endIndex, MatchState state)
        {
            int i;
            for (i = 0; index + i < str.Length && SingleMatch(pattern, patternIndex, endIndex, str[index + i]); i++) {}

            while (i >= 0)
            {
                if (Match(str, pattern, index + i, endIndex + 1, state) is var res and >= 0)
                {
                    return res;
                }

                i--;
            }
            return -1;
        }

        private static int MinExpand(string str, string pattern, int index, int patternIndex, int endIndex,
            MatchState state)
        {
            do
            {
                if (Match(str, pattern, index, endIndex + 1, state) is var res and >= 0)
                {
                    return res;
                }
            } while (index < str.Length && SingleMatch(pattern, patternIndex, endIndex, str[index++]));

            return -1;
        }

        private static bool SingleMatch(string pattern, int patternIndex, int endClass, char c)
        {
            return pattern[patternIndex] switch
            {
                '.' => true,
                '%' => MatchClass(c, pattern[patternIndex + 1]),
                '[' => MatchBracketedClass(c, pattern, patternIndex, endClass),
                var p => p == c,
            };
        }

        private static bool MatchBracketedClass(char c, string pattern, int patternIndex, int endClass)
        {
            var sign = pattern[patternIndex + 1] != '^';
            if (!sign)
            {
                patternIndex++;
            }

            while (++patternIndex <= endClass && patternIndex < pattern.Length)
            {
                if (pattern[patternIndex] == '%')
                {
                    patternIndex++;
                    if (MatchClass(c, pattern[patternIndex]))
                    {
                        return sign;
                    }
                }
                else if (patternIndex + 2 < endClass && pattern[patternIndex + 1] == '-')
                {
                    // Range
                    if (c >= pattern[patternIndex] && c <= pattern[patternIndex + 2])
                    {
                        return sign;
                    }

                    patternIndex += 2;
                }
                else if (c == pattern[patternIndex])
                {
                    return sign;
                }
            }

            return !sign;
        }

        private static bool MatchClass(char c, char patternChar)
        {
            var inverted = patternChar is >= 'A' and <= 'Z';
            var matchChar = inverted ? char.ToLowerInvariant(patternChar) : patternChar;
            
            return matchChar switch
            {
                // TODO: Should we support unicode classes here?
                // and if not should we make custom methods to just handle
                // the ascii variants.
                'a' => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z',
                'c' => char.IsControl(c),
                'd' => c is >= '0' and <= '9',
                'g' => !char.IsControl(c) && !char.IsWhiteSpace(c),
                'l' => c is >= 'a' and <= 'z',
                'p' => char.IsPunctuation(c),
                's' => char.IsWhiteSpace(c),
                'u' => c is >= 'A' and <= 'Z',
                'w' => char.IsLetterOrDigit(c),
                'x' => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F',
                'z' => c == '\0',
                _ => c == patternChar,
            } ^ inverted;
        }

        private static int FindClassEnd(string pattern, int patternIndex)
        {
            switch (pattern[patternIndex++])
            {
                case '%':
                    if (patternIndex >= pattern.Length)
                    {
                        throw ScriptRuntimeException.MalformedPattern("ends with '%'");
                    }

                    return patternIndex + 1;
                case '[':
                    if (patternIndex >= pattern.Length)
                    {
                        throw ScriptRuntimeException.MalformedPattern("missing ']'");
                    }
                    
                    // Inverted class
                    if (pattern[patternIndex] == '^')
                    {
                        patternIndex++;    
                    }

                    // Looks for the closing ']'
                    while (patternIndex < pattern.Length && pattern[patternIndex] != ']')
                    {
                        if (pattern[patternIndex++] == '%')
                        {
                            // Skip escapes
                            patternIndex++;
                        }
                    }

                    if (patternIndex >= pattern.Length)
                    {
                        throw ScriptRuntimeException.MalformedPattern("missing ']'");
                    }
                    
                    return patternIndex + 1;
                default:
                    return patternIndex;
            }
        }

        public static LuaValue Substitute(string str, string pattern, LuaValue repl, int maxMatches, ScriptExecutionContext context)
        {
            var anchor = pattern.Length > 0 && pattern[0] == '^';
            var patternIdx = 0;
            if (anchor)
            {
                patternIdx++;
            }
            
            var n = 0;
            // Note: numbers aren't supported by lua (it's a luajit like extension), we'll do the same,
            // but maybe we want strict mode?
            if (repl.Type != DataType.String && repl.Type != DataType.Number && repl.Type != DataType.Function && repl.Type != DataType.ClrFunction && repl.Type != DataType.Table)
            {
                throw ScriptRuntimeException.ExpectedStringFunctionTableNumber();
            }

            StringBuilder sb = new();
            MatchState match = new();
            var currentIndex = 0;
            while (n < maxMatches)
            {
                match.Level = match.Depth = 0;
                var index = Match(str, pattern, currentIndex, patternIdx, match);
                if (index >= 0)
                {
                    n++;
                    if (repl.Type == DataType.String)
                    {
                        GetStringReplacement(str, repl.String, sb, currentIndex, index, match);
                    }
                    else if (repl.Type == DataType.Number)
                    {
                        sb.Append(repl.Number);
                    }
                    else if (repl.Type is DataType.Function or DataType.ClrFunction)
                    {
                        LuaValue[] values;
                        if (match.Level == 0)
                        {
                            values = [LuaValue.NewString(str[currentIndex..index])];
                        }
                        else
                        {
                            values = new LuaValue[match.Level];
                            PushCaptures(str, match, values, 0);
                        }

                        var result = context.Call(repl, values).ToScalar();
                        if (!result.CastToBool())
                        {
                            result = LuaValue.NewString(str[currentIndex..index]);
                        }
                        // TODO: Expand to all types? being somewhat lazy here
                        if (result.Type == DataType.Boolean)
                        {
                            throw ScriptRuntimeException.InvalidReplacementValue("boolean");
                        }
                        sb.Append(result.ToStringIncludingMetatable(context));
                    }
                    else if (repl.Type == DataType.Table)
                    {
                        var tbl = repl.Table;
                        var capture = match.Level == 0
                            ? LuaValue.NewString(str[currentIndex..index])
                            : GetCapture(str, match, 0);
                        var value = tbl.Get(capture).ToScalar();
                        if (!value.CastToBool())
                        {
                            value = LuaValue.NewString(str[currentIndex..index]);
                        }

                        // TODO: Expand to all types? being somewhat lazy here
                        if (value.Type == DataType.Boolean)
                        {
                            throw ScriptRuntimeException.InvalidReplacementValue("boolean");
                        }
                        
                        sb.Append(value.ToStringIncludingMetatable(context));
                    }
                }

                if (index >= 0 && index > currentIndex)
                {
                    currentIndex = index;
                }
                else if (currentIndex < str.Length)
                {
                    sb.Append(str[currentIndex++]);
                }
                else
                {
                    break;
                }

                if (anchor)
                {
                    break;
                }
            }
            
            if (currentIndex < str.Length)
            {
                sb.Append(str, currentIndex, str.Length - currentIndex);
            }
            
            return LuaValue.NewTuple(LuaValue.NewString(sb.ToString()), LuaValue.NewNumber(n));
        }

        private static void GetStringReplacement(string str, string repl, StringBuilder sb, int currentIndex, int index,
            MatchState match)
        {
            var matchIndex = repl.IndexOf('%');
            if (matchIndex == -1)
            {
                sb.Append(repl);
            }
            else
            {
                if (matchIndex > 0)
                {
                    sb.Append(repl, 0, matchIndex);
                }

                for (var i = matchIndex + 1; i < repl.Length; i++)
                {
                    var c = repl[i];
                    if (c == '0')
                    {
                        // Entire match
                        sb.Append(str, currentIndex, index - currentIndex);
                    }
                    else if (c is >= '1' and <= '9')
                    {
                        // Capture group
                        var capIndex = c - '1';
                        sb.Append(GetCapture(str, match, capIndex).ToPrintString());
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    i++;
                    if (i >= repl.Length)
                    {
                        break;
                    }
                                    
                    // Then look for next %
                    matchIndex = repl.IndexOf('%', i);
                    if (matchIndex == -1)
                    {
                        sb.Append(repl, i, repl.Length - i);
                        break;
                    }

                    if (matchIndex > 0)
                    {
                        sb.Append(repl, i, matchIndex - i);
                    }

                    i = matchIndex;
                }
            }
        }

        public static LuaValue MatchIteratorCallback(string str, string pattern, int init)
        {
            if (init < 0)
            {
                init = Math.Max(0, str.Length + init);
            }
            else if (init > 0)
            {
                init--;
            }

            int currentIndex = init;
            return LuaValue.NewCallback((_, _) =>
            {
                if (currentIndex > str.Length)
                {
                    // no match
                    return LuaValue.Nil;
                }

                MatchState match = new();
                for (; currentIndex <= str.Length; currentIndex++)
                {
                    match.Level = match.Depth = 0;
                    if (Match(str, pattern, currentIndex, 0, match) is var index and >= 0)
                    {
                        var endPos = index;
                        if (endPos == currentIndex)
                        {
                            endPos++;
                        }

                        if (match.Level == 0)
                        {
                            var result = LuaValue.NewString(str[currentIndex..index]);
                            currentIndex = endPos;
                            return result;
                        }
                    
                        var results = new LuaValue[match.Level];
                        PushCaptures(str, match, results, 0);
                        currentIndex = endPos;
                        return LuaValue.NewTuple(results);
                    }
                }

                return LuaValue.Nil;
            });
        }

        public static LuaValue FindMatch(string str, string pattern, int init, bool findMatch)
        {
            if (init < 0)
            {
                init = Math.Max(0, str.Length + init);
            }
            else if (init > 0)
            {
                init--;
            }

            var patternIdx = 0;
            var anchored = false;
            var currentIdx = init;
            if (pattern[0] == '^')
            {
                anchored = true;
                patternIdx++;
            }

            var match = new MatchState
            {
                Level = 0,
                Depth = 0
            };

            do
            {
                // Try to find match
                match.Level = match.Depth = 0;
                var index = Match(str, pattern, currentIdx, patternIdx, match);
                if (index < 0) continue;
                
                if (findMatch)
                {
                    var results = new LuaValue[match.Level + 2];
                    // match found, return the start + end index, noting to re-adjust to 1-based indexing
                    results[0] = LuaValue.NewNumber(currentIdx + 1);
                    results[1] = LuaValue.NewNumber(index);
                    PushCaptures(str, match, results, 2);
                    return LuaValue.NewTuple(results);
                }
                else
                {
                    // TODO: Test??
                    if (match.Level == 0)
                    {
                        return LuaValue.NewString(str[currentIdx..index]);
                    }
                    
                    var results = new LuaValue[match.Level];
                    PushCaptures(str, match, results, 0);
                    return LuaValue.NewTuple(results);
                }
            } while (!anchored && currentIdx++ < str.Length);

            return LuaValue.Nil;
        }

        private static void PushCaptures(string str, MatchState match, LuaValue[] results, int offset)
        {
            for (int i = 0; i < match.Level; i++)
            {
                results[i + offset] = GetCapture(str, match, i);
            }
        }

        private static LuaValue GetCapture(string str, MatchState match, int level)
        {
            if (level >= match.Level)
            {
                throw ScriptRuntimeException.InvalidCaptureIndex(level, match.Level);
            }
            
            var capture = match.Captures[level];
            if (capture.Length == CaptureUnfinished)
            {
                throw ScriptRuntimeException.UnfinishedCapture(capture.StartIndex);
            }
            else if (capture.Length == CapturePosition)
            {
                 return LuaValue.NewNumber(capture.StartIndex + 1);
            }
            else
            {
                return LuaValue.NewString(str.AsSpan(capture.StartIndex, capture.Length).ToString());
            }
        }
    }

    public struct MatchCapture
    {
        public int StartIndex;
        public int Length;
    }
    
    public class MatchState
    {
        // Total Number of captures.
        public int Level;
        public int Depth;
        public readonly MatchCapture[] Captures = new MatchCapture[LuaRegex.MaxCaptures];
    }
}
