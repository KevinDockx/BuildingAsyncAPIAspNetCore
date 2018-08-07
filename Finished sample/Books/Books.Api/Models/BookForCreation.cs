using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Books.Api.Models
{
    public class BookForCreation
    {
        public Guid AuthorId { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }
    }
}
