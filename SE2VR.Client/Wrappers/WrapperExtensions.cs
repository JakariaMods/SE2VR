using System.Reflection;
using SE2VR.Simulation;

namespace SE2VR.Client.Wrappers;

/// <summary>
/// Class to make wrapper and reflection operations.
/// </summary>
internal static class WrapperExtensions
{
    public static void PrintFields(this Type type)
    {
        Logging.Debug($"Field names for {type?.Name ?? "unknown"}");
        if (type == null) 
            return;

        Logging.IncreaseIndent();
        FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        foreach (var field in fields)
            Logging.Debug($"{(field.IsPrivate ? "private" : "public")}{(field.IsStatic ? " static " : " ")}{field.FieldType.Name} {field.Name};");
        Logging.DecreaseIndent();
    }

    public static void PrintMethods(this Type type)
    {
        Logging.Debug($"Method names for {type?.Name ?? "unknown"}");
        if (type == null)
            return;

        Logging.IncreaseIndent();
        MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        foreach (var method in methods)
            Logging.Debug($"{(method.IsPrivate ? "private" : "public")}{(method.IsStatic ? " static " : " ")}{method.ReturnType.Name} {method.Name}");
        Logging.DecreaseIndent();
    }

}
