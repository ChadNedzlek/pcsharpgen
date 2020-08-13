using System;
using System.Collections.Immutable;

namespace Primordially.PluginCore.Data
{
    public class DataSetBonus
    {
        public DataSetBonus(
            string category,
            ImmutableList<string> variables,
            DataSetFormula formula,
            DataSetCondition<CharacterInterface> condition)
        {
            Category = category;
            Variables = variables;
            Formula = formula;
            Condition = condition;
        }

        public string Category { get; }
        public ImmutableList<string> Variables { get; }
        public DataSetFormula Formula { get; }
        public DataSetCondition<CharacterInterface> Condition { get; }
    }
}