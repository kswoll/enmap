using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Enmap.Utils
{
    public class AnonymousTypeCloner
    {
        public static Type CloneType(Type target)
        {
            string assemblyName = "AnonymousTypeClone";

            var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.RunAndSave);
            var module = assembly.DefineDynamicModule(assemblyName, "temp.module.dll");

            // Create a default constructor
            var type = module.DefineType(assemblyName, TypeAttributes.Public);
            type.DefineDefaultConstructor(MethodAttributes.Public);

            // Create a property for each property in target
            foreach (var property in target.GetProperties())
            {
                type.DefineProperty(property.Name, property.PropertyType);
            }

            var result = type.CreateType();
            return result;
        } 
    }
}