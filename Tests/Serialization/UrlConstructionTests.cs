﻿using System;
using System.Collections.Generic;
using System.Linq;
using Saule;
using Saule.Queries.Pagination;
using Saule.Serialization;
using Tests.Helpers;
using Tests.Models;
using Xunit;
using Xunit.Abstractions;

namespace Tests.Serialization
{
    public class UrlConstructionTests
    {
        private readonly ITestOutputHelper _output;

        private static Uri DefaultUrl => new Uri("http://example.com/");
        private static IUrlPathBuilder DefaultPathBuilder => new DefaultUrlPathBuilder("/api");

        public UrlConstructionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact(DisplayName = "Handles query parameters correctly")]
        public void HandlesQueryParams()
        {
            var target = new ResourceSerializer(Get.Person(), new PersonResource(),
                GetUri("123", "a=b&c=d"), DefaultPathBuilder, null);
            var result = target.Serialize();
            _output.WriteLine(result.ToString());

            var jobLinks = result["data"]?["relationships"]?["job"]?["links"];

            var selfLink = result["links"].Value<Uri>("self")?.PathAndQuery;
            var jobSelfLink = jobLinks?.Value<Uri>("self")?.PathAndQuery;
            var jobRelationLink = jobLinks?.Value<Uri>("related")?.PathAndQuery;

            Assert.Equal("/api/people/123?a=b&c=d", selfLink);
            Assert.Equal("/api/people/123/relationships/employer/", jobSelfLink);
            Assert.Equal("/api/people/123/employer/", jobRelationLink);
        }

        [Fact(DisplayName = "Items have self links in a collection")]
        public void SelfLinksInCollection()
        {
            var people = Get.People(5);
            var target = new ResourceSerializer(people, new PersonResource(),
                GetUri(), DefaultPathBuilder, null);
            var result = target.Serialize();
            _output.WriteLine(result.ToString());

            foreach (var elem in result["data"])
            {
                var links = elem["links"];
                Assert.NotNull(links);
                Assert.Equal("/api/people/" + elem.Value<string>("id") + "/", links.Value<Uri>("self").AbsolutePath);
            }
        }

        [Fact(DisplayName = "Item does not have self link in single element")]
        public void NoSelfLinksInObject()
        {
            var target = new ResourceSerializer(Get.Person(), new PersonResource(),
                GetUri("123"), DefaultPathBuilder, null);
            var result = target.Serialize();
            _output.WriteLine(result.ToString());

            var links = result["data"]?["links"];

            Assert.Null(links);
        }

        [Fact(DisplayName = "Adds top level self link")]
        public void SelfLink()
        {
            var target = new ResourceSerializer(Get.Person(), new PersonResource(),
                GetUri("123"), DefaultPathBuilder, null);
            var result = target.Serialize();
            _output.WriteLine(result.ToString());

            var selfLink = result["links"].Value<Uri>("self").AbsolutePath;

            Assert.Equal("/api/people/123", selfLink);
        }

        [Fact(DisplayName = "Adds next link only if needed")]
        public void NextLink()
        {
            var people = Get.People(5);
            var target = new ResourceSerializer(people, new PersonResource(),
                GetUri(), DefaultPathBuilder,
                new PaginationContext(GetQuery(Constants.PageNumberQueryName, "2"), perPage: 10));
            var result = target.Serialize();
            _output.WriteLine(result.ToString());

            Assert.Equal(null, result["links"]["next"]);

            target = new ResourceSerializer(people, new PersonResource(),
                GetUri(), DefaultPathBuilder,
                new PaginationContext(GetQuery(Constants.PageNumberQueryName, "2"), perPage: 4));
            result = target.Serialize();

            var nextLink = Uri.UnescapeDataString(result["links"].Value<Uri>("next").Query);
            Assert.Equal("?page[number]=3", nextLink);
        }

        [Fact(DisplayName = "Adds previous link only if needed")]
        public void PreviousLink()
        {
            var people = Get.People(5);
            var target = new ResourceSerializer(people, new PersonResource(),
                GetUri(), DefaultPathBuilder,
                new PaginationContext(GetQuery(Constants.PageNumberQueryName, "0"), perPage: 10));
            var result = target.Serialize();
            _output.WriteLine(result.ToString());

            Assert.Equal(null, result["links"]["prev"]);

            target = new ResourceSerializer(people, new PersonResource(),
                GetUri(), DefaultPathBuilder,
                new PaginationContext(GetQuery(Constants.PageNumberQueryName, "1"), perPage: 10));
            result = target.Serialize();

            var nextLink = Uri.UnescapeDataString(result["links"].Value<Uri>("prev").Query);
            Assert.Equal("?page[number]=0", nextLink);
        }

        [Fact(DisplayName = "Keeps other query parameters when paginating")]
        public void PaginationQueryParams()
        {
            var people = Get.People(5);
            var target = new ResourceSerializer(people, new PersonResource(),
                GetUri(query: "q=a"), DefaultPathBuilder,
                new PaginationContext(GetQuery("q", "a"), perPage: 4));

            var result = target.Serialize();
            _output.WriteLine(result.ToString());

            var nextLink = Uri.UnescapeDataString(result["links"].Value<Uri>("next").Query);
            Assert.Equal("?q=a&page[number]=1", nextLink);
        }

        [Fact(DisplayName = "Adds first link if paginating")]
        public void FirstLink()
        {
            var people = Get.People(5);
            var target = new ResourceSerializer(people, new PersonResource(),
               GetUri(), DefaultPathBuilder,
                new PaginationContext(Enumerable.Empty<KeyValuePair<string, string>>(), perPage: 4));

            var result = target.Serialize();
            _output.WriteLine(result.ToString());

            var nextLink = Uri.UnescapeDataString(result["links"].Value<Uri>("first").Query);
            Assert.Equal("?page[number]=0", nextLink);
        }

        [Fact(DisplayName = "Serializes relationships' links")]
        public void SerializesRelationshipLinks()
        {
            var target = new ResourceSerializer(Get.Person(), new PersonResource(),
                GetUri("123"), DefaultPathBuilder, null);
            var result = target.Serialize();
            _output.WriteLine(result.ToString());

            var relationships = result["data"]["relationships"];
            var job = relationships["job"];
            var friends = relationships["friends"];

            Assert.Equal("/api/people/123/employer/", job["links"].Value<Uri>("related").AbsolutePath);
            Assert.Equal("/api/people/123/relationships/employer/", job["links"].Value<Uri>("self").AbsolutePath);

            Assert.Equal("/api/people/123/friends/", friends["links"].Value<Uri>("related").AbsolutePath);
            Assert.Equal("/api/people/123/relationships/friends/", friends["links"].Value<Uri>("self").AbsolutePath);
        }

        [Fact(DisplayName = "Supports multiple url builders")]
        public void SerializeDifferentBuilder()
        {
            var target = new ResourceSerializer(Get.Person(), new PersonResource(),
                GetUri("123"), new CanonicalUrlPathBuilder(), null);
            var result = target.Serialize();
            _output.WriteLine(result.ToString());

            var relationships = result["data"]["relationships"];
            var job = relationships["job"];
            var friends = relationships["friends"];

            Assert.Equal("/corporations/456/", job["links"].Value<Uri>("related").AbsolutePath);
            Assert.Equal("/people/123/relationships/employer/", job["links"].Value<Uri>("self").AbsolutePath);

            Assert.Equal("/people/123/relationships/friends/", friends["links"].Value<Uri>("self").AbsolutePath);
            Assert.Null(friends["links"]["relationships"]);
        }

        [Fact(DisplayName = "Builds absolute links correctly")]
        public void BuildsRightLinks()
        {
            var target = new ResourceSerializer(Get.Person(), new PersonResource(),
                GetUri("123"), DefaultPathBuilder, null);
            var result = target.Serialize();
            _output.WriteLine(result.ToString());

            var job = result["data"]["relationships"]["job"];

            Assert.Equal("http://example.com/api/people/123/employer/",
                job["links"].Value<Uri>("related").ToString());
            Assert.Equal("http://example.com/api/people/123/relationships/employer/",
                job["links"].Value<Uri>("self").ToString());
        }

        [Fact(DisplayName = "Does not generate links when url builder returns nothing")]
        public void UrlBuilder()
        {
            var target = new ResourceSerializer(Get.Person(), new PersonResource(),
                GetUri("123"), new EmptyUrlBuilder(), null);
            var result = target.Serialize();
            _output.WriteLine(result.ToString());

            var job = result["data"]["relationships"]["job"];

            Assert.Equal(null,
                job["links"]["related"]);
            Assert.Equal(null,
                job["links"]["self"]);
        }

        private static IEnumerable<KeyValuePair<string, string>> GetQuery(string key, string value)
        {
            yield return new KeyValuePair<string, string>(key, value);
        }

        private static Uri GetUri(string id = null, string query = null)
        {
            var path = "/api/people/";
            if (id != null) path += id;
            if (query != null) path += "?" + query;

            return new Uri(DefaultUrl, path);
        }

        private class EmptyUrlBuilder : IUrlPathBuilder
        {
            public string BuildCanonicalPath(ApiResource resource)
            {
                return string.Empty;
            }

            public string BuildCanonicalPath(ApiResource resource, string id)
            {
                return string.Empty;
            }

            public string BuildRelationshipPath(ApiResource resource, string id, ResourceRelationship relationship)
            {
                return string.Empty;
            }

            public string BuildRelationshipPath(ApiResource resource, string id, ResourceRelationship relationship,
                string relatedResourceId)
            {
                return string.Empty;
            }
        }
    }
}
