using System;

namespace FastProxy.Helpers
{
    public class MissingConstructionInformation : ArgumentNullException
    {
        public enum TypeDefintion
        {
            AbstractType,
            ConcreteType,
            InterceptorType,
            ModuleBuilder
        }

        public TypeDefintion MissingDefinition { get; }

        public MissingConstructionInformation(string paramName, TypeDefintion definition) : base(paramName)
        {
            MissingDefinition = definition;
        }
    }
}
