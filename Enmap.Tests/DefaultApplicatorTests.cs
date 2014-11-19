using System.Data.Entity;
using System.Linq;
using NUnit.Framework;

namespace Enmap.Tests
{
    [TestFixture]
    public class DefaultApplicatorTests
    {
        [Test]
        public async void PrimitiveCopy()
        {
            var mapperRegistry = new MapperRegistry<MapperContext>(new PrimitiveCopyDb(), registry =>
            {
                registry.Map<PrimitiveCopyTable, PrimitiveCopyModel>()
                    .For(x => x.Id).From(x => x.Id)
                    .For(x => x.StringProperty).From(x => x.StringProperty);
            });
            var query = new[] { new PrimitiveCopyTable { Id = 1, StringProperty = "foo" } }.AsQueryable();
            var result = await mapperRegistry.Get<PrimitiveCopyTable, PrimitiveCopyModel>().MapTo(query, new MapperContext(new PrimitiveCopyDb()));
            var model = result.First();
            Assert.AreEqual(1, model.Id);
            Assert.AreEqual("foo", model.StringProperty);
        }

        public class PrimitiveCopyTable
        {
            public int Id { get; set; }
            public string StringProperty { get; set; }
        }

        public class PrimitiveCopyDb : DbContext
        {
            public DbSet<PrimitiveCopyTable> PrimitiveCopies { get; set; }
        }

        public class PrimitiveCopyModel
        {
            public int Id { get; set; }
            public string StringProperty { get; set; }
        }
    }
}