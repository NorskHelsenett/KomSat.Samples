using HelseId.Library.ClientCredentials.Interfaces;
using HelseId.Library.ExtensionMethods;
using HelseId.Library.Models;
using HelseId.Library.Models.DetailsFromClient;

namespace IKJ.Client;

public class AccessTokenKeeper
{
    private const int MarginS = 10;

    private readonly IHelseIdClientCredentialsFlow _clientCredentialsFlow;

    private DateTime _receivedAt = DateTime.MinValue;
    private AccessTokenResponse? _response;

    public AccessTokenKeeper(IHelseIdClientCredentialsFlow clientCredentialsFlow)
    {
        _clientCredentialsFlow = clientCredentialsFlow;
    }

    /// <summary>
    ///     If an access token has been retrieved and cached, and is not close to expiry, then this token is returned.
    ///     If not, null is returned.
    /// </summary>
    /// <returns></returns>
    public AccessTokenResponse? GetReusableToken()
    {
        if (_response is null)
        {
            return null;
        }

        var validFor = _response.ExpiresIn;
        var timeSinceIssued = DateTime.UtcNow - _receivedAt;

        if (timeSinceIssued.TotalSeconds + MarginS < validFor)
        {
            return _response;
        }

        Console.WriteLine(
            "Access token is getting old, must get a new one: validFor={0}s, timeSinceIssued={1:F0}s",
            validFor,
            timeSinceIssued.TotalSeconds);
        return null;
    }

    /// <summary>
    ///     Retrieves access token from HelseID.
    /// </summary>
    /// <param name="parentOrgNumber"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<AccessTokenResponse> GetNewToken(string parentOrgNumber)
    {
        /*
         * Specifies the organization you are acting on behalf of.
         *  - In Test you can use HelseID self-service delegation simulation to give your client access (under Tenants)
         *  - The org.number specified here is included in access token as claim helseid://claims/client/claims/orgnr_parent
         *  - The org.number of your client is included in access token as claim helseid://claims/client/claims/orgnr_supplier
         */
        var organizationNumbers = new OrganizationNumbers
        {
            ParentOrganization = parentOrgNumber
        };

        var tokenResponse = await _clientCredentialsFlow.GetTokenResponseAsync(organizationNumbers);
        if (!tokenResponse.IsSuccessful(out var accessTokenResponse))
        {
            await Console.Error.WriteLineAsync("Access token retrieval failed!");

            var errorResponse = tokenResponse.AsError();
            throw new Exception(errorResponse.Error + " " + errorResponse.ErrorDescription);
        }

        _receivedAt = DateTime.UtcNow;
        _response = accessTokenResponse;
        return _response;
    }
}
