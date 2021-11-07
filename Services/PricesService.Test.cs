using System.Collections.Generic;
using System.Linq;
using dev;
using hypixel;
using NUnit.Framework;

namespace Coflnet.Sky.Commands.Test
{
    public class PricesServiceTests
    {
        /// <summary>
        /// Sum multiple sell orders to get a price for multiple items of same type
        /// </summary>
        [Test]
        public void MultiSellOrderSpan()
        {
            var orders = new List<BuyOrder>(){
                new BuyOrder(){Amount = 3,PricePerUnit = 4},
                new BuyOrder(){Amount = 4,PricePerUnit = 1},
                new BuyOrder(){Amount = 5,PricePerUnit = 100},
            };
            var count = 8;
            double totalCost = new PricesService(null).GetBazaarCostForCount(orders, count);
            Assert.AreEqual(116, totalCost);
        }

        [Test]
        public void SingleOrder()
        {
            var orders = new List<BuyOrder>(){
                new BuyOrder(){Amount = 3,PricePerUnit = 4},
                new BuyOrder(){Amount = 4,PricePerUnit = 1}
            };
            var count = 3;
            double totalCost = new PricesService(null).GetBazaarCostForCount(orders, count);
            Assert.AreEqual(12, totalCost);
        }
    }

}