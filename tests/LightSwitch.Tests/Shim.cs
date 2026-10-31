using System.Reflection;

namespace Xunit;

/// <summary>A tiny attribute so the tests stay readable without a package dependency.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class FactAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class TheoryAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class InlineDataAttribute : Attribute
{
    public InlineDataAttribute(params object?[] data) => Data = data;

    public object?[] Data { get; }
}

public sealed class AssertFailedException : Exception
{
    public AssertFailedException(string message)
        : base(message)
    {
    }
}

public static class Assert
{
    public static void True(bool condition, string? message = null)
    {
        if (!condition)
        {
            throw new AssertFailedException(message ?? "Expected the condition to be true.");
        }
    }

    public static void True(bool? condition, string? message = null) =>
        True(condition == true, message ?? "Expected the condition to be true.");

    public static void False(bool condition, string? message = null) =>
        True(!condition, message ?? "Expected the condition to be false.");

    public static void False(bool? condition, string? message = null) =>
        True(condition == false, message ?? "Expected the condition to be false.");

    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new AssertFailedException($"Expected <{expected}> but got <{actual}>.");
        }
    }

    public static void Null(object? value)
    {
        if (value is not null)
        {
            throw new AssertFailedException($"Expected null but got <{value}>.");
        }
    }

    public static void NotNull(object? value)
    {
        if (value is null)
        {
            throw new AssertFailedException("Expected a non-null value.");
        }
    }

    public static void InRange<T>(T actual, T low, T high)
        where T : IComparable<T>
    {
        if (actual.CompareTo(low) < 0 || actual.CompareTo(high) > 0)
        {
            throw new AssertFailedException($"Expected <{actual}> to be between <{low}> and <{high}>.");
        }
    }
}

public static class TestRunner
{
    public static int Run(Type assemblyMarker)
    {
        var methods = assemblyMarker.Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance |
                BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttribute<FactAttribute>() is not null ||
                             method.GetCustomAttribute<TheoryAttribute>() is not null)
            .OrderBy(method => method.DeclaringType?.Name, StringComparer.Ordinal)
            .ThenBy(method => method.Name, StringComparer.Ordinal)
            .ToList();

        var failed = 0;
        var total = 0;
        foreach (var method in methods)
        {
            var inlineData = method.GetCustomAttributes<InlineDataAttribute>().ToList();
            var rows = inlineData.Count == 0
                ? new List<object?[]?> { null }
                : inlineData.Select(item => (object?[]?)item.Data).ToList();

            foreach (var row in rows)
            {
                total++;
                var name = $"{method.DeclaringType?.Name}.{method.Name}" +
                    (row is null ? string.Empty : $"({string.Join(", ", row)})");
                try
                {
                    var instance = method.IsStatic
                        ? null
                        : Activator.CreateInstance(method.DeclaringType!);
                    method.Invoke(instance, row is null ? null : ConvertArguments(method, row));
                    Console.WriteLine($"PASS {name}");
                }
                catch (TargetInvocationException ex)
                {
                    failed++;
                    Console.WriteLine($"FAIL {name}: {ex.InnerException?.Message}");
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine($"FAIL {name}: {ex.Message}");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"{total - failed}/{total} tests passed.");
        return failed == 0 ? 0 : 1;
    }

    private static object?[] ConvertArguments(MethodInfo method, object?[] values)
    {
        var parameters = method.GetParameters();
        var converted = new object?[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            var target = parameters[index].ParameterType;
            var value = values[index];
            converted[index] = value is null || target.IsInstanceOfType(value)
                ? value
                : Convert.ChangeType(value, target, System.Globalization.CultureInfo.InvariantCulture);
        }

        return converted;
    }
}
