using System;
using System.Linq;
using System.Reflection;

namespace EasyNetQ.AutoRespond
{
    public class AutoResponderRequestHandlerInfo
    {
        public Type ConcreteType;
        public Type InterfaceType;
        public Type RequestType;
        public Type ResponseType;
        public MethodInfo HandleMethod;

        public AutoResponderRequestHandlerInfo(Type concreteType, Type interfaceType, Type requestType, Type responseType)
        {
            Preconditions.CheckNotNull(concreteType, "concreteType");
            Preconditions.CheckNotNull(interfaceType, "interfaceType");
            Preconditions.CheckNotNull(requestType, "requestType");
            Preconditions.CheckNotNull(responseType, "responseType");

            ConcreteType = concreteType;
            InterfaceType = interfaceType;
            RequestType = requestType;
            ResponseType = responseType;
            HandleMethod = ConcreteType.GetInterfaceMap(InterfaceType).TargetMethods.Single();
        }
    }
}