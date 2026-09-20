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
                new BotRegion("lower", new Float3(-2, 0, -2), new Float3(1.5f, 4, 2)),
                new BotRegion("upper", new Float3(1.6f, 0, -2), new Float3(4, 5, 2))
            }, new[]
            {
                new BotPassage("stairs", "lower", "upper", new[]
                {
                    new Float3(1, 3, 0),
                    new Float3(1.55f, 3, 0),
                    new Float3(2.1f, 3, 0)
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
    }
}
