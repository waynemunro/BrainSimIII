using Microsoft.VisualStudio.TestTools.UnitTesting;
using UKS;

namespace BrainSimIII.Tests
{
    [TestClass]
    public class FastMetaHypergraphTests
    {
        private UKS.UKS uks;

        [TestInitialize]
        public void Setup()
        {
            // Create UKS instance before each test
            uks = new UKS.UKS(clear: true);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Ensure proper cleanup after each test
            uks?.Dispose();
            uks = null;
            // Force garbage collection to help with timer cleanup
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
        }

        [TestMethod]
        public void TestBasicThingCreation()
        {
            Thing simpleThing = uks.GetOrAddThing("test", null);
            Assert.IsNotNull(simpleThing);
            Assert.AreEqual("test", simpleThing.Label);
        }

        [TestMethod]
        public void TestMetaRelationshipCreation()
        {
            // Test basic MetaRelationship creation
            Thing source = uks.GetOrAddThing("dog", "Animal");
            Thing relationshipType = uks.GetOrAddThing("has-attribute", null);
            Thing target = uks.GetOrAddThing("4-legs", null);

            var metaRel = new MetaRelationship()
            {
                source = source,
                reltype = relationshipType,
                target = target,
                HyperedgeType = HyperedgeType.SimpleRelation
            };

            Assert.IsNotNull(metaRel);
            Assert.AreEqual(source, metaRel.source);
            Assert.AreEqual(target, metaRel.target);
            Assert.AreEqual(HyperedgeType.SimpleRelation, metaRel.HyperedgeType);
        }

        [TestMethod]
        public void TestHyperedgeTypes()
        {
            // Test all HyperedgeType enum values exist
            Assert.IsTrue(System.Enum.IsDefined(typeof(HyperedgeType), HyperedgeType.SimpleRelation));
            Assert.IsTrue(System.Enum.IsDefined(typeof(HyperedgeType), HyperedgeType.MetaRelation));
            Assert.IsTrue(System.Enum.IsDefined(typeof(HyperedgeType), HyperedgeType.ExceptionRelation));
        }

        [TestMethod]
        public void TestBasicInheritance()
        {
            // Simple inheritance test
            Thing animal = uks.GetOrAddThing("Animal", null);
            Thing dog = uks.GetOrAddThing("Dog", null);
            dog.AddParent(animal);

            // Basic verification
            Assert.IsNotNull(animal);
            Assert.IsNotNull(dog);
            Assert.IsTrue(dog.Parents.Contains(animal));
        }

        [TestMethod]
        public void TestExceptionFlag()
        {
            // Test exception relationship properties
            Thing dog = uks.GetOrAddThing("dog", "Animal");
            Thing exception = uks.GetOrAddThing("exception", null);
            Thing action = uks.GetOrAddThing("action", null);

            var exceptionRel = new MetaRelationship()
            {
                source = dog,
                reltype = exception,
                target = action,
                HyperedgeType = HyperedgeType.ExceptionRelation,
                IsException = true
            };

            Assert.IsTrue(exceptionRel.IsException);
            Assert.AreEqual(HyperedgeType.ExceptionRelation, exceptionRel.HyperedgeType);
        }
    }
}