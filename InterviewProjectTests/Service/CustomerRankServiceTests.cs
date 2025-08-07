using Microsoft.VisualStudio.TestTools.UnitTesting;
using InterviewProject.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InterviewProject.Common;

namespace InterviewProject.Service.Tests
{
    [TestClass()]
    public class CustomerRankServiceTests
    {
         private readonly ICustomerRankService _service;
        public CustomerRankServiceTests()
        {
            _service = new CustomerRankService();
        }

        [TestMethod()]
        public void AddOrUpdateScoreToCustomerTest()
        {
            //test case for params verify
            long customerId = 1;
            decimal score = 10000;
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => _service.AddOrUpdateScoreToCustomer(customerId, score));

            //test case for add customer.
            customerId = 1;
            score = 100;
            Assert.AreEqual(100, _service.AddOrUpdateScoreToCustomer(customerId, score));

            //test case for update customer.
            Assert.AreEqual(200, _service.AddOrUpdateScoreToCustomer(customerId, score));
            Assert.AreEqual(50, _service.AddOrUpdateScoreToCustomer(customerId, -150));

        }

        [TestMethod()]
        public void GetCustomersByRankTest()
        {
            //test case for params verify
            int start=0, end=0;
            Assert.ThrowsException<ArgumentException>(()=>_service.GetCustomersByRank(start, end));

            start = 3; end = 1;
            Assert.ThrowsException<ArgumentException>(() => _service.GetCustomersByRank(start, end));

            //init test data
            for (int i = 0; i < 10; i++)
            {
                long customerId = i;
                decimal score = 10 + i;
                _service.AddOrUpdateScoreToCustomer(customerId,score);
            }

            //test case for order ascending
            var list = _service.GetCustomersByRank(1, 10);
            Assert.AreEqual(10, list.Count);
            var lowest = list[0];
            Assert.AreEqual(10, lowest.Score);
            Assert.AreEqual(1, lowest.Rank);

            var highest = list[9];
            Assert.AreEqual(19, highest.Score);
            Assert.AreEqual(10, highest.Rank);
        }

        [TestMethod()]
        public void GetCustomersByIdTest()
        {
            //test case for params verify
            Assert.ThrowsException<NotFoundException>(() => _service.GetCustomersById(customerId:10000));

            //init test data
            for (int i = 0; i < 10; i++)
            {
                long customerId = i;
                decimal score = 10 + i;
                _service.AddOrUpdateScoreToCustomer(customerId, score);
            }

            //test case for order ascending
            var list = _service.GetCustomersById(5,2,3);
            Assert.AreEqual(6, list.Count);
            var lowest = list[0];
            Assert.AreEqual(13, lowest.Score);
            Assert.AreEqual(4, lowest.Rank);

            var highest = list[5];
            Assert.AreEqual(18, highest.Score);
            Assert.AreEqual(9, highest.Rank);
        }

        [TestMethod()]
        public void LeaderboardCompareTest()
        {
            LeaderboardCompare compare = new LeaderboardCompare();
            var lowest = (1, 10);
            var highest= (2, 10);
            Assert.AreEqual(-1, compare.Compare(lowest, highest));
        }

        [TestMethod()]
        public void GetCustomersByRankTest_with_same_score()
        {
            //init test data
            var lowest = (1, 10);
            var highest = (2, 10);
            _service.AddOrUpdateScoreToCustomer(lowest.Item1,lowest.Item2);
            _service.AddOrUpdateScoreToCustomer(highest.Item1,highest.Item2);

            var list = _service.GetCustomersByRank(1, 2);
            Assert.AreEqual(2, list.Count);

            //test case for if two customers has same score, the customerId should be first which someone is lower.
            Assert.AreEqual(lowest.Item2,list[0].Score);
            Assert.AreEqual(lowest.Item1, list[0].CustomerId);
            Assert.AreEqual(1, list[0].Rank);

            Assert.AreEqual(highest.Item2, list[1].Score);
            Assert.AreEqual(highest.Item1, list[1].CustomerId);
            Assert.AreEqual(2, list[1].Rank);
        }
    }
}