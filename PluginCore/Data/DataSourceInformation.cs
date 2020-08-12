using System;

namespace Primordially.PluginCore.Data
{
    public class DataSourceInformation
    {
        public DataSourceInformation(string l, string s, string web, DateTimeOffset date)
        {
            Long = l;
            Short = s;
            Web = web;
            Date = date;
        }

        public string Long { get; }
        public string Short { get; }
        public string Web { get; }
        public DateTimeOffset Date { get; }
    }
}