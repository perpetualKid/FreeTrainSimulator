using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace FreeTrainSimulator.Models.Signal
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

        // Mutable dictionaries for registration phase, nulled after Freeze()
        private Dictionary<string, SignalFunction> mutableFunctionLookup;
        private Dictionary<string, SignalNormalSubType> mutableSubTypeLookup;

        // Active dictionary references — backed by mutable Dictionary during init,
        // swapped to FrozenDictionary after Freeze()
        private IReadOnlyDictionary<string, SignalFunction> functionLookup;
        private IReadOnlyDictionary<string, SignalNormalSubType> subTypeLookup;

        private SignalTypeRegistry()
        {
            mutableFunctionLookup = new(StringComparer.OrdinalIgnoreCase);
            mutableSubTypeLookup = new(StringComparer.OrdinalIgnoreCase);

            functionLookup = mutableFunctionLookup;
            subTypeLookup = mutableSubTypeLookup;
        }

        /// <summary>Current registry instance, or <see langword="null"/> if not yet initialized.</summary>
        public static SignalTypeRegistry Instance => instance;

        /// <summary>Number of registered function types (predefined + custom).</summary>
        public int FunctionCount => functionLookup.Count;

        /// <summary>Number of registered normal subtypes.</summary>
        public int NormalSubTypeCount => subTypeLookup.Count;

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
            if (mutableFunctionLookup.TryGetValue(name, out SignalFunction existing))
                return existing;

            SignalFunction id = new SignalFunction(mutableFunctionLookup.Count);
            mutableFunctionLookup[name] = id;
            return id;
        }

        /// <summary>
        /// Registers a custom normal subtype. Returns the existing id if already registered.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if registry is frozen.</exception>
        public SignalNormalSubType RegisterNormalSubType(string name)
        {
            ThrowIfFrozen();
            if (mutableSubTypeLookup.TryGetValue(name, out SignalNormalSubType existing))
                return existing;

            SignalNormalSubType id = new(mutableSubTypeLookup.Count);
            mutableSubTypeLookup[name] = id;
            return id;
        }

        /// <summary>
        /// Freezes the registry, disallowing further registrations and switching to
        /// <see cref="FrozenDictionary{TKey, TValue}"/> for optimal lookup performance.
        /// Releases mutable initialization resources.
        /// </summary>
        public void Freeze()
        {
            if (mutableFunctionLookup is null)
                return; // Already frozen

            // Swap active dictionary references to frozen
            functionLookup = mutableFunctionLookup.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
            subTypeLookup = mutableSubTypeLookup.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

            // Release mutable initialization resources
            mutableFunctionLookup = null;
            mutableSubTypeLookup = null;
        }

        /// <summary>Looks up a function type by name.</summary>
        public bool TryGetFunction(string name, out SignalFunction id) => functionLookup.TryGetValue(name, out id);

        /// <summary>Looks up a normal subtype by name.</summary>
        public bool TryGetNormalSubType(string name, out SignalNormalSubType id) => subTypeLookup.TryGetValue(name, out id);

        /// <summary>Checks whether a function type name has been registered.</summary>
        public bool ContainsFunction(string name) => functionLookup.ContainsKey(name);

        /// <summary>Checks whether a normal subtype name has been registered.</summary>
        public bool ContainsNormalSubType(string name) => subTypeLookup.ContainsKey(name);

        /// <summary>Returns the registered name for a function type identifier.</summary>
        public string GetFunctionName(SignalFunction id)
        {
            foreach (KeyValuePair<string, SignalFunction> entry in functionLookup)
            {
                if (entry.Value.Index == id.Index)
                    return entry.Key;
            }
            return "None";
        }

        /// <summary>Returns the registered name for a normal subtype identifier.</summary>
        public string GetNormalSubTypeName(SignalNormalSubType id)
        {
            foreach (KeyValuePair<string, SignalNormalSubType> entry in subTypeLookup)
            {
                if (entry.Value.Index == id.Index)
                    return entry.Key;
            }
            return "None";
        }

        private void ThrowIfFrozen()
        {
            if (mutableFunctionLookup is null)
                throw new InvalidOperationException("Cannot register new types after the registry is frozen.");
        }
    }
}
