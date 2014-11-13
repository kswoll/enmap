using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Common.Mappers.Utils
{
    public static class TypeBuilderUtils
    {
        public static PropertyBuilder DefineProperty(this TypeBuilder type, string propertyName, Type propertyType)
        {
            return DefineProperty(type, propertyName, propertyType,
                (il, field) => il.LoadInstanceField(field),
                (il, field) => il.StoreValueInInstanceField(field));
        }

        public static PropertyBuilder DefineProperty(this TypeBuilder type, string propertyName, Type propertyType, Action<ILGenerator> getter)
        {
            MethodBuilder getMethod = type.DefineMethod("get_" + propertyName, MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Virtual, propertyType, Type.EmptyTypes);
            {
                ILGenerator il = getMethod.GetILGenerator();
                getter(il);
                il.Return();
            }

            return type.DefineProperty(propertyName, PropertyAttributes.None, propertyType, Type.EmptyTypes);            
        }

        public static PropertyBuilder DefineProperty(this TypeBuilder type, string propertyName, Type propertyType, Action<ILGenerator> getter, Action<ILGenerator> setter)
        {
            MethodBuilder getMethod = type.DefineMethod("get_" + propertyName, MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Virtual, propertyType, Type.EmptyTypes);
            {
                ILGenerator il = getMethod.GetILGenerator();
                getter(il);
                il.Return();
            }

            MethodBuilder setMethod = type.DefineMethod("set_" + propertyName, MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Virtual, typeof(void), new[] { propertyType });
            {
                ILGenerator il = setMethod.GetILGenerator();
                setter(il);
                il.Return();
            }

            PropertyBuilder property = type.DefineProperty(propertyName, PropertyAttributes.None, propertyType, Type.EmptyTypes);
            property.SetGetMethod(getMethod);
            property.SetSetMethod(setMethod);
            return property;
        }

        public static PropertyBuilder DefineProperty(this TypeBuilder type, string propertyName, Type propertyType, Action<ILGenerator, FieldBuilder> getter, Action<ILGenerator, FieldBuilder> setter)
        {
            FieldBuilder field = type.DefineField(propertyName.Decapitalize(), propertyType, FieldAttributes.Private);
            return DefineProperty(type, propertyName, propertyType, getter, setter, field);
        }

        public static PropertyBuilder DefineProperty(this TypeBuilder type, string propertyName, Type propertyType, Action<ILGenerator, FieldBuilder> getter, Action<ILGenerator, FieldBuilder> setter, FieldBuilder field)
        {
            MethodBuilder getMethod = type.DefineMethod("get_" + propertyName, MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Virtual, propertyType, Type.EmptyTypes);
            {
                ILGenerator il = getMethod.GetILGenerator();
                getter(il, field);
                il.Return();
            }

            MethodBuilder setMethod = type.DefineMethod("set_" + propertyName, MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Virtual, typeof(void), new[] { propertyType });
            {
                ILGenerator il = setMethod.GetILGenerator();
                setter(il, field);
                il.Return();
            }

            PropertyBuilder property = type.DefineProperty(propertyName, PropertyAttributes.None, propertyType, Type.EmptyTypes);
            property.SetGetMethod(getMethod);
            property.SetSetMethod(setMethod);
            return property;            
        }

        public static void LoadInstanceField(this ILGenerator il, FieldInfo field)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, field);            
        }

        public static void StoreInstanceField(this ILGenerator il, FieldInfo field)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Stfld, field);
        }

        public static void Return(this ILGenerator il)
        {
            il.Emit(OpCodes.Ret);
        }

        public static void StoreValueInInstanceField(this ILGenerator il, FieldInfo field)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, field);            
        }

        static readonly MethodInfo GET_TYPE_FROM_RUNTIME_HANDLE_METHOD = typeof(Type).GetMethod("GetTypeFromHandle");

        public static void LoadType(this ILGenerator il, Type type)
        {
            il.Emit(OpCodes.Ldtoken, type);
            il.Emit(OpCodes.Call, GET_TYPE_FROM_RUNTIME_HANDLE_METHOD);                
        }

        public static bool IsVirtual(this PropertyInfo property)
        {
            MethodInfo method = property.GetGetMethod() ?? property.GetSetMethod();
            return method.IsVirtual();
        }

        public static bool IsVirtual(this MethodInfo method)
        {
            return (method.Attributes & MethodAttributes.Virtual) == MethodAttributes.Virtual;
        }

        public static void InvokeBaseGetter(this ILGenerator il, Type baseType, PropertyInfo property) 
        {
            PropertyInfo baseProperty = baseType.GetProperty(property.Name);
            il.Emit(OpCodes.Ldarg_0);
            il.EmitCall(OpCodes.Call, baseProperty.GetGetMethod(), null);
        }

        public static void InvokeBaseSetter(this ILGenerator il, Type baseType, PropertyInfo property) 
        {
            PropertyInfo baseProperty = baseType.GetProperty(property.Name);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.EmitCall(OpCodes.Call, baseProperty.GetSetMethod(), null);
        }

        private static MethodInfo Type_GetProperty = typeof(Type).GetMethod("GetProperty", new[] { typeof(string) });

        public static void StorePropertyInfo(this ILGenerator il, FieldBuilder staticField, PropertyInfo property)
        {
            il.LoadType(property.DeclaringType);
            il.Emit(OpCodes.Ldstr, property.Name);
            il.EmitCall(OpCodes.Call, Type_GetProperty, null);
            il.Emit(OpCodes.Stsfld, staticField);
        }

        private static MethodInfo MemberInfo_GetAttributes = typeof(MemberInfo).GetMethod("GetCustomAttributes", new[] { typeof(bool) });

        public static void StoreAttributes(this ILGenerator il, FieldBuilder staticField, FieldBuilder member)
        {
            il.Emit(OpCodes.Ldsfld, member);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Callvirt, MemberInfo_GetAttributes);
            il.Emit(OpCodes.Stsfld, staticField);
        }

        private static MethodInfo Type_GetMethod = typeof(Type).GetMethod("GetMethod",
            new[] { typeof(string), typeof(BindingFlags), typeof(Binder), typeof(Type[]), typeof(ParameterModifier[]) });

        public static void StoreMethodInfo(this ILGenerator il, FieldBuilder staticField, MethodInfo method)
        {
            Type[] parameterTypes = method.GetParameters().Select(info => info.ParameterType).ToArray();

            // The type we want to invoke GetMethod upon
            il.LoadType(method.DeclaringType);

            // Arg1: methodName
            il.Emit(OpCodes.Ldstr, method.Name);

            // Arg2: bindingFlags
            il.Emit(OpCodes.Ldc_I4, (int)(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

            // Arg3: binder
            il.Emit(OpCodes.Ldnull);

            // Arg4: parameterTypes
            il.Emit(OpCodes.Ldc_I4, parameterTypes.Length);
            il.Emit(OpCodes.Newarr, typeof(Type));
            // Copy array for each element we are going to set
            for (int i = 0; i < parameterTypes.Length; i++)
            {
                il.Emit(OpCodes.Dup);
            }
            // Set each element 
            for (int i = 0; i < parameterTypes.Length; i++)
            {
                il.Emit(OpCodes.Ldc_I4, i);
                il.LoadType(parameterTypes[i]);
                il.Emit(OpCodes.Stelem, typeof(Type));
            }

            // Arg5: parameterModifiers
            il.Emit(OpCodes.Ldnull);

            // Invoke method
            il.EmitCall(OpCodes.Call, Type_GetMethod, null);

            // Store MethodInfo into the static field
            il.Emit(OpCodes.Stsfld, staticField);
        }

        /// <summary>
        /// This is just so you don't forget that the paramter must be a short.
        /// </summary>
        public static void LoadArg(this ILGenerator il, int argumentIndex)
        {
            il.Emit(OpCodes.Ldarg, (short)argumentIndex);
        }

        public static void Pop(this ILGenerator il)
        {
            il.Emit(OpCodes.Pop);
        }

        public static void LoadNull(this ILGenerator il)
        {
            il.Emit(OpCodes.Ldnull);
        }

        public static void LoadThis(this ILGenerator il)
        {
            il.Emit(OpCodes.Ldarg_0);
        }         

        public static string Decapitalize(this string s)
        {
            return Decapitalize(s, false);
        }

        public static string Decapitalize(this string s, bool correctCase)
        {
            StringBuilder builder = new StringBuilder();
            if (s.Length > 0)
                builder.Append(char.ToLower(s[0]));
            for (int i = 1; i < s.Length; i++)
            {
                builder.Append(correctCase ? char.ToLower(s[i]) : s[i]);
            }
            return builder.ToString();
        }
    }
}