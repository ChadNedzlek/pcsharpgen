namespace Primordially.PluginCore.Data
{
    public class UserInputChooser
    {
        public UserInputChooser(int count, string prompt)
        {
            Count = count;
            Prompt = prompt;
        }

        public int Count { get; }
        public string Prompt { get; }
    }
}