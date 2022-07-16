using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;

using Models;
using System;
using Microsoft.Azure.Cosmos;
using System.Threading.Tasks;
using StaticWebAppAuthentication.Api;
using StaticWebAppAuthentication.Models;
using System.Net;

namespace CosmosDBTest;

public class BlogPosts
{
    public static object UriFactory { get; private set; }

    [Function($"{nameof(BlogPosts)}_Get")]
    public async Task<HttpResponseData> GetAllBlogPosts(
        [HttpTrigger(AuthorizationLevel.Anonymous,
            "get", Route = "blogposts")] HttpRequestData req,
        [CosmosDBInput("SwaBlog", "BlogContainer",
            Connection = "CosmosDbConnectionString",
            SqlQuery = @"
                SELECT
                c.id,
                c.Title,
                c.Author,
                c.PublishedDate,
                LEFT(c.BlogPostMarkdown, 500)
                		As BlogPostMarkdown,
                Length(c.BlogPostMarkdown) <= 500
                		As PreviewIsComplete,
                c.Tags
                FROM c
                WHERE c.Status = 2")
            ] IEnumerable<BlogPost> blogPosts,
        ILogger log)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(blogPosts);

        return response;
    }

    [Function($"{nameof(BlogPosts)}_GetId")]
    public IActionResult GetBlogPost(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get",
                Route = "blogposts/{id}")]
                HttpRequestData req,
        [CosmosDBInput("SwaBlog", "BlogContainer",
                Connection = "CosmosDbConnectionString",
                SqlQuery = @"SELECT
                    c.id,
                    c.Title,
                    c.Author,
                    c.PublishedDate,
                    c.BlogPostMarkdown,
                    c.Status,
                    c.Tags
                    FROM c
                    WHERE c.id = {id}")
            ] IEnumerable<BlogPost> blogposts,
        ILogger log)
    {
        if (blogposts.ToArray().Length == 0)
        {
            return new NotFoundResult();
        }

        return new OkObjectResult(blogposts.First());
    }

    [Function($"{nameof(BlogPosts)}_Post")]
    public IActionResult PostBlogPost(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post",
            Route = "blogposts")]
            BlogPost blogPost,
            HttpRequestData request,
        [CosmosDBInput("SwaBlog", "BlogContainer",
            Connection = "CosmosDbConnectionString")]
            out dynamic document,
        ILogger log)
    {
        ClientPrincipal cp = StaticWebApiAppAuthorization.ParseHttpHeaderForClientPrinciple(request.Headers);

        if (blogPost.Id != null)
        {
            document = null;
            return new BadRequestResult();
        }

        blogPost.Id = Guid.NewGuid();
        blogPost.Author = cp.UserDetails;

        document = new
        {
            id = blogPost.Id.ToString(),
            Title = blogPost.Title,
            Author = blogPost.Author,
            PublishedDate = blogPost.PublishedDate,
            Tags = blogPost.Tags,
            BlogPostMarkdown = blogPost.BlogPostMarkdown,
            Status = 2
        };

        return new OkObjectResult(blogPost);
    }

    [Function($"{nameof(BlogPosts)}_Put")]
    public IActionResult PutBlogPost(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put",
            Route = "blogposts")]
            BlogPost blogPost,
        [CosmosDBInput("SwaBlog",
                "BlogContainer",
                Connection = "CosmosDbConnectionString",
                Id = "{Id}",
                PartitionKey = "{Author}")] BlogPost bp,
        [CosmosDBInput("SwaBlog", "BlogContainer",
            Connection = "CosmosDbConnectionString")]out dynamic document,
        ILogger log)
    {
        if (bp is null)
        {
            document = null;
            return new NotFoundResult();
        }

        document = new
            {
                id = blogPost.Id.ToString(),
                Title = blogPost.Title,
                Author = blogPost.Author,
                PublishedDate = blogPost.PublishedDate,
                Tags = blogPost.Tags,
                BlogPostMarkdown = blogPost.BlogPostMarkdown,
                Status = 2
            };

        return new NoContentResult();
    }

    [Function($"{nameof(BlogPosts)}_Delete")]
    public async Task<IActionResult> DeleteBlogPost(
    [HttpTrigger(AuthorizationLevel.Anonymous, "delete",
            Route = "blogposts/{id}/{author}")]
            HttpRequest request,
            string id,
            string author,
    [CosmosDBInput("SwaBlog",
            "BlogContainer",
            Connection = "CosmosDbConnectionString",
            Id = "{id}",
            PartitionKey = "{author}")] BlogPost bp,
    [CosmosDBInput(
            databaseName: "SwaBlog",
            containerName: "BlogContainer",
            Connection = "CosmosDbConnectionString")] CosmosClient client,

    ILogger log)
    {
        if (bp is null)
        {
            return new NoContentResult();
        }

        Container container = client.GetDatabase("SwaBlog").GetContainer("BlogContainer");
        await container.DeleteItemAsync<BlogPost>(id, new PartitionKey(author));

        return new NoContentResult();
    }
}
