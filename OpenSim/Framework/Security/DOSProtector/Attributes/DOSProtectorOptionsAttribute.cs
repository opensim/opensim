using System;

namespace OpenSim.Framework.Security.DOSProtector.Attributes
{

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class DOSProtectorOptionsAttribute(Type optionsType) : Attribute
    {
        public Type OptionsType { get; set; } = optionsType;
    }
}