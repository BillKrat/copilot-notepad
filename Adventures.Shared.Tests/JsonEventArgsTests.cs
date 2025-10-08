using System.Text.Json;
using System.Text.Json.Nodes;
using Adventures.Shared.Event;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Adventures.Shared.Tests
{
    [TestClass]
    public sealed class JsonEventArgsTests
    {
        [TestMethod]
        public void Ctor_String_ValidObject_ParsesSuccessfully()
        {
            var args = new JsonEventArgs("{ \"name\": \"Alice\", \"age\": 30 }");
            Assert.AreEqual("Alice", args.Json["name"]!.GetValue<string>());
            Assert.AreEqual(30, args.Json["age"]!.GetValue<int>());
        }

        [TestMethod]
        public void Ctor_String_Array_Throws()
        {
            try
            {
                _ = new JsonEventArgs("[1,2,3]");
                Assert.Fail("Expected ArgumentException");
            }
            catch (ArgumentException)
            {
                // success
            }
        }

        [TestMethod]
        public void Ctor_String_NullOrWhitespace_Throws()
        {
            try
            {
                _ = new JsonEventArgs("  ");
                Assert.Fail("Expected ArgumentException");
            }
            catch (ArgumentException)
            {
                // success
            }
        }

        [TestMethod]
        public void Ctor_Dictionary_SimpleValues_CreatesProperties()
        {
            var dict = new Dictionary<string, object?>
            {
                ["id"] = 5,
                ["title"] = "Book",
                ["active"] = true
            };
            var args = new JsonEventArgs(dict);
            Assert.AreEqual(5, args.Json["id"]!.GetValue<int>());
            Assert.AreEqual("Book", args.Json["title"]!.GetValue<string>());
            Assert.IsTrue(args.Json["active"]!.GetValue<bool>());
        }

        private sealed record SampleRecord(int Id, string Label);

        [TestMethod]
        public void Ctor_Object_Poco_Serializes()
        {
            var obj = new SampleRecord(42, "Answer");
            var args = new JsonEventArgs(obj);
            Assert.AreEqual(42, args.Json["Id"]!.GetValue<int>());
            Assert.AreEqual("Answer", args.Json["Label"]!.GetValue<string>());
        }

        [TestMethod]
        public void Ctor_JsonObject_ReusesInstance()
        {
            var original = new JsonObject { ["flag"] = true };
            var args = new JsonEventArgs(original);
            Assert.AreSame(original, args.Json);
        }

        [TestMethod]
        public void Ctor_JsonValue_WrappedInValueProperty()
        {
            JsonNode valueNode = JsonValue.Create(123)!;
            var args = new JsonEventArgs(valueNode);
            Assert.IsTrue(args.Json.ContainsKey("value"));
            Assert.AreEqual(123, args.Json["value"]!.GetValue<int>());
        }

        [TestMethod]
        public void Ctor_NullObject_GivesEmptyObject()
        {
            var args = new JsonEventArgs((object?)null);
            Assert.AreEqual(0, args.Json.Count);
        }

        [TestMethod]
        public void TryGetProperty_Existing_ReturnsTrue()
        {
            var args = new JsonEventArgs("{ \"score\": 99 }");
            var ok = args.TryGetProperty<int>("score", out var score);
            Assert.IsTrue(ok);
            Assert.AreEqual(99, score);
        }

        [TestMethod]
        public void TryGetProperty_Missing_ReturnsFalse()
        {
            var args = new JsonEventArgs("{ \"score\": 99 }");
            var ok = args.TryGetProperty<int>("missing", out var value);
            Assert.IsFalse(ok);
            Assert.AreEqual(default, value);
        }

        [TestMethod]
        public void NestedDictionary_And_Collections_Normalized()
        {
            var nested = new Dictionary<string, object?>
            {
                ["numbers"] = new List<int>{1,2,3},
                ["inner"] = new Dictionary<string, object?>{ ["x"] = 7, ["y"] = 8 },
                ["mixed"] = new object?[]{ "a", 10, true }
            };

            var args = new JsonEventArgs(nested);
            Assert.AreEqual(3, args.Json["numbers"]!.AsArray().Count);
            Assert.AreEqual(7, args.Json["inner"]!.AsObject()["x"]!.GetValue<int>());
            Assert.AreEqual("a", args.Json["mixed"]!.AsArray()[0]!.GetValue<string>());
            Assert.AreEqual(10, args.Json["mixed"]!.AsArray()[1]!.GetValue<int>());
            Assert.IsTrue(args.Json["mixed"]!.AsArray()[2]!.GetValue<bool>());
        }

        [TestMethod]
        public void ToString_ReturnsCompactJson()
        {
            var args = new JsonEventArgs(new { A = 1, B = 2 });
            var json = args.ToString();
            using var doc = JsonDocument.Parse(json);
            Assert.AreEqual(1, doc.RootElement.GetProperty("A").GetInt32());
            Assert.AreEqual(2, doc.RootElement.GetProperty("B").GetInt32());
        }
    }
}
