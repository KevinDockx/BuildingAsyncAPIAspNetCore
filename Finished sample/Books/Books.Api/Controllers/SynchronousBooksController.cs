using Books.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Books.Api.Controllers
{
    [Route("api/synchronousbooks")]
    [ApiController]
    public class SynchronousBooksController : ControllerBase
    {
        private IBooksRepository _booksRepository;

        public SynchronousBooksController(IBooksRepository booksRepository)
        {
            _booksRepository = booksRepository ?? 
                throw new ArgumentNullException(nameof(booksRepository));
        }

        [HttpGet]
        public IActionResult GetBooks()
        {
            // Pitfall #2: blocking asynchronous code
            // var bookEntities = _booksRepository.GetBooksAsync().Result;
            // // _booksRepository.GetBooksAsync().Wait();

            var bookEntities = _booksRepository.GetBooks();
            return Ok(bookEntities);
        }
    }
}
