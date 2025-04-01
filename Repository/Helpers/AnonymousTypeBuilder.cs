using System.Reflection;
using System.Reflection.Emit;

namespace Repository.Helpers
{
    public static class AnonymousTypeBuilder
    {
        private static readonly AssemblyBuilder _assemblyBuilder =
            AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("DynamicAssembly"), AssemblyBuilderAccess.Run);
        private static readonly ModuleBuilder _moduleBuilder = _assemblyBuilder.DefineDynamicModule("DynamicModule");

        public static Type CreateAnonymousType(List<Tuple<string, Type>> properties)
        {
            TypeBuilder typeBuilder = CreateTypeBuilder();
            var fieldBuilders = new List<FieldBuilder>();

            foreach(var property in properties)
                fieldBuilders.Add(CreateProperty(typeBuilder, property));

            DefineConstructor(typeBuilder, properties, fieldBuilders);
            typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);
            return typeBuilder.CreateType();
        }

        private static TypeBuilder CreateTypeBuilder()
        {
            return _moduleBuilder.DefineType($"AnonymousType{Guid.NewGuid()}", TypeAttributes.Public | TypeAttributes.Class);
        }

        private static FieldBuilder CreateProperty(TypeBuilder typeBuilder, Tuple<string, Type> property)
        {
            var fieldBuilder = typeBuilder.DefineField($"_{property.Item1}", property.Item2, FieldAttributes.Public);
            var propertyBuilder = typeBuilder.DefineProperty(property.Item1, PropertyAttributes.HasDefault, property.Item2, null);

            var getMethodBuilder = typeBuilder.DefineMethod($"get_{property.Item1}", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                property.Item2, Type.EmptyTypes);
            var getIL = getMethodBuilder.GetILGenerator();
            getIL.Emit(OpCodes.Ldarg_0);
            getIL.Emit(OpCodes.Ldfld, fieldBuilder);
            getIL.Emit(OpCodes.Ret);

            var setMethodBuilder = typeBuilder.DefineMethod($"set_{property.Item1}", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                null, [property.Item2]);
            var setIL = getMethodBuilder.GetILGenerator();
            setIL.Emit(OpCodes.Ldarg_0);
            setIL.Emit(OpCodes.Ldarg_1);
            setIL.Emit(OpCodes.Stfld, fieldBuilder);
            setIL.Emit(OpCodes.Ret);

            propertyBuilder.SetGetMethod(getMethodBuilder);
            propertyBuilder.SetSetMethod(setMethodBuilder);

            return fieldBuilder;
        }

        private static void DefineConstructor(TypeBuilder typeBuilder, List<Tuple<string, Type>> properties, List<FieldBuilder> fieldBuilders)
        {
            var constrcutorArgs = properties
                .Select(x => x.Item2)
                .ToArray();
            var constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, constrcutorArgs);

            var ctorIL = constructorBuilder.GetILGenerator();
            for(int i = 0; i < properties.Count; i++)
            {
                ctorIL.Emit(OpCodes.Ldarg_0);
                ctorIL.Emit(OpCodes.Ldarg, i + 1);
                ctorIL.Emit(OpCodes.Stfld, fieldBuilders[i]);
            }
            ctorIL.Emit(OpCodes.Ret);
        }
    }
}
