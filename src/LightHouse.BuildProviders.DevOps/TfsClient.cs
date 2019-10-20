﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Flurl.Http;
using LightHouse.Lib;
using Serilog;

namespace LightHouse.BuildProviders.DevOps
{
    public class TfsClient : IProvideBuilds
    {
        private readonly List<string> _urls;
        private readonly ILogger _logger;
        private readonly IMapper _mapper;
        private readonly string _userName;
        private readonly string _accessToken;

        public TfsClient(ILogger logger, IMapper mapper, string userName, string accessToken, string instance, string collection, IEnumerable<string> teamProjects)
        {
            _logger = logger;
            _mapper = mapper;
            _userName = userName;
            _accessToken = accessToken;
            _urls = new List<string>();

            foreach (var teamProject in teamProjects)
            {
                _urls.Add($"http://{instance.Trim()}/{collection.Trim()}/{teamProject.Trim()}/_apis/");
            }
        }

        public async Task<List<Build>> GetAllBuilds()
        {
            try
            {
                var responses = new List<BuildDefinitionsResponse>();

                foreach (var tfsUrl in _urls)
                {
                    var request = tfsUrl
                        .WithBasicAuth(_userName, _accessToken)
                        .AppendPathSegment("build")
                        .AppendPathSegment("Definitions")
                        .SetQueryParam("includeLatestBuilds", true);

                    _logger.Information($"Getting build definitions from url {request.Url}");

                    var response = await request.GetJsonAsync<BuildDefinitionsResponse>();

                    responses.Add(response);
                }

                return responses
                    .SelectMany(response => response.BuildDefinitions.Select(_mapper.Map<BuildDefinition, Build>))
                    .ToList();
            }
            catch (FlurlHttpTimeoutException)
            {
                // FlurlHttpTimeoutException derives from FlurlHttpException; catch here only
                // if you want to handle timeouts as a special case
                _logger.Error("Request timed out.");
            }
            catch (FlurlHttpException ex)
            {
                // ex.Message contains rich details, inclulding the URL, verb, response status,
                // and request and response bodies (if available)
                _logger.Error($"Calling {ex.Call.FlurlRequest.Url} returned the following error:");
                _logger.Error(ex.Message);
                _logger.Error($"Status code: {ex.Call.HttpStatus.ToString()}");
                _logger.Error($"Request Body: {ex.Call.RequestBody}");
                _logger.Error($"Response Body: {await ex.GetResponseStringAsync()}");
            }

            return new List<Build>();
        }
    }
}