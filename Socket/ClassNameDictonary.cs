using System;
using System.Collections.Generic;

namespace Coflnet.Sky.Commands
{
    public class ClassNameDictonary<T> : Dictionary<string, T>
    {
        public void Add<TDerived>() where TDerived : T
        {
            var filter = Activator.CreateInstance<TDerived>();
            this.Add(typeof(TDerived).Name.Replace("Command","").ToLower(), filter);
        }
    }
}