using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MefContrib.Hosting.Conventions;

namespace MefContrib.Integration.Unity.Strategies
{
    using System;
    using System.ComponentModel.Composition;
    using Microsoft.Practices.ObjectBuilder2;

    /// <summary>
    /// Represents a strategy which injects MEF dependencies to
    /// the Unity created instance.
    /// </summary>
    public class ComposeStrategy : BuilderStrategy
    {
        static Dictionary<Type, bool> needsRecompseCache = new Dictionary<Type, bool>();
        private static object composeLock = new object();

        /// <summary>
        /// Called during the chain of responsibility for a build operation. The
        /// PostBuildUp method is called when the chain has finished the PreBuildUp
        /// phase and executes in reverse order from the PreBuildUp calls.
        /// </summary>
        /// <param name="context">Context of the build operation.</param>
        public override void PostBuildUp(IBuilderContext context)
        {
            var type = context.Existing.GetType();
            var attributes = type.GetCustomAttributes(typeof(PartNotComposableAttribute), false);

            if (attributes.Length == 0) {
                var container = context.Policies.Get<ICompositionContainerPolicy>(null).Container;
                if (NeedsRecompose(type)) {
                    while (true) {
                        try {
                            container.ComposeParts(context.Existing);
                            break;
                        }
                        catch (InvalidOperationException ex) {
                            Thread.SpinWait(5000);
                        }

                    }
                }
                else {
                    container.SatisfyImportsOnce(context.Existing);
                }
            }
        }

        private static bool NeedsRecompose(Type type)
        {
            bool needsRecompse;
            if (!needsRecompseCache.TryGetValue(type, out needsRecompse)) {
                var attrs = type.GetAllInstanceProperties()
                    .SelectMany(p => p.GetCustomAttributes(true))
                    .Union(type.GetAllInstanceFields()
                    .SelectMany(p => p.GetCustomAttributes(true)));
                needsRecompse = attrs.Any(p => ((p is ImportAttribute) && ((ImportAttribute)p).AllowRecomposition) ||
                    ((p is ImportManyAttribute) && ((ImportManyAttribute)p).AllowRecomposition));
                lock (composeLock) {
                    needsRecompseCache[type] = needsRecompse;
                }
            }

            return needsRecompse;
        }
    }
}