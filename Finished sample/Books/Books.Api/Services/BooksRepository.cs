using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Books.Api.Contexts;
using Books.Api.Entities;
using Books.Api.ExternalModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Books.Api.Services
{
    public class BooksRepository : IBooksRepository, IDisposable
    {
        private BooksContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<BooksRepository> _logger;
        private CancellationTokenSource _cancellationTokenSource;

        public BooksRepository(BooksContext context, IHttpClientFactory httpClientFactory,
              ILogger<BooksRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _httpClientFactory = httpClientFactory ??
                throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Book> GetBookAsync(Guid id)
        {
            // Pitfall #1: using Task.Run() on the server
            //_logger.LogInformation($"ThreadId when entering GetBookAsync: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
            // var bookPages = await GetBookPages();

            return await _context.Books.Include(b => b.Author)
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        // Pitfall #1: using Task.Run() on the server
        //private Task<int> GetBookPages()
        //{
        //    return Task.Run(() =>
        //    {
        //        _logger.LogInformation($"ThreadId when calculating the amount of pages: {System.Threading.Thread.CurrentThread.ManagedThreadId}");

        //        var pageCalculator = new Books.Legacy.ComplicatedPageCalculator();
        //        return pageCalculator.CalculateBookPages();
        //    });
        //}

        public async Task<IEnumerable<Book>> GetBooksAsync()
        {
            await _context.Database.ExecuteSqlCommandAsync("WAITFOR DELAY '00:00:02';");
            return await _context.Books.Include(b => b.Author).ToListAsync();
        }

        public async Task<IEnumerable<Entities.Book>> GetBooksAsync(IEnumerable<Guid> bookIds)
        {
            return await _context.Books.Where(b => bookIds.Contains(b.Id))
                .Include(b => b.Author).ToListAsync();
        }

        public async Task<BookCover> GetBookCoverAsync(string coverId)
        {
            var httpClient = _httpClientFactory.CreateClient();

            // pass through a dummy name
            var response = await httpClient
                   .GetAsync($"http://localhost:52644/api/bookcovers/{coverId}");

            if (response.IsSuccessStatusCode)
            {
                return JsonConvert.DeserializeObject<BookCover>(
                    await response.Content.ReadAsStringAsync());
            }

            return null;

        }

     // Piftall #3: modifying shared state
     //   // note: using HttpClient directly for readability purposes. 
     //   // It's better to initialize the client via _httpClientFactory, 
     //   // eg on constructing

     //   private HttpClient _httpClient = new HttpClient();

     //   public async Task<IEnumerable<BookCover>> DownloadBookCoverAsync(Guid bookId)
     //   {
     //       var bookCoverUrls = new[]
     //       {
     //    $"http://localhost:52644/api/bookcovers/{bookId}-dummycover1",
     //    $"http://localhost:52644/api/bookcovers/{bookId}-dummycover2"
     //};

     //       var bookCovers = new List<BookCover>();
     //       var downloadTask1 = DownloadBookCoverAsync(bookCoverUrls[0], bookCovers);
     //       var downloadTask2 = DownloadBookCoverAsync(bookCoverUrls[1], bookCovers);
     //       await Task.WhenAll(downloadTask1, downloadTask2);
     //       return bookCovers;
     //   }

     //   private async Task DownloadBookCoverAsync(string bookCoverUrl, List<BookCover> bookCovers)
     //   {
     //       var response = await _httpClient.GetAsync(bookCoverUrl);
     //       var bookCover = JsonConvert.DeserializeObject<BookCover>(
     //               await response.Content.ReadAsStringAsync());

     //       bookCovers.Add(bookCover);
     //   }


        public async Task<IEnumerable<BookCover>> GetBookCoversAsync(Guid bookId)
        {
            var httpClient = _httpClientFactory.CreateClient();
            var bookCovers = new List<BookCover>();
            _cancellationTokenSource = new CancellationTokenSource();

            // create a list of fake bookcovers
            var bookCoverUrls = new[]
            {
                $"http://localhost:52644/api/bookcovers/{bookId}-dummycover1",
              //  $"http://localhost:52644/api/bookcovers/{bookId}-dummycover2?returnFault=true",
                $"http://localhost:52644/api/bookcovers/{bookId}-dummycover3",
                $"http://localhost:52644/api/bookcovers/{bookId}-dummycover4",
                $"http://localhost:52644/api/bookcovers/{bookId}-dummycover5"
            };

            //foreach (var bookCoverUrl in bookCoverUrls)
            //{
            //    var response = await httpClient
            //       .GetAsync(bookCoverUrl);

            //    if (response.IsSuccessStatusCode)
            //    {
            //        bookCovers.Add(JsonConvert.DeserializeObject<BookCover>(
            //            await response.Content.ReadAsStringAsync()));
            //    }
            //}

            // create the tasks
            var downloadBookCoverTasksQuery =
                 from bookCoverUrl
                 in bookCoverUrls
                 select DownloadBookCoverAsync(httpClient, bookCoverUrl, _cancellationTokenSource.Token);

            // start the tasks
            var downloadBookCoverTasks = downloadBookCoverTasksQuery.ToList();

            try
            {
                return await Task.WhenAll(downloadBookCoverTasks);
            }
            catch (OperationCanceledException operationCanceledException)
            {
                _logger.LogInformation($"{operationCanceledException.Message}");
                foreach (var task in downloadBookCoverTasks)
                {
                    _logger.LogInformation($"Task {task.Id} has status {task.Status}");
                }

                return new List<BookCover>();
            }
            catch (Exception exception)
            {
                _logger.LogError($"{exception.Message}");
                throw;
            }
        }

        private async Task<BookCover> DownloadBookCoverAsync(
        HttpClient httpClient, string bookCoverUrl, CancellationToken cancellationToken)
        {
          //  throw new Exception("Cannot download book cover, writer isn't finishing book fast enough.");

            var response = await httpClient
                       .GetAsync(bookCoverUrl, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var bookCover = JsonConvert.DeserializeObject<BookCover>(
                    await response.Content.ReadAsStringAsync());
                return bookCover;
            }

            _cancellationTokenSource.Cancel();

            return null;
        }



        public IEnumerable<Book> GetBooks()
        {
            _context.Database.ExecuteSqlCommand("WAITFOR DELAY '00:00:02';");
            return _context.Books.Include(b => b.Author).ToList();
        }

        public void AddBook(Book bookToAdd)
        {
            if (bookToAdd == null)
            {
                throw new ArgumentNullException(nameof(bookToAdd));
            }

            _context.Add(bookToAdd);
        }

        public async Task<bool> SaveChangesAsync()
        {
            // return true if 1 or more entities were changed
            return (await _context.SaveChangesAsync() > 0);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_context != null)
                {
                    _context.Dispose();
                    _context = null;
                }
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }
            }
        }


    }
}
