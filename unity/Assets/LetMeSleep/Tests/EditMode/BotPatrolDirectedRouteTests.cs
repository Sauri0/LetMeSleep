using LetMeSleep.Gameplay;
using NUnit.Framework;

namespace LetMeSleep.Tests.EditMode
{
    public sealed class BotPatrolDirectedRouteTests
    {
        [Test]
        public void ElevatedPortalWaypointAdvancesAfterHorizontalArrivalBetweenBotDecisions()
        {
            var patrol = new BotPatrol(new[]
            {
                new BotRegion("room", new Float3(-2, 0, -2), new Float3(1.5f, 3, 2)),
                new BotRegion("hall", new Float3(1.6f, 0, -2), new Float3(4, 3, 2))
            }, new[]
            {
                new BotPassage("door", "room", "hall", new[]
                {
                    new Float3(1, 1.5f, 0),
                    new Float3(1.55f, 1.5f, 0),
                    new Float3(2.1f, 1.5f, 0)
                })
            }, 1);

            Float3 first = patrol.DirectionTo(new Float3(.70f, 1.274f, 0), "hall",
                new Float3(3, 1.274f, 0), _ => true);
            Assert.That(first.X, Is.EqualTo(.30f).Within(.0001f));

            // The controller only asks for a new direction every third host tick. It can
            // pass the first point between decisions while retaining the authored Y gap.
            Float3 afterCrossing = patrol.DirectionTo(new Float3(1.09f, 1.274f, 0), "hall",
                new Float3(3, 1.274f, 0), _ => true);

            Assert.That(afterCrossing.X, Is.GreaterThan(.4f),
                "The route must advance to the center waypoint instead of steering back to the entrance point.");
        }

        [Test]
        public void SameHorizontalPointOnAnotherLevelDoesNotAdvanceStairRoute()
        {
            var patrol = new BotPatrol(new[]
            {
                new BotRegion("lower", new Float3(-2, 0, -2), new Float3(1.5f, 2, 2)),
                new BotRegion("upper", new Float3(1.6f, 3, -2), new Float3(4, 5, 2))
            }, new[]
            {
                new BotPassage("stairs", "lower", "upper", new[]
                {
                    new Float3(1, 3, 0),
                    new Float3(1.55f, 3, 0),
                    new Float3(2.1f, 3, 0)
                }, new[]
                {
                    new BotRegion("stairs:flight", new Float3(.5f, 1, -.5f),
                        new Float3(2.5f, 4, .5f))
                })
            }, 1);

            var lowerPosition = new Float3(1.09f, 1.274f, 0);
            patrol.DirectionTo(lowerPosition, "upper", new Float3(3, 3, 0), _ => true);
            Float3 direction = patrol.DirectionTo(lowerPosition, "upper",
                new Float3(3, 3, 0), _ => true);

            Assert.That(direction.X, Is.LessThan(0),
                "Matching XZ on another floor must keep the lower stair waypoint active.");
            Assert.That(direction.Y, Is.GreaterThan(1),
                "The route must still direct the actor toward the authored level transition.");
        }

        [Test]
        public void ExteriorPortalAcceptsAuthoredTorsoHeightFromConnectedFloorBand()
        {
            var patrol = OrdinaryExteriorPortal();
            var target = new Float3(3, 1, 0);

            patrol.DirectionTo(new Float3(.70f, 1, 0), "outside", "inside", target, 0, _ => true);
            Float3 afterCrossing = patrol.DirectionTo(new Float3(1.09f, 1, 0),
                "outside", "inside", target, 3, _ => true);

            Assert.That(afterCrossing.X, Is.GreaterThan(.4f));
            Assert.That(afterCrossing.Y, Is.GreaterThan(.5f),
                "The next portal point remains at authored torso height after the entrance advances.");
        }

        [Test]
        public void OrdinaryPortalAtSameHorizontalPointOnAnotherFloorDoesNotAdvance()
        {
            var patrol = OrdinaryExteriorPortal();
            var target = new Float3(3, 1, 0);

            patrol.DirectionTo(new Float3(.70f, 1, 0), "outside", "inside", target, 0, _ => true);
            Float3 wrongFloor = patrol.DirectionTo(new Float3(1.09f, 4, 0),
                "outside", "inside", target, 3, _ => true);

            Assert.That(wrongFloor.X, Is.LessThan(0),
                "Matching portal XZ outside both connected floor bands must retain the entrance waypoint.");
            Assert.That(wrongFloor.Y, Is.LessThan(-2));
        }

        [Test]
        public void WeightedRouteUsesThreeShortPassagesInsteadOfTwoLongPassages()
        {
            WeightedGraph(out var regions, out var passages);
            Assert.That(BotRoutePlanner.TryPlan(regions, passages, "start", Float3.Zero,
                "finish", new Float3(10, 0, 0), _ => true, out var plan), Is.True);
            Assert.That(plan.Distance, Is.EqualTo(10).Within(.0001f));
            Assert.That(plan.FirstPassage.Id, Is.EqualTo("z-short-start"));

            var patrol = new BotPatrol(regions, passages, 1);
            Float3 direction = patrol.DirectionTo(Float3.Zero, "start", "finish",
                new Float3(10, 0, 0), 0, _ => true);

            Assert.That(direction.X, Is.GreaterThan(0));
            Assert.That(direction.Z, Is.EqualTo(0).Within(.0001f));
            Assert.That(patrol.CurrentProgress.Value.PassageId, Is.EqualTo(plan.FirstPassage.Id),
                "Admission distance and execution must come from the same weighted route contract.");
        }

        [Test]
        public void ClosedShortRouteUsesLongOpenAlternative()
        {
            WeightedGraph(out var regions, out var passages);
            var patrol = new BotPatrol(regions, passages, 1);

            Float3 direction = patrol.DirectionTo(Float3.Zero, "start", "finish",
                new Float3(10, 0, 0), 0, id => id != "z-short-start");

            Assert.That(direction.Z, Is.GreaterThan(0));
            Assert.That(patrol.CurrentProgress.Value.PassageId, Is.EqualTo("a-long-start"));
        }

        [Test]
        public void TemporarilyBlockedShortRouteUsesLongAlternativeDuringCooldown()
        {
            WeightedGraph(out var regions, out var passages);
            var patrol = new BotPatrol(regions, passages, 1);
            patrol.InvalidatePassage("z-short-start", 10);

            patrol.DirectionTo(Float3.Zero, "start", "finish", new Float3(10, 0, 0), 5, _ => true);

            Assert.That(patrol.CurrentProgress.Value.PassageId, Is.EqualTo("a-long-start"));
        }

        [Test]
        public void EqualDistanceRoutesChooseFirstPassageByOrdinalId()
        {
            var regions = new[]
            {
                new BotRegion("start", new Float3(-1, -1, -1), new Float3(1, 1, 1)),
                new BotRegion("finish", new Float3(9, -1, -1), new Float3(11, 1, 1))
            };
            var passages = new[]
            {
                new BotPassage("zeta", "start", "finish", new[]
                    { new Float3(.5f, 0, 0), new Float3(9.5f, 0, 0) }),
                new BotPassage("alpha", "start", "finish", new[]
                    { new Float3(.5f, 0, 0), new Float3(9.5f, 0, 0) })
            };

            Assert.That(BotRoutePlanner.TryPlan(regions, passages, "start", Float3.Zero,
                "finish", new Float3(10, 0, 0), _ => true, out var plan), Is.True);
            Assert.That(plan.FirstPassage.Id, Is.EqualTo("alpha"));
        }

        private static void WeightedGraph(out BotRegion[] regions, out BotPassage[] passages)
        {
            regions = new[]
            {
                new BotRegion("start", new Float3(-1, -1, -1), new Float3(1, 1, 1)),
                new BotRegion("long", new Float3(-1, -1, 19), new Float3(1, 1, 21)),
                new BotRegion("short-a", new Float3(1, -1, -1), new Float3(3, 1, 1)),
                new BotRegion("short-b", new Float3(4, -1, -1), new Float3(6, 1, 1)),
                new BotRegion("finish", new Float3(9, -1, -1), new Float3(11, 1, 1))
            };
            passages = new[]
            {
                new BotPassage("a-long-start", "start", "long", new[]
                    { new Float3(0, 0, .5f), new Float3(0, 0, 20) }),
                new BotPassage("a-long-finish", "long", "finish", new[]
                    { new Float3(0, 0, 20), new Float3(10, 0, 0) }),
                new BotPassage("z-short-start", "start", "short-a", new[]
                    { new Float3(.5f, 0, 0), new Float3(2, 0, 0) }),
                new BotPassage("z-short-middle", "short-a", "short-b", new[]
                    { new Float3(2, 0, 0), new Float3(5, 0, 0) }),
                new BotPassage("z-short-finish", "short-b", "finish", new[]
                    { new Float3(5, 0, 0), new Float3(9.5f, 0, 0) })
            };
        }

        private static BotPatrol OrdinaryExteriorPortal() => new BotPatrol(new[]
        {
            new BotRegion("outside", new Float3(-2, .1f, -2), new Float3(1.5f, 2.65f, 2)),
            new BotRegion("inside", new Float3(1.6f, .4f, -2), new Float3(4, 2.65f, 2))
        }, new[]
        {
            new BotPassage("exterior", "outside", "inside", new[]
            {
                new Float3(1, 1.6f, 0),
                new Float3(1.55f, 1.6f, 0),
                new Float3(2.1f, 1.6f, 0)
            })
        }, 1);
    }
}
