using InterviewProject.Service;
using Microsoft.AspNetCore.Mvc;

namespace InterviewProject.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LeaderboardController : Controller
    {
        private ICustomerRankService _customerRankService;

        public LeaderboardController(ICustomerRankService customerRankService)
        {
            _customerRankService = customerRankService;
        }

        [HttpPost("customer/{customerId}/score/{score}")]
        public IActionResult UpdateCustomer(long customerId, decimal score)
        {
           var updatedScore=_customerRankService.AddOrUpdateScoreToCustomer(customerId, score);
            return Ok(updatedScore);
        }

        [HttpGet("leaderboard")]
        public IActionResult GetCustomersByRank(int start,int end)
        {
            var customers=_customerRankService.GetCustomersByRank(start,end);
            return Ok(customers);
        }

        [HttpGet("leaderboard/{customerId}")]
        public IActionResult GetCustomersById(long customerId, int high = 0, int low = 0)
        {
            var customers=_customerRankService.GetCustomersById(customerId, high, low);
            return Ok(customers);
        }
    }
}
