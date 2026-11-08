using System;
using System.Collections.Generic;
using System.Reflection;
using PowerManagerMod.Core;
using Xunit;

// Stands in for Wind Power's own class, with the same namespace, name and Add and AddCalm signatures (Wind Power 0.9.4).
namespace WindPower
{
    public static class WindBoosts
    {
        public static readonly Dictionary<string, Func<float>> Added = new();

        public static readonly Dictionary<string, Func<float>> Calm = new();

        public static void Add(string source, Func<float> multiplier) => Added[source] = multiplier;

        public static void AddCalm(string source, Func<float> share) => Calm[source] = share;
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

        // CalmOutput goes to Wind Power's calm list, not its multipliers, or it would scale the turbines in every weather.
        [Fact]
        public void TheCalmSettingReachesWindPowersCalmAirNotItsMultipliers()
        {
            var setting = 0.5f;

            Assert.True(WindLink.AddCalm(new[] { typeof(WindLinkTests).Assembly }, "Power Manager calm", () => setting));

            Assert.Equal(0.5f, WindPower.WindBoosts.Calm["Power Manager calm"]());
            Assert.False(WindPower.WindBoosts.Added.ContainsKey("Power Manager calm"));
            setting = 1f;
            Assert.Equal(1f, WindPower.WindBoosts.Calm["Power Manager calm"]());
        }

        [Fact]
        public void WithoutWindPowerNoCalmShareIsAdded()
        {
            Assert.False(WindLink.AddCalm(new[] { typeof(FactAttribute).Assembly, typeof(object).Assembly }, "Power Manager", () => 1f));
        }

        [Theory]
        [InlineData(0f, 0f)]
        [InlineData(0.5f, 0.5f)]
        [InlineData(-1f, 0f)]
        [InlineData(3f, 1f)]
        [InlineData(float.NaN, 0f)]
        public void TheCalmSettingIsKeptBetweenStillAndAClearDay(float setting, float share)
        {
            Assert.Equal(share, WindLink.CalmShare(setting));
        }
    }
}
