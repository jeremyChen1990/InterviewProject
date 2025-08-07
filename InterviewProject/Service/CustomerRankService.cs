using System.Collections.Concurrent;
using System.Linq;
using InterviewProject.BusinessModel;
using InterviewProject.Common;
using InterviewProject.ViewModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace InterviewProject.Service
{
    public class CustomerRankService: ICustomerRankService
    {
        public static SortedList<(decimal score, long customerId), Customer> _leaderboard =
            new SortedList<(decimal score, long customerId), Customer>(new LeaderboardCompare());

        private readonly ReaderWriterLockSlim _lock = new();

        public ConcurrentDictionary<long, Customer> _customerDic = new ConcurrentDictionary<long, Customer>();

        public decimal AddOrUpdateScoreToCustomer(long customerId, decimal score = 0)
        {
            //verify params
            if(score>1000 || score<-1000)
                throw new ArgumentOutOfRangeException(nameof(score),"the score is out of range, valid value is between -1000 and 1000");
            
            Customer existingCustomer;
            _lock.EnterWriteLock();
            try
            {
                if (_customerDic.TryGetValue(customerId, out existingCustomer))
                {
                    _leaderboard.Remove((existingCustomer.Score, existingCustomer.CustomerId));
                    existingCustomer.Score = existingCustomer.Score + score;
                }
                else
                {
                    existingCustomer = new Customer()
                    {
                        CustomerId = customerId,
                        Score = score
                    };
                    _customerDic.TryAdd(customerId, existingCustomer);
                }
                if (existingCustomer.Score > 0)
                {
                    _leaderboard.Add((existingCustomer.Score, existingCustomer.CustomerId), existingCustomer);
                }
                return existingCustomer.Score;
            }           
           finally { _lock.ExitWriteLock(); }
            
        }

        public List<CustomerModel> GetCustomersByRank(int start, int end)
        {
            //verify params
            if (start < 1 || end < 1) throw new ArgumentException("start value or end value should greater than 1.");
            if (start > end) throw new ArgumentException("end value should be greater than start value.");

            _lock.EnterReadLock();
            try
            {
                var customers = _leaderboard.Skip(start - 1)
                .Take(end - start + 1)
                .Select(p => new CustomerModel()
                {
                    CustomerId = p.Value.CustomerId,
                    Score = p.Value.Score,
                    Rank = _leaderboard.IndexOfKey(p.Key) + 1
                })
                .ToList();

                return customers;
            }
            finally { _lock.ExitReadLock(); }
           
          
        }

        public List<CustomerModel> GetCustomersById(long customerId, int high = 0, int low = 0)
        {
            var list = _leaderboard.ToList();
            var index = list.FindIndex(x => x.Key.customerId == customerId);
            if (index == -1) throw new NotFoundException($"Can't found customers by customerId {customerId}");

            _lock.EnterReadLock();
            try
            {
                var start = Math.Max(0, index - high);
                var end = Math.Min(list.Count - 1, index + low);
                var customers = Enumerable.Range(start, end - start + 1)
                    .Select(i => new CustomerModel()
                    {
                        CustomerId = list[i].Key.customerId,
                        Score = list[i].Key.score,
                        Rank = i + 1
                    }).ToList();

                return customers;
            }
            finally
            {
                _lock.ExitReadLock();
            }  
        }
    }

    public interface ICustomerRankService
    {
        /// <summary>
        /// Update customer score if customer id is not existing,add the customer into leaderboard
        /// </summary>
        /// <param name="customerId">customerId, is required</param>
        /// <param name="score">A decimal numberin range of [-1000, +1000],default is zero</param>
        /// <returns></returns>
        decimal AddOrUpdateScoreToCustomer(long customerId, decimal score=0);

        /// <summary>
        /// Get customer by rank range
        /// </summary>
        /// <param name="start"> start rank, included in response if exists</param>
        /// <param name="end">end rank, included in response if exists</param>
        /// <returns>customer and currently rank</returns>
       List<CustomerModel> GetCustomersByRank(int start, int end);

        /// <summary>
        /// Get customer by customerId and nearest neighbors
        /// </summary>
        /// <param name="customerId">customer unique id</param>
        /// <param name="high">optional. Default zero. number of neighbors whose rank is  higher than the specified customer.</param>
        /// <param name="low">optional. Default zero. number of neighbors whose rank is lower than the specified customer.</param>
        /// <returns>customers and currently rank</returns>
        List<CustomerModel> GetCustomersById(long customerId, int high = 0, int low = 0);

    }

    public class LeaderboardCompare : IComparer<(decimal score, long customerId)>
    {
        /// <summary>
        /// Leaderboard Compare, score comparing is first, if score is equality,then lower customerId is first.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public int Compare((decimal score, long customerId) x, (decimal score, long customerId) y)
        {
            var compareWithScore = x.score.CompareTo(y.score);

            return compareWithScore == 0 ? x.customerId.CompareTo(y.customerId) : compareWithScore;
        }
    }
}
