using System;
using System.Collections.Generic;
using System.Reflection;

namespace PowerManagerMod.Core;

/// <summary>
/// Wind Power's turbine multipliers, found by name so this mod loads and runs without Wind Power. The method is
/// WindPower.WindBoosts.Add(string source, Func&lt;float&gt; multiplier), in Wind Power 0.9.3 or newer; every
/// turbine's rating is multiplied by what the function returns, read once a second.
/// </summary>
public static class WindLink
{
    public const string Guid = "com.dev.survivallog.windpower";
    public const string TypeName = "WindPower.WindBoosts";

    /// <summary>The lowest and highest the Wind setting can be, as for solar.</summary>
    public const float Least = 0.1f;
    public const float Most = 10f;

    /// <summary>Hands the multiplier to Wind Power. False when no loaded assembly has Wind Power's Add.</summary>
    public static bool Add(IEnumerable<Assembly> assemblies, string source, Func<float> multiplier)
    {
        var add = Find(assemblies);

        if (add == null) return false;

        add.Invoke(null, new object[] { source, multiplier });

        return true;
    }

    /// <summary>The Wind setting as a turbine multiplier: kept in range, and the game's own (1) if it is not a number.</summary>
    public static float Multiplier(float setting) => float.IsNaN(setting) ? 1f : Math.Clamp(setting, Least, Most);

    private static MethodInfo? Find(IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            Type? type;

            try
            {
                type = assembly.GetType(TypeName);
            }
            catch (Exception)
            {
                continue;
            }

            var add = type?.GetMethod("Add", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(Func<float>) }, null);

            if (add != null) return add;
        }

        return null;
    }
}
