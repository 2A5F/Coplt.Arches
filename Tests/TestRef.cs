using Coplt.Arches;
using Coplt.Arches.Internal;

namespace Tests;

public class TestRef
{
    public class Foo
    {
        public int a;
        public int b;
    }

    [Test]
    public void Test1()
    {
        for (var i = 0; i < 100; i++)
        {
            var foo = new Foo();
            var r = UnsafeRef.CreateRwRef(foo, ref foo.b);
            GC.Collect();
            r.V = 1;
            Assert.That(foo.b, Is.EqualTo(1));
        }
    }
}
