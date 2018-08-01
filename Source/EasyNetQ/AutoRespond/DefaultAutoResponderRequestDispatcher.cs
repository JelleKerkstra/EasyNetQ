using System;
using System.Threading.Tasks;
using EasyNetQ.DI;

namespace EasyNetQ.AutoRespond
{
    public class DefaultAutoResponderRequestDispatcher : IAutoResponderRequestDispatcher
    {
        private readonly IServiceResolver resolver;

        public DefaultAutoResponderRequestDispatcher(IServiceResolver resolver)
        {
            this.resolver = resolver;
        }

        public DefaultAutoResponderRequestDispatcher()
            : this(new ActivatorBasedResolver())
        {
        }

        public async Task<TResponse> DispatchAsync<TRequest, TResponse, TAsyncResponder>(TRequest request)
            where TRequest : class
            where TResponse : class
            where TAsyncResponder : class, IHandleRequestAsync<TRequest, TResponse>
        {
            using (var scope = resolver.CreateScope())
            {
                var asyncResponder = scope.Resolve<TAsyncResponder>();
                return await asyncResponder.HandleAsync(request).ConfigureAwait(false);
            }
        }

        private class ActivatorBasedResolver : IServiceResolver
        {
            public TService Resolve<TService>() where TService : class
            {
                return Activator.CreateInstance<TService>();
            }

            public IServiceResolverScope CreateScope()
            {
                return new ServiceResolverScope(this);
            }
        }
    }
}