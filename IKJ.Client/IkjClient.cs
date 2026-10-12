using System.Net;
using System.Text;
using HelseId.Library;
using HelseId.Library.Interfaces.JwtTokens;
using HelseId.Library.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace IKJ.Client;

public class IkjClient
{
    private const string ParentOrgNumber = "315945062"; // NHN's synthetic organization number

    private readonly IDPoPProofCreatorForApiRequests _dPoPProofCreator;
    private readonly FhirJsonDeserializer _deserializer;
    private readonly IHttpClientFactory _httpClientFactory;

    private readonly AccessTokenKeeper _tokenKeeper;

    public IkjClient(
        IDPoPProofCreatorForApiRequests dPoPProofCreator,
        AccessTokenKeeper tokenKeeper,
        IHttpClientFactory httpClientFactory,
        FhirJsonDeserializer deserializer)
    {
        _dPoPProofCreator = dPoPProofCreator;
        _httpClientFactory = httpClientFactory;
        _deserializer = deserializer;
        _tokenKeeper = tokenKeeper;
    }

    /// <summary>
    ///     Sends HTTP request to the API endpoint.
    ///     - handles retrieving access token(s) from HelseID.
    ///     - creates DPoP proof per request
    ///     For demonstration purposes only.
    ///     - exceptions are not handled gracefully
    ///     - HTTP responses with status code outside 2xx range leads to exception being thrown.
    /// </summary>
    /// <param name="method">HTTP method</param>
    /// <param name="url">Request URL, *not* including the querystring part</param>
    /// <param name="queryString">Querystring</param>
    /// <param name="bodyContents">Request body, JSON</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        string? queryString,
        string? bodyContents,
        CancellationToken ct)
    {
        var requestUri = string.IsNullOrEmpty(queryString)
            ? new Uri(url)
            : new Uri($"{url}?{queryString}");

        var contents = string.IsNullOrEmpty(bodyContents)
            ? null
            : new StringContent(bodyContents, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage
        {
            Method = method,
            RequestUri = requestUri,
            Content = contents
        };

        var accessToken = await GetAccessToken();
        var dPopProof = await _dPoPProofCreator.CreateDPoPProofForApiRequest(method, url, accessToken);
        request.SetDPoPTokenAndProof(accessToken, dPopProof);

        request.Headers.Add("nhn-source-system", "KomSat.Samples");
        request.Headers.Add("Accept", "application/json");

        var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var json = await response.Content.ReadAsStringAsync(ct);
            var outcome = _deserializer.Deserialize<OperationOutcome>(json);
            throw new Exception(outcome.ToString());
        }

        response.EnsureSuccessStatusCode();
        return response;
    }


    private async Task<AccessTokenResponse> GetAccessToken()
    {
        var existingToken = _tokenKeeper.GetReusableToken();
        if (existingToken is not null)
        {
            return existingToken;
        }

        return await _tokenKeeper.GetNewToken(ParentOrgNumber);
    }
}
