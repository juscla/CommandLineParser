using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CommandLineParser
{
    public static class CommandLineParser
    {
        /// <summary>
        /// The delimiters.
        /// </summary>
        private static readonly char[] Delimiters = { '=' };

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
        /// </typeparam>
        /// <returns>
        /// The <see cref="T"/>.
        /// </returns>
        public static T Parse<T>(this IEnumerable<string> args, int maxDistance = 2) where T : class, new()
        {
            var result = new T();

            var props = typeof(T).GetProperties();

            foreach (
                var a in
                args.Select(x => x.Split(Delimiters, StringSplitOptions.RemoveEmptyEntries)))
            {
                if (a.Length != 2)
                {
                    continue;
                }

                var word = a[0].ToLower().ClosestWord(props.Select(x=> x.Name.ToLower()), maxDistance);

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
                    prop.SetValue(result, Convert.ChangeType(a[1], prop.PropertyType));
                }
                else if (prop.PropertyType.GetInterface(typeof(IEnumerable).Name) != null)
                {
                    // check if we are a generic type or a simple Array.
                    var elementType = prop.PropertyType.IsGenericType
                        ? prop.PropertyType.GetGenericArguments().First()
                        : prop.PropertyType.GetElementType();

                    // split the list to parse as a List of elements.
                    var split = a[1].Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    // update the result with the converted list. 
                    prop.SetValue(result, split.ConvertListTo(prop.PropertyType, elementType));
                }
                else
                {
                    // check if we are an enum or not and handle the conversion
                    // properly. 
                    var r = prop.PropertyType.IsEnum
                        ? a[1].ConvertToEnum(prop.PropertyType)
                        : Convert.ChangeType(a[1], prop.PropertyType);

                    if (r != null)
                    {
                        // update the result with the converted object. 
                        prop.SetValue(result, r);
                    }
                }
            }

            return result;
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
        public static object ConvertListTo(this IList value, Type type, Type elementType)
        {
            var temp = (IList)Activator.CreateInstance(type, value.Count);

            for (var index = 0; index < value.Count; index++)
            {
                var item = value[index];

                var result = elementType.IsEnum
                                 ? item.ConvertToEnum(elementType)
                                 : Convert.ChangeType(item, elementType);

                if (result == null)
                {
                    continue;
                }

                if (type.IsArray)
                {
                    temp[index] = result;
                }
                else
                {
                    temp.Add(result);
                }
            }

            return temp;
        }

        /// <summary>
        /// The handle enum.
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
        /// The handle enum.
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
                var temp = word.WordDistance(entry);
                if (temp > distance)
                {
                    continue;
                }

                response = entry;
                distance = temp;
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
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || a == b)
            {
                return 0;
            }

            var distances = new int[a.Length + 1, b.Length + 1];

            ////for (int i = 0; i <= a.Length; distances[i, 0] = i++) ;
            ////for (int j = 0; j <= b.Length; distances[0, j] = j++) ;

            for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = b[j - 1] == a[i - 1] ? 0 : 1;
                distances[i, j] = Math.Min
                (
                    Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                    distances[i - 1, j - 1] + cost
                );
            }

            return distances[a.Length, b.Length];
        }
    }
}
