using System.Reflection;
using FieldLink.Communication;

namespace FieldLink.Communication.Tests;

// 공개·protected 계약을 정렬된 텍스트로 기록한다. 장비나 통신 객체를 생성하지 않는다.
internal static class ApiSurface
{
    internal static string[] Read()
    {
        var lines = new List<string>();
        foreach (AssemblyName reference in typeof(ICommunicationClient).Assembly.GetReferencedAssemblies())
            lines.Add("R " + reference.FullName);
        const BindingFlags flags = BindingFlags.DeclaredOnly | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        foreach (Type type in typeof(ICommunicationClient).Assembly.GetExportedTypes())
        {
            string owner = TypeName(type);
            string kind = type.IsInterface ? "interface" : type.IsEnum ? "enum" : type.IsValueType ? "struct" : "class";
            lines.Add($"T {owner} {kind} abstract={type.IsAbstract} sealed={type.IsSealed} base={TypeName(type.BaseType)} interfaces={string.Join(",", type.GetInterfaces().Select(TypeName).OrderBy(x => x, StringComparer.Ordinal))}");
            foreach (ConstructorInfo item in type.GetConstructors(flags).Where(Visible))
                lines.Add($"C {owner} {Access(item)} ({Parameters(item.GetParameters())})");
            foreach (MethodInfo item in type.GetMethods(flags).Where(item => Visible(item) && !item.IsSpecialName))
                lines.Add($"M {owner}.{item.Name}{(item.IsGenericMethod ? "<" + string.Join(",", item.GetGenericArguments().Select(TypeName)) + ">" : "")} {Access(item)} static={item.IsStatic} virtual={item.IsVirtual} abstract={item.IsAbstract} returns={TypeName(item.ReturnType)} ({Parameters(item.GetParameters())})");
            foreach (PropertyInfo item in type.GetProperties(flags))
            {
                MethodInfo? getter = item.GetGetMethod(true), setter = item.GetSetMethod(true);
                if ((getter != null && Visible(getter)) || (setter != null && Visible(setter)))
                    lines.Add($"P {owner}.{item.Name} {TypeName(item.PropertyType)} get={Accessor(getter)} set={Accessor(setter)} ({Parameters(item.GetIndexParameters())})");
            }
            foreach (FieldInfo item in type.GetFields(flags).Where(item => item.IsPublic || item.IsFamily || item.IsFamilyOrAssembly))
                lines.Add($"F {owner}.{item.Name} {TypeName(item.FieldType)} static={item.IsStatic} readonly={item.IsInitOnly} const={(item.IsLiteral ? Convert.ToString(item.GetRawConstantValue(), System.Globalization.CultureInfo.InvariantCulture) : "-")}");
            foreach (EventInfo item in type.GetEvents(flags))
            {
                MethodInfo? add = item.GetAddMethod(true);
                if (add != null && Visible(add))
                    lines.Add($"E {owner}.{item.Name} {TypeName(item.EventHandlerType)} {Access(add)}");
            }
        }
        return lines.OrderBy(line => line, StringComparer.Ordinal).ToArray();
    }

    private static string Accessor(MethodInfo? method) => method != null && Visible(method) ? Access(method) : "-";
    private static bool Visible(MethodBase method) => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;
    private static string Access(MethodBase method) => method.IsPublic ? "public" : method.IsFamily ? "protected" : "protected-internal";
    private static string Parameters(ParameterInfo[] parameters) => string.Join(",", parameters.Select(parameter =>
        (parameter.IsOut ? "out " : "") + TypeName(parameter.ParameterType) + " " + parameter.Name +
        (parameter.IsOptional ? "=" + Convert.ToString(parameter.DefaultValue, System.Globalization.CultureInfo.InvariantCulture) : "")));
    private static string TypeName(Type? type)
    {
        if (type == null)
            return "-";
        if (type.IsGenericParameter)
            return type.Name;
        if (type.IsByRef)
            return TypeName(type.GetElementType()) + "&";
        if (type.IsArray)
            return TypeName(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        if (!type.IsGenericType)
            return type.FullName!;
        string name = type.GetGenericTypeDefinition().FullName!;
        return name.Substring(0, name.IndexOf('`')) + "<" + string.Join(",", type.GetGenericArguments().Select(TypeName)) + ">";
    }
}
