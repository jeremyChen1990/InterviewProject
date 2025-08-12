using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using InterviewProject.BusinessModel;
using InterviewProject.Common;
using InterviewProject.ViewModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace InterviewProject.Service
{
    public class CustomerRankService: ICustomerRankService
    {
        //customers rank exclude score is zero.
        public static SortedDictionary<(decimal score, long customerId), Customer> _leaderboard =
            new SortedDictionary<(decimal score, long customerId), Customer>(new LeaderboardCompare());

        private static readonly ReaderWriterLockSlim _lock = new();

        //Dictionary with all customers,key is customerId.
        public ConcurrentDictionary<long, Customer> _customerDic = new ConcurrentDictionary<long, Customer>();

        //batch set up snapshot block
        private const int BlockSize = 1000;

        //the snapshot block with customers rank,key is block number(the key 0 is rank 1-1000), value is snapshot.
        public Dictionary<int, ((decimal score, long id)[] keys, Customer[] values)> _leaderboardSnapshotBlocks =
    new Dictionary<int, ((decimal, long)[], Customer[])>();

        private long _leaderboardVersion = 0;

        private long _leaderboardSnapshotVersion = -1;

        public decimal AddOrUpdateScoreToCustomer(long customerId, decimal score = 0)
        {
            //verify params
            if (score > 1000 || score < -1000)
                throw new ArgumentOutOfRangeException(nameof(score), "the score is out of range, valid value is between -1000 and 1000");

            bool entered = false;    
            try
            {
                Customer existingCustomer;
                var oldScore = 0m;
                bool isUpdate = false;
                var updateIndexLst = new List<int>();

                //try to find customer
                if (_customerDic.TryGetValue(customerId, out existingCustomer))
                {
                    isUpdate = true;
                    oldScore = existingCustomer.Score;
                    //score will not change.
                    if (score == 0)
                        return oldScore;
                    existingCustomer.Score = score + oldScore;
                }
                else
                {
                    existingCustomer = new Customer()
                    {
                        Score = score,
                        CustomerId = customerId,
                    };
                    _customerDic.TryAdd(customerId, existingCustomer);
                }
                _lock.EnterWriteLock();
                entered = true;
                if (isUpdate)
                {
                    _leaderboard.Remove((oldScore, customerId));
                    updateIndexLst.Add(GetRankIndex(oldScore, customerId));
                }
                if (existingCustomer.Score > 0)
                {
                    _leaderboard.TryAdd((existingCustomer.Score, customerId), existingCustomer);
                    updateIndexLst.Add(GetRankIndex(existingCustomer.Score, customerId));
                }

                //update leaderboard version.
                Interlocked.Increment(ref _leaderboardVersion);

                //set up leaderboard read only snapshot
                SetUpLeaderBoardSnapshot(updateIndexLst);

                return existingCustomer.Score;
            }
            finally
            {
                if (entered)
                    _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// set up leaderboard snapshot.
        /// </summary>
        /// <param name="updatedIndexLst">blocks index which data has been change</param>
        private void SetUpLeaderBoardSnapshot(List<int> updatedIndexLst)
        {
            if (_leaderboardSnapshotVersion == _leaderboardVersion)
                return;

            if (_leaderboard.Count == 0)
            {
                _leaderboardSnapshotBlocks.Clear();
                return;
            }
            var keys = _leaderboard.Keys.ToArray();
            var values = _leaderboard.Values.ToArray();
            int total = keys.Length;
            int blockCount = (total + BlockSize - 1) / BlockSize;

            for (int i = 0; i < blockCount; i++)
            {
                int start = i * BlockSize;
                int end = Math.Min(start + BlockSize, total);
                var blockKeys = keys[start..end];
                var blockValues = values[start..end];
                _leaderboardSnapshotBlocks[i] = (blockKeys, blockValues);
            }
        }

        public List<CustomerModel> GetCustomersByRank(int start, int end)
        {
            //verify params
            if (start < 1 || end < 1) throw new ArgumentException("start value or end value should greater than 1.");
            if (start > end) throw new ArgumentException("end value should be greater than start value.");
            List<CustomerModel> customers = new List<CustomerModel>();
            _lock.EnterReadLock();
            try
            {
                int starIndex = start - 1;
                int endIndex=end - 1;
                int startBlock = starIndex / BlockSize;
                int endBlock = endIndex / BlockSize;

                for (int block = startBlock; block <= endBlock; block++)
                {
                    if (!_leaderboardSnapshotBlocks.TryGetValue(block, out var snapshot))
                        continue;

                    int blockStart = block * BlockSize;
                    for (int i = 0; i < snapshot.keys.Length; i++)
                    {
                        int globalRank = blockStart + i;
                        if (globalRank >= starIndex && globalRank <= endIndex)
                        {
                            customers.Add(new CustomerModel
                            {
                                CustomerId = snapshot.keys[i].id,
                                Score = snapshot.keys[i].score,
                                Rank = globalRank + 1
                            });
                        }
                    }
                }

                return customers;
            }
            finally { _lock.ExitReadLock(); }
      
        }

        public List<CustomerModel> GetCustomersById(long customerId, int high = 0, int low = 0)
        {
            if (!_customerDic.TryGetValue(customerId, out var c) || c.Score <= 0)
                throw new NotFoundException($"Can't find customer with ID {customerId}");

            _lock.EnterReadLock();
            try
            {
                var totalRankCount= _leaderboardSnapshotBlocks.Values.Sum(b => b.keys.Length);
                // get the target customer index.
                foreach (var kvp in _leaderboardSnapshotBlocks)
                {
                    var blockIndex = kvp.Key;
                    var snapshot = kvp.Value;
                    var keys = snapshot.keys;

                    int localIndex = Array.BinarySearch(keys, (c.Score, customerId), new LeaderboardCompare());
                    if (localIndex >= 0)
                    {
                        var customers = new List<CustomerModel>();
                        int globalIndex = blockIndex * BlockSize + localIndex;

                        int startGlobal = Math.Max(0, globalIndex - high);
                        int endGlobal = Math.Min(totalRankCount - 1, globalIndex + low);

                        int startBlock = startGlobal / BlockSize;
                        int endBlock = endGlobal / BlockSize;

                        for (int b = startBlock; b <= endBlock; b++)
                        {
                            if (!_leaderboardSnapshotBlocks.TryGetValue(b, out var blk)) continue;
                            int blkStart = b * BlockSize;
                            for (int i = 0; i < blk.keys.Length; i++)
                            {
                                int rank = blkStart + i;
                                if (rank >= startGlobal && rank <= endGlobal)
                                {
                                    customers.Add(new CustomerModel
                                    {
                                        CustomerId = blk.keys[i].id,
                                        Score = blk.keys[i].score,
                                        Rank = rank + 1
                                    });
                                }
                            }
                        }

                        return customers;
                    }
                }

                throw new NotFoundException($"Can't find customer with ID {customerId}");
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        private int GetRankIndex(decimal score, long customerId)
        {
            var keys = _leaderboard.Keys.ToArray();
            return Array.BinarySearch(keys, (score, customerId), new LeaderboardCompare());
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
