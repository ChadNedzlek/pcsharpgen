using System;
using NUnit.Framework;
using Splat;

namespace Primordially.PluginCore.Tests
{
    public class NUnitLogger : ILogger
    {
        public void Write(string message, LogLevel logLevel)
        {
            TestContext.WriteLine(" : " +logLevel + " : " + message);
        }

        public void Write(Exception exception, string message, LogLevel logLevel)
        {
            TestContext.WriteLine(" : " +logLevel + " : " + message + " : " + exception);
        }

        public void Write(string message, Type type, LogLevel logLevel)
        {
            TestContext.WriteLine(type.Name + " : " + logLevel + " : " + message);
        }

        public void Write(Exception exception, string message, Type type, LogLevel logLevel)
        {
            TestContext.WriteLine(type.Name + " : " + logLevel + " : " + message + " : " + exception);
        }

        public LogLevel Level => LogLevel.Debug;
    }
}