using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using Microsoft.EntityFrameworkCore;
using static Coflnet.Sky.Core.ItemReferences;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{

    public class ItemPricesCommand : Command
    {
        public class DayCache
        {
            public List<Result> Results { get; set; }
        }

        public override async System.Threading.Tasks.Task Execute(MessageData data)
        {
            ItemSearchQuery details = GetQuery(data);


            
            Console.WriteLine($"Start: {details.Start} End: {details.End}");

            int hourAmount = DetermineHourAmount(details);

            var fromDB = await ItemPrices.Instance.GetPriceFor(details);

            var result = new List<Result>();

            result.AddRange(fromDB.Prices.Select(p=>new Result()
            {
                Count = p.Volume,
                Price = (int)p.Avg,
                End = p.Date
            }));

            var response = GroupResponseByHour(result, hourAmount);

            var maxAge = 100;
            if (details.Start < DateTime.Now - TimeSpan.FromDays(2))
            {
                maxAge = A_DAY;
            }

            await data.SendBack(data.Create("itemResponse", response, maxAge));
        }

        public static ItemSearchQuery GetQuery(MessageData data)
        {
            ItemSearchQuery details;
            try
            {
                details = data.GetAs<ItemSearchQuery>();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw new ValidationException("Format not valid for itemPrices, please see the docs");
            }

            if (details.End == default(DateTime))
            {
                details.End = DateTime.Now.RoundDown(TimeSpan.FromMinutes(5));
            }
            details.Start = details.Start.RoundDown(TimeSpan.FromMinutes(5));

            return details;
        }

        private static int DetermineHourAmount(ItemSearchQuery details)
        {
            var hourAmount = 1;
            if (details.End - details.Start > TimeSpan.FromDays(6))
                hourAmount = 4;
            if (details.End - details.Start > TimeSpan.FromDays(10))
                hourAmount = 12;
            return hourAmount;
        }

        private static List<Result> GroupResponseByHour(List<Result> result, int hourAmount)
        {
            var hourAmountTimeSpan = TimeSpan.FromHours(hourAmount);
            return result.GroupBy(item => item.End.RoundDown(hourAmountTimeSpan))
                .Select(item =>
                   new Result()
                   {
                       Count = item.Sum(i => i.Count),
                       End = item.Key,
                       Price = (int)item.Average(i => i.Price)
                   }).ToList();
        }

        private IEnumerable<Result> QueryDBFor(string itemName, DateTime start, DateTime end, ItemReferences.Reforge reforge, List<Enchantment> enchantments)
        {
            using (var context = new HypixelContext())
            {
                var tag = ItemDetails.Instance.GetIdForName(itemName);
                var itemId = ItemDetails.Instance.GetItemIdForTag(itemName);
                var mainSelect = context.Auctions
                    .Where(auction => /*auction.ItemName == itemName || */auction.ItemId == itemId);


                var selectWithTime = mainSelect.Where(auction => auction.End > start && auction.End < end );

                // override select if there is no cache anyways
                if (end == default(DateTime))
                    selectWithTime = mainSelect.Where(auction => auction.End > start && auction.End < end);


                var moreThanOneBidQuery = selectWithTime
                    .Where(auction => auction.HighestBidAmount > 1);



                if (enchantments != null && enchantments.Any())
                    moreThanOneBidQuery = AddEnchantmentWhere(enchantments, moreThanOneBidQuery);

                if (reforge != Reforge.None)
                    moreThanOneBidQuery = moreThanOneBidQuery.Where(auction => auction.Reforge == reforge);


                return moreThanOneBidQuery

                    .GroupBy(item => new { item.End.Year, item.End.Month, item.End.Day, item.End.Hour })
                    .Select(item =>
                       new
                       {
                           End = new DateTime(item.Key.Year, item.Key.Month, item.Key.Day, item.Key.Hour, 0, 0),
                           Price = (int)item.Average(a => ((int)a.HighestBidAmount) / a.Count), //(a.Count == 0 ? 1 : a.Count)),
                           Count = item.Sum(a => a.Count)
                           //Bids =  (long) item.Sum(a=> a.Bids.Count)
                       }).ToList().Select(i => new Result() { Count = i.Count, End = i.End, Price = i.Price });

                // cache result

            }
        }

        private static IQueryable<SaveAuction> AddEnchantmentWhere(List<Enchantment> enchantments, IQueryable<SaveAuction> moreThanOneBidQuery)
        {
            moreThanOneBidQuery = moreThanOneBidQuery
                                    .Include(auction => auction.Enchantments)
                                    .Where(auction => auction.Enchantments
                                            .Where(e => e.Type == enchantments.First().Type)
                                            .Where(e => e.Level == enchantments.First().Level)
                                            .Any()
                                            );
            return moreThanOneBidQuery;
        }


        DateTime earliest = new DateTime(2019, 10, 10);


        [MessagePackObject]
        public class Result
        {
            [Key("end")]
            public DateTime End;
            [Key("price")]
            public int Price;
            [Key("count")]
            public int Count;

        }

        [MessagePackObject]
        public class SearchDetails
        {
            [Key("name")]
            public string name;
            [Key("start")]
            public long StartTimeStamp
            {
                set
                {
                    Start = value.ThisIsNowATimeStamp();
                }
                get
                {
                    return Start.ToUnix();
                }
            }

            [IgnoreMember]
            public DateTime Start;

            [Key("end")]
            public long EndTimeStamp
            {
                set
                {
                    if (value == 0)
                    {
                        End = DateTime.Now;
                    }
                    else
                        End = value.ThisIsNowATimeStamp();
                }
                get
                {
                    return End.ToUnix();
                }
            }

            [IgnoreMember]
            public DateTime End;
        }
    }
}