using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Orts.Formats.Msts
{
    /// <summary>
    /// Central registry for signal function types and normal subtypes.
    /// Manages name ↔ identifier mapping for both predefined MSTS types and
    /// custom types registered at runtime from configuration files.
    /// </summary>
    /// <remarks>
    /// <para>Lifecycle: call <see cref="Initialize"/> at startup, register custom types via
    /// <see cref="RegisterFunction"/> / <see cref="RegisterNormalSubType"/>,
    /// then call <see cref="Freeze"/> to lock the registry and enable optimized lookups.</para>
    /// <para>After <see cref="Freeze"/>, name lookups use <see cref="FrozenDictionary{TKey, TValue}"/>
    /// for maximum performance.</para>
    /// </remarks>
    public sealed class SignalTypeRegistry
    {
        private static SignalTypeRegistry instance;

        private readonly List<string> functionNames = [];
        private readonly Dictionary<string, SignalFunction> functionLookup = new Dictionary<string, SignalFunction>(StringComparer.OrdinalIgnoreCase);

        private readonly List<string> subTypeNames = [];
        private readonly Dictionary<string, SignalNormalSubType> subTypeLookup = new Dictionary<string, SignalNormalSubType>(StringComparer.OrdinalIgnoreCase);

        // Frozen versions for fast post-initialization lookups
        private FrozenDictionary<string, SignalFunction> frozenFunctionLookup;
        private FrozenDictionary<string, SignalNormalSubType> frozenSubTypeLookup;

        private bool frozen;

        /// <summary>Current registry instance, or <see langword="null"/> if not yet initialized.</summary>
        public static SignalTypeRegistry Instance => instance;

        /// <summary>Number of registered function types (predefined + custom).</summary>
        public int FunctionCount => functionNames.Count;

        /// <summary>Number of registered normal subtypes.</summary>
        public int NormalSubTypeCount => subTypeNames.Count;

        /// <summary>
        /// Initializes a fresh registry pre-loaded with the predefined MSTS function types.
        /// Ordinals are guaranteed to match <see cref="SignalFunction"/> static constants.
        /// </summary>
        public static SignalTypeRegistry Initialize()
        {
            SignalTypeRegistry registry = new SignalTypeRegistry();

            // Register predefined types to match SignalFunctionId constants
            foreach (string name in SignalFunction.MstsNames)
            {
                _ = registry.RegisterFunction(name);
            }

            instance = registry;
            return registry;
        }

        /// <summary>
        /// Registers a custom signal function type. Returns the existing id if already registered.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if registry is frozen.</exception>
        public SignalFunction RegisterFunction(string name)
        {
            ThrowIfFrozen();
            if (functionLookup.TryGetValue(name, out SignalFunction existing))
                return existing;

            SignalFunction id = new(functionNames.Count);
            functionNames.Add(name);
            functionLookup[name] = id;
            return id;
        }

        /// <summary>
        /// Registers a custom normal subtype. Returns the existing id if already registered.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if registry is frozen.</exception>
        public SignalNormalSubType RegisterNormalSubType(string name)
        {
            ThrowIfFrozen();
            if (subTypeLookup.TryGetValue(name, out SignalNormalSubType existing))
                return existing;

            SignalNormalSubType id = new(subTypeNames.Count);
            subTypeNames.Add(name);
            subTypeLookup[name] = id;
            return id;
        }

        /// <summary>
        /// Freezes the registry, disallowing further registrations and switching to
        /// <see cref="FrozenDictionary{TKey, TValue}"/> for optimal lookup performance.
        /// </summary>
        public void Freeze()
        {
            frozenFunctionLookup = functionLookup.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
            frozenSubTypeLookup = subTypeLookup.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
            frozen = true;
        }

        /// <summary>Looks up a function type by name.</summary>
        public bool TryGetFunction(string name, out SignalFunction id)
        {
            return frozen
                ? frozenFunctionLookup.TryGetValue(name, out id)
                : functionLookup.TryGetValue(name, out id);
        }

        /// <summary>Looks up a normal subtype by name.</summary>
        public bool TryGetNormalSubType(string name, out SignalNormalSubType id)
        {
            return frozen
                ? frozenSubTypeLookup.TryGetValue(name, out id)
                : subTypeLookup.TryGetValue(name, out id);
        }

        /// <summary>Checks whether a function type name has been registered.</summary>
        public bool ContainsFunction(string name)
        {
            return frozen
                ? frozenFunctionLookup.ContainsKey(name)
                : functionLookup.ContainsKey(name);
        }

        /// <summary>Checks whether a normal subtype name has been registered.</summary>
        public bool ContainsNormalSubType(string name)
        {
            return frozen
                ? frozenSubTypeLookup.ContainsKey(name)
                : subTypeLookup.ContainsKey(name);
        }

        /// <summary>Returns the registered name for a function type identifier.</summary>
        public string GetFunctionName(SignalFunction id)
        {
            return id.Index >= 0 && id.Index < functionNames.Count ? functionNames[id.Index] : "None";
        }

        /// <summary>Returns the registered name for a normal subtype identifier.</summary>
        public string GetNormalSubTypeName(SignalNormalSubType id)
        {
            return id.Index >= 0 && id.Index < subTypeNames.Count ? subTypeNames[id.Index] : "None";
        }

        /// <summary>Resets the singleton instance. Call when reloading configuration.</summary>
        public static void Reset()
        {
            instance = null;
        }

        private void ThrowIfFrozen()
        {
            if (frozen)
                throw new InvalidOperationException("Cannot register new types after the registry is frozen.");
        }
    }
}
