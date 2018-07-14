using System;

namespace EasyNetQ.AutoRespond
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class ResponderConfigurationAttribute : Attribute
    {
        public ushort PrefetchCount { get; set; }
        public string QueueName { get; set; }
    }
}