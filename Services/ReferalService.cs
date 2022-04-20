using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Coflnet.Sky.Commands;
using HashidsNet;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class ReferalService
    {
        public static ReferalService Instance { get; }
        Hashids hashids = new Hashids("simple salt", 6);
        Prometheus.Counter refCount = Prometheus.Metrics.CreateCounter("refCount", "How many new people were invited");
        static ReferalService()
        {
            Instance = new ReferalService();
        }

        public async Task<string> GetUserName(string refId)
        {
            if (UserService.Instance.TryGetUserById(GetId(refId), out GoogleUser user))
            {
                var accountUuid = (await McAccountService.Instance.GetActiveAccount(user.Id))?.AccountUuid;
                if (accountUuid != null)
                    return await PlayerSearch.Instance.GetNameWithCacheAsync(accountUuid);
            }
            return null;
        }

        public void WasReferedBy(GoogleUser user, string referer)
        {
            throw new CoflnetException("deactivated", "the referral system is now handled via the api ");

        }

        private int GetId(string referer)
        {
            return hashids.Decode(referer)[0];
        }

        public ReeralInfo GetReferalInfo(GoogleUser user)
        {
            using (var context = new HypixelContext())
            {
                var referedUsers = context.Users.Where(u => u.ReferedBy == user.Id).ToList();
                var minDate = new DateTime(2020, 2, 2);
                var boni = context.Boni.Where(b => b.UserId == user.Id).ToList();
                var upgraded = boni.Where(b => b.UserId == user.Id && b.Type == Bonus.BonusType.REFERED_UPGRADE).ToList();
                var receivedHours = boni.Where(b => b.Type != Bonus.BonusType.PURCHASE).Sum(b => b.BonusTime.TotalHours);
                return new ReeralInfo()
                {
                    RefId = hashids.Encode(user.Id),
                    BougthPremium = upgraded.Count,
                    ReceivedTime = TimeSpan.FromHours(receivedHours),
                    ReceivedHours = (int)receivedHours,
                    ReferCount = referedUsers.Count
                };
            }
        }

        [DataContract]
        public class ReeralInfo
        {
            [DataMember(Name = "refId")]
            public string RefId;
            [DataMember(Name = "count")]
            public int ReferCount;
            [DataMember(Name = "receivedTime")]
            public TimeSpan ReceivedTime;
            [DataMember(Name = "receivedHours")]
            public int ReceivedHours;
            [DataMember(Name = "bougthPremium")]
            public int BougthPremium;
        }
    }
}