using System.Linq;
using NUnit.Framework;

namespace DemocracyWay.NewUI.Tests
{
    /// <summary>Η κλήρωση της φυλής κάθε πρυτανείας: τυχαία, αλλά κάθε φυλή πρυτανεύει μία φορά στις 10.</summary>
    public sealed class LotteryTests
    {
        [Test]
        public void TenRandomDraws_EachTribeExactlyOnce()
        {
            var random = new System.Random(7);
            var state = new GameState();
            for (int prytany = 0; prytany < 10; prytany++)
            {
                int available = GameRules.AvailableTribes(state, 10).Count;
                Assert.AreEqual(10 - prytany, available);
                Assert.IsTrue(GameRules.DrawTribe(state, 10, random.Next(available)));
                state.currentTribe = -1;   // όπως στο τέλος κάθε πρυτανείας
            }
            CollectionAssert.AreEquivalent(Enumerable.Range(0, 10), state.drawnTribes);
        }

        [Test]
        public void DifferentSeeds_GiveDifferentOrders()
        {
            int[] Order(int seed)
            {
                var random = new System.Random(seed);
                var state = new GameState();
                for (int i = 0; i < 10; i++)
                {
                    GameRules.DrawTribe(state, 10, random.Next(GameRules.AvailableTribes(state, 10).Count));
                    state.currentTribe = -1;
                }
                return state.drawnTribes.ToArray();
            }
            var orders = Enumerable.Range(1, 5).Select(Order).Select(o => string.Join(",", o)).Distinct().Count();
            Assert.Greater(orders, 1, "η σειρά των φυλών δεν πρέπει να είναι πάντα η ίδια");
        }
    }
}
