using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace IKJ.Client;

public interface IIkjApi
{
    /// <summary>
    ///     Reads resource from IKJ by ID.
    /// </summary>
    /// <param name="id">Resource ID</param>
    /// <param name="ct"></param>
    /// <typeparam name="T">Resource type</typeparam>
    /// <returns>Resource</returns>
    Task<T> GetResourceByIdAsync<T>(string id, CancellationToken ct) where T : Resource;

    /// <summary>
    ///     Gets resource from IKJ by identifier.
    /// </summary>
    /// <param name="system">Identifier system</param>
    /// <param name="value">Identifier value</param>
    /// <param name="ct"></param>
    /// <typeparam name="T">Resource type</typeparam>
    /// <returns>Resource if found, or null</returns>
    Task<T?> GetResourceByIdentifierAsync<T>(
        string system,
        string value,
        CancellationToken ct) where T : Resource;

    /// <summary>
    ///     Gets EpisodeOfCare resources for a given patient - supports pagination
    /// </summary>
    /// <param name="patientId">ResourceID for the patient</param>
    /// <param name="cursor">Pagination cursor</param>
    /// <param name="ct"></param>
    /// <returns>List of EpisodeOfCare, and cursor if there are more pages</returns>
    Task<(IList<EpisodeOfCare>, string?)> GetEpisodesOfCareForPatientAsync(
        string patientId,
        string? cursor,
        CancellationToken ct);

    /// <summary>
    ///     Gets EpisodeOfCare resources of a given type - supports pagination
    /// </summary>
    /// <param name="type">Type coding filter</param>
    /// <param name="managingOrganization">Organization filter</param>
    /// <param name="cursor">Pagination cursor</param>
    /// <param name="ct"></param>
    /// <returns>List of EpisodeOfCare, and cursor if there are more pages</returns>
    Task<(IList<EpisodeOfCare>, string?)> GetEpisodesOfCareByTypeAsync(
        Coding type,
        Identifier? managingOrganization,
        string? cursor,
        CancellationToken ct);

    /// <summary>
    ///     Sends a request to create a resource in IKJ. The create operation is asynchronous.
    ///     If the request is valid, a Task tracking the create operation progress is returned.
    /// </summary>
    /// <param name="resource">Resource to be created</param>
    /// <param name="ct"></param>
    /// <typeparam name="T">Resource type</typeparam>
    /// <returns>Task and retry-after value</returns>
    Task<(string, int)> PostResourceAsync<T>(T resource, CancellationToken ct) where T : Resource;

    /// <summary>
    ///     Sends a request to update (replace) a resource in IKJ. The update operation is asynchronous.
    ///     If the request is valid, a Task tracking the update operation progress is returned.
    /// </summary>
    /// <param name="resource">Resource to be updated</param>
    /// <param name="ct"></param>
    /// <typeparam name="T">Resource type</typeparam>
    /// <returns>Task and retry-after value</returns>
    Task<(string, int)> PutResourceAsync<T>(T resource, CancellationToken ct) where T : Resource;

    /// <summary>
    ///     Polls IKJ task until it is 'completed'.
    /// </summary>
    /// <param name="url">Task URL, including the unique task ID</param>
    /// <param name="pollIntervalS">Time between poll requests</param>
    /// <param name="ct"></param>
    /// <returns>Resource ID</returns>
    /// <exception cref="Exception"></exception>
    Task<string> PollTaskStatusAsync(
        string url,
        int pollIntervalS,
        CancellationToken ct);
}

public class IkjApi : IIkjApi
{
    private readonly IkjClient _client;
    private readonly FhirJsonDeserializer _fhirDeserializer;
    private readonly FhirJsonSerializer _fhirSerializer;

    public IkjApi(
        IkjClient client,
        FhirJsonDeserializer deserializer,
        FhirJsonSerializer serializer)
    {
        _client = client;
        _fhirDeserializer = deserializer;
        _fhirSerializer = serializer;
    }

    public async Task<T> GetResourceByIdAsync<T>(string id, CancellationToken ct) where T : Resource
    {
        var response = await _client.SendAsync(
            HttpMethod.Get,
            $"{Configuration.ApiBaseUrl}/{typeof(T).Name}/{id}",
            null,
            null,
            ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        return _fhirDeserializer.Deserialize<T>(json);
    }

    public async Task<T?> GetResourceByIdentifierAsync<T>(
        string system,
        string value,
        CancellationToken ct) where T : Resource
    {
        var response = await _client.SendAsync(
            HttpMethod.Get,
            $"{Configuration.ApiBaseUrl}/{typeof(T).Name}",
            $"identifier={system}|{value}",
            null,
            ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        // Search using querystring returns a Bundle, but we expect a single Resource response as we search by identifier.
        var bundle = _fhirDeserializer.Deserialize<Bundle>(json);
        var entryComponent = bundle.Entry.SingleOrDefault();
        return entryComponent?.Resource as T;
    }

    public async Task<(IList<EpisodeOfCare>, string?)> GetEpisodesOfCareForPatientAsync(
        string patientId,
        string? cursor,
        CancellationToken ct)
    {
        var queryString = cursor != null
            ? $"cursor={cursor}"
            : $"patient={patientId}&_count={Configuration.PageSize}&_sort=-_lastUpdated";

        var response = await _client.SendAsync(
            HttpMethod.Get,
            $"{Configuration.ApiBaseUrl}/EpisodeOfCare",
            queryString,
            null,
            ct);

        var json = await response.Content.ReadAsStringAsync(ct);
        var bundle = _fhirDeserializer.Deserialize<Bundle>(json);

        var pageResults = bundle.Entry.Select(e => e.Resource).OfType<EpisodeOfCare>().ToList();
        var nextPageCursor = bundle.NextLink?.Query.Split("cursor=").Last();
        return (pageResults, nextPageCursor);
    }

    public async Task<(IList<EpisodeOfCare>, string?)> GetEpisodesOfCareByTypeAsync(
        Coding type,
        Identifier? managingOrganization,
        string? cursor,
        CancellationToken ct)
    {
        var queryString = cursor != null
            ? $"cursor={cursor}"
            : $"type={type.AsString()}&_count={Configuration.PageSize}&_sort=-_lastUpdated";

        if (cursor == null && managingOrganization != null)
        {
            queryString += $"&managingOrganization.identifier={managingOrganization.AsString()}";
        }

        var response = await _client.SendAsync(
            HttpMethod.Get,
            $"{Configuration.ApiBaseUrl}/EpisodeOfCare",
            queryString,
            null,
            ct);

        var json = await response.Content.ReadAsStringAsync(ct);
        var bundle = _fhirDeserializer.Deserialize<Bundle>(json);

        var pageResults = bundle.Entry.Select(e => e.Resource).OfType<EpisodeOfCare>().ToList();
        var nextPageCursor = bundle.NextLink?.Query.Split("cursor=").Last();
        return (pageResults, nextPageCursor);
    }

    public async Task<(string, int)> PostResourceAsync<T>(T resource, CancellationToken ct) where T : Resource
    {
        var resourceJson = _fhirSerializer.SerializeToString(resource);
        var response = await _client.SendAsync(
            HttpMethod.Post,
            $"{Configuration.ApiBaseUrl}/{typeof(T).Name}",
            null,
            resourceJson,
            ct);

        // Successful posts results in a Task being created in IKJ. The Task status must be polled to check
        // for the status of the asynchronous operation.
        return GetTaskInformation(response);
    }

    public async Task<(string, int)> PutResourceAsync<T>(T resource, CancellationToken ct) where T : Resource
    {
        var resourceJson = _fhirSerializer.SerializeToString(resource);
        var response = await _client.SendAsync(
            HttpMethod.Put,
            $"{Configuration.ApiBaseUrl}/{typeof(T).Name}/{resource.Id}",
            null,
            resourceJson,
            ct);

        // Successful posts results in a Task being created in IKJ. The Task status must be polled to check
        // for the status of the asynchronous operation.
        return GetTaskInformation(response);
    }

    public async Task<string> PollTaskStatusAsync(
        string url,
        int pollIntervalS,
        CancellationToken ct)
    {
        var attemptCount = 0;

        while (attemptCount < Configuration.PollTaskStatusMaxAttempts)
        {
            ++attemptCount;
            await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(pollIntervalS), ct);

            var response = await _client.SendAsync(
                HttpMethod.Get,
                url,
                null,
                null,
                ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            var task = _fhirDeserializer.Deserialize<Hl7.Fhir.Model.Task>(json);

            Console.WriteLine(
                "Polling task status: status={0}, attempt={1}, maxAttempts={2}",
                task.Status,
                attemptCount,
                Configuration.PollTaskStatusMaxAttempts);

            if (task.Status == Hl7.Fhir.Model.Task.TaskStatus.Failed)
            {
                throw new Exception(
                    $"Task status indicated that operation failed: status={task.Status}, code={task.Code?.Text}, description={task.Description}");
            }

            if (task.Status != Hl7.Fhir.Model.Task.TaskStatus.Completed)
            {
                continue;
            }

            var outputComponent = task.Output.Single();
            var resourceReference = (ResourceReference)outputComponent.Value;
            return resourceReference.Reference!.Split('/').Last();
        }

        throw new Exception(
            "Task polling aborted due to max attempts limit being reached, and Task is still not completed.");
    }

    private static (string, int) GetTaskInformation(HttpResponseMessage response)
    {
        var taskUrl = response
            .Headers
            .Single(h => h.Key.Equals("location", StringComparison.InvariantCultureIgnoreCase))
            .Value
            .Single();

        var retryAfterS = Convert.ToInt32(
            response
                .Headers
                .Single(h => h.Key.Equals("retry-after", StringComparison.InvariantCultureIgnoreCase))
                .Value
                .Single());

        return (taskUrl, retryAfterS);
    }
}
