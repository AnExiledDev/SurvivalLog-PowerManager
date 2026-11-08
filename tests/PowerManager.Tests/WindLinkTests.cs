using System;
using System.Collections.Generic;
using System.Reflection;
using PowerManagerMod.Core;
using Xunit;

// Stands in for Wind Power's own class, with the same namespace, name and Add signature (Wind Power 0.9.3).
namespace WindPower
{
    public static class WindBoosts
    {
        public static readonly Dictionary<string, Func<float>> Added = new();

        public static void Add(string source, Func<float> multiplier) => Added[source] = multiplier;
    }
}

namespace PowerManager.Tests
{
    public class WindLinkTests
    {
        [Fact]
        public void WithWindPowerLoadedTheSettingReachesItsTurbines()
        {
            var setting = 1.5f;

            Assert.True(WindLink.Add(new[] { typeof(WindLinkTests).Assembly }, "Power Manager", () => setting));

            // Wind Power calls the function each time it reads the multipliers, so a live change reaches it.
            Assert.Equal(1.5f, WindPower.WindBoosts.Added["Power Manager"]());
            setting = 3f;
            Assert.Equal(3f, WindPower.WindBoosts.Added["Power Manager"]());
        }

        [Fact]
        public void WithoutWindPowerNothingIsAddedAndNothingBreaks()
        {
            Assert.False(WindLink.Add(new[] { typeof(FactAttribute).Assembly, typeof(object).Assembly }, "Power Manager", () => 2f));
            Assert.False(WindLink.Add(Array.Empty<Assembly>(), "Power Manager", () => 2f));
        }

        [Theory]
        [InlineData(1f, 1f)]
        [InlineData(2.5f, 2.5f)]
        [InlineData(0f, 0.1f)]
        [InlineData(50f, 10f)]
        [InlineData(float.NaN, 1f)]
        public void TheSettingIsKeptInRange(float setting, float multiplier)
        {
            Assert.Equal(multiplier, WindLink.Multiplier(setting));
        }
    }
}
