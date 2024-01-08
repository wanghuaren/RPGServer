using System;
using Microsoft.Extensions.Logging;

namespace Ddxy.GameServer.Core
{
    public static class XLogFactory
    {
        private static ILoggerFactory _factory;

        public static void Init(ILoggerFactory factory)
        {
            _factory = factory;
        }

        public static ILogger Create(string categoryName) => _factory.CreateLogger(categoryName);

        public static ILogger Create(Type type) => _factory.CreateLogger(type);

        public static ILogger<T> Create<T>() => _factory.CreateLogger<T>();
    }
}