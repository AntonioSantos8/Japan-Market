using System;
using UnityEngine;

namespace JapanMarket.Core
{

    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class ReadOnlyFieldAttribute : PropertyAttribute
    {
    }
}
