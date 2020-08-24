using Primordially.PluginCore.Data.LuaInterfaces;

namespace Primordially.PluginCore.Data.Choosers
{
    public class UserInputChooser : Chooser<UserInputInterface>
    {
        public UserInputChooser(int count, string prompt) : base(DoNotCall)
        {
            Count = count;
            Prompt = prompt;
        }

        private static bool DoNotCall(CharacterInterface character, UserInputInterface arg)
        {
            return false;
        }

        public int Count { get; }
        public string Prompt { get; }
    }

    public class UserInputInterface
    {
    }
}