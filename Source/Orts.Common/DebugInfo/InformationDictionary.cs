using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Orts.Common.DebugInfo
{
    /// <summary>
    /// Specialized dictionary which does not throw but returns null if a key was not found 
    /// </summary>
    public class InformationDictionary : IDictionary<string, string>
    {
        private readonly Dictionary<string, string> dictionary = new Dictionary<string, string>();
        private List<string> currentKeys = new List<string>();
        private List<string> updatedKeys = new List<string>();
        private readonly Func<Dictionary<string, string>, int> versionGet;
        private int version;

        public InformationDictionary() : base()
        {
            FieldInfo field = dictionary.GetType().GetField("_version", BindingFlags.NonPublic | BindingFlags.Instance);
            ParameterExpression instanceExpression = Expression.Parameter(dictionary.GetType(), "instance");
            MemberExpression fieldExpression = Expression.Field(instanceExpression, field);
            UnaryExpression convertExpression = Expression.Convert(fieldExpression, field.FieldType);
            // Create a lambda expression of the latest call & compile it
            versionGet = Expression.Lambda<Func<Dictionary<string, string>, int>>(convertExpression, instanceExpression).Compile();
        }

        public string this[string key]
        {
            get
            {
                if (!TryGetValue(key, out string result))
                {
                    Add(key, null);
                }
                return result;

            }
            set 
            {
                dictionary[key] = value;
                if (version != (version = versionGet(dictionary)))
                    {
                    lock (dictionary)
                    {
                        updatedKeys.Clear();
                        updatedKeys.AddRange(currentKeys);
                        updatedKeys.Add(key);
                        (currentKeys, updatedKeys) = (updatedKeys, currentKeys);
                    }
                }
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
            lock (dictionary)
            {
                updatedKeys.Clear();
                updatedKeys.AddRange(currentKeys);
                updatedKeys.Add(key);
                (currentKeys, updatedKeys) = (updatedKeys, currentKeys);
            }
        }

        public void Add(KeyValuePair<string, string> item)
        {
            (dictionary as IDictionary<string, string>).Add(item);
            lock (dictionary)
            {
                updatedKeys.Clear();
                updatedKeys.AddRange(currentKeys);
                updatedKeys.Add(item.Key);
                (currentKeys, updatedKeys) = (updatedKeys, currentKeys);
            }
        }

        public void Clear()
        {
            lock (dictionary)
            {
                updatedKeys.Clear();
                (currentKeys, updatedKeys) = (updatedKeys, currentKeys);
            }
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
            lock (dictionary)
            {
                updatedKeys.Remove(key);
                (currentKeys, updatedKeys) = (updatedKeys, currentKeys);
            }
            return dictionary.Remove(key);
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
