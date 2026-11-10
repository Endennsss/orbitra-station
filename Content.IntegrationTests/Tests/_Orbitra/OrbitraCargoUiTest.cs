// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using System.Numerics;
using Content.Client.Cargo.UI;
using Content.IntegrationTests.Fixtures;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Orbitra;

public sealed class OrbitraCargoUiTest : GameTest
{
    [Test]
    public async Task LongCatalogueNameWrapsWithoutDisplacingPrice()
    {
        await Client.WaitAssertion(() =>
        {
            var row = new CargoProductRow();
            row.ProductName.SetMessage("Аварийный набор лечения кислородного голодания");
            row.PointCost.Text = "$600";

            row.Measure(new Vector2(620, 200));
            var wideHeight = row.ProductName.DesiredSize.Y;
            row.Measure(new Vector2(260, 200));
            row.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(260, row.DesiredSize.Y)));

            Assert.Multiple(() =>
            {
                Assert.That(row.ProductName.DesiredSize.Y, Is.GreaterThan(wideHeight));
                Assert.That(row.ProductName.Rect.Right, Is.LessThanOrEqualTo(row.PointCost.Rect.Left));
                Assert.That(row.PointCost.GlobalPosition.X + row.PointCost.Width, Is.LessThanOrEqualTo(row.Width + 1));
            });
        });
    }
}
