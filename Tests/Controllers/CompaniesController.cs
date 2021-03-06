﻿using System.Collections.Generic;
using System.Web.Http;
using Saule.Http;
using Tests.Helpers;
using Tests.Models;

namespace Tests.Controllers
{
    [ReturnsResource(typeof(CompanyResource))]
    [RoutePrefix("api")]
    public class CompaniesController : ApiController
    {
        [HttpGet]
        [Route("companies/{id}")]
        public Company GetCompany(string id)
        {
            var company = Get.Company(id);
            company.Location = LocationType.National;

            return company;
        }

        [HttpGet]
        [Paginated(PerPage = 12)]
        [Route("companies")]
        public IEnumerable<Company> GetCompanies()
        {
            return Get.Companies(100);
        }
    }
}
