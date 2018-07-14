using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EasyNetQ.Producer;

namespace EasyNetQ.AutoRespond
{
    /// <summary>
    /// Lets you scan assemblies for implementations of <see cref="IHandleRequest{T,T}"/> so that
    /// these will get registered as subscribers in the bus.
    /// </summary>
    public class AutoResponder
    {
        protected const string HandleMethodName = nameof(IHandleRequest<object, object>.Handle);
        protected const string HandleAsyncMethodName = nameof(IHandleRequestAsync<object, object>.HandleAsync);
        protected const string DispatchMethodName = nameof(IAutoResponderRequestDispatcher.Dispatch);
        protected const string DispatchAsyncMethodName = nameof(IAutoResponderRequestDispatcher.DispatchAsync);
        protected const string RespondMethodName = nameof(IBus.Respond);
        protected const string RespondAsyncMethodName = nameof(IBus.RespondAsync);
        protected readonly IBus bus;

        /// <summary>
        /// Responsible for handling a request with the relevant request handler.
        /// </summary>
        public IAutoResponderRequestDispatcher AutoResponderRequestDispatcher { get; set; }

        /// <summary>
        /// Responsible for setting responder configuration for all 
        /// auto responding handlers <see cref="IHandleRequest{T,T}"/>.
        /// the values may be overriden for particular request handler 
        /// methods by using an <see cref="ResponderConfigurationAttribute"/>.
        /// </summary>
        public Action<IResponderConfiguration> ConfigureResponderConfiguration { protected get; set; }

        public AutoResponder(IBus bus)
        {
            Preconditions.CheckNotNull(bus, "bus");

            this.bus = bus;
            AutoResponderRequestDispatcher = new DefaultAutoResponderRequestDispatcher();
            ConfigureResponderConfiguration = responderConfiguration => { };
        }

        /// <summary>
        /// Registers all request handlers in passed assembly. The actual responder instances is
        /// created using <seealso cref="AutoResponderRequestDispatcher"/>.
        /// </summary>
        /// <param name="assemblies">The assemblies to scan for request handlers.</param>
        public virtual void Respond(params Assembly[] assemblies)
        {
            Preconditions.CheckAny(assemblies, "assemblies", "No assemblies specified.");

            Respond(assemblies.SelectMany(a => a.GetTypes()).ToArray());
        }

        /// <summary>
        /// Registers all types as request handlers. The actual responder instances is
        /// created using <seealso cref="AutoResponderRequestDispatcher"/>.
        /// </summary>
        /// <param name="requestHandlerTypes">the types to register as request handlers.</param>
        public virtual void Respond(params Type[] requestHandlerTypes)
        {
            if (requestHandlerTypes == null) throw new ArgumentNullException(nameof(requestHandlerTypes));

            var genericBusRespondMethod = GetRespondMethodOfBus(RespondMethodName, typeof(Func<,>));
            var responderInfos = GetResponderInfos(requestHandlerTypes, typeof(IHandleRequest<,>));
            Type ResponderDelegate(Type requestType, Type responseType) => typeof(Func<,>).MakeGenericType(requestType, responseType);

            InvokeMethods(responderInfos, DispatchMethodName, genericBusRespondMethod, ResponderDelegate);
        }

        /// <summary>
        /// Registers all request handlers in passed assembly. The actual responder instances is
        /// created using <seealso cref="AutoResponderRequestDispatcher"/>.
        /// </summary>
        /// <param name="assemblies">The assemblies to scan for request handlers.</param>
        public virtual void RespondAsync(params Assembly[] assemblies)
        {
            Preconditions.CheckAny(assemblies, "assemblies", "No assemblies specified.");

            RespondAsync(assemblies.SelectMany(a => a.GetTypes()).ToArray());
        }

        /// <summary>
        /// Registers all types as request handlers. The actual responder instances is
        /// created using <seealso cref="AutoResponderRequestDispatcher"/>.
        /// </summary>
        /// <param name="requestHandlerTypes">the types to register as request handlers.</param>
        public virtual void RespondAsync(params Type[] requestHandlerTypes)
        {
            if (requestHandlerTypes == null) throw new ArgumentNullException(nameof(requestHandlerTypes));

            var genericBusRespondMethod = GetRespondMethodOfBus(RespondAsyncMethodName, typeof(Func<,>));
            var responderInfos = GetResponderInfos(requestHandlerTypes, typeof(IHandleRequestAsync<,>));
            Type ResponderDelegate(Type requestType, Type responseType) => typeof(Func<,>).MakeGenericType(requestType, typeof(Task<>).MakeGenericType(responseType));

            InvokeMethods(responderInfos, DispatchAsyncMethodName, genericBusRespondMethod, ResponderDelegate);
        }

        protected virtual void InvokeMethods(IEnumerable<KeyValuePair<Type, AutoResponderRequestHandlerInfo[]>> responderInfos, string dispatchName, MethodInfo genericBusRepondMethod, Func<Type, Type, Type> responderDelegate)
        {
            foreach (var kv in responderInfos)
            {
                foreach (var responderInfo in kv.Value)
                {
                    var dispatchMethod =
                        AutoResponderRequestDispatcher.GetType()
                            .GetMethod(dispatchName, BindingFlags.Instance | BindingFlags.Public)
                            .MakeGenericMethod(responderInfo.RequestType, responderInfo.ResponseType, responderInfo.ConcreteType);

#if !NETFX
                    var dispatchDelegate = dispatchMethod.CreateDelegate(
                        responderDelegate(responderInfo.RequestType, responderInfo.ResponseType),
                        AutoResponderRequestDispatcher);
#else
                    var dispatchDelegate = Delegate.CreateDelegate(responderDelegate(responderInfo.RequestType, responderInfo.ResponseType), AutoResponderRequestDispatcher, dispatchMethod);
#endif

                    Action<IResponderConfiguration> configAction = GenerateConfigurationAction(responderInfo);
                    var busRespondMethod = genericBusRepondMethod.MakeGenericMethod(responderInfo.RequestType, responderInfo.ResponseType);
                    busRespondMethod.Invoke(bus, new object[] { dispatchDelegate });
                }
            }
        }

        private Action<IResponderConfiguration> GenerateConfigurationAction(AutoResponderRequestHandlerInfo responderInfo)
        {
            return sc =>
            {
                ConfigureResponderConfiguration(sc);
                AutoSubscriberConsumerInfo(responderInfo)(sc);
            };

        }

        private static Action<IResponderConfiguration> AutoSubscriberConsumerInfo(AutoResponderRequestHandlerInfo responderInfo)
        {
            var configSettings = GetResponderConfigurationAttributeValue(responderInfo);
            if (configSettings == null)
            {
                return responderConfiguration => { };
            }
            return configuration =>
            {
                //prefetch count is set to a configurable default in RabbitAdvancedBus
                //so don't touch it unless SubscriptionConfigurationAttribute value is other than 0.
                if (configSettings.PrefetchCount > 0)
                    configuration.WithPrefetchCount(configSettings.PrefetchCount);

                if (!string.IsNullOrEmpty(configSettings.QueueName))
                    configuration.WithQueueName(configSettings.QueueName);
            };
        }

        private static ResponderConfigurationAttribute GetResponderConfigurationAttributeValue(AutoResponderRequestHandlerInfo handlerInfo)
        {
            var customAttributes = handlerInfo.HandleMethod.GetCustomAttributes(typeof(ResponderConfigurationAttribute), true);
            return customAttributes
                .OfType<ResponderConfigurationAttribute>()
                .FirstOrDefault();
        }

        protected virtual MethodInfo GetRespondMethodOfBus(string methodName, Type parmType)
        {
            return typeof(IBus).GetMethods()
                .Where(m => m.Name == methodName)
                .Select(m => new { Method = m, Params = m.GetParameters() })
                .Single(m => m.Params.Length == 2
                    && m.Params[0].ParameterType.GetGenericTypeDefinition() == parmType
                    && m.Params[1].ParameterType == typeof(Action<IResponderConfiguration>)
                ).Method;
        }

        protected virtual IEnumerable<KeyValuePair<Type, AutoResponderRequestHandlerInfo[]>> GetResponderInfos(IEnumerable<Type> types, Type interfaceType)
        {
            foreach (var concreteType in types.Where(t => t.GetTypeInfo().IsClass && !t.GetTypeInfo().IsAbstract))
            {
                var responderInfos = concreteType.GetInterfaces()
                    .Where(i => i.GetTypeInfo().IsGenericType && i.GetGenericTypeDefinition() == interfaceType && !i.GetGenericArguments()[0].IsGenericParameter && !i.GetGenericArguments()[1].IsGenericParameter)
                    .Select(i => new AutoResponderRequestHandlerInfo(concreteType, i, i.GetGenericArguments()[0], i.GetGenericArguments()[1]))
                    .ToArray();

                if (responderInfos.Any())
                    yield return new KeyValuePair<Type, AutoResponderRequestHandlerInfo[]>(concreteType, responderInfos);
            }
        }
    }
}