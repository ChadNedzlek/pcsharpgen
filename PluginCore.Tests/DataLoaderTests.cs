using System.Reflection.Emit;
using FluentAssertions;
using NLua.Exceptions;
using NUnit.Framework;
using Primordially.PluginCore.Data;
using Splat;

namespace Primordially.PluginCore.Tests
{
    public class DataLoaderTests
    {
        [SetUp]
        public void SetUp()
        {
            Locator.CurrentMutable.RegisterConstant(new NUnitLogger(), typeof(ILogger));
        }

        [Test]
        public void InvalidLuaThrows()
        {
            DataModuleLoader loader = new DataModuleLoader(@"C:\Users\Chad Nedzlek\source\repos\primordially\Data", DataSetStrictness.Strict);
            loader.Invoking(l => l.LoadString(@"UndefinedMethod()")).Should().Throw<LuaScriptException>();
        }

        [Test]
        public void ParseMinimalFact()
        {
          DataModuleLoader loader = new DataModuleLoader(@"C:\Users\Chad Nedzlek\source\repos\primordially\Data", DataSetStrictness.Strict);
          var dataSet = loader.LoadString(@"DefineFact({Category=""TESTCAT"",Key=""TestKey"",DataFormat=""String""})");
          dataSet.Facts.Should().HaveCount(1);
          dataSet.Facts.Should().ContainKey("TESTCAT|TestKey");
          var fact = dataSet.Facts["TESTCAT|TestKey"];
          fact.Category.Should().Be("TESTCAT");
          fact.DataFormat.Should().Be("String");
          fact.DisplayName.Should().BeNullOrEmpty();
          fact.Explanation.Should().BeNullOrEmpty();
          fact.Required.Should().BeTrue();
          fact.Selectable.Should().BeTrue();
          fact.Visible.Should().BeTrue();
        }

        [Test]
        public void ParseDoubleFact()
        {
          DataModuleLoader loader = new DataModuleLoader(@"C:\Users\Chad Nedzlek\source\repos\primordially\Data", DataSetStrictness.Strict);
          string doubleFactString = @"DefineFact({Category=""TESTCAT"",Key=""TestKey"",DataFormat=""String""})
DefineFact({Category=""TESTCAT"",Key=""TestKey"",DataFormat=""String""})
";
          loader.Invoking(l => l.LoadString(doubleFactString)).Should().Throw<LuaException>();
        }

        [Test]
        public void ParseCompleteFact()
        {
            DataModuleLoader loader = new DataModuleLoader(@"C:\Users\Chad Nedzlek\source\repos\primordially\Data", DataSetStrictness.Strict);
            var dataSet = loader.LoadString(@"DefineFact({
Category=""TESTCAT"",
Key=""TestKey"",
DataFormat=""String"",
DisplayName=""Test display name"",
Explanation=""Test Explanation"",
Required=false,
Selectable=false,
Visible=false
})");
            dataSet.Facts.Should().HaveCount(1);
            dataSet.Facts.Should().ContainKey("TESTCAT|TestKey");
            var fact = dataSet.Facts["TESTCAT|TestKey"];
            fact.Category.Should().Be("TESTCAT");
            fact.DataFormat.Should().Be("String");
            fact.DisplayName.Should().Be("Test display name");
            fact.Explanation.Should().Be("Test Explanation");
            fact.Required.Should().BeFalse();
            fact.Selectable.Should().BeFalse();
            fact.Visible.Should().BeFalse();
        }

        [Test]
        public void ParseStat()
        {
            DataModuleLoader loader = new DataModuleLoader(@"C:\Users\Chad Nedzlek\source\repos\primordially\Data", DataSetStrictness.Strict);
            var dataSet = loader.LoadString(@"DefineStat({
Name=""TestStatA"",
SortKey=""T1"",
Abbreviation=""TSA"",
Key=""TSK"",
StatModeFormula=""floor(TestScore/2)-5"",
Modifications={
  {
    Target=""TestScore"",
    Action=""SET"",
    Value=""input(\""STATSCORE\"")"",
  },
  {
    Target=""TestMod"",
    Action=""SET"",
    Value=""d20Mod(TestScore)""
  }
},
Definitions = {
    {
      Name=""TestSetOne"",
      InitialValue=""1"",
    },
    {
      Name=""TestSetTwo=THING"",
      InitialValue=""TestScore-10"",
    }
},
Bonuses={
  {
    Category=""TESTCAT"",
    Variables={
      ""TEST.Name"",
    },
    Formula=""TestMod"",
    Conditions={
      function (character)
        return (character.Variables[""TestVariable""] == 0)
      end,
    },
  },
    {
      Category=""VAR"",
      Variables={
        ""OTHERVAR"",
        ""OTHERVAR2"",
      },
      Formula=""2"",
    },
  Abilities={
    {
      Category=""TestGrantCategory"",
      Nature=""TESTAUTO"",
      Names={
        ""TestAbilityName"",
      },
    },
  },
}})");
            dataSet.AbilityScores.Should().HaveCount(1);
        }
    }
}