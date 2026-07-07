using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Track;

using MemoryPack;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Models
{
    // Guards the serialized member order of persisted models. MemoryPack Sequential layout serializes members in
    // base-first declaration order, and because non-[MemoryPackable] base records (e.g. ModelBase) are flattened
    // into their derived types, inserting a member ahead of an existing derived member silently shifts and corrupts
    // reads of pre-existing files. These snapshot tests fail loudly if the flattened member order changes, enforcing
    // the append-only convention. When a change is intentional, append the new member LAST and update the expected
    // list here (and add a migration for the affected type when older files exist).
    [TestClass]
    public class ModelSerializationLayoutTests
    {
        [TestMethod]
        public void PathModelHeaderMemberOrderIsStable()
        {
            AssertMemberOrder(typeof(PathModelHeader), "Id", "Name", "Version", "Tags", "Start", "End", "PlayerPath", "ValidationState");
        }

        [TestMethod]
        public void PathModelMemberOrderIsStable()
        {
            AssertMemberOrder(typeof(PathModel), "Id", "Name", "Version", "Tags", "Start", "End", "PlayerPath", "ValidationState", "PathNodes");
        }

        [TestMethod]
        public void ContentModelMemberOrderIsStable()
        {
            AssertMemberOrder(typeof(ContentModel), "Id", "Name", "Version", "Tags", "ContentFolders");
        }

        [TestMethod]
        public void RouteModelHeaderMemberOrderStartsWithModelBaseMembers()
        {
            AssertMemberOrderStartsWith(typeof(RouteModelHeader), "Id", "Name", "Version", "Tags");
        }

        [TestMethod]
        public void SavePointModelMemberOrderStartsWithModelBaseMembers()
        {
            AssertMemberOrderStartsWith(typeof(SavePointModel), "Id", "Name", "Version", "Tags");
        }

        [TestMethod]
        public void TrackModelMemberOrderStartsWithModelBaseMembers()
        {
            AssertMemberOrderStartsWith(typeof(TrackModel), "Id", "Name", "Version", "Tags");
        }

        [TestMethod]
        public void ProfileSelectionsModelMemberOrderStartsWithModelBaseMembers()
        {
            AssertMemberOrderStartsWith(typeof(ProfileSelectionsModel), "Id", "Name", "Version", "Tags");
        }

        [TestMethod]
        public void PathNodeMemberOrderIsStable()
        {
            AssertMemberOrder(typeof(PathNode), "Location", "NodeType", "NodeIndex", "NextMainNode", "NextSidingNode", "WaitInfo");
        }

        [TestMethod]
        public void FolderModelMemberOrderStartsWithModelBaseMembers()
        {
            AssertMemberOrderStartsWith(typeof(FolderModel), "Id", "Name", "Version", "Tags");
        }

        [TestMethod]
        public void TrackNodeConnectorMemberOrderIsStable()
        {
            AssertMemberOrder(typeof(TrackNodeConnector), "ConnectorType", "Link", "Direction");
        }

        [TestMethod]
        public void TrackNodeConnectorIndexMemberOrderIsStable()
        {
            AssertMemberOrder(typeof(TrackNodeConnectorIndex), "NodeIndex", "InboundCount", "TrackNodeConnectors");
        }

        [TestMethod]
        public void DumpAllMemoryPackableMemberOrders()
        {
            System.Reflection.Assembly assembly = typeof(PathModel).Assembly;
            IEnumerable<Type> types = assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<MemoryPackableAttribute>() != null && !t.IsAbstract && !t.IsGenericTypeDefinition)
                .OrderBy(t => t.FullName, StringComparer.Ordinal);

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            foreach (Type type in types)
            {
                IReadOnlyList<string> order = SerializedMemberOrder(type);
                builder.AppendLine($"{type.FullName}|{string.Join(",", order)}");
            }
            TestContext.WriteLine(builder.ToString());
            System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "memorypack-layout-dump.txt"), builder.ToString());
        }

        public TestContext TestContext { get; set; }

        private static void AssertMemberOrder(Type modelType, params string[] expected)
        {
            IReadOnlyList<string> actual = SerializedMemberOrder(modelType);
            CollectionAssert.AreEqual(expected, actual.ToArray(),
                $"Serialized member order for {modelType.Name} changed. Expected [{string.Join(", ", expected)}] but found [{string.Join(", ", actual)}]. " +
                "Append new members LAST and add a migration when older files exist.");
        }

        private static void AssertMemberOrderStartsWith(Type modelType, params string[] expectedPrefix)
        {
            IReadOnlyList<string> actual = SerializedMemberOrder(modelType);
            string[] prefix = actual.Take(expectedPrefix.Length).ToArray();
            CollectionAssert.AreEqual(expectedPrefix, prefix,
                $"Leading serialized members for {modelType.Name} changed. Expected prefix [{string.Join(", ", expectedPrefix)}] but found [{string.Join(", ", prefix)}]. " +
                "ModelBase members must stay first; append new members LAST.");
        }

        // Approximates the MemoryPack Sequential flattened member order: walk the type hierarchy base-first and, for
        // each type, take its declared public instance properties (metadata/declaration order) that MemoryPack
        // serializes. A property is serialized when it has a getter and is either settable (set/init) or backed by a
        // [MemoryPackConstructor] parameter (get-only records such as PathNode.Location). [MemoryPackIgnore] members
        // (inherited by overrides) and the compiler-generated record EqualityContract are excluded.
        private static IReadOnlyList<string> SerializedMemberOrder(Type modelType)
        {
            HashSet<string> constructorParameters = MemoryPackConstructorParameterNames(modelType);

            List<Type> hierarchy = new List<Type>();
            for (Type current = modelType; current != null && current != typeof(object); current = current.BaseType)
                hierarchy.Add(current);
            hierarchy.Reverse();

            List<string> members = new List<string>();
            foreach (Type type in hierarchy)
            {
                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (property.GetMethod == null)
                        continue;
                    bool settable = property.SetMethod != null;
                    bool constructorBacked = constructorParameters.Contains(property.Name);
                    if (!settable && !constructorBacked)
                        continue;
                    if (property.GetCustomAttribute<MemoryPackIgnoreAttribute>(inherit: true) != null)
                        continue;
                    if (property.Name == "EqualityContract")
                        continue;
                    members.Add(property.Name);
                }
            }
            return members;
        }

        private static HashSet<string> MemoryPackConstructorParameterNames(Type modelType)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ConstructorInfo constructor in modelType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                if (constructor.GetCustomAttribute<MemoryPackConstructorAttribute>() == null)
                    continue;
                foreach (ParameterInfo parameter in constructor.GetParameters())
                    _ = names.Add(parameter.Name);
            }
            return names;
        }
    }
}
