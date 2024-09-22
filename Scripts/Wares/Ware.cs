using System;

namespace SystemWare.Wares
{
    public class Ware
    {
        public required string name;
        private string displayName = "";
        public string DisplayName {
            get => displayName != null && displayName.Length > 0 ? displayName : name;
            set => displayName = value;
        }
        public string description = "";

        public TimeSpan birthday;
        // public required (string, string) brackets;
        public readonly List<(string, string)> AllBrackets = [];

        public void AddBrackets(string bracketsLeft, string bracketsRight)
        {
            AddBrackets((bracketsLeft, bracketsRight));
        }
        public void AddBrackets((string, string) brackets)
        {
            AllBrackets.Add(brackets);
        }
    }
}