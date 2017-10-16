using System.Linq;

namespace NUnit.Framework.Tests
{
    [TestFixture]
    public class EffiencyTests
    {
        [Test]
        public void AllItemsAreUnique()
        {
            CollectionAssert.AllItemsAreUnique(Enumerable.Range(0, 10000).ToArray());
        }
    }
}
