using System.Collections.Generic;

namespace Enmap.Utils
{
    public static class DictionaryExtensions
    {
        public static U Get<T, U>(this IDictionary<T, U> dictionary, T key, U returnIfNotFound = default(U))
        {
            if (dictionary == null)
                return returnIfNotFound;
            if (key == null)
                return returnIfNotFound;

            U result;
            if (dictionary.TryGetValue(key, out result))
                return result;
            else
                return returnIfNotFound;
        }
    }
}