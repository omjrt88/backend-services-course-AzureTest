using AdventureWorks.Models;
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using WebServer.Data;
using WebServer.ViewModels;

namespace WebServer.Controllers
{

    [ApiController]
    [Route ("/api/[Controller]")]
    public class ProductsController : Controller {

        public ProductRepository Repository { get; private set; }
        public LinkGenerator LinkGenerator { get; }

        // Dependency Injection
        public ProductsController (IHostingEnvironment environment, 
            IConfiguration configuration, ProductRepository repository, LinkGenerator linkGenerator) 
        {
            this.Repository = repository;
            LinkGenerator = linkGenerator;
        }

        [HttpGet]
        [EnableQuery]
        public ObjectResult Get ()
        {
            var data = Repository.Get()
								.Include(p => p.ProductModel)
								.Include(p => p.ProductCategory)
								.AsQueryable();

            // https://localhost:5001/api/products?$select=Color,Name
            // https://localhost:5001/api/products?$select=Color,Name&$orderby=Name%20desc
            // https://localhost:5001/api/products?$select=Color,Name&$orderby=Name%20desc&$filter=contains(Name,'Women')
            // https://www.odata.org/documentation/
            // https://www.odata.org/getting-started/basic-tutorial/
        
            var result = new ObjectResult(
							data.Select(p => new
							{
								p.Rowguid,
								p.SellEndDate,
								p.SellStartDate,
								p.Size,
                                p.Color,
								p.StandardCost,
								p.Weight,
								p.ProductModel.Name,
								Description = p.ProductModel.CatalogDescription,
								Catalog = p.ProductModel.CatalogDescription,
								Category = new
								{
									p.ProductCategory.Name,
									ParentCategory = p.ProductCategory.ParentProductCategory.Name
								}
							}).ToArray())
            {
                StatusCode = (int)HttpStatusCode.OK
            };

            Response.Headers.Add("X-Total-Count", new StringValues(data.Count().ToString()));
            Response.Headers.Add("page-size", new StringValues(5.ToString()));

            return result;
        }

        [HttpGet("GetByName/{name}")]
        public IActionResult GetByName(string name)
        {
            return Ok(Repository.Get().First());
        }

        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            var match = Repository.Get(id);

            if (match == null)
            {
                return NotFound();
            }

            //BP-03 Tip: HATEOAS & Linking 
            
            var categoryUrl = match.ProductCategoryId.HasValue ? 
                              UrlUtils.GetControllerAbsoluteUrl(Request, LinkGenerator,
                                nameof(CategoriesController.GetById), CategoriesController.CONTROLLER_NAME,
                                new { id = match.ProductCategoryId })
                              : null;
            var links = new List<Link>();

            dynamic result = new
            {
                match.Color,
                match.ListPrice,
                match.ModifiedDate,
                match.Name,
                Category = match.ProductCategory.Name,
                Links = links
            };

            if (!string.IsNullOrEmpty(result.Category))
            {
                links.Add(new Link
                {
                    Href = categoryUrl,
                    Rel = "self",
                    Method = "GET"
                });
            }

            return Ok(result);
        }

        [HttpGet("[Controller]/{id}/thumbnail")]
        public ActionResult<string> GetThumbnail(int id)
        {
            var match = Repository.Get(id)?.ThumbNailPhoto;

            if (match == null)
            {
                return NotFound();
            }
            //BP-02 Tip: Content-resolution
            //Alternatively: byte[] decodedBytes = Convert.FromBase64String(Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(match)));
            return File(new System.IO.MemoryStream(match), "image/jpeg");
        }

        [HttpPost]
        public IActionResult Post([FromBody] ProductViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }
            var id = Repository.Add(viewModel);

            return CreatedAtAction(nameof(GetById), new { Id = id}, viewModel);
        }

        [HttpPut("{id}")]
        public IActionResult Put([FromRoute] int id, [FromBody] Product viewModel)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }
            var model = Repository.Get(id);

            if (model == null)
            {
                return NotFound();
            }
            model.Name = viewModel.Name;
            model.Color = viewModel.Color;

            Repository.Update(id, model);

            return Ok();
        }

        [HttpDelete("{id}")]
        public IActionResult Delete([FromRoute] int id)
        {
            if (Repository.Get(id) == null)
            {
                return NotFound();
            }
            Repository.Delete(id);

            return Ok();
        }
    }
}
 