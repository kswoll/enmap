using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace Enmap.Utils
{
    public static class GenericExtensions
    {
        private static Dictionary<Type, Type[]> genericArguments = new Dictionary<Type, Type[]>();
        private static Dictionary<Type, Type> genericTypeDefinitions = new Dictionary<Type, Type>();

        public static IEnumerable<Type> GetComposition(this Type type)
        {
            return type.GetAncestry(true);
        }

        private static IEnumerable<Type> GetAncestry(this Type type, bool includeInterfaces)
        {
            Type current = type;
            while (current != null)
            {
                yield return current;
                current = current.BaseType;
            }
            if (includeInterfaces)
            {
                Type[] interfaces = type.GetInterfaces();
                foreach (Type @interface in interfaces)
                {
                    yield return @interface;
                }
            }
        }

        public static bool IsNullableValueType(this Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        public static Type GetNullableValueType(this Type type)
        {
            if (type.IsNullableValueType())
            {
                return type.GetGenericArguments()[0];
            }
            return null;
        }

        public static Type GetGenericArgument(this Type type, Type typeToGetParameterFrom, int argumentIndex)
        {
            foreach (Type current in type.GetComposition())
            {
                type = current;
                if (!current.IsGenericType)
                    continue;

                Type genericTypeDefinition;
                if (!genericTypeDefinitions.TryGetValue(current, out genericTypeDefinition))
                {
                    genericTypeDefinition = current.GetGenericTypeDefinition();
                    genericTypeDefinitions[current] = genericTypeDefinition;
                }
                if (genericTypeDefinition == typeToGetParameterFrom)
                    break;                
            }

            Type[] genericArgs;
            if (!genericArguments.TryGetValue(type, out genericArgs))
            {
                genericArgs = type.GetGenericArguments();
                genericArguments[type] = genericArgs;
            }

            Type result = genericArgs[argumentIndex];
            return result;
        }

        public static object InvokeGenericMethod(Type type, string methodName, Type[] genericParameters, Type[] parameterTypes, object o, object[] parameters)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static); //, null, parameterTypes, null);

            //for (int i = 0; i < parameterTypes.Length; i++) {
            //    Type parameterType = parameterTypes[i];
            //    if (genericParameters[0].IsGenericType && genericParameters[0].GetGenericTypeDefinition().IsAssignableFrom(parameterType)) {
            //        parameterTypes[i] = parameterType.MakeGenericType(genericParameters[0].GetGenericArguments()[0]);
            //    }
            //}

            MethodInfo genericMethod = method.MakeGenericMethod(genericParameters);
            return genericMethod.Invoke(o, parameters);
        }

        public static bool IsInstance(object o, Type type)
        {
            if (o == null)
                return false;
            return IsType(o.GetType(), type);
        }

        /// <summary>
        /// This method assumes that the targetType extends a generic class that itself descends from a non-generic
        /// baseType.  
        /// </summary>
        /// <param name="baseType"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static Type GetGenericType(Type baseType, Type type)
        {
            if (type == null)
                throw new ArgumentNullException("type");

            Type genericConceptType = type;
            Type target = baseType;

            while (genericConceptType.BaseType != target)
            {
                genericConceptType = genericConceptType.BaseType;

                if (genericConceptType == null)
                    throw new ApplicationException("Could not get generic concept type for " + type.Name);
            }
            return genericConceptType;
        }

        public static bool IsGenericList(this Type listType)
        {
            return IsType(listType, typeof(List<>)) || IsType(listType, typeof(Collection<>)) || IsType(listType, typeof(IList<>));
        }

        public static bool IsGenericEnumerable(this Type listType)
        {
            return IsType(listType, typeof(IEnumerable<>));
        }

        public static bool IsGenericDictionary(this Type dictType)
        {
            return IsType(dictType, typeof(Dictionary<,>)) || IsType(dictType, typeof(IDictionary<,>));
        }

        public static bool IsType(this Type type, Type ancestor)
        {
            if (ancestor.IsInterface)
            {
                if (type.IsGenericType)
                {
                    var current = type.GetGenericTypeDefinition();
                    if (current == ancestor)
                        return true;
                }

                var interfaces = type.GetInterfaces();
                foreach (var intf in interfaces)
                {
                    var current = intf;
                    if (current.IsGenericType)
                        current = intf.GetGenericTypeDefinition();
                    if (current == ancestor)
                        return true;
                }
            }
            else
            {
                while (type != null)
                {
                    if (type.IsGenericType)
                        type = type.GetGenericTypeDefinition();
                    if (type == ancestor)
                        return true;
                    type = type.BaseType;
                }                
            }
            return false;
        }

        public static Type GetListElementType(this Type listType)
        {
            return listType.GetGenericArgument(typeof(IList<>), 0);
        }

        public static Type GetDictionaryKeyType(this Type dictionaryType)
        {
            return dictionaryType.GetGenericArgument(typeof(IDictionary<,>), 0);
        }

        public static Type GetDictionaryValueType(this Type dictionaryType)
        {
            return dictionaryType.GetGenericArgument(typeof(IDictionary<,>), 1);
        }
    }
}