using System;

namespace Enmap.Utils
{
    public static class MetadataExtensions
    {
        public static string GetEntityName(this Type entityType)
        {
            var result = entityType.FullName;
            var plusIndex = result.IndexOf('+');
            if (plusIndex != -1)
            {
                var previousDotIndex = result.LastIndexOf('.', plusIndex);
                if (previousDotIndex == -1)
                    previousDotIndex = 0;
                else 
                    previousDotIndex++;

                result = result.Substring(0, previousDotIndex) + result.Substring(plusIndex + 1);
            }
            return result;
        }
    }
}