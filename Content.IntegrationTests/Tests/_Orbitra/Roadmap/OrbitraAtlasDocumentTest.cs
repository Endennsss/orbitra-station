using System.Linq;
using Content.Server._Orbitra.Roadmap;
using Content.Shared._Orbitra.Roadmap;

namespace Content.IntegrationTests.Tests._Orbitra.Roadmap;

[TestFixture]
public sealed class OrbitraAtlasDocumentTest
{
    [Test]
    public void OrderedRouteDoesNotExposeUnpublishedChanges()
    {
        var route = new OrbitraAtlasDocument([]);
        var update = new AtlasContent("Обновление", "", AtlasStatus.Active, []);
        var ids = Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToArray();
        foreach (var id in ids)
            Apply(id, 0, AtlasAction.Publish, update);
        Apply(ids[2], 1, AtlasAction.Save, update with { Title = "Черновик" });
        Apply(ids[1], 1, AtlasAction.MoveDown);
        var stars = route.Read(false);
        Assert.That(stars.Select(s => s.Id).Take(4), Is.EqualTo(new[] { ids[0], ids[2], ids[1], ids[3] }));
        Assert.That(stars[1].Content.Title, Is.EqualTo("Обновление"));
        Assert.That(route.Read(true)[1].Content.Title, Is.EqualTo("Черновик"));
        Apply(ids[2], 3, AtlasAction.Unpublish);
        Assert.That(route.Read(false), Has.Length.EqualTo(19));
        Assert.That(route.Read(true)[1].Content.Title, Is.EqualTo("Черновик"));

        void Apply(Guid id, int revision, AtlasAction action, AtlasContent content = null)
        {
            var change = new AtlasChange { Id = id, RequestId = Guid.NewGuid(), Revision = revision, Action = action, Content = content };
            Assert.That(route.TryChange(change, out var next), Is.Null);
            route = next;
        }
    }

    [Test]
    public void InvalidEditsLeaveTheDocumentIntact()
    {
        var route = new OrbitraAtlasDocument([]);
        var content = new AtlasContent("Тест", "", AtlasStatus.Done, []);
        foreach (var invalid in new[] { content with { Tasks = [new(" ", AtlasStatus.Active)] },
                     content with { Title = new string('я', AtlasLimits.Title + 1) },
                     content with { Status = (AtlasStatus) 42 } })
        {
            var change = new AtlasChange { Id = Guid.NewGuid(), RequestId = Guid.NewGuid(), Action = AtlasAction.Publish, Content = invalid };
            Assert.That(route.TryChange(change, out var result), Is.EqualTo("orbitra-atlas-invalid"));
            Assert.That(result, Is.SameAs(route));
        }
    }
}
