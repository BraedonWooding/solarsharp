﻿using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;
using System;
using System.Collections.Generic;
using System.Text;

namespace SolarSharp.Interpreter.CoreLib
{
    /// <summary>
    /// Class implementing time related Lua functions from the 'os' module.
    /// </summary>
    [MoonSharpModule(Namespace = "os")]
    public class OsTimeModule
    {
        private static readonly DateTime Time0 = DateTime.UtcNow;
        private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static DynValue GetUnixTime(DateTime dateTime, DateTime? epoch = null)
        {
            double time = (dateTime - (epoch ?? Epoch)).TotalSeconds;

            if (time < 0.0)
                return DynValue.Nil;

            return DynValue.NewNumber(time);
        }

        private static DateTime FromUnixTime(double unixtime)
        {
            TimeSpan ts = TimeSpan.FromSeconds(unixtime);
            return Epoch + ts;
        }

        [MoonSharpModuleMethod]
#pragma warning disable IDE0060 // Remove unused parameter
        public static DynValue clock(ScriptExecutionContext _, CallbackArguments _args)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            var t = GetUnixTime(DateTime.UtcNow, Time0);
            if (t.IsNil()) return DynValue.NewNumber(0.0);
            return t;
        }

        [MoonSharpModuleMethod]
        public static DynValue difftime(ScriptExecutionContext _, CallbackArguments args)
        {
            DynValue t2 = args.AsType(0, "difftime", DataType.Number, false);
            DynValue t1 = args.AsType(1, "difftime", DataType.Number, true);

            if (t1.IsNil())
                return DynValue.NewNumber(t2.Number);

            return DynValue.NewNumber(t2.Number - t1.Number);
        }

        [MoonSharpModuleMethod]
        public static DynValue time(ScriptExecutionContext _, CallbackArguments args)
        {
            DateTime date = DateTime.UtcNow;

            if (args.Count > 0)
            {
                DynValue vt = args.AsType(0, "time", DataType.Table, true);
                if (vt.Type == DataType.Table)
                    date = ParseTimeTable(vt.Table);
            }

            return GetUnixTime(date);
        }

        private static DateTime ParseTimeTable(Table t)
        {
            int sec = GetTimeTableField(t, "sec") ?? 0;
            int min = GetTimeTableField(t, "min") ?? 0;
            int hour = GetTimeTableField(t, "hour") ?? 12;
            int? day = GetTimeTableField(t, "day");
            int? month = GetTimeTableField(t, "month");
            int? year = GetTimeTableField(t, "year");

            if (day == null)
                throw new ErrorException("field 'day' missing in date table");

            if (month == null)
                throw new ErrorException("field 'month' missing in date table");

            if (year == null)
                throw new ErrorException("field 'year' missing in date table");

            return new DateTime(year.Value, month.Value, day.Value, hour, min, sec);
        }


        private static int? GetTimeTableField(Table t, string key)
        {
            DynValue v = t.Get(key);
            double? d = v.CastToNumber();

            if (d.HasValue)
                return (int)d.Value;

            return null;
        }

        [MoonSharpModuleMethod]
        public static DynValue date(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            DateTime reference = DateTime.UtcNow;

            DynValue vformat = args.AsType(0, "date", DataType.String, true);
            DynValue vtime = args.AsType(1, "date", DataType.Number, true);

            string format = vformat.IsNil() ? "%c" : vformat.String;

            if (vtime.IsNotNil())
                reference = FromUnixTime(vtime.Number);

            bool isDst = false;

            if (format.StartsWith("!"))
            {
                format = format[1..];
            }
            else
            {
#if !(PCL || ENABLE_DOTNET || NETFX_CORE)

                try
                {
                    reference = TimeZoneInfo.ConvertTimeFromUtc(reference, TimeZoneInfo.Local);
                    isDst = reference.IsDaylightSavingTime();
                }
                catch (TimeZoneNotFoundException)
                {
                    // this catches a weird mono bug: https://bugzilla.xamarin.com/show_bug.cgi?id=11817
                    // however the behavior is definitely not correct. damn.
                }
#endif
            }


            if (format == "*t")
            {
                Table t = new(executionContext.GetScript());

                t.Set("year", DynValue.NewNumber(reference.Year));
                t.Set("month", DynValue.NewNumber(reference.Month));
                t.Set("day", DynValue.NewNumber(reference.Day));
                t.Set("hour", DynValue.NewNumber(reference.Hour));
                t.Set("min", DynValue.NewNumber(reference.Minute));
                t.Set("sec", DynValue.NewNumber(reference.Second));
                t.Set("wday", DynValue.NewNumber((int)reference.DayOfWeek + 1));
                t.Set("yday", DynValue.NewNumber(reference.DayOfYear));
                t.Set("isdst", DynValue.NewBoolean(isDst));

                return DynValue.NewTable(t);
            }

            else return DynValue.NewString(StrFTime(format, reference));
        }

        private static string StrFTime(string format, DateTime d)
        {
            // ref: http://www.cplusplus.com/reference/ctime/strftime/

            Dictionary<char, string> STANDARD_PATTERNS = new()
            {
                { 'a', "ddd" },
                { 'A', "dddd" },
                { 'b', "MMM" },
                { 'B', "MMMM" },
                { 'c', "f" },
                { 'd', "dd" },
                { 'D', "MM/dd/yy" },
                { 'F', "yyyy-MM-dd" },
                { 'g', "yy" },
                { 'G', "yyyy" },
                { 'h', "MMM" },
                { 'H', "HH" },
                { 'I', "hh" },
                { 'm', "MM" },
                { 'M', "mm" },
                { 'p', "tt" },
                { 'r', "h:mm:ss tt" },
                { 'R', "HH:mm" },
                { 'S', "ss" },
                { 'T', "HH:mm:ss" },
                { 'y', "yy" },
                { 'Y', "yyyy" },
                { 'x', "d" },
                { 'X', "T" },
                { 'z', "zzz" },
                { 'Z', "zzz" },
            };


            StringBuilder sb = new();

            bool isEscapeSequence = false;

            for (int i = 0; i < format.Length; i++)
            {
                char c = format[i];

                if (c == '%')
                {
                    if (isEscapeSequence)
                    {
                        sb.Append('%');
                        isEscapeSequence = false;
                    }
                    else
                        isEscapeSequence = true;

                    continue;
                }

                if (!isEscapeSequence)
                {
                    sb.Append(c);
                    continue;
                }

                if (c == 'O' || c == 'E') continue; // no modifiers

                isEscapeSequence = false;

                if (STANDARD_PATTERNS.ContainsKey(c))
                {
                    sb.Append(d.ToString(STANDARD_PATTERNS[c]));
                }
                else if (c == 'e')
                {
                    string s = d.ToString("%d");
                    if (s.Length < 2) s = " " + s;
                    sb.Append(s);
                }
                else if (c == 'n')
                {
                    sb.Append('\n');
                }
                else if (c == 't')
                {
                    sb.Append('\t');
                }
                else if (c == 'C')
                {
                    sb.Append(d.Year / 100);
                }
                else if (c == 'j')
                {
                    sb.Append(d.DayOfYear.ToString("000"));
                }
                else if (c == 'u')
                {
                    int weekDay = (int)d.DayOfWeek;
                    if (weekDay == 0)
                        weekDay = 7;

                    sb.Append(weekDay);
                }
                else if (c == 'w')
                {
                    int weekDay = (int)d.DayOfWeek;
                    sb.Append(weekDay);
                }
                else if (c == 'U')
                {
                    // Week number with the first Sunday as the first day of week one (00-53)
                    sb.Append("??");
                }
                else if (c == 'V')
                {
                    // ISO 8601 week number (00-53)
                    sb.Append("??");
                }
                else if (c == 'W')
                {
                    // Week number with the first Monday as the first day of week one (00-53)
                    sb.Append("??");
                }
                else
                {
                    throw new ErrorException("bad argument #1 to 'date' (invalid conversion specifier '{0}')", format);
                }
            }

            return sb.ToString();
        }
    }
}
