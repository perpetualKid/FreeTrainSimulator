using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace FreeTrainSimulator.Common.DebugInfo
{
    /// <summary>
    /// Specialized dictionary which does not throw but returns null if a key was not found 
    /// </summary>
    public class InformationDictionary : IDictionary<string, string>
    {
        private static readonly List<string> empty = new List<string>();
        private readonly Dictionary<string, string> dictionary = new Dictionary<string, string>();
        private List<string> currentKeys = new List<string>();
        private readonly Func<Dictionary<string, string>, int> versionGet;
        private int version;

        public InformationDictionary() : base()
        {
            FieldInfo field = dictionary.GetType().GetField("_version", BindingFlags.NonPublic | BindingFlags.Instance);
            ParameterExpression instanceExpression = Expression.Parameter(dictionary.GetType(), "instance");
            MemberExpression fieldExpression = Expression.Field(instanceExpression, field);
            UnaryExpression convertExpression = Expression.Convert(fieldExpression, field.FieldType);
            versionGet = Expression.Lambda<Func<Dictionary<string, string>, int>>(convertExpression, instanceExpression).Compile();
        }

        public string this[string key]
        {
            get
            {
                _ = TryGetValue(key, out string result);
                return result;
            }
            set
            {
                dictionary[key] = value;
                if (version != (version = versionGet(dictionary)))
                    Interlocked.Exchange(ref currentKeys, dictionary.Keys.ToList());
            }
        }

        public ICollection<string> Values => dictionary.Values;

        public int Count => dictionary.Count;

        public bool IsReadOnly => false;

        ICollection<string> IDictionary<string, string>.Keys => currentKeys;

        public ICollection<string> Keys => currentKeys;

        public void Add(string key, string value)
        {
            dictionary.Add(key, value);
            Interlocked.Exchange(ref currentKeys, dictionary.Keys.ToList());
        }

        public void Add(KeyValuePair<string, string> item)
        {
            (dictionary as IDictionary<string, string>).Add(item);
            Interlocked.Exchange(ref currentKeys, dictionary.Keys.ToList());
        }

        public void Clear()
        {
            Interlocked.Exchange(ref currentKeys, empty);
            dictionary.Clear();
        }

        public bool Contains(KeyValuePair<string, string> item)
        {
            return dictionary.Contains(item);
        }

        public bool ContainsKey(string key)
        {
            return dictionary.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
        {
            (dictionary as IDictionary<string, string>).CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return dictionary.GetEnumerator();
        }

        public bool Remove(string key)
        {
            bool result = dictionary.Remove(key);
            Interlocked.Exchange(ref currentKeys, dictionary.Keys.ToList());
            return result;

        }

        public bool Remove(KeyValuePair<string, string> item)
        {
            return Remove(item.Key);
        }

        public bool TryGetValue(string key, out string value)
        {
            return dictionary.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return currentKeys.GetEnumerator();
        }
    }
}
