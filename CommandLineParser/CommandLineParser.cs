namespace CommandLineParser
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// The command line parser.
    /// </summary>
    public static class CommandLineParser
    {
        /// <summary>
        /// The delimiters.
        /// </summary>
        private static readonly char[] Delimiters = { '=' };

        /// <summary>
        /// Types that a string can represent.
        /// </summary>
        public enum TimeSpanTypes
        {
            /// <summary>
            /// Default value.
            /// </summary>
            Invalid = 0,

            /// <summary>
            /// Millisecond use F
            /// </summary>
            Milliseconds = 'F',

            /// <summary>
            /// Seconds use S
            /// </summary>
            Seconds = 'S',

            /// <summary>
            /// Minutes use M
            /// </summary>
            Minutes = 'M',

            /// <summary>
            /// Hour use H
            /// </summary>
            Hours = 'H',

            /// <summary>
            /// Day use D
            /// </summary>
            Days = 'D'
        }

        public static bool IsValidInstance(this object source)
        {
            foreach (var p in source.GetType().GetProperties().Where(x => x.GetCustomAttributes<RequiredAttribute>().Any()))
            {
                if (p.GetValue(source).Equals(p.GetDefaultValueForProperty()))
                {
                    return false;
                }
            }

            return true;
        }

        public static object GetDefaultValueForProperty(this PropertyInfo property)
        {
            var a = property.GetCustomAttributes<DefaultValueAttribute>().FirstOrDefault();
            if (a != null)
            {
                return a.Value;
            }

            return property.PropertyType.IsValueType ? Activator.CreateInstance(property.PropertyType) : null;
        }

        /// <summary>
        /// The parse.
        /// </summary>
        /// <param name="args">
        /// The args.
        /// </param>
        /// <param name="maxDistance">
        /// The maximum Distance to compare words
        /// [set to 0 for only exact matches]
        /// </param>
        /// <typeparam name="T">
        /// The type of class to create.
        /// </typeparam>
        /// <returns>
        /// The instance of said class with options set according to passed arguments.
        /// </returns>
        public static T Parse<T>(this IEnumerable<string> args, int maxDistance = 2) where T : class, new()
        {
            var props = typeof(T).GetProperties();
            var response = new T();
            var names = props.Select(x => x.Name.ToLower()).ToArray();

            foreach (var arg in args.Select(x => x.Split(Delimiters, StringSplitOptions.RemoveEmptyEntries)))
            {
                if (arg.Length != 2)
                {
                    continue;
                }

                // find the exact or closest class property that matches to the argument passed.
                var word = arg[0].ToLower().ClosestWord(names, maxDistance);

                // grab the property by name.
                var prop = props.FirstOrDefault(x => x.Name.Equals(word, StringComparison.OrdinalIgnoreCase));

                if (prop == null)
                {
                    // failed to find a corresponding property to set.
                    continue;
                }

                // check if we have a string...
                // since this also implements the Ienumerable we have 
                // to check for string values first. 
                if (prop.PropertyType == typeof(string))
                {
                    // set the value of the string on our response. 
                    prop.SetValue(response, Convert.ChangeType(arg[1], prop.PropertyType));
                }
                else if (prop.PropertyType == typeof(TimeSpan))
                {
                    // set the value of the timespan on our response. 
                    prop.SetValue(response, FromTimespanString(arg[1]));
                }
                else if (prop.PropertyType.GetInterface(typeof(IEnumerable).Name) != null)
                {
                    // check if we are a generic type or a simple Array.
                    var elementType = prop.PropertyType.IsGenericType
                        ? prop.PropertyType.GetGenericArguments().First()
                        : prop.PropertyType.GetElementType();

                    // split the list to parse as a List of elements.
                    var split = arg[1].Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    // update the response with the converted list. 
                    prop.SetValue(response, split.ConvertListTo(prop.PropertyType, elementType));
                }
                else
                {
                    // check if we are an enum or not and handle the conversion
                    // properly. 
                    var r = prop.PropertyType.IsEnum
                        ? arg[1].ConvertToEnum(prop.PropertyType)
                        : Convert.ChangeType(arg[1], prop.PropertyType);

                    if (r != null)
                    {
                        // update the response with the converted object or Enum. 
                        prop.SetValue(response, r);
                    }
                }
            }

            return response;
        }

        /// <summary>
        /// The convert to.
        /// </summary>
        /// <param name="value">
        /// The value.
        /// </param>
        /// <param name="type">
        /// The type.
        /// </param>
        /// <param name="elementType">
        /// The element type.
        /// </param>
        /// <returns>
        /// The <see cref="object"/>.
        /// </returns>
        public static IList ConvertListTo(this IList value, Type type, Type elementType)
        {
            // create an instance of our list given the type and count.
            var response = (IList)Activator.CreateInstance(type, value.Count);

            for (var index = 0; index < value.Count; index++)
            {
                var item = value[index];

                // change the type to the correct one.
                var result = elementType.IsEnum
                                 ? item.ConvertToEnum(elementType)
                                 : Convert.ChangeType(item, elementType);

                if (result == null)
                {
                    // invalid result. 
                    continue;
                }

                if (type.IsArray)
                {
                    // put the result in our new array.
                    response[index] = result;
                }
                else
                {
                    // add the result into our List.
                    response.Add(result);
                }
            }

            return response;
        }

        /// <summary>
        /// The handle enumeration conversions.
        /// </summary>
        /// <param name="value">
        /// The value.
        /// </param>
        /// <param name="elementType">
        /// The element type.
        /// </param>
        /// <param name="singleChar">
        /// The single Char.
        /// </param>
        /// <returns>
        /// The <see cref="object"/>.
        /// </returns>
        public static object ConvertToEnum(this object value, Type elementType, bool singleChar = false)
        {
            if (!elementType.IsEnum)
            {
                return null;
            }

            int t;

            // check if this is a number that has been passed.
            if (int.TryParse(value.ToString(), out t))
            {
                return Enum.ToObject(elementType, t);
            }

            if (elementType.GetCustomAttributes<FlagsAttribute>().Any())
            {
                try
                {
                    var members = value.ToString().Split(new[] { ',', '-', ' ', '|', '_' }, StringSplitOptions.RemoveEmptyEntries);

                    if (members.Length > 1)
                    {
                        return members.Aggregate(0, (current, member) => current | (int)member.ConvertToEnum(elementType, member.Length < 2));
                    }
                }
                catch (Exception)
                {
                    return null;
                }
            }

            var name =
                Enum.GetNames(elementType).FirstOrDefault(x => singleChar ?
                x.StartsWith(value.ToString(), StringComparison.OrdinalIgnoreCase) :
                x.Equals(value.ToString(), StringComparison.OrdinalIgnoreCase));

            return string.IsNullOrEmpty(name) ? null : Enum.Parse(elementType, name, true);
        }

        /// <summary>
        /// Convert a Timespan to our String format for arguments. 
        /// </summary>
        /// <param name="time">
        /// The Timespan Object
        /// </param>
        /// <param name="type">
        /// How to represent the object.
        /// </param>
        /// <returns>
        /// The ConvertedString
        /// </returns>
        public static string ToSimpleString(this TimeSpan time, TimeSpanTypes type)
        {
            var suffix = (char)type;

            switch (type)
            {
                case TimeSpanTypes.Seconds:
                    return $"{time.TotalSeconds}{suffix}";

                case TimeSpanTypes.Minutes:
                    return $"{time.TotalMinutes}{suffix}";

                case TimeSpanTypes.Hours:
                    return $"{time.TotalHours}{suffix}";

                case TimeSpanTypes.Days:
                    return $"{time.TotalDays}{suffix}";

                case TimeSpanTypes.Invalid:
                    return string.Empty;

                default:
                    return $"{time.TotalMilliseconds}{suffix}";
            }
        }

        /// <summary>
        /// Converts a String into a Timespan using 
        /// </summary>
        /// <param name="time">
        /// The time to convert this supports our argument format.
        /// </param>
        /// <returns>
        /// The Converted Timespan.
        /// </returns>
        public static TimeSpan FromTimespanString(this string time)
        {
            if (TimeSpan.TryParse(time, out var result))
            {
                // able to parse the string into a timespan.
                return result;
            }

            // read the last character as a TimeSpanType.
            var type = (TimeSpanTypes)ConvertToEnum(time.Substring(time.Length - 1), typeof(TimeSpanTypes), true);
            if (type == TimeSpanTypes.Invalid)
            {
                return default(TimeSpan);
            }

            // get the value of the string.
            var val = double.Parse(time.Substring(0, time.Length - 1));

            switch (type)
            {
                default:
                    return TimeSpan.FromMilliseconds(val);

                case TimeSpanTypes.Seconds:
                    return TimeSpan.FromSeconds(val);

                case TimeSpanTypes.Minutes:
                    return TimeSpan.FromMinutes(val);

                case TimeSpanTypes.Hours:
                    return TimeSpan.FromHours(val);

                case TimeSpanTypes.Days:
                    return TimeSpan.FromDays(val);
            }
        }

        /// <summary>
        /// Finds the closest word to a list of words. 
        /// </summary>
        /// <param name="word">
        /// The word to compare.
        /// </param>
        /// <param name="words">
        /// The list of words to compare the word to.
        /// </param>
        /// <param name="maxDistance">
        /// The maximum distance to find a match.
        /// </param>
        /// <returns>
        /// The matching string.
        /// </returns>
        public static string ClosestWord(this string word, IEnumerable<string> words, int maxDistance)
        {
            var distance = maxDistance;
            var response = string.Empty;

            foreach (var entry in words)
            {
                // calculate the word distance.
                var currentDistance = word.WordDistance(entry);
                if (currentDistance == 0)
                {
                    // an exact match was found.
                    return entry;
                }

                if (currentDistance > distance)
                {
                    // word distance is too far.
                    continue;
                }

                response = entry;
                distance = currentDistance;
            }

            return response;
        }

        /// <summary>
        /// Finds the distance between two words.
        /// </summary>
        /// <param name="a">
        /// The first word to compare.
        /// </param>
        /// <param name="b">
        /// The second word to compare.
        /// </param>
        /// <returns>
        /// The distance between two words. 
        /// </returns>
        public static int WordDistance(this string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            {
                // we have an empty string so lets not use this.
                return int.MaxValue;
            }

            if (a.Equals(b, StringComparison.OrdinalIgnoreCase))
            {
                // the words are an exact match, ignoring casing.
                return 0;
            }

            var distance = new int[a.Length + 1, b.Length + 1];

            for (var i = 1; i <= a.Length; i++)
            {
                for (var j = 1; j <= b.Length; j++)
                {
                    var cost = b[j - 1] == a[i - 1] ? 0 : 1;
                    distance[i, j] = Math.Min(
                        Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                        distance[i - 1, j - 1] + cost);
                }
            }

            return distance[a.Length, b.Length];
        }
    }

    public class RequiredAttribute : Attribute
    {

    }
}
